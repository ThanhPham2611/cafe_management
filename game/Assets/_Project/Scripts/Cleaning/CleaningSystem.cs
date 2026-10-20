using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Bộ máy dọn dẹp duy nhất của scene (OwnerController.Init gọi Ensure):
    ///  - nhịp ~4 lần/giây cho CleanlinessModel (CI), đặt lại theo ngày;
    ///  - bồn rửa (Sink_Counter, fallback quầy pha): chồng ly bẩn = các job WashCup trong WorkQueue;
    ///  - hình ảnh: icon "bàn bẩn", vòng tiến độ lau/rửa, ruồi bay quanh bàn bẩn lâu (một Update duy nhất, không Update từng vật).
    public class CleaningSystem : MonoBehaviour
    {
        private const float TickInterval = 0.25f;
        private const int FlySwatch = 51;          // than/đen
        private const int FliesPerTable = 3;
        private const float IconSize = 64f;
        private const float RingSize = 112f;

        public static CleaningSystem Instance { get; private set; }

        public CleanlinessModel Model { get; private set; }
        public GameBootstrap Game { get { return game; } }

        private GameBootstrap game;
        private float tickAcc;

        // bồn rửa
        private PlacedFurniture sink;
        private bool sinkIsCart;
        private float sinkSearchAt = -10f;

        // UI
        private bool uiBuilt;
        private Canvas canvas;
        private Camera cam;
        private RectTransform iconLayer;
        private readonly List<DirtyIcon> icons = new List<DirtyIcon>(8);
        private RectTransform ringRoot;
        private Image ringFill;
        private Image ringIcon;

        // ruồi
        private Transform fliesRoot;
        private readonly List<Transform> flies = new List<Transform>(12);
        private int activeFlies;
        private MaterialPropertyBlock flyMpb;

        private class DirtyIcon
        {
            public RectTransform rect;
            public Image disc;
            public Image icon;
            public bool shown;
        }

        // ------------------------------------------------------------------ tạo

        public static CleaningSystem Ensure(GameBootstrap g)
        {
            if (Instance != null) return Instance;
            if (g == null) return null;
            var go = new GameObject("Cleaning");
            var cs = go.AddComponent<CleaningSystem>();
            cs.Init(g);
            return cs;
        }

        private void Init(GameBootstrap g)
        {
            Instance = this;
            game = g;
            Model = new CleanlinessModel(g);
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.CustomerServed += OnCustomerServed;
        }

        private void OnDestroy()
        {
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.CustomerServed -= OnCustomerServed;
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ sự kiện

        private void OnCustomerServed(Customer c)
        {
            if (Model != null) Model.AddServedCustomer();
        }

        /// Ngày mới: bàn bẩn/ly ở bồn được dọn qua đêm (ly về kho), sàn bớt bẩn 10 điểm.
        private void OnDayStarted(int day)
        {
            if (game == null) return;

            // ly bẩn còn ở bồn -> về kho
            int pile = WorkQueue.Count(WorkJobType.WashCup, false);
            if (game.Stock != null)
                for (int i = 0; i < pile; i++) game.Stock.Return();
            WorkQueue.Clear();

            if (game.Grid != null)
            {
                var tables = game.Grid.Tables;
                for (int i = 0; i < tables.Count; i++)
                {
                    TableDirt td = tables[i] != null ? tables[i].Dirt : null;
                    if (td != null) td.ResetSilently();
                }
            }
            if (Model != null) Model.OnNewDay();
        }

        // ------------------------------------------------------------------ bồn rửa

        /// Bồn rửa (FurnitureDef id "Sink_Counter"); chưa có thì quầy pha. Null nếu không có cả hai.
        public PlacedFurniture Sink
        {
            get
            {
                RefreshSink();
                return sink;
            }
        }

        /// Đang dùng quầy pha làm bồn rửa (chưa đặt Sink_Counter).
        public bool SinkIsCart
        {
            get
            {
                RefreshSink();
                return sinkIsCart;
            }
        }

        private void RefreshSink()
        {
            if (game == null || game.Grid == null) return;
            if (sink != null && sink.gameObject.activeInHierarchy && Time.time - sinkSearchAt < 2f) return;
            sinkSearchAt = Time.time;

            PlacedFurniture found = null;
            var placed = game.Grid.Placed;
            for (int i = 0; i < placed.Count; i++)
            {
                PlacedFurniture pf = placed[i];
                if (pf != null && pf.Def != null && pf.Def.id == GameBalance.SinkFurnitureId)
                {
                    found = pf;
                    break;
                }
            }
            sinkIsCart = found == null;
            sink = found != null ? found : game.Grid.BrewStationFurniture;
        }

        /// Điểm đứng rửa ly (trên NavMesh). Sink là quầy pha thì dùng điểm đứng quầy của chủ quán.
        public bool TryGetSinkApproach(Vector3 from, out Vector3 point)
        {
            point = from;
            PlacedFurniture s = Sink;
            if (s == null) return false;
            if (sinkIsCart && game.Owner != null)
            {
                point = game.Owner.CartPoint;
                return true;
            }
            return CleaningNav.TryGetApproach(s.WorldBounds, from, out point);
        }

        /// Đặt n ly bẩn xuống bồn: mỗi ly là một job WashCup để chủ quán / nhân viên rửa.
        public void DropAtSink(int cups)
        {
            for (int i = 0; i < cups; i++) WorkQueue.PostWashCup();
        }

        /// Rửa xong MỘT ly (job WashCup): ly sạch về kho.
        public void WashOne(WorkJob job)
        {
            if (job == null || job.finished) return;
            if (game != null && game.Stock != null) game.Stock.Return();
            WorkQueue.Complete(job);
            PlacedFurniture s = Sink;
            if (s != null) UiFx.FloatText(s.WorldBounds.center + Vector3.up * 0.9f, "+1 " + Strings.CupName, HudBuilder.Mint);
        }

        /// Số ly bẩn đang chờ rửa ở bồn.
        public int SinkPile { get { return WorkQueue.Count(WorkJobType.WashCup, false); } }

        // ------------------------------------------------------------------ nhịp

        private void Update()
        {
            if (game == null) return;
            tickAcc += Time.deltaTime;
            if (tickAcc >= TickInterval)
            {
                Model.Tick(tickAcc);
                tickAcc = 0f;
            }
            UpdateFlies();
        }

        private void LateUpdate()
        {
            if (game == null) return;
            if (!uiBuilt) TryBuildUi();
            if (uiBuilt) UpdateUi();
        }

        // ------------------------------------------------------------------ ruồi

        private void UpdateFlies()
        {
            int want = 0;
            if (CleaningRules.Enabled && game.Grid != null)
            {
                var tables = game.Grid.Tables;
                float t = Time.time;
                for (int i = 0; i < tables.Count; i++)
                {
                    TableDirt td = tables[i] != null ? tables[i].Dirt : null;
                    if (td == null || !td.IsOld) continue;
                    Vector3 top = td.TopPoint;
                    for (int k = 0; k < FliesPerTable; k++)
                    {
                        Transform f = GetFly(want);
                        want++;
                        float a = t * (2.2f + k * 0.8f) + k * 2.1f + i;
                        float r = 0.22f + 0.07f * k;
                        float y = 0.2f + 0.07f * Mathf.Sin(t * 3.1f + k * 1.7f);
                        f.position = new Vector3(top.x + Mathf.Cos(a) * r, top.y + y, top.z + Mathf.Sin(a) * r);
                    }
                }
            }
            for (int i = want; i < activeFlies; i++)
                if (i < flies.Count && flies[i] != null) flies[i].gameObject.SetActive(false);
            activeFlies = want;
        }

        private Transform GetFly(int index)
        {
            while (flies.Count <= index) flies.Add(CreateFly());
            Transform f = flies[index];
            if (!f.gameObject.activeSelf) f.gameObject.SetActive(true);
            return f;
        }

        private Transform CreateFly()
        {
            if (fliesRoot == null)
            {
                fliesRoot = new GameObject("Flies").transform;
                fliesRoot.SetParent(transform, false);
            }
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Fly";
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(fliesRoot, false);
            go.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
            var r = go.GetComponent<Renderer>();
            if (game.toonMaterial != null) r.sharedMaterial = game.toonMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (flyMpb == null) flyMpb = new MaterialPropertyBlock();
            flyMpb.SetFloat(Shader.PropertyToID("_SwatchIndex"), FlySwatch);
            r.SetPropertyBlock(flyMpb);
            go.SetActive(false);
            return go.transform;
        }

        // ------------------------------------------------------------------ UI trên bàn

        private void TryBuildUi()
        {
            if (game.Hud == null || game.Hud.BubbleLayer == null) return;
            iconLayer = game.Hud.BubbleLayer;
            Canvas c = iconLayer.GetComponentInParent<Canvas>();
            canvas = c != null ? c.rootCanvas : null;
            if (canvas == null) return;

            // vòng tiến độ lau / rửa
            ringFill = HudBuilder.CreateRing(iconLayer, "CleanRing", RingSize, out ringRoot);
            ringRoot.anchorMin = Vector2.zero;
            ringRoot.anchorMax = Vector2.zero;
            ringRoot.pivot = new Vector2(0.5f, 0.5f);
            ringRoot.sizeDelta = new Vector2(RingSize, RingSize);
            ringIcon = HudBuilder.Icon(ringRoot, "Icon", null, RingSize * 0.5f);
            HudBuilder.SetFixed(ringIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(RingSize * 0.5f, RingSize * 0.5f));
            ringRoot.gameObject.SetActive(false);
            uiBuilt = true;
        }

        private DirtyIcon GetIcon(int index)
        {
            while (icons.Count <= index)
            {
                var di = new DirtyIcon();
                Image d = HudBuilder.Img(iconLayer, "DirtyTable" + icons.Count, HudBuilder.T.disc, HudBuilder.Cream, false, false);
                d.rectTransform.anchorMin = Vector2.zero;
                d.rectTransform.anchorMax = Vector2.zero;
                d.rectTransform.pivot = new Vector2(0.5f, 0f);
                d.rectTransform.sizeDelta = new Vector2(IconSize, IconSize);
                di.rect = d.rectTransform;
                di.disc = d;
                di.icon = HudBuilder.Icon(d.transform, "Icon", CleaningRules.BroomIcon(HudBuilder.T), IconSize * 0.7f);
                HudBuilder.SetFixed(di.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(IconSize * 0.7f, IconSize * 0.7f));
                d.gameObject.SetActive(false);
                icons.Add(di);
            }
            return icons[index];
        }

        private void UpdateUi()
        {
            if (cam == null) cam = Camera.main;
            bool enabled = CleaningRules.Enabled && cam != null && game.Grid != null;
            float k = canvas.scaleFactor > 0.01f ? 1f / canvas.scaleFactor : 1f;

            // icon bàn bẩn
            int used = 0;
            if (enabled)
            {
                var tables = game.Grid.Tables;
                float t = Time.time;
                for (int i = 0; i < tables.Count; i++)
                {
                    TableDirt td = tables[i] != null ? tables[i].Dirt : null;
                    if (td == null || !td.IsDirty || td.IsCleaning) continue;
                    Vector3 sp = cam.WorldToScreenPoint(td.TopPoint + new Vector3(0f, 0.55f, 0f));
                    if (sp.z <= 0.1f) continue;
                    DirtyIcon di = GetIcon(used++);
                    if (!di.shown)
                    {
                        di.shown = true;
                        di.rect.gameObject.SetActive(true);
                    }
                    HudRules.Place(di.rect, new Vector2(sp.x, sp.y) * k);
                    bool old = td.IsOld;
                    di.disc.color = old ? HudBuilder.Red : HudBuilder.Cream;
                    float s = old ? 1f + 0.1f * Mathf.Sin(t * 6f) : 1f;
                    di.rect.localScale = new Vector3(s, s, 1f);
                }
            }
            for (int i = used; i < icons.Count; i++)
            {
                if (!icons[i].shown) continue;
                icons[i].shown = false;
                icons[i].rect.gameObject.SetActive(false);
            }

            // vòng tiến độ của chủ quán
            OwnerController owner = game.Owner;
            bool ring = enabled && owner != null && owner.WorkProgress >= 0f;
            if (ring)
            {
                Vector3 sp = cam.WorldToScreenPoint(owner.WorkAnchor);
                ring = sp.z > 0.1f;
                if (ring)
                {
                    if (!ringRoot.gameObject.activeSelf) ringRoot.gameObject.SetActive(true);
                    HudRules.Place(ringRoot, new Vector2(sp.x, sp.y) * k);
                    float p = Mathf.Clamp01(owner.WorkProgress);
                    if (ringFill.type == Image.Type.Filled) ringFill.fillAmount = HudRules.QuantizeRing(p);
                    else ringFill.rectTransform.localScale = new Vector3(p, p, 1f);
                    ringFill.color = HudBuilder.Good;
                    Sprite ic = owner.WorkType == OwnerTaskType.WashCups ? HudBuilder.T.iconCup : CleaningRules.BroomIcon(HudBuilder.T);
                    if (ringIcon.sprite != ic) HudBuilder.SetIcon(ringIcon, ic);
                }
            }
            if (!ring && ringRoot.gameObject.activeSelf) ringRoot.gameObject.SetActive(false);
        }
    }
}
