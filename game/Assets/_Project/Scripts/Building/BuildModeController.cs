using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cafe
{
    /// Chế độ bày trí (GDD §2.1): mua từ cửa hàng -> kéo ghost lên lưới -> xoay -> xác nhận;
    /// chạm món đã đặt -> Di chuyển / Bán / Xoay. Sau mỗi thay đổi: bake lại NavMesh, kiểm tra đường đi, lưu.
    [DefaultExecutionOrder(-200)]
    public class BuildModeController : MonoBehaviour
    {
        // Ô palette (art_bible §3)
        private const int SwatchGood = 28;
        private const int SwatchBad = 24;
        private const int SwatchSelect = 30;
        private const int SwatchGrid = 0;

        private const float GridLineY = 0.02f;
        private const float FootY = 0.035f;
        private const float GridLineW = 0.025f;
        private const float DragGrab = 0.8f;      // m quanh footprint ghost để bắt đầu kéo
        private const float BtnW = 200f;
        private const float BtnGap = 12f;
        private const float BarH = 112f;
        private const float BarW = BtnW * 3f + BtnGap * 2f;

        private enum Mode { Browse, Placing }

        private static readonly int SwatchId = Shader.PropertyToID("_SwatchIndex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private GameBootstrap game;
        private GridSystem grid;
        private bool open;
        private Mode mode;
        private readonly Action<Vector2> tapHandler;
        private MaterialPropertyBlock mpb; // tạo trong Awake: Unity cấm tạo trong field initializer của MonoBehaviour
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private readonly List<Transform> ignore = new List<Transform>(2);
        private Camera cam;
        private Material fxMat;

        // ghost
        private GameObject ghost;
        private Renderer[] ghostRenderers;
        private FurnitureDef gdef;
        private Vector2Int gcell;
        private int grot;
        private bool isMove;
        private bool fromInventory;   // đặt đồ lấy từ kho (miễn phí, không tính giới hạn mua)
        private FurnitureDef origDef;
        private Vector2Int origCell;
        private int origRot;
        private bool ghostValid;
        private bool customerBlock;
        private int ghostSwatch = -1;
        private float revalTimer;
        private GameObject footQuad;
        private MeshRenderer footRenderer;

        // chọn món đã đặt
        private PlacedFurniture selected;
        private GameObject selQuad;
        private MeshRenderer selRenderer;

        // kéo
        private bool dragging;
        private Vector3 dragOffset;
        private float lastTouchTime = -10f;

        // lưới
        private GameObject gridVisual;
        private static Mesh unitRect;

        // đường đi
        private readonly Dictionary<Seat, bool> seatOk = new Dictionary<Seat, bool>();
        private bool stationOk, exitOk, snapshotValid;
        private NavMeshPath navPath; // tạo trong Awake (Unity cấm tạo trong field initializer)

        // UI
        private bool uiBuilt;
        private Canvas canvas;
        private RectTransform placeBar, selectBar;
        private Text confirmLabel;
        private GameObject sellDialog;
        private Text sellText;
        private Action sellYes;

        public event Action Changed;

        public bool IsOpen { get { return open; } }
        public bool IsPlacing { get { return open && mode == Mode.Placing; } }

        /// Nút "Đặt" trên thanh đặt đồ (hướng dẫn chỉ tay); null khi thanh chưa hiện.
        public RectTransform PlaceConfirmRect
        {
            get { return placeBar != null && placeBar.gameObject.activeInHierarchy ? placeBar.Find("Btn1") as RectTransform : null; }
        }

        public BuildModeController()
        {
            tapHandler = OnWorldTap;
        }

        private void Awake()
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            if (navPath == null) navPath = new NavMeshPath();
        }

        public void Init(GameBootstrap g)
        {
            game = g;
            grid = g.Grid;
            GameEvents.DayEnded += OnDayEnded;
        }

        /// Chuyển cấp quán: lưới (cùng instance) đổi gốc/kích thước -> đóng build mode và dựng lại các đường kẻ ô.
        public void RebindGrid()
        {
            if (open) Close();
            grid = game.Grid;
            if (gridVisual != null)
            {
                Destroy(gridVisual);
                gridVisual = null;
            }
            if (uiBuilt) BuildGridVisual(); // chưa từng mở thì EnsureUi sẽ dựng
        }

        private void OnDestroy()
        {
            GameEvents.DayEnded -= OnDayEnded;
            if (TapInput.TapOverride == tapHandler) TapInput.TapOverride = null;
        }

        private void OnDayEnded(DaySummary s)
        {
            Close();
        }

        // ------------------------------------------------------------------ mở / đóng

        public void Open()
        {
            if (open) return;
            EnsureUi();
            open = true;
            mode = Mode.Browse;
            gridVisual.SetActive(true);
            TapInput.TapOverride = tapHandler;
            Raise();
        }

        public void Close()
        {
            if (!open) return;
            if (mode == Mode.Placing) CancelPlacing();
            Deselect();
            HideSellDialog();
            StopDrag();
            open = false;
            if (gridVisual != null) gridVisual.SetActive(false);
            if (TapInput.TapOverride == tapHandler) TapInput.TapOverride = null;
            Raise();
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }

        // ------------------------------------------------------------------ mua / đặt (ghost)

        private bool bypassLevelLock;

        /// Món mua mới còn khóa theo level uy tín (đồ trong kho và di chuyển không bị khóa).
        private bool LevelLocked(FurnitureDef def)
        {
            return !bypassLevelLock && game.Reputation != null && !ShopUnlocks.IsUnlocked(def.id, game.Reputation.RewardedLevel);
        }

        /// Bắt đầu đặt món mua mới (chưa trừ Xu). fromInv = true: đặt món có sẵn trong kho (miễn phí).
        public void BeginPlace(FurnitureDef def, bool fromInv = false)
        {
            if (!open || def == null || def.prefab == null) return;
            if (mode == Mode.Placing) CancelPlacing();
            Deselect();
            if (fromInv)
            {
                if (game.Tier == null || game.Tier.InventoryCount(def.id) <= 0) return;
            }
            else if (ShopRules.AtMax(grid, def))
            {
                return;
            }
            else if (LevelLocked(def))
            {
                GameEvents.RaiseToast(string.Format(Strings.LockedByLevelToast, ShopUnlocks.RequiredLevel(def.id)));
                return;
            }
            TakeSnapshot();
            isMove = false;
            fromInventory = fromInv;

            Vector2Int start = Vector2Int.zero;
            Vector3 p;
            int rot = FacingCameraRotation();
            if (GroundPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out p, def)) start = SnapCell(def, rot, p);
            StartGhost(def, start, rot);
        }

        /// Món mới đặt quay mặt trước (+Z của model) về phía camera, theo bước 90°.
        private static int FacingCameraRotation()
        {
            Camera cam = Camera.main;
            if (cam == null) return 0;
            Vector3 toCam = -cam.transform.forward;
            float yaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
            return GridSystem.NormalizeRotation(Mathf.RoundToInt(yaw / 90f));
        }

        private void StartMove(PlacedFurniture pf)
        {
            if (!CanTouch(pf)) return;
            CollectCups(pf);
            TakeSnapshot();
            origDef = pf.Def;
            origCell = pf.Cell;
            origRot = pf.Rotation;
            Deselect();
            grid.Remove(pf);
            isMove = true;
            fromInventory = false;
            StartGhost(origDef, origCell, origRot);
        }

        private void StartGhost(FurnitureDef def, Vector2Int cell, int rot)
        {
            DestroyGhost();
            gdef = def;
            gcell = cell;
            grot = GridSystem.NormalizeRotation(rot);

            ghost = Instantiate(def.prefab, grid.PlacePos(def, gcell, grot), Quaternion.Euler(0f, 90f * grot, 0f));
            ghost.name = "Ghost_" + def.id;
            Collider[] cols = ghost.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
            ghostRenderers = ghost.GetComponentsInChildren<Renderer>();

            footQuad = MakeRectObject("GhostFootprint", out footRenderer);
            mode = Mode.Placing;
            ghostSwatch = -1;
            StopDrag();
            Revalidate();
            confirmLabel.text = (isMove || fromInventory) ? Strings.PlaceBtn : string.Format(Strings.PlaceBtnFmt, Strings.FormatInt(ShopRules.Price(grid, def)));
            placeBar.gameObject.SetActive(true);
            selectBar.gameObject.SetActive(false);
            PositionBar(placeBar, ghost.transform.position);
            Raise();
        }

        private void DestroyGhost()
        {
            if (ghost != null)
            {
                ghost.SetActive(false);
                Destroy(ghost);
                ghost = null;
            }
            if (footQuad != null)
            {
                Destroy(footQuad);
                footQuad = null;
            }
            ghostRenderers = null;
        }

        private void CancelPlacing()
        {
            if (mode != Mode.Placing) return;
            bool move = isMove;
            DestroyGhost();
            StopDrag();
            mode = Mode.Browse;
            isMove = false;
            fromInventory = false;
            placeBar.gameObject.SetActive(false);
            if (move && origDef != null)
            {
                grid.Place(origDef, origCell, origRot);
                RebuildNav();
                game.Spawner.RefreshDemand();
            }
            Raise();
        }

        private void OnConfirm()
        {
            if (mode != Mode.Placing || gdef == null) return;
            Revalidate();
            if (!ghostValid)
            {
                GameEvents.RaiseToast(customerBlock ? Strings.CustomerInTheWay : Strings.CantPlaceHere);
                return;
            }

            int price = 0;
            if (!isMove && fromInventory)
            {
                if (game.Tier == null || game.Tier.InventoryCount(gdef.id) <= 0)
                {
                    CancelPlacing();
                    return;
                }
            }
            else if (!isMove)
            {
                if (LevelLocked(gdef))
                {
                    GameEvents.RaiseToast(string.Format(Strings.LockedByLevelToast, ShopUnlocks.RequiredLevel(gdef.id)));
                    return;
                }
                if (ShopRules.AtMax(grid, gdef))
                {
                    GameEvents.RaiseToast(string.Format(Strings.MaxReachedFmt, gdef.maxCount));
                    return;
                }
                price = ShopRules.Price(grid, gdef);
                if (game.Wallet.Xu < price)
                {
                    GameEvents.RaiseToast(Strings.NotEnoughXu);
                    return;
                }
            }

            PlacedFurniture pf = TryCommit(gdef, gcell, grot, isMove || !(GridSystem.IsOverhead(gdef) || GridSystem.IsMount(gdef)));
            if (pf == null) return;

            if (price > 0)
            {
                game.Wallet.Spend(price);
                if (game.Missions != null) game.Missions.RecordPurchase();
            }
            if (fromInventory && !isMove && game.Tier != null) game.Tier.TakeFromInventory(gdef.id);
            Vector3 fx = pf.WorldBounds.center + Vector3.up * 0.8f;
            DestroyGhost();
            StopDrag();
            mode = Mode.Browse;
            isMove = false;
            fromInventory = false;
            placeBar.gameObject.SetActive(false);
            if (price > 0) UiFx.FloatText(fx, "-" + Strings.FormatInt(price), HudBuilder.Accent);
            AfterChange(-price);
            Raise();
        }

        /// Đặt thật, bake NavMesh, kiểm tra đường; lỗi thì gỡ ra và báo. Trả về món đã đặt hoặc null.
        private PlacedFurniture TryCommit(FurnitureDef def, Vector2Int cell, int rot, bool rebake)
        {
            PlacedFurniture pf = grid.Place(def, cell, rot);
            if (pf == null)
            {
                GameEvents.RaiseToast(Strings.CantPlaceHere);
                return null;
            }
            if (rebake)
            {
                RebuildNav();
                if (!VerifyPaths())
                {
                    grid.Remove(pf);
                    RebuildNav();
                    GameEvents.RaiseToast(Strings.BlockedPath);
                    return null;
                }
            }
            return pf;
        }

        private void AfterChange(int xuDelta)
        {
            game.Spawner.RefreshDemand();
            game.NotifyShopTransaction(xuDelta);
        }

        // ------------------------------------------------------------------ chọn / sửa món đã đặt

        private void Select(PlacedFurniture pf)
        {
            if (pf == null || pf.Def.kind == FurnitureKind.BrewStation) return;
            selected = pf;
            if (selQuad == null) selQuad = MakeRectObject("SelectFootprint", out selRenderer);
            selQuad.SetActive(true);
            SetRect(selQuad, pf.Cell, pf.Footprint);
            Paint(selRenderer, SwatchSelect);
            selectBar.gameObject.SetActive(true);
            PositionBar(selectBar, pf.WorldBounds.center);
        }

        private void Deselect()
        {
            selected = null;
            if (selQuad != null) selQuad.SetActive(false);
            if (selectBar != null) selectBar.gameObject.SetActive(false);
        }

        private void OnMoveClicked()
        {
            if (selected != null && mode == Mode.Browse) StartMove(selected);
        }

        private void OnSellClicked()
        {
            PlacedFurniture pf = selected;
            if (pf == null || mode != Mode.Browse || !CanTouch(pf)) return;
            int value = ShopRules.SellValue(grid, pf.Def);
            string msg = string.Format(Strings.SellConfirmFmt, DisplayName(pf.Def), Strings.FormatInt(value));
            ShowSellDialog(msg, () => DoSell(pf, value));
        }

        private void DoSell(PlacedFurniture pf, int value)
        {
            if (pf == null || !CanTouch(pf)) return;
            CollectCups(pf);
            Vector3 fx = pf.WorldBounds.center + Vector3.up * 0.8f;
            Deselect();
            grid.Remove(pf);
            RebuildNav();
            game.Wallet.Earn(value);
            UiFx.FloatText(fx, "+" + Strings.FormatInt(value), HudBuilder.Accent);
            AfterChange(value);
        }

        private void OnRotateSelectedClicked()
        {
            PlacedFurniture pf = selected;
            if (pf == null || mode != Mode.Browse || !CanTouch(pf)) return;

            FurnitureDef def = pf.Def;
            Vector2Int oc = pf.Cell;
            int orot = pf.Rotation;
            int nrot = GridSystem.NormalizeRotation(orot + 1);
            Vector2Int nc = SnapCell(def, nrot, grid.FootprintCenter(def, oc, orot));

            CollectCups(pf);
            TakeSnapshot();
            Deselect();
            grid.Remove(pf);

            PlacedFurniture np = null;
            if (grid.CanPlace(def, nc, nrot) && !CustomersBlock(def, nc, nrot))
                np = TryCommit(def, nc, nrot, true);
            else
                GameEvents.RaiseToast(Strings.CantPlaceHere);

            if (np == null)
            {
                np = grid.Place(def, oc, orot);
                RebuildNav();
            }
            if (np != null) Select(np);
            AfterChange(0);
        }

        /// Ghế có khách ngồi / giữ chỗ, hoặc ly còn đang dùng trên ghế/bàn -> không đụng được.
        private bool CanTouch(PlacedFurniture pf)
        {
            if (pf == null) return false;
            Seat seat = pf.GetComponent<Seat>();
            if (seat != null)
            {
                if (seat.Occupant != null || (seat.Cup != null && !seat.Cup.IsCollectable))
                {
                    GameEvents.RaiseToast(Strings.SeatBusy);
                    return false;
                }
            }
            if (pf.Def.kind == FurnitureKind.Table)
            {
                Bounds b = pf.WorldBounds;
                b.Expand(0.3f);
                var cups = game.LeftoverCups;
                for (int i = 0; i < cups.Count; i++)
                {
                    LeftoverCup c = cups[i];
                    if (c != null && !c.IsCollectable && b.Contains(c.transform.position))
                    {
                        GameEvents.RaiseToast(Strings.SeatBusy);
                        return false;
                    }
                }
            }
            return true;
        }

        /// Thu ly để lại trên ghế / bàn trước khi gỡ.
        private void CollectCups(PlacedFurniture pf)
        {
            Seat seat = pf.GetComponent<Seat>();
            if (seat != null && seat.Cup != null && seat.Cup.IsCollectable) seat.Cup.Collect(false);
            if (pf.Def.kind == FurnitureKind.Table)
            {
                Bounds b = pf.WorldBounds;
                b.Expand(0.3f);
                var cups = game.LeftoverCups;
                for (int i = cups.Count - 1; i >= 0; i--)
                {
                    LeftoverCup c = cups[i];
                    if (c != null && c.IsCollectable && b.Contains(c.transform.position)) c.Collect(false);
                }
            }
        }

        private static string DisplayName(FurnitureDef d)
        {
            return string.IsNullOrEmpty(d.displayName) ? d.id : d.displayName;
        }

        // ------------------------------------------------------------------ nhập liệu

        private void Update()
        {
            if (!open) return;

            if (mode == Mode.Placing)
            {
                PlacingInput();
                revalTimer -= Time.unscaledDeltaTime;
                if (revalTimer <= 0f) Revalidate();
                if (ghost != null) PositionBar(placeBar, ghost.transform.position);
            }
            else if (selected != null)
            {
                PositionBar(selectBar, selected.WorldBounds.center);
            }
            else if (selectBar != null && selectBar.gameObject.activeSelf)
            {
                Deselect();
            }
        }

        private void PlacingInput()
        {
            Vector2 pos;
            bool down, up, held;
            int id = -1;
            int tc = Input.touchCount;
            if (tc >= 2)
            {
                StopDrag();
                return;
            }
            if (tc == 1)
            {
                lastTouchTime = Time.unscaledTime;
                Touch t = Input.GetTouch(0);
                pos = t.position;
                id = t.fingerId;
                down = t.phase == TouchPhase.Began;
                up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                held = !up;
            }
            else if (Time.unscaledTime - lastTouchTime > 0.3f)
            {
                pos = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                up = Input.GetMouseButtonUp(0);
                held = Input.GetMouseButton(0);
            }
            else
            {
                return;
            }

            if (down)
            {
                if (!TapInput.IsOverUI(pos, id))
                {
                    Vector3 g;
                    if (GroundPoint(pos, out g))
                    {
                        Vector3 c = grid.FootprintCenter(gdef, gcell, grot);
                        Vector2Int fp = GridSystem.RotatedFootprint(gdef.footprint, grot);
                        float hx = fp.x * GridSystem.Cell * 0.5f + DragGrab;
                        float hz = fp.y * GridSystem.Cell * 0.5f + DragGrab;
                        if (Mathf.Abs(g.x - c.x) <= hx && Mathf.Abs(g.z - c.z) <= hz)
                        {
                            dragging = true;
                            dragOffset = c - g;
                            dragOffset.y = 0f;
                            SetCameraBlocked(true);
                        }
                    }
                }
            }
            else if (dragging)
            {
                if (held && !up)
                {
                    Vector3 g;
                    if (GroundPoint(pos, out g)) MoveGhostTo(SnapCell(gdef, grot, g + dragOffset));
                }
                else
                {
                    StopDrag();
                }
            }
            if (up) StopDrag();
        }

        private void StopDrag()
        {
            dragging = false;
            SetCameraBlocked(false);
        }

        private void SetCameraBlocked(bool v)
        {
            if (game != null && game.CameraRig != null) game.CameraRig.InputBlocked = v;
        }

        /// Chạm (không kéo) vào thế giới khi build mode mở.
        private void OnWorldTap(Vector2 screenPos)
        {
            if (!open) return;
            if (mode == Mode.Placing)
            {
                Vector3 g;
                if (GroundPoint(screenPos, out g)) MoveGhostTo(SnapCell(gdef, grot, g));
                return;
            }
            PlacedFurniture pf = PickFurniture(screenPos);
            if (pf != null && pf.Def.kind != FurnitureKind.BrewStation) Select(pf);
            else Deselect();
        }

        private PlacedFurniture PickFurniture(Vector2 screenPos)
        {
            Camera c = Cam();
            if (c == null) return null;
            Ray ray = c.ScreenPointToRay(screenPos);

            PlacedFurniture best = null;
            float bestD = float.MaxValue;
            int n = Physics.RaycastNonAlloc(ray, hits, 200f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null || h.distance >= bestD) continue;
                PlacedFurniture pf = h.collider.GetComponentInParent<PlacedFurniture>();
                if (pf == null) continue;
                best = pf;
                bestD = h.distance;
            }
            if (best != null) return best;

            Vector3 p;
            Vector2Int cell;
            if (GroundPoint(screenPos, out p) && grid.WorldToCell(p, out cell))
            {
                best = grid.GetAt(cell);
                if (best != null) return best;
            }

            // Món "trên cao" (dù) không có collider: thử theo hộp bao
            for (int i = 0; i < grid.Placed.Count; i++)
            {
                PlacedFurniture pf = grid.Placed[i];
                float d;
                if (pf.Overhead && pf.WorldBounds.IntersectRay(ray, out d) && d < bestD)
                {
                    best = pf;
                    bestD = d;
                }
            }
            return best;
        }

        private Camera Cam()
        {
            if (cam == null) cam = Camera.main;
            return cam;
        }

        /// Điểm chạm trên mặt phẳng sàn; đồ đặt trên quầy (def, hoặc ghost đang kéo) dùng mặt phẳng ngang mặt quầy cho ngón tay khớp hình.
        private bool GroundPoint(Vector2 screenPos, out Vector3 p, FurnitureDef forDef = null)
        {
            p = Vector3.zero;
            Camera c = Cam();
            if (c == null) return false;
            Ray r = c.ScreenPointToRay(screenPos);
            FurnitureDef pointDef = forDef != null ? forDef : (ghost != null ? gdef : null);
            float y = grid.Origin.y + (GridSystem.IsMount(pointDef) ? grid.CounterTopY : 0f);
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            float d;
            if (!plane.Raycast(r, out d)) return false;
            p = r.GetPoint(d);
            return true;
        }

        // ------------------------------------------------------------------ ghost

        /// Ô góc dưới-trái sao cho footprint nhận tâm gần world nhất, kẹp trong lưới.
        private Vector2Int SnapCell(FurnitureDef def, int rot, Vector3 world)
        {
            Vector2Int fp = GridSystem.RotatedFootprint(def.footprint, rot);
            int cx = Mathf.RoundToInt((world.x - grid.Origin.x) / GridSystem.Cell - fp.x * 0.5f);
            int cy = Mathf.RoundToInt((world.z - grid.Origin.z) / GridSystem.Cell - fp.y * 0.5f);
            cx = Mathf.Clamp(cx, 0, Mathf.Max(0, grid.Size.x - fp.x));
            cy = Mathf.Clamp(cy, 0, Mathf.Max(0, grid.Size.y - fp.y));
            return new Vector2Int(cx, cy);
        }

        /// Dùng cho test tự động (DebugAutoPilot -autoshop): đi qua đúng luồng mở → đặt ghost → xác nhận.
        /// Trả về true nếu món đã được đặt thật (đã trừ tiền).
        public bool DebugPlace(FurnitureDef def, Vector2Int cell)
        {
            int before = grid.Placed.Count;
            bypassLevelLock = true; // bot đo cân bằng bỏ qua khóa level để số đo kinh tế không đổi (xem ShopUnlocks)
            try { return DebugPlaceInner(def, cell, before); }
            finally { bypassLevelLock = false; }
        }

        private bool DebugPlaceInner(FurnitureDef def, Vector2Int cell, int before)
        {
            if (!open) Open();
            BeginPlace(def);
            if (ghost == null) return false;
            gcell = new Vector2Int(int.MinValue, int.MinValue);
            MoveGhostTo(cell);
            OnConfirm();
            bool placed = grid.Placed.Count > before;
            if (mode == Mode.Placing) CancelPlacing();
            Close();
            return placed;
        }

        private void MoveGhostTo(Vector2Int cell)
        {
            if (ghost == null || cell == gcell) return;
            gcell = cell;
            ghost.transform.position = grid.PlacePos(gdef, gcell, grot);
            Revalidate();
        }

        private void OnRotateGhostClicked()
        {
            if (ghost == null) return;
            Vector3 center = grid.FootprintCenter(gdef, gcell, grot);
            grot = GridSystem.NormalizeRotation(grot + 1);
            gcell = SnapCell(gdef, grot, center);
            ghost.transform.position = grid.PlacePos(gdef, gcell, grot);
            ghost.transform.rotation = Quaternion.Euler(0f, 90f * grot, 0f);
            Revalidate();
        }

        private void Revalidate()
        {
            revalTimer = 0.3f;
            if (ghost == null) return;
            customerBlock = CustomersBlock(gdef, gcell, grot);
            ghostValid = grid.CanPlace(gdef, gcell, grot) && !customerBlock;
            SetRect(footQuad, gcell, GridSystem.RotatedFootprint(gdef.footprint, grot));
            int sw = ghostValid ? SwatchGood : SwatchBad;
            if (sw == ghostSwatch) return;
            ghostSwatch = sw;
            Paint(footRenderer, sw);
            if (ghostRenderers != null)
                for (int i = 0; i < ghostRenderers.Length; i++)
                    if (ghostRenderers[i] != null) PaintSwatchOnly(ghostRenderers[i], sw);
        }

        /// Có khách đang đứng / ngồi trong footprint (cộng lề nhỏ).
        private bool CustomersBlock(FurnitureDef def, Vector2Int cell, int rot)
        {
            if (GridSystem.IsOverhead(def) || GridSystem.IsMount(def) || game.Spawner == null) return false;
            Vector2Int fp = GridSystem.RotatedFootprint(def.footprint, rot);
            Vector3 c = grid.FootprintCenter(def, cell, rot);
            float hx = fp.x * GridSystem.Cell * 0.5f + 0.15f;
            float hz = fp.y * GridSystem.Cell * 0.5f + 0.15f;
            IList<Customer> list = game.Spawner.Active;
            for (int i = 0; i < list.Count; i++)
            {
                Customer cu = list[i];
                if (cu == null) continue;
                Vector3 p = cu.transform.position;
                if (Mathf.Abs(p.x - c.x) <= hx && Mathf.Abs(p.z - c.z) <= hz) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ hình ảnh lưới / ô

        private Material FxMaterial()
        {
            if (fxMat != null) return fxMat;
            if (game.toonMaterial != null)
            {
                fxMat = game.toonMaterial;
            }
            else
            {
                Shader s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Sprites/Default");
                fxMat = new Material(s);
            }
            return fxMat;
        }

        private static Color FallbackColor(int swatch)
        {
            switch (swatch)
            {
                case SwatchGood: return new Color(0.25f, 0.8f, 0.35f, 1f);
                case SwatchBad: return new Color(0.9f, 0.22f, 0.2f, 1f);
                case SwatchSelect: return new Color(0.95f, 0.72f, 0.23f, 1f);
                default: return new Color(0.29f, 0.17f, 0.12f, 1f);
            }
        }

        /// Tô quad/lưới: swatch toon (hoặc màu trơn khi không có toonMaterial).
        private void Paint(Renderer r, int swatch)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(SwatchId, swatch);
            if (game.toonMaterial == null) mpb.SetColor(BaseColorId, FallbackColor(swatch));
            r.SetPropertyBlock(mpb);
        }

        private void PaintSwatchOnly(Renderer r, int swatch)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(SwatchId, swatch);
            r.SetPropertyBlock(mpb);
        }

        private static Mesh UnitRect()
        {
            if (unitRect != null) return unitRect;
            unitRect = new Mesh();
            unitRect.name = "BuildUnitRect";
            unitRect.vertices = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, 0) };
            unitRect.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            unitRect.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            unitRect.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            unitRect.RecalculateBounds();
            return unitRect;
        }

        private GameObject MakeRectObject(string name, out MeshRenderer mr)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = UnitRect();
            mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = FxMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        private void SetRect(GameObject go, Vector2Int cell, Vector2Int fp)
        {
            if (go == null) return;
            Transform t = go.transform;
            t.position = grid.Origin + new Vector3(cell.x * GridSystem.Cell, FootY, cell.y * GridSystem.Cell);
            t.localScale = new Vector3(fp.x * GridSystem.Cell, 1f, fp.y * GridSystem.Cell);
        }

        private void BuildGridVisual()
        {
            gridVisual = new GameObject("BuildGrid");
            gridVisual.transform.position = grid.Origin + new Vector3(0f, GridLineY, 0f);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            if (grid.Zones == null)
            {
                AddGridLines(verts, tris, 0, 0, grid.Size.x, grid.Size.y);
            }
            else
            {
                // nhà hàng cấp 3: chỉ kẻ ô ở khu đã mở (khu chưa mở không đặt được đồ)
                for (int z = 0; z < grid.Zones.Rects.Length; z++)
                {
                    if (!grid.Zones.IsOpen((ZoneId)z)) continue;
                    RectInt rc = grid.Zones.Rects[z];
                    AddGridLines(verts, tris, rc.xMin, rc.yMin, rc.xMax, rc.yMax);
                }
            }

            var m = new Mesh();
            m.name = "BuildGridLines";
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            var normals = new Vector3[verts.Count];
            var uvs = new Vector2[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            m.normals = normals;
            m.uv = uvs;
            m.RecalculateBounds();

            gridVisual.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = gridVisual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = FxMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Paint(mr, SwatchGrid);
            gridVisual.SetActive(false);
        }

        /// Đường kẻ ô của hình chữ nhật ô [x0, x1] x [y0, y1] (toạ độ ô).
        private static void AddGridLines(List<Vector3> v, List<int> t, int x0, int y0, int x1, int y1)
        {
            const float c = GridSystem.Cell;
            float half = GridLineW * 0.5f;
            for (int i = x0; i <= x1; i++)
                AddRect(v, t, i * c - half, y0 * c, i * c + half, y1 * c);
            for (int j = y0; j <= y1; j++)
                AddRect(v, t, x0 * c, j * c - half, x1 * c, j * c + half);
        }

        private static void AddRect(List<Vector3> v, List<int> t, float x0, float z0, float x1, float z1)
        {
            int b = v.Count;
            v.Add(new Vector3(x0, 0f, z0));
            v.Add(new Vector3(x0, 0f, z1));
            v.Add(new Vector3(x1, 0f, z1));
            v.Add(new Vector3(x1, 0f, z0));
            t.Add(b); t.Add(b + 1); t.Add(b + 2);
            t.Add(b); t.Add(b + 2); t.Add(b + 3);
        }

        // ------------------------------------------------------------------ NavMesh + kiểm tra đường

        private void RebuildNav()
        {
            ignore.Clear();
            if (ghost != null) ignore.Add(ghost.transform);
            NavMeshBaker.BakeFor(game, ignore);
            for (int i = 0; i < grid.Seats.Count; i++) grid.Seats[i].PrepareApproach();
            RepairAgents();
        }

        /// Sau khi NavMesh đổi: khách đang đi bị mất mesh thì đặt lại lên mesh gần nhất.
        private void RepairAgents()
        {
            NavMeshBaker.RepairCustomers(game.Spawner != null ? game.Spawner.Active : null);
        }

        private bool SpawnOnNav(out Vector3 p)
        {
            NavMeshHit h;
            if (NavMesh.SamplePosition(game.SpawnPosition, out h, 3f, NavMesh.AllAreas))
            {
                p = h.position;
                return true;
            }
            p = game.SpawnPosition;
            return false;
        }

        private bool CanReach(Vector3 from, Vector3 to)
        {
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, navPath) && navPath.status == NavMeshPathStatus.PathComplete;
        }

        private bool StationReachable(Vector3 from)
        {
            PlacedFurniture st = grid.BrewStationFurniture;
            if (st == null) return true;
            Bounds b = st.WorldBounds;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 q = b.ClosestPoint(b.center + dir * 20f) + dir * 0.5f;
                q.y = grid.Origin.y;
                NavMeshHit h;
                if (NavMesh.SamplePosition(q, out h, 0.3f, NavMesh.AllAreas) && CanReach(from, h.position)) return true;
            }
            return false;
        }

        private bool ExitReachable(Vector3 from)
        {
            NavMeshHit h;
            return NavMesh.SamplePosition(game.ExitPosition, out h, 3f, NavMesh.AllAreas) && CanReach(from, h.position);
        }

        /// Chụp trạng thái đi lại được TRƯỚC khi sửa bố cục.
        private void TakeSnapshot()
        {
            seatOk.Clear();
            Vector3 from;
            snapshotValid = SpawnOnNav(out from);
            if (!snapshotValid) return;
            for (int i = 0; i < grid.Seats.Count; i++) seatOk[grid.Seats[i]] = grid.Seats[i].IsReachableFrom(from, navPath);
            stationOk = StationReachable(from);
            exitOk = ExitReachable(from);
        }

        /// Mọi thứ từng đi tới được (xe pha, đường ra, từng ghế) vẫn phải đi tới được; ghế mới phải đi tới được.
        private bool VerifyPaths()
        {
            if (!snapshotValid) return true;
            Vector3 from;
            if (!SpawnOnNav(out from)) return false;
            for (int i = 0; i < grid.Seats.Count; i++)
            {
                Seat s = grid.Seats[i];
                bool was;
                bool needed = !seatOk.TryGetValue(s, out was) || was;
                if (needed && !s.IsReachableFrom(from, navPath)) return false;
            }
            if (stationOk && !StationReachable(from)) return false;
            if (exitOk && !ExitReachable(from)) return false;
            return true;
        }

        // ------------------------------------------------------------------ UI nổi

        private void EnsureUi()
        {
            if (uiBuilt) return;
            uiBuilt = true;
            if (gridVisual == null) BuildGridVisual();

            canvas = HudBuilder.CreateCanvas("BuildCanvas", 12, true, transform);

            placeBar = MakeBar("PlaceBar");
            AddBarButton(placeBar, 0, ClayStyle.Gold, Strings.Rotate, OnRotateGhostClicked);
            ClayButton ok = AddBarButton(placeBar, 1, ClayStyle.Green, Strings.PlaceBtn, OnConfirm);
            confirmLabel = ok.Content.GetComponentInChildren<Text>();
            AddBarButton(placeBar, 2, ClayStyle.Red, Strings.Cancel, CancelPlacing);

            selectBar = MakeBar("SelectBar");
            AddBarButton(selectBar, 0, ClayStyle.Gold, Strings.MoveBtn, OnMoveClicked);
            AddBarButton(selectBar, 1, ClayStyle.Red, Strings.SellBtn, OnSellClicked);
            AddBarButton(selectBar, 2, ClayStyle.Cream, Strings.Rotate, OnRotateSelectedClicked);

            BuildSellDialog();
            placeBar.gameObject.SetActive(false);
            selectBar.gameObject.SetActive(false);
        }

        private RectTransform MakeBar(string name)
        {
            RectTransform bar = HudBuilder.NewRect(name, canvas.transform);
            HudBuilder.SetFixed(bar, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(BarW, BarH));
            return bar;
        }

        private ClayButton AddBarButton(RectTransform bar, int index, ClayStyle style, string label, UnityAction onClick)
        {
            ClayButton b = HudBuilder.AddLabelButton(bar, "Btn" + index, style, label, 42, onClick, out _);
            HudBuilder.SetFixed((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(index * (BtnW + BtnGap), 0f), new Vector2(BtnW, BarH));
            return b;
        }

        private void PositionBar(RectTransform bar, Vector3 world)
        {
            if (bar == null || canvas == null) return;
            Camera c = Cam();
            if (c == null) return;
            Vector3 sp = c.WorldToScreenPoint(world);
            if (sp.z < 0.1f) return;
            float k = canvas.scaleFactor > 0.01f ? 1f / canvas.scaleFactor : 1f;
            float cw = Screen.width * k;
            float ch = Screen.height * k;
            float x = Mathf.Clamp(sp.x * k, BarW * 0.5f + 16f, Mathf.Max(BarW * 0.5f + 16f, cw - BarW * 0.5f - 16f));
            float y = Mathf.Clamp(sp.y * k - 90f, BarH + 16f, Mathf.Max(BarH + 16f, ch - 16f));
            bar.anchoredPosition = new Vector2(x, y);
        }

        private void BuildSellDialog()
        {
            Image dim = HudBuilder.Img(canvas.transform, "SellDialog", HudBuilder.T.dim, Color.white, false, true);
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            HudBuilder.Stretch(dim.rectTransform);
            sellDialog = dim.gameObject;

            Image panel = HudBuilder.Panel(dim.transform, "Card", true);
            HudBuilder.SetFixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 420f));

            sellText = HudBuilder.AddText(panel.transform, "Msg", "", 50, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(sellText.rectTransform, Vector2.zero, Vector2.one, new Vector2(40f, 170f), new Vector2(-40f, -30f));
            HudBuilder.Fit(sellText, 30);

            Text lt;
            ClayButton yes = HudBuilder.AddLabelButton(panel.transform, "Yes", ClayStyle.Green, Strings.SellBtn, 48, OnSellYes, out lt);
            HudBuilder.SetFixed((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 36f), new Vector2(300f, 120f));
            ClayButton no = HudBuilder.AddLabelButton(panel.transform, "No", ClayStyle.Red, Strings.Cancel, 48, HideSellDialog, out lt);
            HudBuilder.SetFixed((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(14f, 36f), new Vector2(300f, 120f));

            sellDialog.SetActive(false);
        }

        private void ShowSellDialog(string msg, Action yes)
        {
            sellYes = yes;
            sellText.text = msg;
            sellDialog.SetActive(true);
        }

        private void OnSellYes()
        {
            Action a = sellYes;
            HideSellDialog();
            if (a != null) a();
        }

        private void HideSellDialog()
        {
            sellYes = null;
            if (sellDialog != null) sellDialog.SetActive(false);
        }
    }
}
