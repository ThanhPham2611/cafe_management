using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Điểm vào duy nhất của scene. Editor (Cafe/Build Prototype Scene) gán các field public bên dưới;
    /// phần dựng hệ thống (lưới, nội thất, NavMesh, HUD, khách) do Gameplay Programmer hiện thực.
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Data (Editor gán)")]
        public FurnitureDef[] catalog;
        public RecipeDef[] recipes;
        public LayoutEntry[] defaultLayout;
        public Material toonMaterial;
        [Tooltip("UI theme (sprites + fonts). Editor: UiSpriteImporter.EnsureTheme()")]
        public UiTheme uiTheme;

        [Header("Lưới bày trí (ô 0.5 m)")]
        public Vector3 gridOrigin = new Vector3(-4f, 0f, -2.5f);
        public Vector2Int gridSize = new Vector2Int(16, 10);

        [Header("Nhân vật khách (Editor gán từ Prefabs/Characters)")]
        public GameObject[] customerPrefabs;

        [Header("Chủ quán (Editor gán PF_Char_Owner_*, thứ tự = OwnerLook.preset)")]
        public GameObject[] ownerPrefabs;

        [Header("Nhân viên (Editor gán PF_Char_Staff_*, thứ tự = StaffSave.preset)")]
        public GameObject[] staffPrefabs;

        [Header("Truyện khách quen & câu nói (Editor gán từ Data/Story/*.json)")]
        public TextAsset regularsJson;
        public TextAsset barksJson;

        [Header("Đường đi của khách")]
        public Transform spawnPoint;
        public Transform exitPoint;

        // ---- Hệ thống runtime (dựng trong Start) ----
        public static GameBootstrap Instance { get; private set; }

        public Wallet Wallet { get; private set; }
        public GemWallet Gems { get; private set; }
        public RecipeBook Recipes { get; private set; }
        public Reputation Reputation { get; private set; }
        /// Cấp quán (vỉa hè / thuê mặt bằng), tiền thuê, kho đồ. Luôn khác null sau Build().
        public TierManager Tier { get; private set; }
        public CupStock Stock { get; private set; }
        public OrderQueue Orders { get; private set; }
        /// Quầy giao: ly Pha chế đã pha xong chờ nhân viên Phục vụ mang đi.
        public ReadyShelf Shelf { get; private set; }
        public BrewStation Station { get; private set; }
        public GridSystem Grid { get; private set; }
        public DayCycle Day { get; private set; }
        public CustomerSpawner Spawner { get; private set; }
        public HudController Hud { get; private set; }
        public CafeCameraController CameraRig { get; private set; }
        public BuildModeController Builder { get; private set; }
        public OwnerController Owner { get; private set; }
        public StaffRoster Staff { get; private set; }
        public RegularsManager Regulars { get; private set; }
        /// Nhiệm vụ ngày + rương (đổi lúc 04:00 giờ VN). Luôn khác null sau Build().
        public MissionBook Missions { get; private set; }
        public ShopSign Sign { get; private set; }
        public CreatorScreen Creator { get; private set; }
        public GameObject CupPrefab { get; private set; }
        public readonly List<LeftoverCup> LeftoverCups = new List<LeftoverCup>();

        private readonly Dictionary<string, FurnitureDef> defById = new Dictionary<string, FurnitureDef>();
        private float dayStartXu;
        private float dayStartRp;
        private bool started;

        // ---- Hồ sơ chủ quán (SaveData v2) ----
        public bool CreatorDone;
        public string OwnerName = "";
        public string ShopName = Strings.DefaultShopName;
        public OwnerLook OwnerLook = OwnerLook.Default;
        /// Ngày sẽ bắt đầu sau khi tạo nhân vật xong (>0 khi đang chờ màn tạo nhân vật lần đầu).
        private int pendingStartDay = -1;
        /// Tên quán / ngoại hình đã đổi (HUD, biển hiệu cập nhật).
        public event System.Action ProfileChanged;

        public Vector3 SpawnPosition
        {
            get { return spawnPoint != null ? spawnPoint.position : gridOrigin + new Vector3(-1.5f, 0f, gridSize.y * 0.25f); }
        }

        public Vector3 ExitPosition
        {
            get { return exitPoint != null ? exitPoint.position : gridOrigin + new Vector3(gridSize.x * 0.5f + 1.5f, 0f, gridSize.y * 0.25f); }
        }

        private void Awake()
        {
            Instance = this;
#if UNITY_ANDROID || UNITY_IOS
            // Mục tiêu 60 FPS (plan §4). ProjectSetup chỉ đặt trong Editor nên bản build trước đó chạy theo mặc định của máy.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
#endif
        }

        private void Start()
        {
            Build();
        }

        private void Build()
        {
            // Danh mục
            if (catalog != null)
            {
                for (int i = 0; i < catalog.Length; i++)
                {
                    FurnitureDef d = catalog[i];
                    if (d != null && !string.IsNullOrEmpty(d.id)) defById[d.id] = d;
                }
            }
            FurnitureDef cupDef = GetDef("Cup_Phin_SuaDa");
            CupPrefab = cupDef != null ? cupDef.prefab : null;

            // Lưu
            SaveData save;
            bool hasSave = SaveSystem.TryLoad(out save);

            Wallet = new Wallet();
            Reputation = new Reputation();
            Stock = new CupStock();
            Orders = new OrderQueue();
            Station = new BrewStation(this);

            Wallet.Init(hasSave ? save.xu : GameBalance.StartXu);
            Reputation.Init(hasSave ? save.rp : 0f);
            // Cấp quán: phải có trước sổ công thức và lưới (cả hai đọc cấp hiện tại)
            Tier = new GameObject("TierManager").AddComponent<TierManager>();
            Tier.Init(this, hasSave ? save : null);
            RecipeBook.TierProvider = () => Tier != null ? Tier.Current : 1;
            CleaningRules.TierProvider = () => Tier != null ? Tier.Current : 1;
            // Hạt + sổ công thức (SaveData v3: gems, knownRecipes, menuToday, toolLevels). RecipeBook.TierProvider phải được gán TRƯỚC dòng này.
            Gems = new GemWallet();
            Gems.Init(hasSave ? save.gems : 0);
            Recipes = new RecipeBook(this);
            Recipes.Load(hasSave ? save : null);
            if (Tier != null) Tier.TierChanged += Recipes.NotifyTierChanged; // lên cấp: mở món thường của cấp mới, làm mới khay/sổ
            Stock.Init(GameBalance.StartCups);
            Shelf = new ReadyShelf(() => Stock.Return()); // ly bị bỏ (khách đi mất) trả ly sạch về kho
            int startDay = hasSave ? Mathf.Max(1, save.day) : 1;
            dayStartXu = Wallet.Xu;
            dayStartRp = Reputation.RP;
            if (hasSave)
            {
                CreatorDone = save.creatorDone;
                OwnerName = save.ownerName ?? "";
                ShopName = string.IsNullOrEmpty(save.shopName) ? Strings.DefaultShopName : save.shopName;
                OwnerLook = save.ownerLook;
            }

            // Lưới + nội thất
            var furnitureRoot = new GameObject("Furniture").transform;
            Vector3 gOrigin = gridOrigin;
            Vector2Int gSize = gridSize;
            if (Tier.Current >= 2) Tier.PrepareRoomOnLoad(out gOrigin, out gSize); // save cấp 2: vào thẳng phòng thuê
            Grid = new GridSystem(gOrigin, gSize, furnitureRoot);
            Tier.OnGridCreated(Grid);
            LayoutEntry[] layout = (hasSave && save.layout != null && save.layout.Length > 0) ? save.layout : (Tier.Current >= 2 ? null : defaultLayout);
            if (layout != null)
            {
                for (int i = 0; i < layout.Length; i++)
                {
                    LayoutEntry e = layout[i];
                    FurnitureDef d = GetDef(e.furnitureId);
                    if (d == null)
                    {
                        Debug.LogWarning("[GameBootstrap] Không có FurnitureDef id=" + e.furnitureId);
                        continue;
                    }
                    if (Grid.Place(d, e.cell, e.rotation) == null)
                        Debug.LogWarning("[GameBootstrap] Không đặt được " + e.furnitureId + " tại " + e.cell);
                }
            }

            Tier.FinishLoadLayout(Grid); // cấp 2 chưa có bố cục thì đặt mặc định; thiếu quầy pha thì bổ sung

            // NavMesh
            Bounds nb = Grid.WorldBounds;
            nb.Encapsulate(SpawnPosition);
            nb.Encapsulate(ExitPosition);
            nb.Expand(new Vector3(6f, 6f, 6f));
            NavMeshBaker.Bake(nb, Grid.Placed, null);
            for (int i = 0; i < Grid.Seats.Count; i++) Grid.Seats[i].PrepareApproach();

            // Hệ thống chạy mỗi frame
            Day = new GameObject("DayCycle").AddComponent<DayCycle>();
            Day.Init(this);
            Spawner = new GameObject("CustomerSpawner").AddComponent<CustomerSpawner>();
            Spawner.Init(this);

            // Chủ quán (người chơi) đứng ở xe đẩy; phải có trước HUD vì BrewPanel nghe sự kiện của chủ quán
            Owner = OwnerController.Create(this, OwnerLook);
            Sign = ShopSign.Create(this);
            Staff = new StaffRoster(Wallet, Tier.Ledger, () => Tier != null ? Tier.Current : 1);
            Staff.Load(hasSave ? save : null);
            Staff.Attach(this);
            Regulars = new RegularsManager();
            Regulars.Init(this, hasSave ? save : null);

            // Nhiệm vụ ngày: thưởng đi thẳng vào ví; mốc đầu ngày dời theo để thoát-vào không làm mất thưởng
            Missions = new MissionBook(
                xu => { Wallet.Earn(xu); NotifyRewardGranted(xu, 0f); },
                gems => { Gems.Add(gems); SaveSnapshot(); },
                rp => NotifyRewardGranted(0, Reputation.Add(rp)));
            Missions.Load(hasSave ? save : null);
            EnsureMissions();
            GameEvents.CustomerServed += OnCustomerServedForMissions;
            GameEvents.DayStarted += OnDayStartedForMissions;
            GameEvents.DayEnded += OnDayEndedForMissions;

            // Camera + input
            Camera cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera", typeof(Camera));
                cgo.tag = "MainCamera";
                cam = cgo.GetComponent<Camera>();
            }
            CameraRig = CafeCameraController.Create(cam, Grid.WorldBounds);
            Tier.OnCameraReady(); // cấp 2: khung nhìn rộng hơn cho phòng thuê
            gameObject.AddComponent<TapInput>();

            // Build mode (UI dựng lười khi mở lần đầu) - phải có trước HUD vì ShopPanel dùng
            Builder = new GameObject("BuildMode").AddComponent<BuildModeController>();
            Builder.Init(this);

            // HUD
            Hud = new GameObject("HUD").AddComponent<HudController>();
            Hud.Init(this);

            started = true;
            if (CreatorDone)
            {
                StartDay(startDay);
            }
            else
            {
                // Lần đầu (hoặc save cũ chưa có nhân vật): tạo chủ quán trước, ngày chỉ bắt đầu khi xác nhận
                pendingStartDay = startDay;
                OpenCreator(false);
            }
        }

        // ---- Chủ quán: tạo / sửa nhân vật ----

        /// editMode = true: sửa nhân vật giữa các ngày (không reset tiến trình).
        public void OpenCreator(bool editMode)
        {
            if (Creator == null) Creator = CreatorScreen.Create(this);
            if (Creator.IsOpen) return;
            Creator.Open(editMode);
        }

        /// CreatorScreen gọi khi người chơi bấm "Mở quán!" / "Lưu".
        public void ConfirmProfile(string ownerName, string shopName, OwnerLook look)
        {
            OwnerName = ownerName;
            ShopName = shopName;
            OwnerLook = look;
            CreatorDone = true;
            if (Owner != null) Owner.SetLook(look);
            if (Sign != null) Sign.SetText(shopName);
            if (ProfileChanged != null) ProfileChanged();
            SaveSnapshot();
            if (pendingStartDay > 0)
            {
                int day = pendingStartDay;
                pendingStartDay = -1;
                StartDay(day);
            }
        }

        public FurnitureDef GetDef(string id)
        {
            FurnitureDef d;
            return (!string.IsNullOrEmpty(id) && defById.TryGetValue(id, out d)) ? d : null;
        }

        // ---- Ngày ----

        public void StartNextDay()
        {
            StartDay(Day.Day + 1);
        }

        private void StartDay(int day)
        {
            // Qua đêm: thu hết ly để lại
            while (LeftoverCups.Count > 0)
            {
                LeftoverCup c = LeftoverCups[LeftoverCups.Count - 1];
                if (c == null) LeftoverCups.RemoveAt(LeftoverCups.Count - 1);
                else c.Collect(true);
            }

            Orders.Clear();
            Shelf.Clear();
            Station.ResetAll();
            if (Owner != null) Owner.ResetForNewDay();
            if (Staff != null) Staff.ResetForNewDay();

            Day.Begin(day);
            Tier.OnDayBegin(Day.Summary); // cấp 2: tới kỳ thì trừ tiền thuê nhà (trước khi chốt mốc Xu đầu ngày)
            dayStartXu = Wallet.Xu;
            dayStartRp = Reputation.RP;
            Spawner.BeginDay(day, Reputation.Stars, Grid.Seats.Count);
            GameEvents.RaiseDayStarted(day);
        }

        /// Hệ số nhân thời gian pha của nhân viên Pha chế theo máy móc đang đặt trong quán (1 = không có máy).
        public float BrewTimeFactor
        {
            get { return Grid != null ? MachineRules.BrewTimeFactor(Grid.HasMachine) : 1f; }
        }

        /// Bổ sung ly cho đủ theo số ghế (gọi khi bắt đầu ngày và mỗi khi bố cục / cấp quán đổi).
        public void TopUpCups()
        {
            if (Stock == null || Grid == null || Tier == null) return;
            Stock.EnsureTotal(GameBalance.CupsFor(Tier.Current, Grid.Seats.Count));
        }

        /// Chọn món theo orderWeight trong số món đã biết + trong menu + đúng cấp (RecipeBook.PickRecipe).
        public RecipeDef PickRecipe(int day)
        {
            return Recipes != null ? Recipes.PickRecipe(day) : null;
        }

        public static bool IsUnlocked(RecipeDef r, int day)
        {
            return r != null && r.unlockTier <= 1 && day >= r.unlockDay;
        }

        /// C_ly (T1): 1 nếu ly để lại được thu kịp; giảm tuyến tính về 0 sau 2 giờ in-game.
        public float LeftoverCleanliness()
        {
            float now = Day.InGameSeconds;
            float worst = 0f;
            for (int i = 0; i < LeftoverCups.Count; i++)
            {
                LeftoverCup c = LeftoverCups[i];
                if (c != null && c.IsCollectable) worst = Mathf.Max(worst, now - c.CollectableSince);
            }
            float over = Mathf.Max(0f, worst - GameBalance.LeftoverBlockInGameSeconds);
            return 1f - Mathf.Clamp01(over / GameBalance.LeftoverFullDecayInGameSeconds);
        }

        // ---- Lưu ----

        /// Cuối ngày: lưu ngày kế tiếp. Giữa ngày: lưu mốc đầu ngày (không lợi dụng thoát-vào để giữ tiền).
        public void SaveSnapshot()
        {
            if (!started) return;
            var data = new SaveData();
            data.layout = Grid.ExportLayout().ToArray();
            data.purchases = Grid.ExportPurchases();
            data.creatorDone = CreatorDone;
            data.ownerName = OwnerName;
            data.shopName = ShopName;
            data.ownerLook = OwnerLook;
            data.gems = Gems != null ? Gems.Gems : 0;
            if (Recipes != null) Recipes.Save(data); // knownRecipes, menuToday, toolLevels
            if (Tier != null) Tier.WriteToSave(data); // tier, deposit, rentDebt, daysSinceRent, inventory
            if (Staff != null) Staff.WriteToSave(data);
            if (Regulars != null) Regulars.WriteToSave(data);
            if (Missions != null) Missions.WriteToSave(data);
            if (Day.CurrentPhase == DayCycle.Phase.Summary)
            {
                data.day = Day.Day + 1;
                data.xu = Wallet.Xu;
                data.rp = Reputation.RP;
            }
            else
            {
                data.day = Day.Day > 0 ? Day.Day : Mathf.Max(1, pendingStartDay);
                data.xu = Mathf.RoundToInt(dayStartXu);
                data.rp = dayStartRp;
            }
            using (PerfNote.Measure("lưu game")) SaveSystem.Save(data);
        }

        /// Chốt Xu / RP hiện tại làm mốc đầu ngày (chuyển cấp quán: mốc quan trọng, không quay lại trạng thái trước cọc khi thoát-vào).
        public void CommitDayBaseline()
        {
            dayStartXu = Wallet.Xu;
            dayStartRp = Reputation.RP;
        }

        /// Cửa hàng: dời mốc Xu đầu ngày theo giao dịch (mua = âm, bán = dương) để thoát-vào không hoàn tiền; rồi lưu.
        public void NotifyShopTransaction(int xuDelta)
        {
            dayStartXu = Mathf.Max(0f, dayStartXu + xuDelta);
            SaveSnapshot();
        }

        /// Thưởng nhận giữa ngày (nhiệm vụ, rương): dời cả mốc Xu và RP đầu ngày để không bị mất khi thoát-vào; rồi lưu.
        public void NotifyRewardGranted(int xu, float rp)
        {
            dayStartXu = Mathf.Max(0f, dayStartXu + xu);
            dayStartRp = Mathf.Max(0f, dayStartRp + rp);
            SaveSnapshot();
        }

        /// Gợi ý đã hiện ở bảng tổng kết gần nhất (không lưu): để không lặp lại cùng một gợi ý hai ngày liền.
        public DayAdvice LastAdvice { get; private set; }

        /// Chọn gợi ý cho bảng tổng kết của ngày vừa xong.
        public DayAdvice AdviseFor(DaySummary summary)
        {
            DayAdvice advice = DayAdvisor.Pick(new DayAdvisorInput
            {
                summary = summary,
                tier = Tier != null ? Tier.Current : 1,
                hasCleaner = Staff != null && Staff.HasRole(StaffRole.Cleaner),
                debt = Tier != null && Tier.Ledger != null ? Tier.Ledger.Debt : 0,
                missionReady = Missions != null && Missions.HasClaimable,
                lastAdvice = LastAdvice
            });
            LastAdvice = advice;
            return advice;
        }

        // ---- Nhiệm vụ ngày ----

        private void EnsureMissions()
        {
            if (Missions == null) return;
            bool hidden = false;
            if (recipes != null && Recipes != null)
                for (int i = 0; i < recipes.Length && !hidden; i++)
                    hidden = recipes[i] != null && recipes[i].hidden && Recipes.IsKnown(recipes[i]);
            var ctx = new MissionContext { RecipeKnown = id => Recipes != null && Recipes.IsKnown(id), HasHiddenKnown = hidden };
            if (Missions.EnsureToday(MissionClock.DayKey(System.DateTime.UtcNow), Tier != null ? Tier.Current : 1, ctx) && started)
                SaveSnapshot();
        }

        private void OnCustomerServedForMissions(Customer c)
        {
            RecipeDef r = c != null ? c.ServedRecipe : null;
            if (r == null || Missions == null) return;
            Missions.RecordServed(r.id, c.ServedQuality, r.hidden, c.LastTip);
        }

        private void OnDayStartedForMissions(int day)
        {
            EnsureMissions(); // phiên chơi dài qua 04:00 thì sang bộ nhiệm vụ mới
        }

        private void OnDayEndedForMissions(DaySummary s)
        {
            if (Missions == null || s == null) return;
            Missions.RecordDay(s.served, s.lost, s.cleanliness);
            SaveSnapshot();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveSnapshot();
            else EnsureMissions(); // quay lại app sau khi qua 04:00
        }

        private void OnApplicationQuit()
        {
            SaveSnapshot();
        }

        private void OnDestroy()
        {
            GameEvents.CustomerServed -= OnCustomerServedForMissions;
            GameEvents.DayStarted -= OnDayStartedForMissions;
            GameEvents.DayEnded -= OnDayEndedForMissions;
            if (Staff != null) Staff.Dispose();
            if (Station != null) Station.Dispose();
            if (Recipes != null)
            {
                if (Tier != null) Tier.TierChanged -= Recipes.NotifyTierChanged;
                Recipes.Dispose();
            }
            if (Instance == this) Instance = null;
        }
    }
}
