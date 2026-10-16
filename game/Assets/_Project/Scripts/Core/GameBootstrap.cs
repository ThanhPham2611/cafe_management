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
        /// Hướng dẫn người chơi mới (ngày 1 + gợi ý theo tình huống). Tạo sau HUD.
        public TutorialDirector Tutorial { get; private set; }
        public CafeCameraController CameraRig { get; private set; }
        public BuildModeController Builder { get; private set; }
        public OwnerController Owner { get; private set; }
        public StaffRoster Staff { get; private set; }
        public RegularsManager Regulars { get; private set; }
        /// Lời thoại nền của khách (barks.json); xem Regulars/BarkBook.cs.
        public BarkBook Barks { get; } = new BarkBook();
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
        /// Ngày lấy từ save lúc nạp: giữ để SaveSnapshot khi màn tiêu đề còn mở (Day.Day == 0) không ghi lùi về ngày 1.
        private int loadedStartDay = 1;
        /// Tên quán / ngoại hình đã đổi (HUD, biển hiệu cập nhật).
        public event System.Action ProfileChanged;

        /// Build() đã chạy xong (HUD + màn tiêu đề đã dựng): màn tải của scene Boot chờ cờ này rồi mới mờ đi.
        public static bool IsBuilt { get; private set; }
        public static event System.Action BuildFinished;
        /// Màn tiêu đề (khác null sau Build(); IsOpen = đang hiện, ngày chưa bắt đầu).
        public TitleScreen Title { get; private set; }

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
            IsBuilt = false;
            // Mức đồ họa (Thấp / Vừa / Cao): người chơi chọn trong Cài đặt, lần đầu tự chọn theo máy. Trên điện thoại đặt luôn
            // giới hạn FPS (30 cho mức Thấp, 60 cho còn lại; plan §4); trên máy tính không giới hạn để đo hiệu năng.
            GraphicsQuality.Init();
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
            Reputation.Init(hasSave ? save.rp : 0f, hasSave ? save.levelRewarded : 0);
            // Thưởng level đã nhận: save mới có levelClaimed; save từ lúc thưởng còn trả ngay thì mọi level đã đạt đã được trả;
            // save trước hệ level (cả hai trường trống) được trả bù một lần sau khi HUD dựng xong (quà tri ân)
            ClaimedLevel = !hasSave ? 1 : (save.levelClaimed >= 1 ? save.levelClaimed : Mathf.Max(1, save.levelRewarded));
            ClaimedLevel = Mathf.Min(ClaimedLevel, Reputation.RewardedLevel);
            levelBackPay = hasSave && save.levelClaimed < 0 && save.levelRewarded <= 0 && Reputation.RewardedLevel > 1;
            // Cấp quán: phải có trước sổ công thức và lưới (cả hai đọc cấp hiện tại)
            Tier = new GameObject("TierManager").AddComponent<TierManager>();
            Tier.Init(this, hasSave ? save : null);
            RecipeBook.TierProvider = () => Tier != null ? Tier.Current : 1;
            CleaningRules.TierProvider = () => Tier != null ? Tier.Current : 1;
            // Hạt + sổ công thức (SaveData v3: gems, knownRecipes, menuToday, toolLevels). RecipeBook.TierProvider phải được gán TRƯỚC dòng này.
            Gems = new GemWallet();
            Gems.Init(hasSave ? save.gems : 0);
            Weekly.Load(hasSave ? save.weekIndex : 0, hasSave ? save.weekProgress : 0, hasSave && save.weekDone, hasSave ? save.weekTier : 1);
            if (hasSave && save.fundTier > 0) Fund.Load(save.fundTier, save.fundStage);
            else Fund.Reset(Tier.NextTier, Wallet.Xu, Tier.NextTierXu); // save cũ / ván mới: không thưởng bù chặng đã qua
            Recipes = new RecipeBook(this);
            Recipes.Load(hasSave ? save : null);
            if (Tier != null) Tier.TierChanged += Recipes.NotifyTierChanged; // lên cấp: mở món thường của cấp mới, làm mới khay/sổ
            Stock.Init(GameBalance.StartCups);
            Shelf = new ReadyShelf(() => Stock.Return()); // ly bị bỏ (khách đi mất) trả ly sạch về kho
            int startDay = hasSave ? Mathf.Max(1, save.day) : 1;
            loadedStartDay = startDay;
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
                // Save nhà hàng cũ (lưới 20 x 24): sảnh nay bắt đầu ở ô x = 12 của lưới 44 x 24
                int shiftX = hasSave ? TierManager.LayoutShiftCells(save) : 0;
                for (int i = 0; i < layout.Length; i++)
                {
                    LayoutEntry e = layout[i];
                    FurnitureDef d = GetDef(e.furnitureId);
                    if (d == null)
                    {
                        Debug.LogWarning("[GameBootstrap] Không có FurnitureDef id=" + e.furnitureId);
                        continue;
                    }
                    Vector2Int cell = new Vector2Int(e.cell.x + shiftX, e.cell.y);
                    if (Grid.Place(d, cell, e.rotation) == null)
                        Debug.LogWarning("[GameBootstrap] Không đặt được " + e.furnitureId + " tại " + cell);
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
            Staff = new StaffRoster(Wallet, Tier.Ledger, () => Tier != null ? Tier.Current : 1, () => Tier != null ? Tier.OpenZoneCount : 0);
            Staff.Load(hasSave ? save : null);
            Staff.Attach(this);
            Regulars = new RegularsManager();
            Regulars.Init(this, hasSave ? save : null);
            Barks.Load(barksJson);

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
            GameEvents.LevelUp += OnLevelReached;
            GameEvents.MoneyChanged += OnMoneyForFund;
            GameEvents.DayEnded += OnDayEndedClaimLevels;

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
            if (levelBackPay)
            {
                // Save trước hệ level: trả bù thưởng các level đã đạt một lần (quà tri ân)
                levelBackPay = false;
                ClaimAllLevels();
                GameEvents.RaiseToast(Strings.LevelGiftToast);
            }
            Hud.RefreshLevelCardNow();
            Tutorial = TutorialDirector.Create(this, hasSave ? save : null); // phải có trước StartDay: nghe DayStarted(1)

            started = true;
            // Màn tiêu đề chặn đầu ngày: Day còn Idle nên chưa có khách. Bot / batchmode bỏ qua (TitleScreen.SkipForAutomation).
            if (TitleScreen.SkipForAutomation)
            {
                AfterTitle(startDay);
            }
            else
            {
                Title = TitleScreen.Create(this, () => AfterTitle(startDay));
                Title.Show();
            }
            IsBuilt = true;
            if (BuildFinished != null) BuildFinished();
        }

        /// Sau màn tiêu đề: chưa tạo chủ quán thì mở màn tạo, ngược lại bắt đầu ngày.
        private void AfterTitle(int startDay)
        {
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

        /// Mở cửa ngày kế tiếp: từ nút "Ngày mới" ở màn tổng kết hoặc nút "Mở cửa" ở pha chuẩn bị.
        public void StartNextDay()
        {
            StartDay(Day.Day + 1);
        }

        /// Màn tổng kết -> pha chuẩn bị: đồng hồ đứng, chưa có khách; mua đồ / bày quán / nâng cấp quán rồi mới mở cửa.
        public void EnterPrep()
        {
            if (Day.CurrentPhase != DayCycle.Phase.Summary) return;
            ResetForNight();
            Day.EnterPrep();
            SaveSnapshot();
            GameEvents.RaisePrepStarted();
        }

        /// Qua đêm: thu hết ly để lại, dọn đơn / quầy, chủ quán và nhân viên về chỗ.
        private void ResetForNight()
        {
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
        }

        private void StartDay(int day)
        {
            ResetForNight();

            Day.Begin(day);
            Day.Summary.levelStart = Reputation.Level;
            Weekly.Sync(day, Tier.Current);
            TodayEvent = DayEvents.For(day);
            if (!TodayEvent.IsNormal) Debug.Log($"[Event] ngày {day}: {TodayEvent.name} (khách x{TodayEvent.demand:0.00}, tip x{TodayEvent.tip:0.00}, kiên nhẫn x{TodayEvent.patience:0.00})");
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

        /// Số ly để lại đã chạm được (khách uống xong) nhưng chưa thu.
        public int WaitingCupCount()
        {
            int n = 0;
            for (int i = 0; i < LeftoverCups.Count; i++)
                if (LeftoverCups[i] != null && LeftoverCups[i].IsCollectable) n++;
            return n;
        }

        /// Giao chủ quán thu mọi ly đang chờ (ly chờ lâu nhất trước); dừng khi hàng việc đầy. Trả số ly đã giao.
        public int QueueCollectAll()
        {
            if (Owner == null) return 0;
            var waiting = new List<LeftoverCup>();
            for (int i = 0; i < LeftoverCups.Count; i++)
                if (LeftoverCups[i] != null && LeftoverCups[i].IsCollectable) waiting.Add(LeftoverCups[i]);
            waiting.Sort((a, b) => a.CollectableSince.CompareTo(b.CollectableSince));
            int queued = 0;
            for (int i = 0; i < waiting.Count; i++)
            {
                if (!Owner.QueueCollect(waiting[i])) break;
                queued++;
            }
            return queued;
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
            if (Tutorial != null) Tutorial.WriteToSave(data);
            data.levelRewarded = Reputation.RewardedLevel;
            data.levelClaimed = ClaimedLevel;
            data.fundTier = Fund.TargetTier;
            data.fundStage = Fund.Stage;
            data.weekIndex = Weekly.Week;
            data.weekProgress = Weekly.Progress;
            data.weekDone = Weekly.Done;
            data.weekTier = Weekly.Tier;
            if (Day.CurrentPhase == DayCycle.Phase.Summary || Day.CurrentPhase == DayCycle.Phase.Prep)
            {
                data.day = Day.Day + 1;
                data.xu = Wallet.Xu;
                data.rp = Reputation.RP;
            }
            else
            {
                data.day = Day.Day > 0 ? Day.Day : Mathf.Max(1, Mathf.Max(pendingStartDay, loadedStartDay));
                data.xu = Mathf.RoundToInt(dayStartXu);
                // Không lưu RP thấp hơn mốc level đã thưởng: vào lại giữa ngày thì HUD không hiện level thấp hơn level đã nhận
                data.rp = Mathf.Max(dayStartRp, LevelRules.RpFor(Reputation.RewardedLevel));
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
            ShiftDayBaseline(xu, rp);
            SaveSnapshot();
        }

        /// Dời mốc Xu / RP đầu ngày mà chưa lưu. Gọi TRƯỚC Wallet.Earn / Gems.Add: Earn bắn MoneyChanged (quỹ, HUD...) và một handler
        /// lỗi giữa chừng không được để thưởng đã tính mà mốc chưa dời (thoát-vào sẽ mất thưởng).
        private void ShiftDayBaseline(int xu, float rp)
        {
            dayStartXu = Mathf.Max(0f, dayStartXu + xu);
            dayStartRp = Mathf.Max(0f, dayStartRp + rp);
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
                canOpenZone = CanAffordAnyZone(),
                lastAdvice = LastAdvice
            });
            LastAdvice = advice;
            return advice;
        }

        /// Cấp 3 và còn một khu chưa mở mà ví đủ tiền (để gợi ý ở bảng tổng kết).
        private bool CanAffordAnyZone()
        {
            if (Tier == null || Tier.Current < 3 || Wallet == null) return false;
            for (int i = 0; i < ZoneInfo.Buyable.Length; i++)
            {
                ZoneId z = ZoneInfo.Buyable[i];
                if (!Tier.IsZoneOpen(z) && Wallet.Xu >= ZoneInfo.Cost(z)) return true;
            }
            return false;
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
            bool favorite = Regulars != null && Regulars.IsFavorite(c.RegularId, r.id);
            Missions.RecordServed(r.id, c.ServedQuality, r.hidden, c.LastTip, favorite);
        }

        private void OnDayStartedForMissions(int day)
        {
            EnsureMissions(); // phiên chơi dài qua 04:00 thì sang bộ nhiệm vụ mới
        }

        /// Level uy tín cao nhất đã nhận thưởng (<= Reputation.RewardedLevel = level đã đạt). Chênh lệch là thưởng đang chờ
        /// người chơi chạm thẻ ở HUD; cuối ngày tự nhận hết để thưởng không bị bỏ quên.
        public int ClaimedLevel { get; private set; } = 1;
        private bool levelBackPay;

        /// Sự kiện của ngày đang chơi (DayEvents.For theo số ngày); báo trước ở pha Chuẩn bị.
        public DayEvent TodayEvent { get; private set; } = DayEvents.Normal;

        /// Dự án tuần (tuần game = 7 ngày) và quỹ lên cấp quán 4 chặng; xem Economy/WeeklyProject.cs, HouseFund.cs.
        public WeeklyProject Weekly { get; } = new WeeklyProject();
        public HouseFundTracker Fund { get; } = new HouseFundTracker();

        public int PendingLevels { get { return Mathf.Max(0, Reputation.RewardedLevel - ClaimedLevel); } }

        private void OnLevelReached(int level)
        {
            Debug.Log($"[Level] đạt Lv {level} (RP {Reputation.RP:0}), chờ nhận +{LevelRules.RewardXu(level)} Xu");
        }

        private void OnDayEndedClaimLevels(DaySummary s)
        {
            ClaimAllLevels();
        }

        /// Nhận thưởng level kế tiếp đang chờ (chạm thẻ ở HUD). Trả true nếu có gì để nhận.
        public bool ClaimNextLevel() { return ClaimLevels(1); }

        /// Nhận hết thưởng đang chờ (cuối ngày, hoặc quà tri ân).
        public void ClaimAllLevels() { ClaimLevels(int.MaxValue); }

        /// Cộng Xu (và Hạt ở level mốc sao). Dời mốc Xu đầu ngày như thưởng nhiệm vụ để vào lại giữa ngày không mất thưởng;
        /// level đã nhận được lưu cùng lúc nên không bị thưởng hai lần. Hiệu ứng nằm ở HudController (LevelsClaimed).
        private bool ClaimLevels(int max)
        {
            int from = ClaimedLevel + 1, count = 0, xu = 0, gems = 0;
            while (count < max && ClaimedLevel < Reputation.RewardedLevel)
            {
                ClaimedLevel++;
                count++;
                xu += LevelRules.RewardXu(ClaimedLevel);
                gems += LevelRules.RewardGems(ClaimedLevel);
            }
            if (count == 0) return false;
            ShiftDayBaseline(xu, 0f);
            if (gems > 0 && Gems != null) Gems.Add(gems);
            Wallet.Earn(xu);
            Debug.Log($"[Level] nhận Lv {from}" + (ClaimedLevel > from ? "-" + ClaimedLevel : "") + $" (RP {Reputation.RP:0}): +{xu} Xu" + (gems > 0 ? $", +{gems} Hạt" : ""));
            SaveSnapshot();
            GameEvents.RaiseLevelsClaimed(from, ClaimedLevel, xu, gems);
            return true;
        }

        private void OnDayEndedForMissions(DaySummary s)
        {
            if (Missions == null || s == null) return;
            // "Không ai bỏ về" chỉ tính khách bực (chờ lâu / bẩn); hết chỗ là quán đông, không phải lỗi người chơi
            Missions.RecordDay(s.served, s.lost - s.lostNoSeat, s.cleanliness, s.tier);
            if (Weekly.RecordDay(s.day, s.tier, s.served, s.tips, s.NetProfit))
            {
                int xu = WeeklyProject.RewardXu(Weekly.Tier), gems = WeeklyProject.RewardGems(Weekly.Tier);
                ShiftDayBaseline(xu, 0f);
                if (Gems != null) Gems.Add(gems);
                Wallet.Earn(xu);
                Debug.Log($"[Weekly] xong dự án tuần {Weekly.Week + 1} ({Weekly.Kind}, {Weekly.Progress}/{Weekly.Target}): +{xu} Xu, +{gems} Hạt");
                GameEvents.RaiseWeeklyDone(xu, gems);
            }
            SaveSnapshot();
        }

        /// Xu đổi: quỹ lên cấp quán đạt chặng mới thì thưởng Hạt (mỗi chặng một lần cho mỗi cấp đích).
        private void OnMoneyForFund(int xu)
        {
            if (Tier == null || Wallet == null || Tier.Transitioning || Day == null || Day.CurrentPhase == DayCycle.Phase.Idle) return;
            try { AdvanceFund(); }
            catch (System.Exception e) { Debug.LogException(e); } // MoneyChanged nằm trong Customer.Pay: lỗi ở đây không được làm đứt tính tiền
        }

        private void AdvanceFund()
        {
            int from, to;
            if (!Fund.Advance(Tier.NextTier, Wallet.Xu, Tier.NextTierXu, out from, out to)) return;
            int gemsTotal = 0;
            for (int stage = from + 1; stage <= to; stage++) gemsTotal += HouseFund.RewardGems(stage, Fund.TargetTier);
            if (gemsTotal <= 0 || Gems == null) return;
            Gems.Add(gemsTotal);
            Debug.Log($"[Fund] quỹ cấp {Fund.TargetTier} đạt chặng {to}/4: +{gemsTotal} Hạt");
            NotifyRewardGranted(0, 0f);
            GameEvents.RaiseFundStage(Fund.TargetTier, Mathf.Min(to, HouseFund.Stages - 1), gemsTotal);
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
            GameEvents.LevelUp -= OnLevelReached;
            GameEvents.MoneyChanged -= OnMoneyForFund;
            GameEvents.DayEnded -= OnDayEndedClaimLevels;
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
