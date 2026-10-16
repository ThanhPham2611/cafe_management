using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cafe
{
    /// Dựng và cập nhật HUD ngang "Clay Café" (docs/ui_style.md §4):
    /// chip trên (xu / sao + RP / ngày + giờ), cột thẻ order bên trái, bàn pha giữa-dưới, chip ly, tổng kết cuối ngày.
    public class HudController : MonoBehaviour
    {
        private const int MaxCards = 4; // 5+ thẻ chạm nút Cửa hàng ở canvas HUD 900
        private const float CardHeight = 112f;
        private const float CardSpacing = 12f;
        private const float Margin = 24f;
        private const float ChipH = 96f;
        private const float MenuBtnH = 108f; // >= 44 pt ở canvas HUD 900
        private const float MenuItemW = 260f;
        private const float MenuBtnW = 200f;
        private const float MenuRight = Margin + 310f + 64f; // chừa chip ngày (310) + icon đồng hồ nhô ra trái (~48)

        private class CardView
        {
            public ClayButton button;
            public Image icon;
            public Text nameText;
            public Text priceText;
            public Image ringFill;
            public Order order;
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
            float bottomCanvas = 58f + orderCardCount * (CardHeight + CardSpacing);
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
        private int cupWaitShown = -1;
        private Text emptyText;
        private Text moreText;
        private readonly CardView[] cards = new CardView[MaxCards];

        private Text shopNameText;
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
        private StoryDialog storyDialog;
        public bool StoryOpen { get { return storyDialog != null && storyDialog.IsOpen; } }
        public void DebugAdvanceStory() { if (storyDialog != null) storyDialog.DebugAdvance(); }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugToggleStaffPanel() { if (staffPanel != null) staffPanel.DebugToggle(); }
        public void DebugScrollStaff(float normalized) { if (staffPanel != null) staffPanel.DebugScroll(normalized); }
        public void DebugOpenSettings(bool open) { if (settingsPanel != null) settingsPanel.DebugOpen(open); }
        public void DebugOpenMissions(bool open) { if (missionPanel != null) missionPanel.DebugOpen(open); }
        public void DebugOpenZones(bool open) { if (zonePanel != null) zonePanel.DebugOpen(open); }
#endif

        private ClayButton openShopButton; // nút MỞ CỬA, chỉ hiện ở pha chuẩn bị
        private RectTransform menuRoot;
        private GameObject menuDot;     // chấm đỏ trên nút MENU khi có nhiệm vụ nhận được
        private GameObject missionDot;
        private int lastMinute = -1;
        private bool xuInit;
        private int xuTarget;
        private float xuShown;
        private int xuShownInt = int.MinValue;
        private float coinPop = -1f;

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
            UpgradePanel.Create(game, safe); // thẻ "Thuê mặt bằng" (cấp 1) / dòng tiền thuê (cấp 2) + hộp xác nhận + màn tối chuyển cấp
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
            BuildMenu(safe); // gom các nút phụ vào popup MENU
            BuildOpenShopButton(safe);
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
            if (shopNameText != null) shopNameText.text = game.ShopName;
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
            if (storyDialog != null) storyDialog.Dispose();
        }

        // ---------- dựng giao diện ----------

        /// Chip viên thuốc + icon tròn lồi ra bên trái (tâm icon cách mép trái chip 8 px).
        private Image MakeChip(RectTransform parent, string name, Sprite icon, out Image iconImg)
        {
            Image chip = HudBuilder.Chip(parent, name, false);
            iconImg = HudBuilder.Icon(chip.transform, "Icon", icon, 112f);
            HudBuilder.SetFixed(iconImg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8f, 9f), new Vector2(112f, 112f));
            return chip;
        }

        private void BuildChips(RectTransform safe)
        {
            // --- Xu
            Image coinChip = MakeChip(safe, "CoinChip", HudBuilder.T.iconCoin, out coinIcon);
            HudBuilder.SetFixed(coinChip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin + 46f, -Margin), new Vector2(310f, ChipH));
            xuText = HudBuilder.AddText(coinChip.transform, "Xu", "", 44, TextAnchor.MiddleRight, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(xuText.rectTransform, Vector2.zero, Vector2.one, new Vector2(66f, 22f), new Vector2(-28f, -4f));
            HudBuilder.Fit(xuText, 26);

            // --- Sao + RP
            Image starIcon;
            Image starChip = MakeChip(safe, "StarChip", HudBuilder.T.iconStar, out starIcon);
            HudBuilder.SetFixed(starChip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin + 46f + 310f + 16f + 46f, -Margin), new Vector2(440f, ChipH));
            starCountText = HudBuilder.AddText(starChip.transform, "Stars", "", 56, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(starCountText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(68f, -4f), new Vector2(54f, 70f));
            rpText = HudBuilder.AddText(starChip.transform, "Rp", "", 28, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(rpText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -8f), new Vector2(290f, 32f));
            rpBar = HudBuilder.CreateBar(starChip.transform, "RpBar");
            HudBuilder.SetFixed(rpBar.root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(128f, -42f), new Vector2(286f, 28f));

            // --- Ngày + giờ (phải)
            Image clockIcon;
            Image dayChip = MakeChip(safe, "DayChip", HudBuilder.T.iconClock, out clockIcon);
            HudBuilder.SetFixed(dayChip.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(310f, ChipH));
            dayText = HudBuilder.AddText(dayChip.transform, "Day", "", 28, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(dayText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(74f, -6f), new Vector2(220f, 30f));
            timeText = HudBuilder.AddText(dayChip.transform, "Time", "", 46, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(timeText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(74f, -30f), new Vector2(220f, 46f));

            // --- Tên quán (dưới chip ngày, canh phải)
            shopNameText = HudBuilder.AddText(safe, "ShopName", "", 30, TextAnchor.MiddleRight, HudBuilder.Cream, true, true);
            HudBuilder.SetFixed(shopNameText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin - 6f, -(Margin + ChipH + 6f)), new Vector2(520f, 40f));
            HudBuilder.Fit(shopNameText, 18);
            shopNameText.text = game.ShopName;
            game.ProfileChanged += OnProfileChanged;

            // --- Ly sạch (dưới phải)
            Image cupIcon;
            Image cupChip = MakeChip(safe, "CupChip", HudBuilder.T.iconCup, out cupIcon);
            HudBuilder.SetFixed(cupChip.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, Margin), new Vector2(210f, ChipH));
            cupText = HudBuilder.AddText(cupChip.transform, "Cups", "", 48, TextAnchor.MiddleRight, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(cupText.rectTransform, Vector2.zero, Vector2.one, new Vector2(70f, 22f), new Vector2(-30f, -4f));

            // Chạm chip ly = giao chủ quán thu hết ly đang chờ; dòng nhỏ phía trên cho biết còn bao nhiêu ly chờ thu
            cupChipImg = cupChip;
            cupChip.raycastTarget = true;
            cupChip.gameObject.AddComponent<Button>().onClick.AddListener(OnCupChipClicked);
            cupWaitText = HudBuilder.AddText(safe, "CupsWaiting", "", 34, TextAnchor.MiddleRight, HudBuilder.Cream, true, true);
            HudBuilder.SetFixed(cupWaitText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin - 6f, Margin + ChipH + 6f), new Vector2(300f, 44f));
            HudBuilder.Fit(cupWaitText, 20);
            cupWaitText.gameObject.SetActive(false);
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

            RectTransform column = HudBuilder.NewRect("Column", menuRoot);
            HudBuilder.SetFixed(column, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-MenuRight, -(Margin + MenuBtnH + 8f)), new Vector2(MenuItemW, 0f));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (string name in new[] { "RegularsButton", "StaffButton", "ZonesButton", "MissionButton", "GraphicsButton" })
            {
                Transform t = safe.Find(name);
                if (t == null) continue;
                t.SetParent(column, false);
                var le = t.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = MenuItemW;
                le.preferredHeight = MenuBtnH;
                Text label = t.GetComponentInChildren<Text>();
                if (label != null) { label.fontSize = 32; label.resizeTextMaxSize = 32; }
                if (name == "MissionButton") { Transform d = t.Find("Dot"); if (d != null) missionDot = d.gameObject; }
            }
            foreach (Button b in column.GetComponentsInChildren<Button>(true)) b.onClick.AddListener(CloseMenu);

            Text menuLabel;
            ClayButton menuBtn = HudBuilder.AddLabelButton(safe, "MenuButton", ClayStyle.Coffee, "MENU", 32, ToggleMenu, out menuLabel);
            HudBuilder.SetFixed((RectTransform)menuBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-MenuRight, -Margin), new Vector2(MenuBtnW, MenuBtnH));
            Image dot = HudBuilder.Img(menuBtn.transform, "Dot", HudBuilder.T.disc, HudBuilder.Red, false, false);
            HudBuilder.SetFixed(dot.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-6f, -6f), new Vector2(32f, 32f));
            menuDot = dot.gameObject;
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

        private void ToggleMenu() { menuRoot.gameObject.SetActive(!menuRoot.gameObject.activeSelf); }
        private void CloseMenu() { menuRoot.gameObject.SetActive(false); }

        private void BuildOrderColumn(RectTransform parent)
        {
            RectTransform col = HudBuilder.NewRect("OrderColumn", parent);
            orderColumn = col;
            HudBuilder.SetFixed(col, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -(Margin + ChipH + 18f)), new Vector2(390f, 860f));

            Text t = HudBuilder.AddText(col, "Title", Strings.OrdersTitle, 40, TextAnchor.MiddleLeft, HudBuilder.Cream, true, true);
            HudBuilder.SetFixed(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, 0f), new Vector2(370f, 50f));

            emptyText = HudBuilder.AddText(col, "Empty", Strings.NoOrders, 28, TextAnchor.UpperLeft, HudBuilder.Cream, false, true);
            HudBuilder.SetFixed(emptyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -58f), new Vector2(370f, 80f));
            emptyText.horizontalOverflow = HorizontalWrapMode.Wrap;

            for (int i = 0; i < MaxCards; i++)
            {
                int index = i;
                var cv = new CardView();
                ClayButton btn = HudBuilder.AddClayButton(col, "Card" + i, HudBuilder.T.card, null, HudBuilder.Cream, () => OnCardClicked(index));
                HudBuilder.SetFixed((RectTransform)btn.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -(58f + i * (CardHeight + CardSpacing))), new Vector2(380f, CardHeight));
                cv.button = btn;

                cv.icon = HudBuilder.Icon(btn.Content, "Icon", null, 76f);
                HudBuilder.SetFixed(cv.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(54f, 0f), new Vector2(76f, 76f));

                cv.nameText = HudBuilder.AddText(btn.Content, "Name", "", 36, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
                HudBuilder.SetFixed(cv.nameText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(102f, -6f), new Vector2(176f, 44f));
                HudBuilder.Fit(cv.nameText, 22);

                cv.priceText = HudBuilder.AddText(btn.Content, "Price", "", 30, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
                HudBuilder.SetFixed(cv.priceText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(102f, -48f), new Vector2(176f, 34f));

                RectTransform ringRoot;
                cv.ringFill = HudBuilder.CreateRing(btn.Content, "Ring", 76f, out ringRoot);
                HudBuilder.SetFixed(ringRoot, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-52f, 0f), new Vector2(76f, 76f));

                cards[i] = cv;
                btn.gameObject.SetActive(false);
            }

            moreText = HudBuilder.AddText(col, "More", "", 30, TextAnchor.MiddleLeft, HudBuilder.Cream, false, true);
            HudBuilder.SetFixed(moreText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -(58f + MaxCards * (CardHeight + CardSpacing))), new Vector2(370f, 44f));
            moreText.enabled = false;
        }

        // ---------- sự kiện ----------

        private void OnMoney(int xu)
        {
            if (xuText == null) return;
            if (!xuInit)
            {
                xuInit = true;
                xuShown = xu;
            }
            else if (xu > xuTarget)
            {
                coinPop = 0f;
            }
            xuTarget = xu;
            SetXuText(Mathf.RoundToInt(xuShown));
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
            rpText.text = next < 0
                ? string.Format(Strings.LevelMaxFmt, level, Strings.FormatInt(Mathf.FloorToInt(rp)))
                : string.Format(Strings.LevelRpFmt, level, Strings.FormatInt(Mathf.FloorToInt(rp)), Strings.FormatInt(next));
            rpBar.Set(next < 0 ? 1f : Mathf.Clamp01((rp - LevelRules.RpFor(level)) / (float)(next - LevelRules.RpFor(level))), HudBuilder.Accent);
        }

        /// Lên level uy tín: chữ nổi giữa-trên (kèm thưởng), pháo giấy; level mốc sao thêm biểu ngữ "SAO N!".
        private void OnLevelUp(int level)
        {
            int star;
            bool isStar = LevelRules.IsStarLevel(level, out star);
            int gems = LevelRules.RewardGems(level);
            string msg = string.Format(Strings.LevelUpFmt, level, Strings.FormatInt(LevelRules.RewardXu(level)));
            if (gems > 0) msg += string.Format(Strings.LevelGemsFmt, gems);
            UiFx.FloatTextTop(msg, HudBuilder.Accent);
            if (game.Tier != null) game.Tier.PlayConfetti(isStar, isStar ? string.Format(Strings.StarUpFmt, star) : null);
        }

        private void OnCupChipClicked()
        {
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
                cupWaitText.gameObject.SetActive(waiting > 0);
                if (waiting > 0) cupWaitText.text = string.Format(Strings.CupsWaitingFmt, waiting);
            }
            bool urgent = waiting > 0 && game.Stock.Count <= 1;
            float k = urgent ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
            if (cupChipImg != null) cupChipImg.rectTransform.localScale = new Vector3(k, k, 1f);
        }

        private void OnCups()
        {
            if (cupText != null) cupText.text = string.Format(Strings.CupsFmt, game.Stock.Count);
        }

        private void OnOrderChanged(Order o)
        {
            RebuildCards();
        }

        /// Pha chuẩn bị: ẩn tổng kết, giữ nút Cửa hàng, hiện MỞ CỬA; đồng hồ đứng ở 06:00 của ngày kế tiếp.
        private void OnPrepStarted()
        {
            summaryPanel.Hide();
            shopPanel.SetVisible(true);
            openShopButton.gameObject.SetActive(true);
            dayText.text = string.Format(Strings.DayChipFmt, game.Day.Day + 1);
            timeText.text = "06:00";
            lastMinute = -1;
        }

        private void OnDayStarted(int day)
        {
            summaryPanel.Hide();
            openShopButton.gameObject.SetActive(false);
            brewPanel.SetVisible(true);
            shopPanel.SetVisible(true);
            recipeBook.SetVisible(true);
            dayText.text = string.Format(Strings.DayChipFmt, day);
            timeText.text = "06:00";
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
            summaryPanel.Show(s);
        }

        private void OnServed(Customer c)
        {
            if (c == null) return;
            string msg = c.LastTip > 0
                ? string.Format(Strings.PaidTipFmt, c.LastPay + c.LastTip, c.LastTip)
                : string.Format(Strings.PaidFmt, c.LastPay);
            UiFx.FloatText(c.transform.position + new Vector3(0f, 1.9f, 0f), msg, HudBuilder.Accent);
        }

        private void OnCardClicked(int index)
        {
            CardView cv = cards[index];
            if (cv.order == null) return;
            game.Station.Select(cv.order);
        }

        /// Thông báo chung (không gắn với khách): chữ nổi dưới thanh chip.
        public void ShowToast(string msg)
        {
            if (game != null && game.Tutorial != null && game.Tutorial.IsShowingCard) return; // thẻ hướng dẫn che đúng chỗ này
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
                    cv.priceText.text = string.Format(Strings.XuFmt, o.recipe.price);
                    cv.button.SetTint(o == selected ? sel : (o.IsClaimed ? brewing : normal));
                    if (o.customer != null) HudBuilder.SetRing(cv.ringFill, o.customer.PatienceNormalized);
                }
                else
                {
                    cv.order = null;
                    cv.button.gameObject.SetActive(false);
                }
            }

            orderCardCount = Mathf.Min(items.Count, MaxCards);
            emptyText.enabled = items.Count == 0;
            int extra = items.Count - MaxCards;
            moreText.enabled = extra > 0;
            if (extra > 0) moreText.text = string.Format(Strings.MoreOrdersFmt, extra);
        }

        private void Update()
        {
            if (game == null) return;
            if (storyDialog != null) storyDialog.Tick();
            if (menuDot != null) menuDot.SetActive(!menuRoot.gameObject.activeSelf && missionDot != null && missionDot.activeSelf);
            float dt = Time.unscaledDeltaTime;
            RefreshCupWaiting();

            // Vòng kiên nhẫn trên thẻ order
            for (int i = 0; i < MaxCards; i++)
            {
                CardView cv = cards[i];
                if (cv.order == null || cv.order.customer == null) continue;
                HudBuilder.SetRing(cv.ringFill, cv.order.customer.PatienceNormalized);
            }

            // Đếm số Xu chạy dần
            if (xuText != null && Mathf.Abs(xuShown - xuTarget) > 0.01f)
            {
                xuShown = Mathf.Lerp(xuShown, xuTarget, 1f - Mathf.Exp(-9f * dt));
                if (Mathf.Abs(xuShown - xuTarget) < 0.6f) xuShown = xuTarget;
                SetXuText(Mathf.RoundToInt(xuShown));
            }

            // Icon xu nảy khi nhận tiền
            if (coinPop >= 0f && coinIcon != null)
            {
                coinPop += dt;
                float t = Mathf.Clamp01(coinPop / 0.3f);
                float s = 1f + 0.22f * Mathf.Sin(t * Mathf.PI);
                coinIcon.rectTransform.localScale = new Vector3(s, s, 1f);
                if (t >= 1f)
                {
                    coinPop = -1f;
                    coinIcon.rectTransform.localScale = Vector3.one;
                }
            }

            // Đồng hồ
            if (game.Day.CurrentPhase == DayCycle.Phase.Open || game.Day.CurrentPhase == DayCycle.Phase.Closing)
            {
                int minutes = (int)(game.Day.ClockHours * 60f);
                if (minutes != lastMinute)
                {
                    lastMinute = minutes;
                    dayText.text = string.Format(Strings.DayChipFmt, game.Day.Day);
                    timeText.text = (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
                }
            }

            if (hudHidden) return;
            brewPanel.Tick();
            recipeBook.Tick();
            summaryPanel.Tick();
        }
    }
}
