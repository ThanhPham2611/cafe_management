using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Cấp quán (phase2_spec §1, phase3_spec 3.1/3.2). Truy cập qua GameBootstrap.Tier.
    ///  - Lên cấp 2: RP >= 300 và Xu >= 18.000 (cọc). Tiền cọc bị giữ lại (Deposit, có lưu).
    ///  - Lên cấp 3 (mua nhà hàng): RP >= 3.000 và Xu >= 140.000 - cọc (GameBalance.Tier3StarRP / Tier3Price); cọc được hoàn, hết tiền thuê.
    ///  - Chuyển cảnh: tối dần -> dọn khách/đơn/ly -> cất đồ cũ vào kho (Inventory) và hủy xe đẩy -> dựng phòng
    ///    (EnvironmentBuilder theo RoomSpec của cấp) + lưới + bố cục mặc định -> bake NavMesh -> sáng dần -> pháo giấy + "Khai trương!".
    ///  - Từ save cấp >= 2, game vào thẳng phòng của cấp (PrepareRoomOnLoad).
    ///  - Tiền thuê (chỉ cấp 2) / nợ / chi phí cố định theo RentLedger.
    public class TierManager : MonoBehaviour
    {
        private const float FadeSeconds = 0.6f;

        /// Cấp quán hiện tại cho các hệ thống chưa giữ tham chiếu (vd. CleaningRules.TierProvider = () => TierManager.CurrentTier).
        public static int CurrentTier
        {
            get
            {
                GameBootstrap g = GameBootstrap.Instance;
                return g != null && g.Tier != null ? g.Tier.Current : 1;
            }
        }

        private GameBootstrap game;
        private RentLedger ledger;
        private UpgradePanel panel;
        private GameObject t1Env;
        private GameObject roomRoot;
        private int roomTier;
        private readonly Dictionary<string, int> inventory = new Dictionary<string, int>();

        /// 1 vỉa hè, 2 thuê mặt bằng, 3 mua nhà hàng (4 chưa làm).
        public int Current { get; private set; }
        /// Tiền cọc đang giữ (hoàn lại khi mua nhà hàng, cấp 3; sau đó = 0).
        public int Deposit { get; private set; }
        /// Đang chạy chuyển cảnh: đồng hồ ngày đứng, HUD khóa.
        public bool Transitioning { get; private set; }

        /// Số nhân viên đang làm: Staff đặt vào đây để sức chứa T2 (1 + 0,25·NV) tính đúng. Mặc định 0.
        public int StaffCount;

        /// Mặt nạ các khu phụ đã mở (cấp 3): bit ZoneInfo.Bit(ZoneId). Sảnh chính luôn mở nên không nằm trong mặt nạ.
        public int ZonesOpen { get; private set; }
        /// Số khu phụ đã mở; mỗi khu +20% khách.
        public int OpenZoneCount { get { return ZoneInfo.OpenCount(ZonesOpen); } }
        public float ZoneDemandMult { get { return Current >= 3 ? GameBalance.ZoneDemandMult(OpenZoneCount) : 1f; } }
        /// Mở thêm khu xong (đã dựng sàn, bake NavMesh, tặng bàn ghế): UI vẽ lại.
        public event Action ZonesChanged;

        public RentLedger Ledger { get { return ledger; } }
        /// Đang thuê mặt bằng (cấp 2): có tiền thuê hàng tuần. Cấp 3 đã mua nhà.
        public bool IsRented { get { return Current == 2; } }
        public RoomSpec Room { get { return RoomLayout.For(Current); } }
        public int RentDebt { get { return ledger != null ? ledger.Debt : 0; } }

        /// Cấp quán vừa đổi (đã dựng xong phòng mới). Recipes/Cleaning nghe để làm mới theo cấp.
        public event Action TierChanged;
        public event Action InventoryChanged;

        /// Chi phí cố định mỗi ngày: phí vỉa hè 60 (cấp 1) / vận hành 150 (cấp 2) / 700 (cấp 3).
        public int DailyFixedCost
        {
            get { return GameBalance.FixedCostFor(Current); }
        }

        // ------------------------------------------------------------------ khởi tạo

        public void Init(GameBootstrap g, SaveData save)
        {
            game = g;
            Current = 1;
            ledger = new RentLedger();
            if (save != null)
            {
                Current = Mathf.Max(1, save.tier);
                Deposit = Mathf.Max(0, save.deposit);
                ZonesOpen = Current >= 3 ? save.zonesOpen & ValidZoneMask : 0;
                ledger.Load(save.rentDebt, save.daysSinceRent);
                if (save.inventory != null)
                {
                    for (int i = 0; i < save.inventory.Length; i++)
                    {
                        PurchaseCount pc = save.inventory[i];
                        if (!string.IsNullOrEmpty(pc.furnitureId) && pc.count > 0) inventory[pc.furnitureId] = pc.count;
                    }
                }
            }
            t1Env = GameObject.Find("Environment"); // môi trường vỉa hè của scene (còn active lúc này)
            AttachStreetViewHide(t1Env);
        }

        /// Tường nhà phố, mái che và cửa cuốn của vỉa hè: ẩn khi camera vòng ra phía sau tường để nhìn ngang không bị chắn kín.
        /// Hướng "ra ngoài" lấy từ chính transform của tường (cả môi trường bị xoay trong scene): +z cục bộ là phía đường.
        private static void AttachStreetViewHide(GameObject env)
        {
            if (env == null) return;
            Transform wall = FindDeep(env.transform, "Wall_Shophouse");
            if (wall == null) return;
            Vector3 outward = wall.forward;
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f) return;
            outward.Normalize();
            Vector3 plane = wall.position - outward * 0.55f; // mặt phẳng của mái che / tường (mái nhô ra phía trong 0,45 m)
            foreach (string n in new[] { "Wall_Shophouse", "Awning_Roof", "Shutter_Door" })
            {
                Transform tr = FindDeep(env.transform, n);
                if (tr == null || tr.GetComponent<ViewHide>() != null) continue;
                var vh = tr.gameObject.AddComponent<ViewHide>();
                vh.Outward = outward;
                vh.PlanePoint = plane;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform c in root.GetComponentsInChildren<Transform>(true))
                if (c.name == name) return c;
            return null;
        }

        public void AttachPanel(UpgradePanel p)
        {
            panel = p;
        }

        private void Update()
        {
            if (panel != null) panel.Tick(Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            if (panel != null) panel.Dispose();
        }

        // ------------------------------------------------------------------ nạp save cấp >= 2

        /// Gọi từ GameBootstrap.Build TRƯỚC khi tạo GridSystem khi save là cấp >= 2: dựng phòng, đặt điểm khách vào/ra.
        public void PrepareRoomOnLoad(out Vector3 gridOrigin, out Vector2Int gridSize)
        {
            RoomSpec room = RoomLayout.For(Current);
            gridOrigin = room.GridOrigin;
            gridSize = room.GridSize;
            HideT1Env();
            BuildRoom();
            ConfigureWorldPoints();
        }

        private static int ValidZoneMask
        {
            get
            {
                int m = 0;
                for (int i = 0; i < ZoneInfo.Buyable.Length; i++) m |= ZoneInfo.Bit(ZoneInfo.Buyable[i]);
                return m;
            }
        }

        /// Gọi ngay sau khi tạo GridSystem (mọi cấp): chỉ số nội thất tham chiếu theo cấp, bản đồ khu (cấp 3).
        public void OnGridCreated(GridSystem grid)
        {
            if (grid == null) return;
            grid.FRef = GameBalance.FRefFor(Current);
            ZoneMap map = Current >= 3 ? RoomLayout.For(Current).CreateZoneMap() : null;
            if (map != null) map.SetOpen(ZonesOpen);
            grid.SetZones(map);
        }

        /// Gọi ngay sau khi tạo CameraRig: cấp >= 2 dùng khung nhìn rộng hơn cho phòng.
        public void OnCameraReady()
        {
            if (Current >= 2 && game.CameraRig != null)
            {
                RoomSpec room = RoomLayout.For(Current);
                game.CameraRig.SetBounds(game.Grid.WorldBounds, room.CameraDistance, room.CameraPivotZ);
            }
        }

        /// Gọi sau khi đặt layout đã lưu: cấp >= 2 mà chưa có đồ thì đặt bố cục mặc định; thiếu quầy pha thì bổ sung.
        public void FinishLoadLayout(GridSystem grid)
        {
            if (Current < 2 || grid == null) return;
            if (grid.Placed.Count == 0) RoomLayout.PlaceDefault(game, grid, this);
            else RoomLayout.EnsureBar(game, grid);
        }

        /// Save nhà hàng cũ (lưới 20 x 24, chưa có khu) -> lưới 44 x 24: dời mọi món sang phải bằng số ô dải sân vườn.
        public static int LayoutShiftCells(SaveData save)
        {
            if (save == null || save.tier < 3 || save.gridVersion >= 1) return 0;
            return RoomLayout.For(3).AnnexCells;
        }

        // ------------------------------------------------------------------ điều kiện lên cấp

        public int XuNow { get { return game != null ? game.Wallet.Xu : 0; } }
        public int RpNow { get { return game != null ? Mathf.FloorToInt(game.Reputation.RP) : 0; } }

        /// Cấp kế tiếp (2 hoặc 3); 0 khi đã ở cấp cao nhất hiện có.
        public int NextTier { get { return Current >= 1 && Current < 3 ? Current + 1 : 0; } }
        public int NextTierRp { get { return GameBalance.UpgradeStarRP(NextTier); } }
        /// Xu phải có để lên cấp kế tiếp (cấp 2 = cọc; cấp 3 = giá nhà trừ cọc được hoàn).
        public int NextTierXu { get { return GameBalance.UpgradeCost(NextTier, Deposit); } }

        public bool CanUpgrade
        {
            get
            {
                return NextTier != 0 && game != null
                    && game.Reputation.RP >= NextTierRp
                    && game.Wallet.Xu >= NextTierXu;
            }
        }

        /// Được phép chuyển cấp ngay lúc này (đã qua màn tạo nhân vật, ngày đã bắt đầu, không đang đặt đồ, không đang chuyển).
        public bool UpgradeAllowedNow
        {
            get
            {
                if (NextTier == 0 || Transitioning || game == null || game.Day == null) return false;
                if (game.Day.CurrentPhase == DayCycle.Phase.Idle) return false;
                if (game.Creator != null && game.Creator.IsOpen) return false;
                if (game.Builder != null && game.Builder.IsPlacing) return false;
                return true;
            }
        }

        /// Nút "Thuê mặt bằng" trên HUD: đủ điều kiện thì mở hộp xác nhận, chưa đủ thì báo thiếu gì.
        public void RequestUpgrade()
        {
            if (!UpgradeAllowedNow) return;
            if (!CanUpgrade)
            {
                GameEvents.RaiseToast(NeedMessage());
                return;
            }
            if (game.Builder != null && game.Builder.IsOpen) game.Builder.Close();
            if (panel != null) panel.ShowConfirm();
            else ConfirmUpgrade();
        }

        /// "Cần đủ X Xu và Y RP để ..." theo cấp kế tiếp.
        public string NeedMessage()
        {
            return string.Format(NextTier >= 3 ? Strings.Tier3NeedFmt : Strings.TierNeedFmt,
                Strings.FormatInt(NextTierXu), Strings.FormatInt(NextTierRp));
        }

        /// Người chơi bấm "Thuê ngay" / "Mua ngay" (hoặc autopilot gọi thẳng). Trả về false nếu không bắt đầu được.
        public bool ConfirmUpgrade()
        {
            if (!CanUpgrade || !UpgradeAllowedNow) return false;
            Transitioning = true; // đặt đồng bộ để autopilot / UI thấy ngay
            StartCoroutine(UpgradeRoutine());
            return true;
        }

        /// Dành cho DebugAutoPilot: bỏ qua hộp xác nhận.
        public bool BeginUpgrade(bool skipConfirm)
        {
            if (skipConfirm) return ConfirmUpgrade();
            RequestUpgrade();
            return true;
        }

        // ------------------------------------------------------------------ chuỗi chuyển cấp

        private IEnumerator UpgradeRoutine()
        {
            Transitioning = true;
            int target = NextTier;
            try
            {
                if (panel != null) panel.HideConfirm();

                int paid = game.Wallet.Spend(NextTierXu);
                if (target >= 3)
                {
                    Debug.Log("[Tier] Mua nhà hàng: trả " + paid + " Xu, hoàn cọc " + Deposit + " Xu");
                    Deposit = 0; // cọc được trừ vào giá nhà (đã tính trong NextTierXu)
                }
                else
                {
                    Deposit += paid;
                    Debug.Log("[Tier] Thuê mặt bằng: cọc " + paid + " Xu");
                }

                yield return FadeTo(1f);

                Step(ClearDayState, "dọn trạng thái ngày");
                yield return null;

                Step(() => StashLayout(target), "cất đồ cũ");
                yield return null; // để Destroy của xe đẩy/đồ cũ chạy xong trước khi bake

                Step(() => BuildWorld(target), "dựng phòng");
                yield return null;
                Step(RebuildNavigation, "bake NavMesh");
                Step(FinishWorldSwitch, "hoàn tất chuyển cấp");
                yield return null;
                yield return null;

                yield return FadeTo(0f);
                Celebrate();
            }
            finally
            {
                Transitioning = false;
                if (panel != null) panel.SetFade(0f);
            }
        }

        /// Chạy một bước, lỗi thì ghi log rồi đi tiếp (không để game kẹt màn đen).
        private static void Step(Action a, string what)
        {
            try { a(); }
            catch (Exception e) { Debug.LogError("[Tier] Lỗi khi " + what + ": " + e); }
        }

        private IEnumerator FadeTo(float target)
        {
            if (panel == null) yield break;
            float from = panel.FadeAlpha;
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / FadeSeconds);
                k = k * k * (3f - 2f * k);
                panel.SetFade(Mathf.Lerp(from, target, k));
                yield return null;
            }
            panel.SetFade(target);
        }

        /// Khách đang có mặt bị xóa ngay, đơn/ly/việc của chủ quán về trạng thái đầu ngày.
        private void ClearDayState()
        {
            if (game.Spawner != null) game.Spawner.PurgeAll();
            game.Orders.Clear();
            game.Shelf.Clear();
            for (int i = game.LeftoverCups.Count - 1; i >= 0; i--)
            {
                if (i >= game.LeftoverCups.Count) continue;
                LeftoverCup c = game.LeftoverCups[i];
                if (c == null) game.LeftoverCups.RemoveAt(i);
                else c.Collect(true);
            }
            game.Station.ResetAll();
            if (game.Owner != null) game.Owner.ResetForNewDay();
        }

        /// Đồ đã mua (ghế, bàn, trang trí) về kho; xe đẩy / quầy cũ bị hủy. Đồ chỉ dùng được ở cấp cũ (onlyTier) được hoàn 50% giá.
        private void StashLayout(int toTier)
        {
            GridSystem grid = game.Grid;
            // Biển hiệu đang gắn vào xe đẩy: tháo ra trước để không bị hủy theo
            if (game.Sign != null) game.Sign.transform.SetParent(null, true);

            int refund = 0;
            for (int i = 0; i < grid.Placed.Count; i++)
            {
                PlacedFurniture pf = grid.Placed[i];
                FurnitureDef d = pf != null ? pf.Def : null;
                if (d == null || d.kind == FurnitureKind.BrewStation) continue;
                if (d.onlyTier != 0 && d.onlyTier != toTier) refund += Mathf.RoundToInt(d.price * ShopRules.SellRatio);
                else AddToInventory(d.id, 1);
            }
            grid.RemoveAll();
            if (refund > 0) game.Wallet.Earn(refund);
        }

        private void BuildWorld(int tier)
        {
            HideT1Env();
            Current = tier;
            BuildRoom();
            ConfigureWorldPoints();

            GridSystem grid = game.Grid;
            RoomSpec room = RoomLayout.For(tier);
            grid.Reconfigure(room.GridOrigin, room.GridSize);
            // Nợ cũ (thuê nhà, lương) vẫn phải trả; cấp 2 bắt đầu kỳ thuê mới, cấp 3 hết thuê.
            if (tier == 2) ledger.Reset();
            else ledger.ClearRentCycle();
            ZonesOpen = 0; // mua nhà hàng: chỉ có sảnh chính, các khu phụ mua dần
            OnGridCreated(grid);
            RoomLayout.PlaceDefault(game, grid, this);
        }

        private void RebuildNavigation()
        {
            GridSystem grid = game.Grid;
            NavMeshBaker.BakeFor(game, null);
            for (int i = 0; i < grid.Seats.Count; i++) grid.Seats[i].PrepareApproach();
        }

        private void FinishWorldSwitch()
        {
            GridSystem grid = game.Grid;
            RoomSpec room = RoomLayout.For(Current);
            if (game.CameraRig != null) game.CameraRig.SetBounds(grid.WorldBounds, room.CameraDistance, room.CameraPivotZ);
            if (game.Builder != null) game.Builder.RebindGrid();
            if (game.Day != null)
            {
                float ratio = game.Day.OnTierChanged(); // ngày cấp 3 dài hơn: giữ nguyên giờ trên đồng hồ
                if (game.Spawner != null) game.Spawner.RescaleRemaining(ratio);
            }
            if (game.Spawner != null) game.Spawner.RefreshDemand();
            if (game.Owner != null) game.Owner.ResetForNewDay(); // đứng ở điểm phục vụ của quầy bar mới
            Step(RaiseTierChanged, "báo đổi cấp");                // Recipes/Cleaning cập nhật trước khi lưu
            if (Current >= 3) game.Wallet.Earn(GameBalance.Tier3OpeningGift); // trước mốc đầu ngày để thoát-vào không mất quà
            game.CommitDayBaseline();
            game.SaveSnapshot();
            Debug.Log("[Tier] Đã lên cấp " + Current + ": ghế=" + grid.Seats.Count + ", đồ=" + grid.Placed.Count + ", kho=" + InventoryTotal);
        }

        private void RaiseTierChanged()
        {
            if (TierChanged != null) TierChanged();
        }

        /// Pháo giấy ngoài lúc lên cấp quán (vd. lên level uy tín); withBanner: kèm biểu ngữ lớn bannerText.
        public void PlayConfetti(bool withBanner, string bannerText)
        {
            if (panel != null) panel.PlayConfetti(withBanner, bannerText);
        }

        private void Celebrate()
        {
            if (panel != null) panel.PlayConfetti();
            GameEvents.RaiseToast(Strings.GrandOpening);
            if (Current >= 3) GameEvents.RaiseToast(string.Format(Strings.OpeningGiftFmt, Strings.FormatInt(GameBalance.Tier3OpeningGift)));
            StartCoroutine(OwnerCheer());
        }

        private IEnumerator OwnerCheer()
        {
            CustomerVisual v = game.Owner != null ? game.Owner.GetComponent<CustomerVisual>() : null;
            for (int i = 0; i < 3 && v != null; i++)
            {
                v.SetPose(CustomerPose.Happy);
                yield return new WaitForSecondsRealtime(0.9f);
            }
        }

        // ------------------------------------------------------------------ môi trường

        private void HideT1Env()
        {
            if (t1Env != null) t1Env.SetActive(false);
        }

        /// Bật/tắt môi trường vỉa hè của scene (cấp 1) và ẩn/hiện phòng thuê tương ứng. Dùng khi thử nghiệm / quay lại cấp 1.
        public void ShowT1Environment(bool show)
        {
            if (t1Env != null) t1Env.SetActive(show);
            if (roomRoot != null) roomRoot.SetActive(!show);
        }

        /// Dựng phòng của cấp hiện tại; phòng cũ (cấp thấp hơn) bị hủy ngay để collider không lọt vào NavMesh.
        private void BuildRoom()
        {
            if (roomRoot != null && roomTier == Current) return;
            if (roomRoot != null)
            {
                roomRoot.SetActive(false);
                Destroy(roomRoot);
                roomRoot = null;
            }
            if (game.toonMaterial == null) Debug.LogWarning("[Tier] GameBootstrap.toonMaterial trống: phòng sẽ không có màu palette");
            roomRoot = EnvironmentBuilder.BuildRoom(game.toonMaterial, null, RoomLayout.For(Current), Current >= 3 ? ZonesOpen : 0);
            roomTier = Current;
        }

        /// Điểm khách đi vào / đi ra nằm trên vỉa hè trước mặt tiền.
        private void ConfigureWorldPoints()
        {
            RoomSpec room = RoomLayout.For(Current);
            game.spawnPoint = EnsureMarker(game.spawnPoint, "SpawnPoint", room.SpawnPoint);
            game.exitPoint = EnsureMarker(game.exitPoint, "ExitPoint", room.ExitPoint);
        }

        private Transform EnsureMarker(Transform t, string name, Vector3 pos)
        {
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(game.transform, false);
                t = go.transform;
            }
            t.position = pos;
            return t;
        }

        // ------------------------------------------------------------------ ngày

        /// Đầu ngày (sau Day.Begin, trước khi chốt mốc Xu đầu ngày): tới kỳ thì thu tiền thuê nhà.
        public void OnDayBegin(DaySummary summary)
        {
            if (!IsRented || ledger == null) return; // cấp 3: đã mua nhà, không còn tiền thuê
            int shortfall;
            int paid = ledger.OnDayBegin(game.Wallet, summary, out shortfall);
            if (paid > 0 || shortfall > 0)
            {
                GameEvents.RaiseToast(string.Format(Strings.RentPaidFmt, Strings.FormatInt(paid)));
                if (shortfall > 0) GameEvents.RaiseToast(string.Format(Strings.RentShortFmt, Strings.FormatInt(shortfall)));
            }
        }

        /// Cuối ngày (sau khi trừ chi phí cố định, trước khi lưu): đếm ngày thuê, trả nợ 30% doanh thu.
        public void OnDayEnd(DaySummary summary)
        {
            if (summary == null) return;
            summary.tier = Current;
            if (Current >= 2 && ledger != null) ledger.OnDayEnd(game.Wallet, summary);
        }

        /// Ghi nợ thêm (lương nhân viên thiếu tiền...).
        public void AddDebt(int amount)
        {
            if (ledger != null) ledger.AddDebt(amount);
        }

        // ------------------------------------------------------------------ các khu của nhà hàng (cấp 3)

        public bool IsZoneOpen(ZoneId z)
        {
            return z == ZoneId.Hall || (ZonesOpen & ZoneInfo.Bit(z)) != 0;
        }

        public bool CanAffordZone(ZoneId z)
        {
            return game != null && game.Wallet.Xu >= ZoneInfo.Cost(z);
        }

        /// Được phép mở khu này ngay lúc này (cấp 3, khu phụ chưa mở, không đang chuyển cấp, ngày đã bắt đầu). Chưa kể tiền.
        public bool CanUnlockZone(ZoneId z)
        {
            if (Current < 3 || z == ZoneId.Hall || IsZoneOpen(z) || Transitioning) return false;
            if (game == null || game.Grid == null || game.Grid.Zones == null || roomRoot == null) return false;
            return game.Day != null && game.Day.CurrentPhase != DayCycle.Phase.Idle;
        }

        /// Lối sang khu (lỗ mở rộng 3 m trên tường sảnh) còn thông ít nhất 1 m, không bị đồ của sảnh chặn.
        public bool OpeningClear(ZoneId z)
        {
            GridSystem grid = game != null ? game.Grid : null;
            if (grid == null || grid.Zones == null) return true;
            return grid.Zones.OpeningClear(z, c => grid.GetAt(c) == null);
        }

        /// Mua khu: trừ Xu, dựng sàn + tường + bỏ bãi đất rào, mở ô lưới, tặng 2 bộ bàn ghế, bake lại NavMesh, lưu.
        /// Không cần màn tối: sàn khu mới là collider mới còn phần bị phá (tường hông, bãi rào) không có collider.
        public bool UnlockZone(ZoneId z)
        {
            if (!CanUnlockZone(z) || !CanAffordZone(z)) return false;
            if (!OpeningClear(z))
            {
                GameEvents.RaiseToast(string.Format(Strings.ZoneBlockedFmt, ZoneInfo.Name(z)));
                return false; // chưa trừ Xu: người chơi dọn lối đi rồi mở lại
            }
            int paid = game.Wallet.Spend(ZoneInfo.Cost(z));
            ZonesOpen |= ZoneInfo.Bit(z);

            EnvironmentBuilder.RefreshZones(roomRoot.transform, game.toonMaterial, RoomLayout.For(Current), ZonesOpen);
            GridSystem grid = game.Grid;
            grid.Zones.SetOpen(ZonesOpen);
            int sets = RoomLayout.PlaceZoneStarter(game, grid, this, z);

            RebuildNavigation();
            NavMeshBaker.RepairCustomers(game.Spawner != null ? game.Spawner.Active : null);
            if (game.Builder != null) game.Builder.RebindGrid();
            if (game.Spawner != null) game.Spawner.RefreshDemand();
            game.NotifyShopTransaction(-paid); // dời mốc Xu đầu ngày theo giao dịch rồi lưu (thoát-vào không hoàn tiền)

            Debug.Log("[Tier] Mở khu " + z + ": trả " + paid + " Xu, tặng " + sets + " bộ bàn ghế, ghế=" + grid.Seats.Count +
                      ", khu đã mở=" + OpenZoneCount + ", nhu cầu x" + ZoneDemandMult.ToString("0.00"));
            GameEvents.RaiseToast(string.Format(Strings.ZoneOpenedToastFmt, ZoneInfo.Name(z)));
            if (panel != null) panel.PlayConfetti();
            if (ZonesChanged != null) ZonesChanged();
            return true;
        }

        // ------------------------------------------------------------------ kho đồ

        public int InventoryTotal
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<string, int> kv in inventory) n += kv.Value;
                return n;
            }
        }

        public int InventoryCount(string id)
        {
            int n;
            return !string.IsNullOrEmpty(id) && inventory.TryGetValue(id, out n) ? n : 0;
        }

        /// Số món trong kho thuộc một loại (vd. số bàn đã mua để bày ra khi mở nhà hàng).
        public int InventoryKindCount(FurnitureKind kind)
        {
            int n = 0;
            foreach (KeyValuePair<string, int> kv in inventory)
            {
                FurnitureDef d = game != null ? game.GetDef(kv.Key) : null;
                if (d != null && d.kind == kind) n += kv.Value;
            }
            return n;
        }

        public void AddToInventory(string id, int count)
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            int n;
            inventory.TryGetValue(id, out n);
            inventory[id] = n + count;
            if (InventoryChanged != null) InventoryChanged();
        }

        /// Lấy 1 món ra khỏi kho (khi đặt xuống). false nếu kho không có.
        public bool TakeFromInventory(string id)
        {
            int n;
            if (string.IsNullOrEmpty(id) || !inventory.TryGetValue(id, out n) || n <= 0) return false;
            if (n == 1) inventory.Remove(id);
            else inventory[id] = n - 1;
            if (InventoryChanged != null) InventoryChanged();
            return true;
        }

        /// Một def trong kho theo loại (singleCell: chỉ lấy món 1x1, dùng cho ghế); null nếu không có.
        public FurnitureDef FindInventoryDef(FurnitureKind kind, bool singleCell)
        {
            foreach (KeyValuePair<string, int> kv in inventory)
            {
                if (kv.Value <= 0) continue;
                FurnitureDef d = game.GetDef(kv.Key);
                if (d == null || d.prefab == null || d.kind != kind) continue;
                if (singleCell && (d.footprint.x != 1 || d.footprint.y != 1)) continue;
                return d;
            }
            return null;
        }

        public PurchaseCount[] ExportInventory()
        {
            var arr = new PurchaseCount[inventory.Count];
            int i = 0;
            foreach (KeyValuePair<string, int> kv in inventory)
                arr[i++] = new PurchaseCount { furnitureId = kv.Key, count = kv.Value };
            return arr;
        }

        // ------------------------------------------------------------------ lưu

        public void WriteToSave(SaveData data)
        {
            if (data == null) return;
            data.tier = Current;
            data.zonesOpen = Current >= 3 ? ZonesOpen : 0;
            data.gridVersion = 1;
            data.deposit = Deposit;
            data.rentDebt = ledger != null ? ledger.Debt : 0;
            data.daysSinceRent = ledger != null ? ledger.DaysSinceRent : 0;
            data.inventory = ExportInventory();
        }
    }
}
