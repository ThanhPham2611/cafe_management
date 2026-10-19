using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cafe
{
    /// Dựng và cập nhật HUD ngang "Clay Café" (docs/ui_style.md §4):
    /// chip trên (xu / sao + RP / ngày + giờ), cột thẻ order bên trái, bàn pha giữa-dưới, chip ly, tổng kết cuối ngày.
    public class HudController : MonoBehaviour
    {
        // HUD gọn (docs/hud_brew_redesign_2026-10-18.md A2): số spec ở 1280x720 x 1,25 = canvas HUD 900
        private const int MaxCards = 3; // tối đa 3 thẻ + dòng "+N"
        private const float CardWidth = 250f;
        private const float CardHeight = 80f;
        private const float CardSpacing = 10f;
        private const float CardTitleH = 44f; // tiêu đề "Đơn chờ" (chỉ trong hướng dẫn)
        private const float Margin = 20f;
        private const float TopMargin = 10f;
        private const float ChipH = 64f;     // mọi chip cùng một họ (hud_visual_polish §2)
        private const float ChipIcon = 76f;  // đĩa icon tròn lồi ra trái
        private const float DayChipW = 200f;
        private const float MenuBtnH = 108f; // ô trong popup MENU, >= 44 pt ở canvas HUD 900
        private const float MenuItemW = 260f;
        private const float MenuGap = 12f;
        private const float MenuBtnW = 72f;  // nút coffee 72x64 + icon_menu
        private const float MenuRight = Margin + DayChipW + ChipIcon * 0.5f + 4f; // chừa chip ngày + icon đồng hồ nhô ra trái
        private const float CompactShopSize = 96f; // nút Cửa hàng tròn 96 + icon_shop khi mở cửa
        private const float XuCountSeconds = 0.4f;   // số Xu đếm lên
        private const float CoinPopSeconds = 0.3f;   // chip Xu nảy 1,08 + đĩa coin xoay nhẹ
        private const float CardSlideSeconds = 0.25f; // thẻ đơn mới: trượt từ trái + nảy
        private const float RpRevealSeconds = 3f;

        private class CardView
        {
            public ClayButton button;
            public Image icon;
            public Text nameText;
            public Text priceText;
            public Image ringFill;
            public Image iconBg;
            public GameObject border;   // viền vàng 4 px: thẻ đang pha
            public Order order;
            public float slide = -1f;   // >= 0: đang trượt vào
        }

        private GameBootstrap game;
        private RectTransform bubbleLayer;

        private Text xuText;
        private Image coinIcon;
        private Text starCountText;
        private Text rpText;
        private BarView rpBar;
        private Text dayText;
        private Text timeText;
        private Text cupText;
        private RectTransform orderColumn;
        private int orderCardCount;

        /// Điểm màn hình nằm dưới các thẻ "Đơn chờ" đang hiện (bong bóng order ở đó bị thẻ che, nên ẩn hẳn cho gọn).
        public bool IsUnderOrderCards(Vector2 screenPos)
        {
            if (orderColumn == null || orderCardCount <= 0) return false;
            Vector3[] c = cornerBuf;
            orderColumn.GetWorldCorners(c); // canvas overlay: toạ độ thế giới = pixel màn hình
            float bottomCanvas = cardTop + orderCardCount * (CardHeight + CardSpacing);
            float scale = orderColumn.lossyScale.y;
            float bottom = c[1].y - bottomCanvas * scale;
            return screenPos.x >= c[0].x && screenPos.x <= c[2].x + 20f * scale && screenPos.y <= c[1].y && screenPos.y >= bottom;
        }
        private readonly Vector3[] cornerBuf = new Vector3[4];

        /// Chữ nổi rơi vào vùng thẻ "Đơn chờ": dời sang phải cột để không đè chữ trên thẻ. Trả x màn hình mới.
        public float PushRightOfOrderCards(Vector2 screenPos)
        {
            if (!IsUnderOrderCards(screenPos)) return screenPos.x;
            orderColumn.GetWorldCorners(cornerBuf);
            return cornerBuf[2].x + 170f * orderColumn.lossyScale.x; // ~nửa bề rộng chữ "+115 Xu (tip 19)"
        }
        private Image cupChipImg;
        private Text cupWaitText;
        private GameObject cupWaitButton;
        private GameObject viewButton;
        private Text viewText;
        private int viewShown = -1;
        private int cupWaitShown = -1;
        private Text emptyText;
        private Text moreText;
        private Text ordersTitle;
        private float cardTop = CardTitleH;   // y (từ mép trên cột) của thẻ đầu: có / không có tiêu đề "Đơn chờ"
        private readonly CardView[] cards = new CardView[MaxCards];
        private readonly Color[] cardTint = new Color[MaxCards];
        private bool cardFlashing;

        // HUD gọn: huy hiệu level chạm để xem số RP, nút Cửa hàng tròn khi mở cửa, viên thuốc mục tiêu
        private float rpRevealUntil = -1f;
        private string rpShort = "", rpFull = "";
        private RectTransform shopButtonRt;
        private Text shopButtonLabel;
        private int shopCompact = -1;
        private UpgradePanel upgradePanel;
        private string tutorialReveal;     // mục tiêu hướng dẫn đang được mở ra (RevealForTutorial)
        private bool menuOpenedForTutorial;

        private Text shopNameText;
        private Image shopNameChip;        // viên kem chứa tên quán (trước là chữ trơn nổi trên tường quán)
        private GameObject hudCanvasGo;
        private bool hudHidden;

        private BrewPanel brewPanel;
        private SummaryPanel summaryPanel;
        private ShopPanel shopPanel;
        private RecipeBookPanel recipeBook;
        private StaffPanel staffPanel;
        private RegularsBookPanel regularsBook;
        private MissionPanel missionPanel;
        private SettingsPanel settingsPanel;
        private ZonePanel zonePanel;
        private DecorPanel decorPanel;
        private StoryDialog storyDialog;
        public bool StoryOpen { get { return storyDialog != null && storyDialog.IsOpen; } }
        public void DebugAdvanceStory() { if (storyDialog != null) storyDialog.DebugAdvance(); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugToggleStaffPanel() { if (staffPanel != null) staffPanel.DebugToggle(); }
        public void DebugScrollStaff(float normalized) { if (staffPanel != null) staffPanel.DebugScroll(normalized); }
        public void DebugOpenSettings(bool open) { if (settingsPanel != null) settingsPanel.DebugOpen(open); }
        public void DebugOpenMissions(bool open) { if (missionPanel != null) missionPanel.DebugOpen(open); }
        public void DebugOpenRegulars(bool open) { if (regularsBook != null) regularsBook.DebugOpen(open); }
        public void DebugShopTab(bool decor) { if (shopPanel != null) shopPanel.DebugSetTab(decor); }
        public void DebugOpenZones(bool open) { if (zonePanel != null) zonePanel.DebugOpen(open); }
        public void DebugOpenMenu(bool open) { if (menuRoot != null) menuRoot.gameObject.SetActive(open); }
        public DecorPanel DebugDecorPanel { get { return decorPanel; } }
#endif

        private ClayButton openShopButton; // nút MỞ CỬA, chỉ hiện ở pha chuẩn bị
        private Text eventText;            // "Hôm nay: Trời mưa - ..." (chỉ ngày có sự kiện)
        private Image eventChip;           // viên nền của dòng sự kiện
        private const float EventChipMaxW = 760f;
        private ClayButton levelCard;      // thẻ "Lv N: NHẬN +X Xu" khi có thưởng level đang chờ
        private Text levelCardText;
        private float levelCardShownAt;
        private RectTransform menuRoot;
        private GameObject menuDot;     // chấm đỏ trên nút MENU khi có nhiệm vụ nhận được
        private GameObject missionDot;
        private int lastMinute = -1;
        private bool xuInit;
        private int xuTarget;
        private float xuShown;
        private int xuShownInt = int.MinValue;
        private float coinPop = -1f;
        private int xuWallet;            // số Xu thật
        private int xuPending;           // Xu đang bay (CoinFly) chưa cộng vào số hiển thị
        private float xuFrom, xuTo;
        private float xuTween = -1f;     // >= 0: đang đếm lên / xuống
        private RectTransform coinChipRt;
        private CoinFly coinFly;
        private GameObject menuBadge;
        private Text menuBadgeText;
        private int menuBadgeShown = -1;
        private Image ordersTab;
        private Text ordersTabCount;
        private Image shopIcon;
        private readonly Order[] prevOrders = new Order[MaxCards];
        private bool cardAnimating;
        private int cupShown = -1, cupTotalShown = -1;

        /// Lớp UI chứa bong bóng order (dưới HUD, không chặn chạm).
        public RectTransform BubbleLayer { get { return bubbleLayer; } }

        /// Thẻ món trong Cửa hàng (hướng dẫn mua ghế chỉ vào đây); null nếu bảng chưa mở.
        public RectTransform ShopCardRect(string furnitureId)
        {
            return shopPanel != null ? shopPanel.CardRectFor(furnitureId) : null;
        }

        public void Init(GameBootstrap g)
        {
            game = g;
            HudBuilder.SetTheme(g.uiTheme);

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }
            // Ngưỡng kéo ~2 mm theo mật độ điểm ảnh (mặc định 10 px quá nhỏ trên điện thoại): ngón tay rung nhẹ không biến một lần chạm
            // nút THUÊ / NÂNG / MUA trong bảng cuộn thành thao tác kéo làm hụt nút
            EventSystem eventSystem = EventSystem.current != null ? EventSystem.current : FindAnyObjectByType<EventSystem>();
            if (eventSystem != null && Screen.dpi > 0f) eventSystem.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt(Screen.dpi * 0.08f));

            // Lớp bong bóng (dưới)
            Canvas bubbleCanvas = HudBuilder.CreateCanvas("BubbleCanvas", 0, false, transform);
            bubbleLayer = HudBuilder.NewRect("BubbleLayer", bubbleCanvas.transform);
            HudBuilder.Stretch(bubbleLayer);

            // HUD chính
            Canvas canvas = HudBuilder.CreateCanvas("HudCanvas", 10, true, transform, HudBuilder.HudRefHeight);
            hudCanvasGo = canvas.gameObject;
            RectTransform safe = HudBuilder.NewRect("SafeArea", canvas.transform);
            HudBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            BuildChips(safe);
            BuildOrderColumn(safe);
            upgradePanel = UpgradePanel.Create(game, safe); // viên thuốc / thẻ "Thuê mặt bằng" (cấp 1) / dòng tiền thuê (cấp 2) + hộp xác nhận + màn tối chuyển cấp
            brewPanel = new BrewPanel(game, safe);
            shopPanel = new ShopPanel(game, safe);
            recipeBook = new RecipeBookPanel(game, safe, brewPanel); // nút "Sổ công thức" trên mép bàn pha + bảng
            CleaningHud.Create(game, safe); // chip độ sạch (CI%) + nút "Lau sàn"; chỉ hiện khi CleaningRules.Enabled
            staffPanel = new StaffPanel(game, safe);
            summaryPanel = new SummaryPanel(game, canvas.transform);
            regularsBook = new RegularsBookPanel(game, safe, canvas.transform);
            missionPanel = new MissionPanel(game, safe, canvas.transform);
            settingsPanel = new SettingsPanel(game, safe, canvas.transform); // nút "ĐỒ HỌA" + chọn Thấp / Vừa / Cao
            zonePanel = new ZonePanel(game, safe, canvas.transform);         // nút "MỞ RỘNG" (cấp 3) + bảng Sân vườn / Lầu / VIP
            decorPanel = new DecorPanel(game, safe, canvas.transform);       // nút "TRANG TRÍ" (cấp 2+) + bảng sơn tường / sàn
            BuildMenu(safe); // gom các nút phụ vào popup MENU
            BuildOpenShopButton(safe);
            BuildLevelCard(safe);
            BuildEventLabel(safe);
            // Chip độ sạch + nút Lau sàn dựng sau Cửa hàng nên trước đây vẽ ĐÈ lên bảng Cửa hàng (pha chuẩn bị)
            Transform cleanHud = safe.Find("CleaningHud");
            if (cleanHud != null) SendBelowPanels(safe, cleanHud);
            shopButtonRt = safe.Find("ShopButton") as RectTransform;
            if (shopButtonRt != null) shopButtonLabel = shopButtonRt.GetComponentInChildren<Text>(true);
            storyDialog = new StoryDialog(game, canvas.transform);

            // Chữ nổi (trên cùng)
            FloatLayer.Create(transform);

            GameEvents.MoneyChanged += OnMoney;
            GameEvents.ReputationChanged += OnReputation;
            GameEvents.OrderAdded += OnOrderChanged;
            GameEvents.OrderRemoved += OnOrderChanged;
            GameEvents.OrderClaimChanged += OnOrderChanged;
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.DayEnded += OnDayEnded;
            GameEvents.PrepStarted += OnPrepStarted;
            GameEvents.LevelUp += OnLevelUp;
            GameEvents.LevelsClaimed += OnLevelsClaimed;
            GameEvents.FundStage += OnFundStage;
            GameEvents.WeeklyDone += OnWeeklyDone;
            GameEvents.CustomerServed += OnServed;
            GameEvents.Toast += ShowToast;
            game.Station.Changed += RebuildCards;
            game.Stock.Changed += OnCups;

            OnMoney(game.Wallet.Xu);
            OnReputation(game.Reputation.RP, game.Reputation.Stars);
            OnCups();
            RebuildCards();
        }

        private void OnProfileChanged()
        {
            SetShopName(game.ShopName);
        }

        /// Màn tạo nhân vật ẩn toàn bộ HUD để lộ cảnh 3D.
        public void SetHudActive(bool v)
        {
            hudHidden = !v;
            if (hudCanvasGo != null) hudCanvasGo.SetActive(v);
            if (!v && game != null && game.CameraRig != null) game.CameraRig.SetBottomInset(0f);
        }

        private void OnDestroy()
        {
            if (game != null) game.ProfileChanged -= OnProfileChanged;
            GameEvents.MoneyChanged -= OnMoney;
            GameEvents.ReputationChanged -= OnReputation;
            GameEvents.OrderAdded -= OnOrderChanged;
            GameEvents.OrderRemoved -= OnOrderChanged;
            GameEvents.OrderClaimChanged -= OnOrderChanged;
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DayEnded -= OnDayEnded;
            GameEvents.PrepStarted -= OnPrepStarted;
            GameEvents.LevelUp -= OnLevelUp;
            GameEvents.LevelsClaimed -= OnLevelsClaimed;
            GameEvents.FundStage -= OnFundStage;
            GameEvents.WeeklyDone -= OnWeeklyDone;
            GameEvents.CustomerServed -= OnServed;
            GameEvents.Toast -= ShowToast;
            if (game != null)
            {
                if (game.Station != null) game.Station.Changed -= RebuildCards;
                if (game.Stock != null) game.Stock.Changed -= OnCups;
            }
            if (brewPanel != null) brewPanel.Dispose();
            if (shopPanel != null) shopPanel.Dispose();
            if (recipeBook != null) recipeBook.Dispose();
            if (staffPanel != null) staffPanel.Dispose();
            if (regularsBook != null) regularsBook.Dispose();
            if (missionPanel != null) missionPanel.Dispose();
            if (settingsPanel != null) settingsPanel.Dispose();
            if (zonePanel != null) zonePanel.Dispose();
            if (decorPanel != null) decorPanel.Dispose();
            if (storyDialog != null) storyDialog.Dispose();
        }

        // ---------- dựng giao diện ----------

        /// Chip viên thuốc + icon tròn lồi ra bên trái (tâm icon cách mép trái chip 8 px).
        private Image MakeChip(RectTransform parent, string name, Sprite icon, out Image iconImg)
        {
            Image chip = HudBuilder.Chip(parent, name, false);
            iconImg = HudBuilder.Icon(chip.transform, "Icon", icon, ChipIcon);
            HudBuilder.SetFixed(iconImg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8f, 4f), new Vector2(ChipIcon, ChipIcon));
            return chip;
        }

        private void BuildChips(RectTransform safe)
        {
            float iconOut = ChipIcon * 0.5f - 8f; // phần icon nhô ra trái chip

            // --- Xu: trên-trái (16, 8) 200x56 (gồm icon)
            Image coinChip = MakeChip(safe, "CoinChip", HudBuilder.T.iconCoin, out coinIcon);
            HudBuilder.SetFixed(coinChip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin + iconOut, -TopMargin), new Vector2(250f - iconOut, ChipH));
            coinChipRt = coinChip.rectTransform;
            xuText = HudBuilder.AddText(coinChip.transform, "Xu", "", 36, TextAnchor.MiddleRight, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(xuText.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 12f), new Vector2(-18f, -2f));
            HudBuilder.Fit(xuText, 20);

            // --- Huy hiệu "★2 Lv6" + thanh RP mảnh: x = 228 (x1,25); chạm thì hiện số RP vài giây
            Image starIcon;
            Image starChip = MakeChip(safe, "StarChip", HudBuilder.T.iconStar, out starIcon);
            HudBuilder.SetFixed(starChip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(285f + iconOut, -TopMargin), new Vector2(288f - iconOut, ChipH));
            // số sao in trong ngôi sao (Baloo 30, coffee); bên phải: "Lv 2" Baloo 24 + thanh RP 14, không đè nhau
            starCountText = HudBuilder.AddText(starIcon.transform, "Stars", "", 30, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(starCountText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(0f, -8f));
            rpText = HudBuilder.AddText(starChip.transform, "Rp", "", 24, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(rpText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, 1f), new Vector2(190f, 36f)); // Baloo cao dòng ~1,5x: khung thấp làm best-fit co chữ
            HudBuilder.Fit(rpText, 14); // "Lv 30  11.250/11.800" khi chạm xem số: co chữ
            rpBar = HudBuilder.CreateBar(starChip.transform, "RpBar");
            HudBuilder.SetFixed(rpBar.root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(50f, -32f), new Vector2(190f, 14f));
            starChip.raycastTarget = true;
            starChip.gameObject.AddComponent<Button>().onClick.AddListener(OnStarChipClicked);

            // --- Ngày + giờ (phải): "N1 · 07:29" một dòng
            Image clockIcon;
            Image dayChip = MakeChip(safe, "DayChip", HudBuilder.T.iconClock, out clockIcon);
            HudBuilder.SetFixed(dayChip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -TopMargin), new Vector2(DayChipW, ChipH));
            dayText = HudBuilder.AddText(dayChip.transform, "Day", "", 28, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(dayText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(46f, -6f), new Vector2(140f, 30f));
            dayText.gameObject.SetActive(false); // gộp vào dòng giờ
            timeText = HudBuilder.AddText(dayChip.transform, "Time", "", 32, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(timeText.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 12f), new Vector2(-10f, -2f));
            HudBuilder.Fit(timeText, 20);

            // --- Tên quán: hàng trên, bên trái nút MENU; chỉ hiện ở pha Chuẩn bị (ẩn khi mở cửa)
            shopNameChip = HudBuilder.Img(safe, "ShopName", HudBuilder.T.tag != null ? HudBuilder.T.tag : HudBuilder.T.chip, HudBuilder.Cream, true, false);
            shopNameChip.color = new Color(1f, 1f, 1f, 0.94f);
            HudBuilder.SetFixed(shopNameChip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-(MenuRight + MenuBtnW + 16f), -(TopMargin + (ChipH - ShopNameChipH) * 0.5f)), new Vector2(ShopNameMaxW, ShopNameChipH));
            shopNameText = HudBuilder.AddText(shopNameChip.transform, "Text", "", 24, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(shopNameText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            HudBuilder.Fit(shopNameText, 16);
            shopNameText.raycastTarget = false;
            SetShopName(game.ShopName);
            game.ProfileChanged += OnProfileChanged;

            // --- Ly sạch (dưới phải): "3/6" = ly sạch / tổng, đỏ khi hết
            Image cupIcon;
            Image cupChip = MakeChip(safe, "CupChip", HudBuilder.T.iconCup, out cupIcon);
            HudBuilder.SetFixed(cupChip.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, Margin), new Vector2(160f, ChipH));
            cupText = HudBuilder.AddText(cupChip.transform, "Cups", "", 34, TextAnchor.MiddleRight, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(cupText.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 12f), new Vector2(-18f, -2f));
            HudBuilder.Fit(cupText, 20);

            // Chạm chip ly = giao chủ quán thu hết ly đang chờ; nút "Thu hết ly" phía trên chỉ hiện khi có ly chờ thu
            cupChipImg = cupChip;
            cupChip.raycastTarget = true;
            cupChip.gameObject.AddComponent<Button>().onClick.AddListener(OnCupChipClicked);
            // "Thu hết (N)": nút vàng icon ly + huy hiệu số thay cho chữ dài
            ClayButton collectAll = HudBuilder.AddClayButton(safe, "CollectAllButton", ClayStyle.Gold, OnCupChipClicked);
            HudBuilder.SetFixed((RectTransform)collectAll.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, Margin + ChipH + 10f), new Vector2(96f, 74f));
            Image collectIcon = HudBuilder.Icon(collectAll.Content, "Icon", HudBuilder.T.iconCup, 50f);
            HudBuilder.SetFixed(collectIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            MakeBadge(collectAll.transform, "Badge", 34f, out cupWaitText);
            cupWaitButton = collectAll.gameObject;
            cupWaitButton.SetActive(false);

            // Góc nhìn: Trên -> Chéo -> Ngang (ngang tầm mắt nhân vật); nằm ngay trên nút "Thu hết"
            ClayButton view = HudBuilder.AddLabelButton(safe, "ViewButton", ClayStyle.Cream, Strings.ViewMid, 26, OnViewClicked, out viewText);
            HudBuilder.SetFixed((RectTransform)view.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, Margin + ChipH + 8f + 84f + 14f), new Vector2(230f, 84f));
            HudBuilder.Fit(viewText, 18);
            viewButton = view.gameObject;
        }

        private void OnViewClicked()
        {
            if (game.CameraRig != null) game.CameraRig.CycleView();
            RefreshViewButton();
        }

        /// Nút góc nhìn: ẩn khi đang xây (chế độ xây tự nâng góc) và khi hướng dẫn (hướng dẫn cần thấy rõ vật thể); đổi chữ theo góc.
        private void RefreshViewButton()
        {
            CafeCameraController rig = game.CameraRig;
            if (rig == null || viewButton == null) return;
            bool tutorial = TutorialGate.Active;
            rig.TutorialFloor = tutorial;
            bool show = !tutorial && (game.Builder == null || !game.Builder.IsOpen);
            if (viewButton.activeSelf != show) viewButton.SetActive(show);
            int idx = rig.ViewIndex;
            if (idx != viewShown)
            {
                viewShown = idx;
                viewText.text = idx == 0 ? Strings.ViewTop : (idx == 1 ? Strings.ViewMid : Strings.ViewEye);
            }
        }

        /// Nút phụ (Khách quen, Nhân viên, Mở rộng khu, Nhiệm vụ, Đồ họa) gom vào popup MENU: HUD gọn, nút đủ to để chạm.
        /// Các panel tự dựng nút của mình trong `safe`; ở đây chuyển chúng vào cột popup (xếp dọc, nút ẩn thì bỏ qua).
        private void BuildMenu(RectTransform safe)
        {
            menuRoot = HudBuilder.NewRect("MenuRoot", safe);
            HudBuilder.Stretch(menuRoot);
            var scrim = menuRoot.gameObject.AddComponent<Image>(); // nền trong suốt: chạm ra ngoài để đóng
            scrim.color = Color.clear;
            menuRoot.gameObject.AddComponent<Button>().onClick.AddListener(CloseMenu);

            // Lưới 2 cột: cấp 3 có tới 8 mục, một cột 108 px sẽ tràn khỏi canvas cao 900 (nút cuối bị cắt mất)
            RectTransform column = HudBuilder.NewRect("Column", menuRoot);
            HudBuilder.SetFixed(column, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-MenuRight, -(TopMargin + ChipH + 8f)), new Vector2(MenuItemW * 2f + MenuGap, 0f));
            var layout = column.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(MenuItemW, MenuBtnH);
            layout.spacing = new Vector2(MenuGap, MenuGap);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (string name in new[] { "RegularsButton", "StaffButton", "ZonesButton", "DecorButton", "MissionButton", "RecipeBookMenuButton", "GraphicsButton" })
            {
                Transform t = safe.Find(name);
                if (t == null) continue;
                t.SetParent(column, false);
                var le = t.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = MenuItemW;
                le.preferredHeight = MenuBtnH;
                Text label = t.GetComponentInChildren<Text>();
                if (label != null) { label.fontSize = 32; label.resizeTextMaxSize = 32; }
                AddMenuItemIcon(t, name, label);
                if (name == "MissionButton") { Transform d = t.Find("Dot"); if (d != null) missionDot = d.gameObject; }
            }
            foreach (Button b in column.GetComponentsInChildren<Button>(true)) b.onClick.AddListener(CloseMenu);

            // Nút MENU: nút coffee 72x64 + icon_menu (chưa có sprite thì 3 thanh vẽ bằng Image); huy hiệu ui_badge có số khi > 1
            ClayButton menuBtn = HudBuilder.AddClayButton(safe, "MenuButton", ClayStyle.Coffee, ToggleMenu);
            HudBuilder.SetFixed((RectTransform)menuBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-MenuRight, -TopMargin), new Vector2(MenuBtnW, ChipH));
            if (HudBuilder.T.iconMenu != null)
            {
                Image mi = HudBuilder.Icon(menuBtn.Content, "Icon", HudBuilder.T.iconMenu, 46f);
                HudBuilder.SetFixed(mi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    Image bar = HudBuilder.Img(menuBtn.Content, "Bar" + i, null, HudBuilder.Cream, false, false);
                    HudBuilder.SetFixed(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f - i * 12f), new Vector2(40f, 6f));
                }
            }
            menuBadge = MakeBadge(menuBtn.transform, "Dot", 28f, out menuBadgeText);
            menuDot = menuBadge;
            menuRoot.gameObject.SetActive(false);

            // MENU + popup nằm dưới bảng Cửa hàng (bảng này phủ lên hàng trên cùng), trên mọi thứ dựng trước nó.
            Transform shop = safe.Find("ShopPanel");
            if (shop != null)
            {
                int i = shop.GetSiblingIndex();
                menuRoot.SetSiblingIndex(i);
                menuBtn.transform.SetSiblingIndex(i + 1);
            }
        }

        /// Huy hiệu số: viên tròn đỏ viền kem (ui_badge) ở góc trên-phải của parent, chữ kem Baloo. Ẩn sẵn.
        private static GameObject MakeBadge(Transform parent, string name, float size, out Text count)
        {
            Image b = HudBuilder.Img(parent, name, HudBuilder.T.badge != null ? HudBuilder.T.badge : HudBuilder.T.disc, HudBuilder.Red, false, false);
            if (HudBuilder.T.badge == null) b.color = HudBuilder.Red; // ui_disc trắng: nhuộm đỏ
            HudBuilder.SetFixed(b.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(size, size));
            count = HudBuilder.AddText(b.transform, "N", "", Mathf.RoundToInt(size * 0.62f), TextAnchor.MiddleCenter, HudBuilder.Cream, true, false);
            HudBuilder.SetRect(count.rectTransform, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, 0f));
            HudBuilder.Fit(count, 10);
            b.gameObject.SetActive(false);
            return b.gameObject;
        }

        /// Icon sprite bên trái nhãn các mục trong popup MENU (nhiệm vụ / nhân viên / sổ công thức...).
        private static void AddMenuItemIcon(Transform item, string name, Text label)
        {
            Sprite s;
            switch (name)
            {
                case "MissionButton": s = HudBuilder.T.iconMission; break;
                case "StaffButton": s = HudBuilder.T.iconStaff; break;
                case "RecipeBookMenuButton": s = HudBuilder.T.iconBook; break;
                case "RegularsButton": s = HudBuilder.T.iconHeart; break;
                case "ZonesButton": s = HudBuilder.T.iconHome; break;
                case "DecorButton": s = HudBuilder.T.iconBroom; break;
                default: s = null; break;
            }
            if (s == null) return;
            ClayButton cb = item.GetComponent<ClayButton>();
            Transform host = cb != null && cb.Content != null ? (Transform)cb.Content : item;
            if (host.Find("MenuIcon") != null) return;
            Image ic = HudBuilder.Icon(host, "MenuIcon", s, 60f);
            HudBuilder.SetFixed(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44f, 0f), new Vector2(60f, 60f));
            if (label != null && label.rectTransform.anchorMin == Vector2.zero && label.rectTransform.anchorMax == Vector2.one)
                label.rectTransform.offsetMin = new Vector2(Mathf.Max(label.rectTransform.offsetMin.x, 78f), label.rectTransform.offsetMin.y);
        }

        /// Nút MỞ CỬA giữa-dưới (pha chuẩn bị). Nằm dưới bảng Cửa hàng để bảng phủ lên khi mở ở màn hẹp.
        private void BuildOpenShopButton(RectTransform safe)
        {
            Text label;
            openShopButton = HudBuilder.AddLabelButton(safe, "OpenShopButton", ClayStyle.Red, Strings.OpenShop, 54, game.StartNextDay, out label);
            HudBuilder.SetFixed((RectTransform)openShopButton.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, Margin), new Vector2(480f, 136f));
            openShopButton.gameObject.SetActive(false);
            Transform shop = safe.Find("ShopPanel");
            if (shop != null) openShopButton.transform.SetSiblingIndex(shop.GetSiblingIndex());
        }

        private void ToggleMenu()
        {
            menuOpenedForTutorial = false;
            menuRoot.gameObject.SetActive(!menuRoot.gameObject.activeSelf);
        }
        private void CloseMenu() { menuRoot.gameObject.SetActive(false); Audio.UiBack(); }

        /// Cột "Đơn chờ" trên-trái (16, 76): thẻ 200x64 (x1,25), tối đa 3 + dòng "+N". Cột không có Graphic nên không chặn chạm,
        /// chỉ thẻ nhận chạm. Tiêu đề "Đơn chờ" chỉ hiện trong hướng dẫn.
        private void BuildOrderColumn(RectTransform parent)
        {
            RectTransform col = HudBuilder.NewRect("OrderColumn", parent);
            orderColumn = col;
            HudBuilder.SetFixed(col, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -(TopMargin + ChipH + 15f)),
                new Vector2(CardWidth + 10f, CardTitleH + MaxCards * (CardHeight + CardSpacing) + 40f));

            // Tab nhỏ "ĐƠN" + huy hiệu số đơn dính đầu cột (thay chữ nổi "Đơn chờ"); chỉ hiện khi có đơn hoặc đang hướng dẫn
            ordersTab = HudBuilder.Img(col, "OrdersTab", HudBuilder.T.btnCoffee, HudBuilder.InkSoft, true, false);
            ordersTab.pixelsPerUnitMultiplier = 3f; // viền 9-slice của nút (40/52 px) vừa tab cao 36
            HudBuilder.SetFixed(ordersTab.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(120f, 36f));
            ordersTitle = HudBuilder.AddText(ordersTab.transform, "Title", Strings.OrdersTab, 22, TextAnchor.MiddleLeft, HudBuilder.Cream, true, false);
            HudBuilder.SetRect(ordersTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 4f), new Vector2(-40f, 0f));
            MakeBadge(ordersTab.transform, "Count", 30f, out ordersTabCount).SetActive(true);
            ((RectTransform)ordersTabCount.transform.parent).anchoredPosition = new Vector2(-18f, -18f);
            ordersTab.gameObject.SetActive(false);

            // Không còn dòng "Chờ khách ghé quán..." (để trống là đủ; trong hướng dẫn bà Sáu đã nói)
            emptyText = HudBuilder.AddText(col, "Empty", "", 24, TextAnchor.UpperLeft, HudBuilder.Cream, false, true);
            HudBuilder.SetFixed(emptyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -CardTitleH), new Vector2(CardWidth, 70f));
            emptyText.enabled = false;

            for (int i = 0; i < MaxCards; i++)
            {
                int index = i;
                var cv = new CardView();
                ClayButton btn = HudBuilder.AddClayButton(col, "Card" + i, HudBuilder.T.card, null, HudBuilder.Cream, () => OnCardClicked(index));
                HudBuilder.SetFixed((RectTransform)btn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -(cardTop + i * (CardHeight + CardSpacing))), new Vector2(CardWidth, CardHeight));
                cv.button = btn;

                // ô icon món nền kem đậm bo tròn bên trái
                cv.iconBg = HudBuilder.Img(btn.Content, "IconBg", HudBuilder.T.card, HudBuilder.CreamDark, true, false);
                cv.iconBg.color = HudBuilder.T.card != null ? new Color(0.9f, 0.84f, 0.72f, 1f) : HudBuilder.CreamDark;
                cv.iconBg.pixelsPerUnitMultiplier = 2f;
                HudBuilder.SetFixed(cv.iconBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(54f, 54f));
                cv.icon = HudBuilder.Icon(cv.iconBg.transform, "Icon", null, 44f);
                HudBuilder.SetFixed(cv.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(44f, 44f));

                cv.nameText = HudBuilder.AddText(btn.Content, "Name", "", 24, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
                HudBuilder.SetFixed(cv.nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, 1f), new Vector2(118f, 36f));
                HudBuilder.Fit(cv.nameText, 16);

                // giá: icon coin 22 + số
                Image priceCoin = HudBuilder.Icon(btn.Content, "PriceCoin", HudBuilder.T.iconCoin, 22f);
                HudBuilder.SetFixed(priceCoin.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(70f, -45f), new Vector2(22f, 22f));
                cv.priceText = HudBuilder.AddText(btn.Content, "Price", "", 22, TextAnchor.MiddleLeft, HudBuilder.InkSoft, true, false);
                HudBuilder.SetFixed(cv.priceText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -28f), new Vector2(92f, 34f));
                HudBuilder.Fit(cv.priceText, 14);

                RectTransform ringRoot;
                cv.ringFill = HudBuilder.CreateRing(btn.Content, "Ring", 50f, out ringRoot);
                HudBuilder.SetFixed(ringRoot, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-32f, 0f), new Vector2(50f, 50f));

                // viền vàng 4 px khi thẻ đang được pha
                RectTransform border = HudBuilder.NewRect("BrewBorder", btn.transform);
                HudBuilder.SetRect(border, Vector2.zero, Vector2.one, new Vector2(-2f, -2f), new Vector2(2f, 2f));
                for (int k = 0; k < 4; k++)
                {
                    Image bar = HudBuilder.Img(border, "Bar" + k, null, HudBuilder.Accent, false, false);
                    RectTransform br = bar.rectTransform;
                    bool hz = k < 2;
                    br.anchorMin = hz ? new Vector2(0f, k == 0 ? 1f : 0f) : new Vector2(k == 2 ? 0f : 1f, 0f);
                    br.anchorMax = hz ? new Vector2(1f, k == 0 ? 1f : 0f) : new Vector2(k == 2 ? 0f : 1f, 1f);
                    br.pivot = new Vector2(k == 2 ? 0f : (k == 3 ? 1f : 0.5f), k == 0 ? 1f : (k == 1 ? 0f : 0.5f));
                    br.sizeDelta = hz ? new Vector2(0f, 4f) : new Vector2(4f, 0f);
                    br.anchoredPosition = Vector2.zero;
                }
                cv.border = border.gameObject;
                cv.border.SetActive(false);

                cards[i] = cv;
                btn.gameObject.SetActive(false);
            }

            moreText = HudBuilder.AddText(col, "More", "", 24, TextAnchor.MiddleLeft, HudBuilder.Cream, false, true);
            HudBuilder.SetFixed(moreText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -(cardTop + MaxCards * (CardHeight + CardSpacing))), new Vector2(CardWidth, 34f));
            moreText.enabled = false;
        }

        /// Tab "ĐƠN" chỉ hiện khi có đơn hoặc hướng dẫn chưa xong; thẻ luôn nằm dưới chỗ của tab (không nhảy lên xuống).
        private void LayoutOrderColumn()
        {
            int count = game.Orders.Items.Count;
            bool showTab = count > 0 || (game.Tutorial != null && !game.Tutorial.IsCoreDone);
            if (ordersTab.gameObject.activeSelf != showTab) ordersTab.gameObject.SetActive(showTab);
            ordersTabCount.text = count.ToString();
            ordersTabCount.transform.parent.gameObject.SetActive(count > 0);
            float top = CardTitleH;
            if (Mathf.Approximately(top, cardTop)) return;
            cardTop = top;
            for (int i = 0; i < MaxCards; i++)
                ((RectTransform)cards[i].button.transform).anchoredPosition = new Vector2(0f, -(cardTop + i * (CardHeight + CardSpacing)));
            emptyText.rectTransform.anchoredPosition = new Vector2(6f, -cardTop);
            moreText.rectTransform.anchoredPosition = new Vector2(6f, -(cardTop + MaxCards * (CardHeight + CardSpacing)));
        }

        // ---------- sự kiện ----------

        private void OnMoney(int xu)
        {
            if (xuText == null) return;
            xuWallet = xu;
            if (!xuInit)
            {
                xuInit = true;
                xuShown = xu;
                xuTarget = xu;
                SetXuText(xu);
                return;
            }
            RetargetXu();
        }

        /// Số hiển thị = Xu thật trừ phần đang bay; đổi đích thì đếm lên / xuống 0,4 s từ số đang hiện (chỉ chạy khi có sự kiện).
        private void RetargetXu()
        {
            int want = xuWallet - xuPending;
            if (want == xuTarget) return;
            bool gain = want > xuTarget;
            xuTarget = want;
            xuFrom = xuShown;
            xuTo = want;
            if (GraphicsQuality.ReduceMotion)
            {
                xuShown = want;
                xuTween = -1f;
                SetXuText(want);
            }
            else
            {
                xuTween = 0f;
            }
            if (gain) coinPop = GraphicsQuality.ReduceMotion ? -1f : 0f;
        }

        private void SetXuText(int v)
        {
            if (v == xuShownInt) return;
            xuShownInt = v;
            xuText.text = string.Format(Strings.XuFmt, Strings.FormatInt(v));
        }

        private void OnReputation(float rp, int stars)
        {
            if (starCountText == null) return;
            starCountText.text = stars.ToString();
            // Thanh và chữ theo LEVEL (bậc nhỏ, đầy nhanh) thay vì cả hạng sao: người chơi thấy tiến bộ thường xuyên
            int level = LevelRules.LevelFor(rp);
            int next = LevelRules.NextRp(level);
            // Huy hiệu gọn chỉ ghi "Lv N"; chạm vào thì hiện số RP vài giây (OnStarChipClicked)
            rpFull = next < 0
                ? string.Format(Strings.LevelMaxFmt, level, Strings.FormatInt(Mathf.FloorToInt(rp)))
                : string.Format(Strings.LevelRpFmt, level, Strings.FormatInt(Mathf.FloorToInt(rp)), Strings.FormatInt(next));
            rpShort = string.Format(Strings.LevelShortFmt, level);
            rpText.text = Time.unscaledTime < rpRevealUntil ? rpFull : rpShort;
            rpBar.Set(next < 0 ? 1f : Mathf.Clamp01((rp - LevelRules.RpFor(level)) / (float)(next - LevelRules.RpFor(level))), HudBuilder.Accent);
        }

        /// Thẻ thưởng level: dưới cột đơn, bên trái (16, >= 300) x1,25 - ra khỏi vùng chơi giữa màn hình; chạm để nhận.
        private void BuildLevelCard(RectTransform safe)
        {
            levelCard = HudBuilder.AddLabelButton(safe, "LevelCard", ClayStyle.Gold, "", 28, OnLevelCardTapped, out levelCardText);
            HudBuilder.SetFixed((RectTransform)levelCard.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Margin, -(TopMargin + ChipH + 15f + CardTitleH + MaxCards * (CardHeight + CardSpacing) + 40f)), new Vector2(CardWidth, 80f));
            HudBuilder.Fit(levelCardText, 16);
            levelCard.gameObject.SetActive(false);
            SendBelowPanels(safe, levelCard.transform);
        }

        /// Xếp `t` xuống dưới Cửa hàng và popup MENU (cùng cách với nút MỞ CỬA): thẻ nhận thưởng / dòng sự kiện nằm giữa-trên màn hình
        /// không được che hay chặn chạm các bảng này ở màn 16:9.
        private static void SendBelowPanels(RectTransform safe, Transform t)
        {
            Transform shop = safe.Find("ShopPanel");
            Transform menu = safe.Find("MenuRoot");
            int idx = shop != null ? shop.GetSiblingIndex() : t.GetSiblingIndex();
            if (menu != null) idx = Mathf.Min(idx, menu.GetSiblingIndex());
            t.SetSiblingIndex(idx);
        }

        private const float ShopNameChipH = 42f;
        private const float ShopNameMaxW = 420f;

        /// Tên quán trong viên kem ôm vừa chữ.
        private void SetShopName(string name)
        {
            if (shopNameText == null) return;
            shopNameText.text = name;
            float w = Mathf.Clamp(shopNameText.preferredWidth + 36f, 140f, ShopNameMaxW);
            shopNameChip.rectTransform.sizeDelta = new Vector2(w, ShopNameChipH);
        }

        /// Gọi sau khi dựng HUD để hiện thẻ cho thưởng đang chờ từ lần chơi trước.
        public void RefreshLevelCardNow() { RefreshLevelCard(); }

        /// Dòng sự kiện của ngày, giữa-trên dưới hàng chip (cân với tên quán bên phải).
        private void BuildEventLabel(RectTransform safe)
        {
            // Viên kem nhỏ chữ coffee (trước là chữ trơn viền mỏng nổi trên cảnh, đè lên bong bóng đơn của khách); không chặn chạm
            eventChip = HudBuilder.Img(safe, "DayEvent", HudBuilder.T.tag != null ? HudBuilder.T.tag : HudBuilder.T.chip, HudBuilder.Cream, true, false);
            eventChip.color = new Color(1f, 1f, 1f, 0.94f);
            HudBuilder.SetFixed(eventChip.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(TopMargin + ChipH + 10f)), new Vector2(EventChipMaxW, 38f));
            eventText = HudBuilder.AddText(eventChip.transform, "Text", "", 22, TextAnchor.MiddleCenter, HudBuilder.Ink, false, false);
            HudBuilder.SetRect(eventText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-18f, 0f));
            HudBuilder.Fit(eventText, 16);
            eventText.raycastTarget = false;
            eventChip.gameObject.SetActive(false);
            SendBelowPanels(safe, eventChip.transform);
        }

        /// today = true: "Hôm nay: ...", false: "Ngày mai: ..." (báo trước ở pha Chuẩn bị). Ngày thường thì ẩn dòng.
        private void ShowEvent(DayEvent ev, bool today)
        {
            if (eventText == null) return;
            bool show = ev != null && !ev.IsNormal;
            eventChip.gameObject.SetActive(show);
            if (!show) return;
            eventText.text = string.Format(today ? Strings.EventTodayFmt : Strings.EventTomorrowFmt, ev.name, ev.blurb);
            float w = Mathf.Min(EventChipMaxW, eventText.preferredWidth + 40f); // viên ôm vừa chữ
            eventChip.rectTransform.sizeDelta = new Vector2(Mathf.Max(160f, w), eventChip.rectTransform.sizeDelta.y);
        }

        private void OnStarChipClicked()
        {
            rpRevealUntil = Time.unscaledTime + RpRevealSeconds;
            rpText.text = rpFull;
        }

        /// Chip giờ một dòng: "N1 · 07:29".
        private void SetClock(int day, string hhmm)
        {
            dayText.text = string.Format(Strings.DayChipFmt, day);
            timeText.text = string.Format(Strings.DayTimeShortFmt, day, hhmm);
        }

        private void OnLevelCardTapped()
        {
            if (game.ClaimNextLevel()) RefreshLevelCard();
        }

        /// Hiện / ẩn thẻ theo số thưởng đang chờ; chữ là level kế tiếp phải nhận (+ số level còn lại).
        private void RefreshLevelCard()
        {
            if (levelCard == null) return;
            int pending = game.PendingLevels;
            if (pending <= 0) { levelCard.gameObject.SetActive(false); return; }
            int level = game.ClaimedLevel + 1, star;
            string xu = Strings.FormatInt(LevelRules.RewardXu(level));
            string text = LevelRules.IsStarLevel(level, out star)
                ? string.Format(Strings.LevelCardStarFmt, star, xu)
                : string.Format(Strings.LevelCardFmt, level, xu);
            int gems = LevelRules.RewardGems(level);
            if (gems > 0) text += string.Format(Strings.LevelCardGemsFmt, gems);
            if (pending > 1) text += string.Format(Strings.LevelCardMoreFmt, pending - 1);
            levelCardText.text = text;
            if (!levelCard.gameObject.activeSelf) { levelCard.gameObject.SetActive(true); levelCardShownAt = Time.unscaledTime; }
        }

        /// Đạt level mới (chưa nhận): chữ nổi ngắn + hiện thẻ nhận thưởng.
        private void OnLevelUp(int level)
        {
            RefreshLevelCard();   // thẻ "NHẬN +X Xu" tự báo; không thêm chữ nổi đè lên thẻ
        }

        /// Quỹ lên cấp quán đạt chặng mới: chữ nổi kèm Hạt (không pháo giấy: chặng nhỏ, tránh nhiễu với lên level).
        private void OnFundStage(int toTier, int stage, int gems)
        {
            UiFx.FloatTextTop(string.Format(Strings.FundStageFmt, Strings.TierShortName(toTier), stage * 100 / HouseFund.Stages, gems), HudBuilder.Good);
        }

        /// Xong dự án tuần: chữ nổi + pháo giấy (không biểu ngữ lớn).
        private void OnWeeklyDone(int xu, int gems)
        {
            UiFx.Banner(string.Format(Strings.WeeklyDoneFmt, Strings.FormatInt(xu), gems));
            if (game.Tier != null) game.Tier.PlayConfetti(false, null);
        }

        /// Đã nhận thưởng (chạm thẻ hoặc tự nhận cuối ngày): chữ nổi thưởng + pháo giấy; có level mốc sao thì thêm biểu ngữ "SAO N!".
        private void OnLevelsClaimed(int from, int to, int xu, int gems)
        {
            int star = 0;
            for (int lv = from; lv <= to; lv++)
            {
                int s;
                if (LevelRules.IsStarLevel(lv, out s)) star = Mathf.Max(star, s);
            }
            string msg = from == to
                ? string.Format(Strings.LevelUpFmt, from, Strings.FormatInt(xu))
                : string.Format(Strings.LevelRangeFmt, from, to, Strings.FormatInt(xu));
            if (gems > 0) msg += string.Format(Strings.LevelGemsFmt, gems);
            if (star > 0) msg = string.Format(Strings.StarUpFmt, star) + "  " + msg;
            // Banner ruy-băng dưới thanh chip (không đặt chữ lớn giữa màn hình); pháo giấy vẫn giữ
            UiFx.Banner(msg);
            if (game.Tier != null) game.Tier.PlayConfetti(false, null);
            // Mở khóa trong Cửa hàng + danh hiệu mới của các level vừa nhận (báo lúc nhận, thẻ đã biến mất nên không đè chữ)
            for (int lv = from; lv <= to; lv++)
            {
                foreach (string id in ShopUnlocks.UnlockedAt(lv))
                {
                    FurnitureDef d = game.GetDef(id);
                    ShowToast(string.Format(Strings.UnlockedItemFmt, d != null && !string.IsNullOrEmpty(d.displayName) ? d.displayName : id));
                }
                if (LevelTitles.IsTitleLevel(lv)) ShowToast(string.Format(Strings.NewTitleFmt, LevelTitles.TitleFor(lv)));
            }
            RefreshLevelCard();
        }

        private void OnCupChipClicked()
        {
            Audio.UiTap();
            if (game.WaitingCupCount() > 0) game.QueueCollectAll();
        }

        /// Dòng "N ly chờ thu" trên chip ly; chip nhấp nháy khi hết ly sạch mà vẫn còn ly chờ thu. Chỉ gán chữ khi số đổi.
        private void RefreshCupWaiting()
        {
            if (cupWaitText == null) return;
            int waiting = game.WaitingCupCount();
            if (waiting != cupWaitShown)
            {
                cupWaitShown = waiting;
                cupWaitButton.SetActive(waiting > 0);
                if (waiting > 0) cupWaitText.text = waiting.ToString();
                cupWaitText.transform.parent.gameObject.SetActive(waiting > 0);
            }
            bool urgent = waiting > 0 && game.Stock.Count <= 1;
            float k = urgent && !GraphicsQuality.ReduceMotion ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
            if (cupChipImg != null && !Mathf.Approximately(cupChipImg.rectTransform.localScale.x, k)) cupChipImg.rectTransform.localScale = new Vector3(k, k, 1f);
        }

        /// Chip ly "3/6" = ly sạch / tổng số ly; đỏ khi hết ly sạch. Chỉ gán chữ khi số đổi.
        private void OnCups()
        {
            if (cupText == null) return;
            int n = game.Stock.Count, total = Mathf.Max(n, game.Stock.Total);
            if (n == cupShown && total == cupTotalShown) return;
            cupShown = n;
            cupTotalShown = total;
            cupText.text = string.Format(Strings.CupsOfFmt, n, total);
            cupText.color = n <= 0 ? HudBuilder.Red : HudBuilder.Ink;
        }

        private int specialToastDay = -1;

        private void OnOrderChanged(Order o)
        {
            RebuildCards();
            // Đơn đặc biệt đầu tiên của ngày: báo một lần (chỉ chủ quán pha, thưởng x2)
            if (o != null && o.ownerOnly && game.Day != null && specialToastDay != game.Day.Day)
            {
                specialToastDay = game.Day.Day;
                ShowToast(Strings.SpecialOrderToast);
            }
        }

        /// Pha chuẩn bị: ẩn tổng kết, giữ nút Cửa hàng, hiện MỞ CỬA; đồng hồ đứng ở 06:00 của ngày kế tiếp.
        private void OnPrepStarted()
        {
            summaryPanel.Hide();
            shopPanel.SetVisible(true);
            openShopButton.gameObject.SetActive(true);
            SetClock(game.Day.Day + 1, "06:00");
            ShowEvent(DayEvents.For(game.Day.Day + 1), false);
            lastMinute = -1;
        }

        private void OnDayStarted(int day)
        {
            summaryPanel.Hide();
            ShowEvent(game.TodayEvent, true);
            if (!game.TodayEvent.IsNormal) ShowToast(string.Format(Strings.EventTodayFmt, game.TodayEvent.name, game.TodayEvent.blurb));
            openShopButton.gameObject.SetActive(false);
            brewPanel.SetVisible(true);
            shopPanel.SetVisible(true);
            recipeBook.SetVisible(true);
            SetClock(day, "06:00");
            lastMinute = -1;
            for (int i = 0; i < game.recipes.Length; i++)
            {
                RecipeDef r = game.recipes[i];
                if (r != null && r.unlockDay > 0 && r.unlockDay == day)
                    ShowToast(string.Format(Strings.NewRecipeFmt, Strings.FullName(r)));
            }
            RebuildCards();
        }

        private void OnDayEnded(DaySummary s)
        {
            brewPanel.SetVisible(false);
            shopPanel.SetVisible(false);
            recipeBook.SetVisible(false);
            if (eventChip != null) eventChip.gameObject.SetActive(false);
            summaryPanel.Show(s);
        }

        private void OnServed(Customer c)
        {
            if (c == null) return;
            string msg = c.LastTip > 0
                ? string.Format(Strings.PaidTipFmt, c.LastPay + c.LastTip, c.LastTip)
                : string.Format(Strings.PaidFmt, c.LastPay);
            UiFx.FloatText(c.transform.position + new Vector3(0f, 1.9f, 0f), msg, HudBuilder.Accent);

            // Xu bay từ khách tới đĩa coin; đồng cuối chạm mới cộng số (tắt khi "Giảm chuyển động")
            int amount = c.LastPay + c.LastTip;
            Camera cam = Camera.main;
            if (amount <= 0 || cam == null || coinIcon == null) return;
            if (coinFly == null) coinFly = CoinFly.Create(hudCanvasGo.transform, HudBuilder.T.iconCoin);
            Vector3 sp = cam.WorldToScreenPoint(c.transform.position + new Vector3(0f, 1.6f, 0f));
            if (sp.z <= 0.1f) return;
            int coins = Mathf.Clamp(3 + amount / 60, 3, 5);
            xuPending += amount;
            if (!coinFly.Fly(new Vector2(sp.x, sp.y), coinIcon.rectTransform, coins, () => { xuPending -= amount; RetargetXu(); }))
                xuPending -= amount;
            RetargetXu();
        }

        private void OnCardClicked(int index)
        {
            CardView cv = cards[index];
            if (cv.order == null) return;
            Audio.Play(Sfx.OrderSelect);
            game.Station.Select(cv.order);
        }

        /// Thông báo chung (không gắn với khách): chữ nổi nhỏ ngay trên mép bàn pha (UiFx.ToastScreenY), không chặn chạm.
        public void ShowToast(string msg)
        {
            if (game != null && game.Tutorial != null && game.Tutorial.IsShowingCard) return; // thẻ hướng dẫn che đúng chỗ này
            // Nhắc việc ở quầy không đè lên bảng đang mở (Mở rộng, Nhiệm vụ...); người chơi đóng bảng là thấy ly sẵn sàng trên khay
            if (ModalMarker.AnyOpen && (msg == Strings.BrewDone || msg == Strings.OwnerComing)) return;
            UiFx.FloatTextTop(msg, HudBuilder.Cream);
        }

        // ---------- cập nhật ----------

        private void RebuildCards()
        {
            if (game == null || cards[0] == null) return;
            var items = game.Orders.Items;
            Order selected = game.Station.Selected;
            Color normal = HudBuilder.T.card != null ? Color.white : HudBuilder.Cream;
            Color sel = HudBuilder.T.card != null ? new Color(1f, 0.82f, 0.42f, 1f) : HudBuilder.Accent;
            Color brewing = HudBuilder.T.card != null ? new Color(0.72f, 0.95f, 0.78f, 1f) : HudBuilder.Mint; // nhân viên Pha chế đang làm
            Color special = HudBuilder.T.card != null ? new Color(1f, 0.74f, 0.86f, 1f) : HudBuilder.Red;     // đơn đặc biệt: chỉ chủ quán

            for (int i = 0; i < MaxCards; i++)
            {
                CardView cv = cards[i];
                if (i < items.Count)
                {
                    Order o = items[i];
                    cv.order = o;
                    cv.button.gameObject.SetActive(true);
                    HudBuilder.SetIcon(cv.icon, HudBuilder.T.IconFor(o.recipe));
                    cv.nameText.text = Strings.ShortName(o.recipe);
                    cv.priceText.text = o.ownerOnly
                        ? string.Format(Strings.SpecialPriceFmt, Strings.FormatInt(o.recipe.price * 2))
                        : string.Format(Strings.XuFmt, o.recipe.price);
                    cardTint[i] = o == selected ? sel : (o.IsClaimed ? brewing : (o.ownerOnly ? special : normal));
                    cv.button.SetTint(cardTint[i]);
                    if (o.customer != null) SetPatienceRing(cv);
                    bool brewingThis = o.IsClaimed || (o == selected && game.Station.State == BrewStation.Phase.Brewing);
                    if (cv.border.activeSelf != brewingThis) cv.border.SetActive(brewingThis);
                    if (!brewingThis) cv.icon.rectTransform.localEulerAngles = Vector3.zero;
                    // đơn mới vào (không phải chỉ dồn lên): trượt từ trái + nảy
                    if (System.Array.IndexOf(prevOrders, o) < 0 && !GraphicsQuality.ReduceMotion)
                    {
                        cv.slide = 0f;
                        cardAnimating = true;
                    }
                }
                else
                {
                    cv.order = null;
                    cv.slide = -1f;
                    cv.button.gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < MaxCards; i++) prevOrders[i] = cards[i].order;

            cardFlashing = false;
            LayoutOrderColumn();
            orderCardCount = Mathf.Min(items.Count, MaxCards);
            emptyText.enabled = false; // không còn dòng "Chờ khách ghé quán..."
            int extra = items.Count - MaxCards;
            moreText.enabled = extra > 0;
            if (extra > 0) moreText.text = string.Format(Strings.MoreOrdersFmt, extra);
        }

        private void Update()
        {
            if (game == null) return;
            if (storyDialog != null) storyDialog.Tick();
            RefreshMenuBadge();
            float dt = Time.unscaledDeltaTime;
            RefreshCupWaiting();
            RefreshViewButton();

            // Thẻ thưởng level nhún nhẹ để dễ thấy (tắt khi Giảm chuyển động)
            if (levelCard != null && levelCard.gameObject.activeSelf)
            {
                // Chỉ nhún ~3 giây đầu: nhún mãi làm cả HudCanvas dựng lại mỗi khung suốt ngày (thẻ có thể nằm đó tới cuối ngày)
                float shown = Time.unscaledTime - levelCardShownAt;
                float k = GraphicsQuality.ReduceMotion || shown > 3f ? 1f : 1f + 0.035f * Mathf.Sin(shown * 6f);
                if (!Mathf.Approximately(levelCard.transform.localScale.x, k)) levelCard.transform.localScale = new Vector3(k, k, 1f);
            }

            // Vòng kiên nhẫn trên thẻ order (+ nghiêng icon thẻ đang pha, thẻ mới trượt vào)
            TickCards(dt);

            // Số Xu đếm lên / xuống 0,4 s (chỉ khi có sự kiện tiền)
            if (xuTween >= 0f && xuText != null)
            {
                xuTween += dt / XuCountSeconds;
                float k = Mathf.Clamp01(xuTween);
                xuShown = Mathf.Lerp(xuFrom, xuTo, 1f - (1f - k) * (1f - k));
                SetXuText(Mathf.RoundToInt(xuShown));
                if (k >= 1f)
                {
                    xuTween = -1f;
                    xuShown = xuTo;
                    SetXuText(Mathf.RoundToInt(xuTo));
                }
            }

            // Chip Xu nảy 1,08 rồi về + đĩa coin xoay nhẹ khi tiền vào
            if (coinPop >= 0f && coinIcon != null)
            {
                coinPop += dt;
                float t = Mathf.Clamp01(coinPop / CoinPopSeconds);
                float bump = Mathf.Sin(t * Mathf.PI);
                float s = 1f + 0.08f * bump;
                if (coinChipRt != null) coinChipRt.localScale = new Vector3(s, s, 1f);
                coinIcon.rectTransform.localEulerAngles = new Vector3(0f, 0f, -14f * Mathf.Sin(t * Mathf.PI * 2f));
                if (t >= 1f)
                {
                    coinPop = -1f;
                    if (coinChipRt != null) coinChipRt.localScale = Vector3.one;
                    coinIcon.rectTransform.localEulerAngles = Vector3.zero;
                }
            }

            // Đồng hồ
            if (game.Day.CurrentPhase == DayCycle.Phase.Open || game.Day.CurrentPhase == DayCycle.Phase.Closing)
            {
                int minutes = (int)(game.Day.ClockHours * 60f);
                if (minutes != lastMinute)
                {
                    lastMinute = minutes;
                    SetClock(game.Day.Day, (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"));
                }
            }

            if (hudHidden) return;
            TickCompactHud();
            brewPanel.Tick();
            UiFx.ToastScreenY = brewPanel.ToastScreenY();
            recipeBook.Tick();
            summaryPanel.Tick();
        }

        // ---------- HUD v2 (docs/hud_visual_polish_2026-10-18.md) ----------

        /// Vòng kiên nhẫn: xanh -> vàng (< 50%) -> đỏ nhấp nháy (< 20%).
        private static void SetPatienceRing(CardView cv)
        {
            float v = cv.order.customer.PatienceNormalized;
            HudBuilder.SetRing(cv.ringFill, v);
            Color c = v < 0.2f ? HudBuilder.Bad : (v < 0.5f ? HudBuilder.Accent : HudBuilder.Good);
            if (v < 0.2f && !GraphicsQuality.ReduceMotion) c.a = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f));
            cv.ringFill.color = c;
        }

        private void TickCards(float dt)
        {
            bool any = false;
            for (int i = 0; i < MaxCards; i++)
            {
                CardView cv = cards[i];
                if (cv.order == null) continue;
                if (cv.order.customer != null) SetPatienceRing(cv);

                // thẻ đang pha: icon lắc nhẹ
                if (cv.border.activeSelf && !GraphicsQuality.ReduceMotion)
                    cv.icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, 7f * Mathf.Sin(Time.unscaledTime * 6f));

                if (cv.slide >= 0f)
                {
                    cv.slide += dt / CardSlideSeconds;
                    float k = Mathf.Clamp01(cv.slide);
                    RectTransform rt = (RectTransform)cv.button.transform;
                    float back = 1f - k;
                    float x = -80f * back * back * back + 10f * Mathf.Sin(k * Mathf.PI) * back; // vào từ trái, vọt nhẹ rồi về
                    rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
                    if (k >= 1f)
                    {
                        cv.slide = -1f;
                        rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
                    }
                    else any = true;
                }
            }
            cardAnimating = any;
        }

        /// Huy hiệu số trên nút MENU: số món nhận được (nhiệm vụ xong + rương); 1 thì chỉ là chấm đỏ.
        private void RefreshMenuBadge()
        {
            if (menuBadge == null) return;
            int n = 0;
            MissionBook book = game.Missions;
            if (book != null)
            {
                for (int i = 0; i < book.Items.Count; i++)
                    if (book.Items[i].IsDone && !book.Items[i].claimed) n++;
                if (book.ChestReady) n++;
            }
            if (n == 0 && missionDot != null && missionDot.activeSelf) n = 1;
            if (menuRoot.gameObject.activeSelf) n = 0;
            if (n == menuBadgeShown) return;
            menuBadgeShown = n;
            menuBadge.SetActive(n > 0);
            menuBadgeText.text = n > 1 ? n.ToString() : "";
        }

        // ---------- HUD gọn (docs/hud_brew_redesign_2026-10-18.md A2) ----------

        /// Theo pha trong ngày: mở cửa thì ẩn tên quán + nút Cửa hàng thành nút tròn; Chuẩn bị / tổng kết thì như cũ.
        /// Kèm: hết hạn xem số RP, thẻ đơn cũ nhất nháy vàng mỗi 3 s khi chưa chọn đơn, đóng phần đã mở cho hướng dẫn.
        private void TickCompactHud()
        {
            DayCycle.Phase ph = game.Day.CurrentPhase;
            bool open = ph == DayCycle.Phase.Open || ph == DayCycle.Phase.Closing;
            if (shopNameChip != null && shopNameChip.gameObject.activeSelf == open) shopNameChip.gameObject.SetActive(!open);
            SetShopCompact(open);

            if (rpRevealUntil > 0f && Time.unscaledTime >= rpRevealUntil)
            {
                rpRevealUntil = -1f;
                rpText.text = rpShort;
            }

            // Chưa chọn đơn: thẻ đơn cũ nhất nhấp nháy vàng một nhịp mỗi 3 s (thay cho dải "Chạm một đơn để pha")
            CardView first = cards[0];
            BrewStation st = game.Station;
            bool wantFlash = open && first.order != null && st.State == BrewStation.Phase.Idle && st.Selected == null && !st.Experiment
                             && !first.order.IsClaimed;
            float phase = Time.unscaledTime % 3f;
            if (wantFlash && phase < 0.6f)
            {
                float k = Mathf.Sin(phase / 0.6f * Mathf.PI);
                first.button.SetTint(Color.Lerp(cardTint[0], HudBuilder.Accent, 0.65f * k));
                cardFlashing = true;
            }
            else if (cardFlashing)
            {
                cardFlashing = false;
                first.button.SetTint(cardTint[0]);
            }

            // Thẻ hướng dẫn đã tắt: đóng MENU / thu viên thuốc / ẩn bàn pha đã mở ra cho nó
            if (tutorialReveal != null && (game.Tutorial == null || !game.Tutorial.IsShowingCard)) EndTutorialReveal();
        }

        /// Nút Cửa hàng: tròn 72x72 (x1,25) ở dưới-trái khi mở cửa; nút chữ to như cũ ở pha Chuẩn bị.
        private void SetShopCompact(bool compact)
        {
            int want = compact ? 1 : 0;
            if (shopButtonRt == null || want == shopCompact) return;
            shopCompact = want;
            if (shopIcon == null && HudBuilder.T.iconShop != null)
            {
                ClayButton cb = shopButtonRt.GetComponent<ClayButton>();
                Transform host = cb != null && cb.Content != null ? (Transform)cb.Content : shopButtonRt;
                shopIcon = HudBuilder.Icon(host, "ShopIcon", HudBuilder.T.iconShop, 56f);
                HudBuilder.SetFixed(shopIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(56f, 56f));
            }
            if (shopIcon != null) shopIcon.gameObject.SetActive(compact);
            if (compact)
            {
                // nút vàng tròn 96: icon_shop 56 + nhãn "Cửa hàng" Baloo 18 một dòng bên dưới (không có sprite thì chỉ nhãn)
                HudBuilder.SetFixed(shopButtonRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Margin, Margin), new Vector2(CompactShopSize, CompactShopSize));
                if (shopButtonLabel != null)
                {
                    bool withIcon = shopIcon != null;
                    shopButtonLabel.fontSize = withIcon ? 18 : 24;
                    shopButtonLabel.resizeTextMaxSize = shopButtonLabel.fontSize;
                    shopButtonLabel.resizeTextMinSize = 12;
                    shopButtonLabel.horizontalOverflow = HorizontalWrapMode.Overflow; // một dòng
                    shopButtonLabel.alignment = withIcon ? TextAnchor.LowerCenter : TextAnchor.MiddleCenter;
                    shopButtonLabel.rectTransform.offsetMin = new Vector2(2f, withIcon ? -2f : 0f);
                    shopButtonLabel.rectTransform.offsetMax = new Vector2(-2f, 0f);
                }
            }
            else
            {
                HudBuilder.SetFixed(shopButtonRt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 16f), new Vector2(280f, 112f));
                if (shopButtonLabel != null)
                {
                    shopButtonLabel.fontSize = 46;
                    shopButtonLabel.resizeTextMaxSize = 46;
                    shopButtonLabel.resizeTextMinSize = 20;
                    shopButtonLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                    shopButtonLabel.alignment = TextAnchor.MiddleCenter;
                    shopButtonLabel.rectTransform.offsetMin = new Vector2(10f, 0f);
                    shopButtonLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);
                }
            }
        }

        private static readonly string[] MenuItems =
            { "RegularsButton", "StaffButton", "ZonesButton", "DecorButton", "MissionButton", "RecipeBookMenuButton", "GraphicsButton" };

        /// Hướng dẫn sắp đặt "lỗ" lên mục tiêu targetName (tên GameObject, có thể "Cha/Con"): mở trước phần HUD đang thu gọn chứa nó
        /// (popup MENU, viên thuốc mục tiêu "Mua nhà hàng" / "Thuê mặt bằng", bàn pha khi chưa chọn đơn). Tự đóng lại khi thẻ hướng dẫn
        /// tắt hoặc hướng dẫn chuyển sang mục tiêu khác. Trả true nếu vừa mở thêm thứ gì (người gọi quét lại cây UI ngay).
        public bool RevealForTutorial(string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return false;
            if (targetName == tutorialReveal) return false; // đã mở cho mục tiêu này (người chơi tự đóng MENU thì không ép mở lại)
            EndTutorialReveal();
            tutorialReveal = targetName;
            string last = targetName;
            int slash = targetName.LastIndexOf('/');
            if (slash >= 0) last = targetName.Substring(slash + 1);

            if (System.Array.IndexOf(MenuItems, last) >= 0)
            {
                if (menuRoot != null && !menuRoot.gameObject.activeSelf)
                {
                    menuRoot.gameObject.SetActive(true);
                    menuOpenedForTutorial = true;
                    return true;
                }
                return false;
            }
            if (last == "TierCard" || last == "TierChip" || last == "TierPill")
            {
                if (upgradePanel != null) upgradePanel.SetPinned(true);
                return true;
            }
            // Nút nguyên liệu (Step_*) chỉ được trỏ khi đã chọn đơn = bàn pha đang hiện; ép hiện ở đây sẽ để lại dải trống sau khi pha xong
            if (last == "BrewPanel")
            {
                bool wasShown = brewPanel.Shown;
                brewPanel.SetTutorialReveal(true);
                return !wasShown;
            }
            return false;
        }

        /// Đóng những gì RevealForTutorial đã mở.
        public void EndTutorialReveal()
        {
            if (tutorialReveal == null) return;
            tutorialReveal = null;
            if (menuOpenedForTutorial && menuRoot != null) menuRoot.gameObject.SetActive(false);
            menuOpenedForTutorial = false;
            if (upgradePanel != null) upgradePanel.SetPinned(false);
            if (brewPanel != null) brewPanel.SetTutorialReveal(false);
        }
    }
}
