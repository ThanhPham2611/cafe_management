using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cafe
{
    /// Bàn pha dạng dải đáy-giữa (docs/hud_brew_redesign_2026-10-18.md A3), canvas HUD 900 = spec 1280x720 x 1,25:
    ///  - Chưa chọn đơn: ẩn hẳn (trượt ra ngoài màn hình rồi SetActive(false)), không còn dải "Chạm một đơn để pha".
    ///  - Đã chọn đơn / đang pha / thử pha: dải 1050x195 (21,7% chiều cao). Hàng trên 45: icon + tên/giá, chấm bước, gợi ý,
    ///    thanh "pha nhanh", nút X Hủy. Hàng dưới 140: ly-thước (trái) + khay nguyên liệu 140x130 (Tier 2+ cuộn ngang).
    ///  - Đang cầm ly: viên thuốc 475x75 "tên món + sao + chạm khách" và nút Đổ ly; chỉ nút Đổ ly nhận chạm.
    /// Mỗi nút làm theo kiểu của bước (StepActions): chạm / rót (PourButtonBehaviour) / vuốt / xoay. Xong -> BrewStation.TapStep.
    /// Nút đúng bước kế có viền vàng + nhịp phóng nhẹ (trừ khi thử pha tự do). Chạm nút kế khi đang rót = dừng rót + làm bước đó.
    public class BrewPanel
    {
        private enum Mode { Hidden, Strip, Pill }

        private const float PanelW = 1050f;
        private const float PanelH = 195f;
        private const float PillW = 475f;
        private const float PillH = 75f;
        private const float RestY = 16f;  // nhích lên 16 px khỏi mép dưới vùng an toàn (hud_visual_polish §4)
        private const float HiddenGap = 30f;
        /// Phần màn hình bị bàn pha che, báo camera (spec: 0 khi ẩn, 0,22 khi mở).
        private const float BottomInsetOpen = 0.22f;
        private const int MaxSlots = 6;

        // Hàng dưới: ly-thước + khay nguyên liệu
        private const float TrayTop = 50f;
        private const float TrayH = 140f;
        private const float GaugeX = 16f;
        private const float GaugeTop = 55f;
        private const float GaugeW = 90f;
        private const float GaugeH = 130f;
        private const float TrayLeft = 116f;
        private const float TrayRightPad = 92f; // chừa cột nút X ở góc phải
        private const float BtnW = 140f; // 5 nút Tier 1 + khe 15 = 760 < khay 842: vừa, không phải cuộn
        private const float BtnMinW = 112f; // khay đông (6 món cấp 2) thì thu hẹp nút cho vừa, khỏi cuộn
        private const float BtnH = 130f;
        private const float BtnGap = 15f; // ~12 px ở 720: giữ nút liền kề không bị chạm nhầm
        private const float BtnTop = 5f;
        private const float BtnIcon = 64f;
        private const float SlotSize = 38f;  // đĩa chuỗi bước
        private const float SlotStep = 44f;
        private const int Tier1TrayCount = 5;

        // Tô sáng bước kế: viền vàng 6 px (x1,25) + phóng 1,00-1,06 ở 1,2 Hz
        private const float HighlightBar = 8f;
        private const float HighlightHz = 1.2f;
        private const float HighlightScale = 0.06f;

        /// Thứ tự trên khay: 5 nguyên liệu Tier 1 trước, rồi nguyên liệu Tier 2.
        private static readonly BrewStep[] TrayOrder =
        {
            BrewStep.Phin, BrewStep.SuaDac, BrewStep.SuaTuoi, BrewStep.Da, BrewStep.Tra,
            BrewStep.Duong, BrewStep.Muoi, BrewStep.KemMuoi, BrewStep.CotDua, BrewStep.Trung,
            BrewStep.Chanh, BrewStep.Lac, BrewStep.Danh, BrewStep.Cacao
        };

        private class Slot
        {
            public GameObject root;
            public Image disc;
            public Image icon;
            public Image badge;
        }

        private readonly GameBootstrap game;
        private readonly RectTransform root;
        private readonly Image panelImage;
        private readonly RectTransform stripGroup;
        private readonly RectTransform pillGroup;
        private readonly Image recipeIcon;
        private readonly Text title;
        private readonly Text hint;
        private readonly Text timer;
        private readonly Slot[] slots = new Slot[MaxSlots];
        private readonly ClayButton[] stepButtons = new ClayButton[TrayOrder.Length];
        private readonly PourButtonBehaviour[] pours = new PourButtonBehaviour[TrayOrder.Length];
        private readonly ClayButton actionButton;
        private readonly Text actionLabel;
        private readonly ClayButton doneButton;

        // viên thuốc "đang cầm ly"
        private readonly Image pillIcon;
        private readonly Text pillTitle;
        private readonly Text pillHint;
        private readonly Image[] stars = new Image[3];

        // ly-thước
        private RectTransform gaugeRoot;
        private RectTransform gaugeFillRt;
        private Image gaugeFill;
        private float gaugeShown = -1f;
        private float gaugeAfter;      // mức đọng lại sau khi rót xong (chạm dừng / tự đầy)
        private float gaugeAfterTimer;
        private Color gaugeColor = Color.white;

        private readonly RectTransform trayContent;
        private readonly ScrollRect trayScroll;
        private readonly RectTransform scrollIndicator;
        private readonly RectTransform scrollHandle;
        private int trayCount = -1;
        private float trayX0;
        private float trayContentW;
        private readonly float trayViewW;
        private float btnW = BtnW;
        private bool trayOverflow;
        private float autoScrollX = float.NaN;
        private float lastIndicatorX = float.NaN;

        // tô sáng bước kế
        private readonly RectTransform highlight;
        private int highlightIndex = -1;
        private bool canTapNow;

        private PourButtonBehaviour activePour;

        // ly-thước v2: thủy tinh + chất lỏng Filled + mặt gợn + lóe sáng / tia / rung
        private Image gaugeGlass;
        private RectTransform gaugeBody;     // khung ly (để rung)
        private RectTransform gaugeSurface;  // mặt chất lỏng gợn sóng
        private Image gaugeSurfaceImg;
        private Image gaugeFlash;
        private readonly RectTransform[] sparkles = new RectTransform[4];
        private readonly Image[] sparkleImgs = new Image[4];
        private float flashT = -1f;
        private float shakeT = -1f;
        private bool glassFull = true;       // alpha ly: 1 khi rót, 0,5 khi trống
        private const float FillBottom = 0.0875f; // lòng ly trong sprite ui_gauge_mask (96x160): y 14..140 từ đáy
        private const float FillTop = 0.875f;

        // mũi tên ▼ trên nút bước kế
        private RectTransform nextArrow;
        private bool tutorialReveal;

        private int lastTimerPct = -1;
        private bool toolMsgShown;
        private readonly Image speedTrack;
        private readonly Image speedFill;
        private readonly RectTransform speedFillRt;
        private int lastWrong;
        private float flashTimer;
        private float slideY;
        private float targetY;
        private bool visible = true;
        private Mode mode = Mode.Hidden;
        private readonly Vector3[] cornerBuf = new Vector3[4];

        private float CurH { get { return mode == Mode.Pill ? PillH : PanelH; } }
        private float HiddenY { get { return -(CurH + HiddenGap); } }

        /// Mép trên của bàn pha (px canvas tham chiếu, tính từ đáy màn hình): nút Sổ công thức cưỡi lên trên.
        public float TopY { get { return root.gameObject.activeSelf ? slideY + CurH : 0f; } }

        /// Bàn pha đang mở dạng dải (có đơn được chọn, đang pha, hoặc thử pha tự do).
        public bool Expanded { get; private set; }

        /// Đang thử pha tự do (nút Sổ công thức chỉ nổi lên trên bàn pha lúc này; bình thường nằm trong MENU).
        public bool Experimenting { get { return game.Station != null && game.Station.Experiment; } }

        /// Bàn pha đang hiện trên màn hình (dải hoặc viên thuốc).
        public bool Shown { get { return visible && mode != Mode.Hidden; } }

        /// Bề ngang dải pha (canvas HUD).
        public float Width { get { return PanelW; } }

        public BrewPanel(GameBootstrap game, Transform parent)
        {
            this.game = game;

            panelImage = HudBuilder.Panel(parent, "BrewPanel", true);
            root = panelImage.rectTransform;
            HudBuilder.SetFixed(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, RestY), new Vector2(PanelW, PanelH));

            stripGroup = HudBuilder.NewRect("Strip", root);
            HudBuilder.Stretch(stripGroup);
            pillGroup = HudBuilder.NewRect("Pill", root);
            HudBuilder.Stretch(pillGroup);

            // --- hàng trên (45): icon, tên + giá, chấm bước, gợi ý, thanh pha nhanh, nút X
            recipeIcon = HudBuilder.Icon(stripGroup, "RecipeIcon", null, 40f);
            HudBuilder.SetFixed(recipeIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -4f), new Vector2(40f, 40f));

            title = HudBuilder.AddText(stripGroup, "Title", "", 30, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(62f, -2f), new Vector2(250f, 44f));
            HudBuilder.Fit(title, 18);

            for (int i = 0; i < MaxSlots; i++)
            {
                var sl = new Slot();
                Image d = HudBuilder.Img(stripGroup, "Slot" + i, HudBuilder.T.disc, HudBuilder.CreamDark, false, false);
                HudBuilder.SetFixed(d.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(318f + i * SlotStep, -3f), new Vector2(SlotSize, SlotSize));
                sl.disc = d;
                sl.root = d.gameObject;
                sl.icon = HudBuilder.Icon(d.transform, "Icon", null, 28f);
                HudBuilder.SetFixed(sl.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(28f, 28f));
                sl.badge = HudBuilder.Icon(d.transform, "Badge", null, 20f);
                HudBuilder.SetFixed(sl.badge.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-3f, 4f), new Vector2(20f, 20f));
                sl.root.SetActive(false);
                slots[i] = sl;
            }

            // gợi ý: chữ coffee_md đậm (Baloo), không xám nhạt; cạnh chuỗi bước (hàng trên chỉ cao 45)
            hint = HudBuilder.AddText(stripGroup, "Hint", "", 22, TextAnchor.MiddleLeft, HudBuilder.InkSoft, true, false);
            HudBuilder.SetFixed(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(586f, -1f), new Vector2(170f, 44f));
            HudBuilder.Fit(hint, 14);

            // Thanh "pha nhanh" mảnh: đầy lúc bắt đầu, cạn dần tới hạn 3 sao (hết = mất 3★)
            timer = HudBuilder.AddText(stripGroup, "SpeedLabel", Strings.SpeedLabel, 18, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(timer.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(762f, -2f), new Vector2(190f, 22f));
            HudBuilder.Fit(timer, 12);
            speedTrack = HudBuilder.Img(stripGroup, "SpeedTrack", HudBuilder.T.barTrack, HudBuilder.CreamDark, true, false);
            HudBuilder.SetFixed(speedTrack.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(762f, -27f), new Vector2(190f, 14f));
            speedFill = HudBuilder.Img(speedTrack.transform, "SpeedFill", HudBuilder.T.barFill, HudBuilder.Mint, true, false);
            speedFillRt = speedFill.rectTransform;
            HudBuilder.SetRect(speedFillRt, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            actionButton = HudBuilder.AddLabelButton(stripGroup, "Action", ClayStyle.Coffee, "X", 34, OnAction, out actionLabel);
            HudBuilder.SetFixed((RectTransform)actionButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -6f), new Vector2(70f, 70f));
            if (HudBuilder.T.iconClose != null)
            {
                actionLabel.enabled = false; // icon thay chữ "X"
                Image xi = HudBuilder.Icon(actionButton.Content, "Icon", HudBuilder.T.iconClose, 40f);
                HudBuilder.SetFixed(xi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            }

            BuildGauge();

            // Nút "Xong" của thử pha tự do: thay chỗ ly-thước (thử pha không rót, nguyên liệu vào ngay)
            Text doneLabel;
            doneButton = HudBuilder.AddLabelButton(stripGroup, "Done", ClayStyle.Green, RecipeStrings.Done, 30, OnDone, out doneLabel);
            HudBuilder.SetFixed((RectTransform)doneButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(GaugeX, -GaugeTop), new Vector2(GaugeW, GaugeH));
            doneButton.gameObject.SetActive(false);

            // --- khay nguyên liệu: viewport cắt (RectMask2D) + ScrollRect ngang
            trayViewW = PanelW - TrayLeft - TrayRightPad;
            RectTransform viewport = HudBuilder.NewRect("Tray", stripGroup);
            HudBuilder.SetFixed(viewport, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(TrayLeft, -TrayTop), new Vector2(trayViewW, TrayH));
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f);
            vpImg.raycastTarget = true; // nhận kéo ở khoảng trống giữa các nút
            viewport.gameObject.AddComponent<RectMask2D>();

            trayContent = HudBuilder.NewRect("Content", viewport);
            trayContent.anchorMin = new Vector2(0f, 1f);
            trayContent.anchorMax = new Vector2(0f, 1f);
            trayContent.pivot = new Vector2(0f, 1f);
            trayContent.anchoredPosition = Vector2.zero;
            trayContent.sizeDelta = new Vector2(trayViewW, TrayH);

            trayScroll = viewport.gameObject.AddComponent<ScrollRect>();
            trayScroll.viewport = viewport;
            trayScroll.content = trayContent;
            trayScroll.horizontal = true;
            trayScroll.vertical = false;
            trayScroll.movementType = ScrollRect.MovementType.Clamped;
            trayScroll.inertia = true;
            trayScroll.scrollSensitivity = 40f;

            for (int i = 0; i < TrayOrder.Length; i++) stepButtons[i] = CreateStepButton(i);

            // Viền vàng của nút bước kế (4 thanh, không bắt chạm), gắn vào nút đang tô sáng
            highlight = HudBuilder.NewRect("NextHighlight", trayContent);
            for (int i = 0; i < 4; i++)
            {
                Image bar = HudBuilder.Img(highlight, "Bar" + i, null, HudBuilder.Accent, false, false);
                RectTransform br = bar.rectTransform;
                bool horizontal = i < 2;
                br.anchorMin = horizontal ? new Vector2(0f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 0f);
                br.anchorMax = horizontal ? new Vector2(1f, i == 0 ? 1f : 0f) : new Vector2(i == 2 ? 0f : 1f, 1f);
                br.pivot = new Vector2(i == 2 ? 0f : (i == 3 ? 1f : 0.5f), i == 0 ? 1f : (i == 1 ? 0f : 0.5f));
                br.sizeDelta = horizontal ? new Vector2(0f, HighlightBar) : new Vector2(HighlightBar, 0f);
                br.anchoredPosition = Vector2.zero;
            }
            highlight.gameObject.SetActive(false);

            // Mũi tên ▼ nhỏ phía trên nút bước kế (nằm ngoài khay để không bị RectMask2D cắt), không bắt chạm
            if (HudBuilder.T.arrowDown != null)
            {
                Image arr = HudBuilder.Img(stripGroup, "NextArrow", HudBuilder.T.arrowDown, HudBuilder.Accent, false, false);
                arr.preserveAspect = true;
                nextArrow = arr.rectTransform;
                HudBuilder.SetFixed(nextArrow, new Vector2(0f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, -(TrayTop + 4f)), new Vector2(24f, 18f));
                nextArrow.gameObject.SetActive(false);
            }

            // Chỉ báo cuộn mảnh dưới khay (chỉ hiện khi khay tràn)
            Image track = HudBuilder.Img(stripGroup, "ScrollTrack", null, new Color(HudBuilder.Ink.r, HudBuilder.Ink.g, HudBuilder.Ink.b, 0.18f), false, false);
            scrollIndicator = track.rectTransform;
            HudBuilder.SetFixed(scrollIndicator, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(TrayLeft + trayViewW * 0.5f, -(TrayTop + TrayH - 2f)), new Vector2(300f, 5f));
            Image handle = HudBuilder.Img(scrollIndicator, "Handle", null, new Color(HudBuilder.Ink.r, HudBuilder.Ink.g, HudBuilder.Ink.b, 0.6f), false, false);
            scrollHandle = handle.rectTransform;
            scrollHandle.anchorMin = new Vector2(0f, 0f);
            scrollHandle.anchorMax = new Vector2(0f, 1f);
            scrollHandle.pivot = new Vector2(0f, 0.5f);
            scrollIndicator.gameObject.SetActive(false);

            // --- viên thuốc "đang cầm ly": icon, tên món + gợi ý, 3 sao, nút Đổ ly (chỉ nút này nhận chạm)
            pillIcon = HudBuilder.Icon(pillGroup, "Icon", null, 52f);
            HudBuilder.SetFixed(pillIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(52f, 52f));
            pillTitle = HudBuilder.AddText(pillGroup, "Title", "", 28, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(pillTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -4f), new Vector2(226f, 36f));
            HudBuilder.Fit(pillTitle, 18);
            pillHint = HudBuilder.AddText(pillGroup, "Hint", "", 20, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(pillHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(72f, -40f), new Vector2(226f, 28f));
            HudBuilder.Fit(pillHint, 14);
            RectTransform sg = HudBuilder.NewRect("Quality", pillGroup);
            HudBuilder.SetFixed(sg, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(302f, 0f), new Vector2(84f, 28f));
            for (int i = 0; i < 3; i++)
            {
                Image s = HudBuilder.Icon(sg, "Star" + i, HudBuilder.T.iconStar, 26f);
                HudBuilder.SetFixed(s.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 28f, 0f), new Vector2(26f, 26f));
                stars[i] = s;
            }
            Text discardLabel;
            ClayButton discard = HudBuilder.AddLabelButton(pillGroup, "Discard", ClayStyle.Coffee, Strings.DiscardCup, 26, OnAction, out discardLabel);
            HudBuilder.SetFixed((RectTransform)discard.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(84f, 60f));
            HudBuilder.Fit(discardLabel, 16);
            pillGroup.gameObject.SetActive(false);

            // Bắt đầu ẩn (chưa chọn đơn)
            slideY = HiddenY;
            targetY = slideY;
            root.anchoredPosition = new Vector2(0f, slideY);
            root.gameObject.SetActive(false);

            game.Station.Changed += Refresh;
            if (game.Owner != null) game.Owner.Changed += Refresh;
            if (game.Recipes != null) game.Recipes.Changed += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            game.Station.Changed -= Refresh;
            if (game.Owner != null) game.Owner.Changed -= Refresh;
            if (game.Recipes != null) game.Recipes.Changed -= Refresh;
        }

        // ---------------------------------------------------------------- dựng nút nguyên liệu

        private static ClayStyle StyleFor(BrewStep s)
        {
            switch (s)
            {
                case BrewStep.Phin: return ClayStyle.Coffee;
                case BrewStep.SuaDac: return ClayStyle.Gold;
                case BrewStep.Tra: return ClayStyle.Green;
                case BrewStep.CotDua: return ClayStyle.Gold;
                case BrewStep.Trung: return ClayStyle.Gold;
                case BrewStep.Chanh: return ClayStyle.Green;
                case BrewStep.Lac:
                case BrewStep.Danh:
                case BrewStep.Cacao:
                    return ClayStyle.Coffee;
                default: return ClayStyle.Cream; // SuaTuoi, Da, Duong, Muoi, KemMuoi
            }
        }

        /// Màu chất lỏng trong ly-thước theo nguyên liệu đang rót.
        private static Color PourColor(BrewStep s)
        {
            switch (s)
            {
                case BrewStep.Phin: return new Color32(0x5A, 0x34, 0x1F, 0xFF);
                case BrewStep.SuaDac: return new Color32(0xF3, 0xE2, 0xB5, 0xFF);
                case BrewStep.SuaTuoi: return new Color32(0xFB, 0xF7, 0xEE, 0xFF);
                case BrewStep.Tra: return new Color32(0xB5, 0x7A, 0x2A, 0xFF);
                case BrewStep.CotDua: return new Color32(0xF6, 0xF0, 0xE2, 0xFF);
                default: return HudBuilder.Cream;
            }
        }

        private ClayButton CreateStepButton(int index)
        {
            BrewStep step = TrayOrder[index];
            StepAction action = StepActions.For(step);
            ClayStyle style = StyleFor(step);

            // Chạm: onClick như cũ. Rót/vuốt/xoay: không gắn onClick, component thao tác tự gọi khi đủ.
            UnityAction click = null;
            if (action == StepAction.Tap) click = () => OnStep(step);
            ClayButton b = HudBuilder.AddClayButton(trayContent, "Step_" + step, style, click);
            HudBuilder.SetFixed((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -BtnTop), new Vector2(BtnW, BtnH));
            if (step == BrewStep.Da && HudBuilder.T.Button(style, false) != null) b.SetTint(new Color(0.78f, 0.94f, 1f, 1f));

            // icon 64 cách mép trên 5: tâm -37, trùng tâm lớp phủ tiến độ của vuốt / xoay (StepIconFallback.CreateFillOverlay)
            Image ic = HudBuilder.Icon(b.Content, "Icon", null, BtnIcon);
            HudBuilder.SetFixed(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(BtnIcon, BtnIcon));
            StepIconFallback.Apply(ic, step);

            bool dark = style == ClayStyle.Coffee || style == ClayStyle.Green;
            Text lab = HudBuilder.AddText(b.Content, "Label", Strings.StepName(step), 22, TextAnchor.MiddleCenter, dark ? HudBuilder.Cream : HudBuilder.Ink, true, dark);
            HudBuilder.SetRect(lab.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 0f), new Vector2(-4f, 30f));
            HudBuilder.Fit(lab, 14);

            // Nhãn kiểu thao tác (Rót / Chạm / Vuốt / Xoay): ui_tag nằm TRONG góc trên-trái mặt nút, không tràn mép
            Image pill = HudBuilder.Img(b.Content, "ActionPill", HudBuilder.T.tag != null ? HudBuilder.T.tag : HudBuilder.T.chip, HudBuilder.Cream, true, false);
            if (HudBuilder.T.tag == null) pill.color = HudBuilder.Cream;
            HudBuilder.SetFixed(pill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(7f, -6f), new Vector2(50f, 24f));
            Text pt = HudBuilder.AddText(pill.transform, "T", RecipeStrings.ActionName(action), 16, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(pt.rectTransform, Vector2.zero, Vector2.one, new Vector2(3f, 1f), new Vector2(-3f, 0f));
            HudBuilder.Fit(pt, 10);

            // Component thao tác (tạo sau cùng để lớp phủ tiến độ nằm trên icon/nhãn)
            switch (action)
            {
                case StepAction.Hold:
                    if (StepActions.UseLegacyHold)
                    {
                        HoldButtonBehaviour.Attach(b, step, StepActions.HoldSeconds(step), OnStep, OnHoldReleased);
                    }
                    else
                    {
                        PourButtonBehaviour p = PourButtonBehaviour.Attach(b, step, StepActions.PourSeconds(step), OnPourBegin, OnPourFinished);
                        p.PlaceOverlay(new Vector2(0f, -37f), 80f);
                        pours[index] = p;
                    }
                    break;
                case StepAction.Swipe:
                    SwipeBehaviour.Attach(b, step, OnStep);
                    break;
                case StepAction.Spin:
                    SpinBehaviour.Attach(b, step, OnStep);
                    break;
            }
            return b;
        }

        /// Hiện n nút đầu của khay, căn giữa nếu ít hơn bề ngang, tràn thì cuộn.
        private void LayoutTray(int count)
        {
            trayCount = count;
            btnW = count > 0 ? Mathf.Clamp((trayViewW - 4f - (count - 1) * BtnGap) / count, BtnMinW, BtnW) : BtnW;
            float needed = count * btnW + (count - 1) * BtnGap;
            trayContentW = Mathf.Max(trayViewW, needed + 4f);
            trayX0 = (trayContentW - needed) * 0.5f;
            trayContent.sizeDelta = new Vector2(trayContentW, TrayH);
            for (int i = 0; i < stepButtons.Length; i++)
            {
                bool on = i < count;
                stepButtons[i].gameObject.SetActive(on);
                if (!on) continue;
                var brt = (RectTransform)stepButtons[i].transform;
                brt.anchoredPosition = new Vector2(trayX0 + i * (btnW + BtnGap), -BtnTop);
                brt.sizeDelta = new Vector2(btnW, BtnH);
            }
            trayOverflow = trayContentW > trayViewW + 1f;
            if (!trayOverflow) trayContent.anchoredPosition = new Vector2(0f, trayContent.anchoredPosition.y);
            scrollIndicator.gameObject.SetActive(trayOverflow);
            lastIndicatorX = float.NaN;
            SetHighlight(-1);
        }

        private static int TrayIndexOf(BrewStep s)
        {
            for (int i = 0; i < TrayOrder.Length; i++)
                if (TrayOrder[i] == s) return i;
            return -1;
        }

        /// Đặt đích cuộn để nút của bước này nằm trọn trong khay (nếu chưa).
        private void ScrollToStep(BrewStep s)
        {
            autoScrollX = float.NaN;
            if (!trayOverflow) return;
            int i = TrayIndexOf(s);
            if (i < 0 || i >= trayCount) return;
            float left = trayX0 + i * (btnW + BtnGap);
            float curX = trayContent.anchoredPosition.x;
            if (left + curX >= 0f && left + btnW + curX <= trayViewW) return; // đã thấy đủ
            float minX = -(trayContentW - trayViewW);
            autoScrollX = Mathf.Clamp(-(left - (trayViewW - btnW) * 0.5f), minX, 0f);
        }

        // ---------------------------------------------------------------- hiển thị

        /// Ẩn hẳn (khi hiện bảng tổng kết).
        public void SetVisible(bool v)
        {
            visible = v;
            if (!v)
            {
                CancelActivePour();
                if (game.CameraRig != null) game.CameraRig.SetBottomInset(0f);
                root.gameObject.SetActive(false);
                return;
            }
            Refresh();
        }

        /// Hướng dẫn đang trỏ vào bàn pha (RevealForTutorial): hiện dải kể cả khi chưa chọn đơn.
        public void SetTutorialReveal(bool v)
        {
            if (tutorialReveal == v) return;
            tutorialReveal = v;
            Refresh();
        }

        /// Vị trí (pixel màn hình) cho toast chung: ngay trên mép bàn pha / viên thuốc; bàn pha ẩn thì sát đáy.
        public float ToastScreenY()
        {
            RectTransform parent = root.parent as RectTransform;
            float scale = root.lossyScale.y > 0.0001f ? root.lossyScale.y : 1f;
            if (Shown && root.gameObject.activeSelf)
            {
                root.GetWorldCorners(cornerBuf); // canvas overlay: toạ độ thế giới = pixel màn hình
                return Mathf.Max(cornerBuf[1].y, 0f) + 34f * scale;
            }
            if (parent == null) return 0f;
            parent.GetWorldCorners(cornerBuf);
            return cornerBuf[0].y + 150f * scale;
        }

        private void OnStep(BrewStep step)
        {
            // Đang rót mà chạm nút khác: một lần chạm vừa dừng rót (chấm điểm) vừa làm bước đó.
            // Chạm quá sớm (< 15% thời gian rót) thì bỏ qua cả lần chạm, ly vẫn rót tiếp.
            if (activePour != null && activePour.IsPouring && !activePour.StopPour()) return;
            game.Station.TapStep(step);
        }

        /// Chạm nút rót: dừng lần rót đang chạy (nếu có), rồi hỏi BrewStation có được rót bước này không.
        private bool OnPourBegin(PourButtonBehaviour p)
        {
            if (activePour != null && activePour != p && activePour.IsPouring && !activePour.StopPour()) return false;
            BrewStation.TapResult res;
            if (!game.Station.TryBeginPour(p.Step, out res)) return false; // sai nguyên liệu / chưa có ly...: BrewStation đã báo
            activePour = p;
            gaugeColor = PourColor(p.Step);
            gaugeAfterTimer = 0f;
            hint.text = RecipeStrings.PourStopHint;
            return true;
        }

        private void OnPourFinished(PourButtonBehaviour p, PourGrade grade)
        {
            float level = grade == PourGrade.Under ? 0.45f : (grade == PourGrade.Perfect ? 0.75f : 1f);
            if (activePour == p)
            {
                activePour = null;
                gaugeAfter = level;
                gaugeAfterTimer = 0.6f;
            }
            ShowGrade(grade);
            game.Station.TapStep(p.Step, grade);
        }

        /// Chạm ly-thước: dừng rót.
        private void StopActivePour()
        {
            if (activePour != null && activePour.IsPouring) activePour.StopPour();
        }

        private void CancelActivePour()
        {
            if (activePour != null) activePour.CancelPour();
            activePour = null;
        }

        /// Chữ nổi nhỏ trên ly-thước: "Chuẩn!" / "Đủ" / "Hơi ít" (không chặn chạm).
        private void ShowGrade(PourGrade g)
        {
            if (!gaugeRoot.gameObject.activeInHierarchy) return;
            gaugeRoot.GetWorldCorners(cornerBuf);
            Vector2 p = new Vector2((cornerBuf[1].x + cornerBuf[2].x) * 0.5f + 40f * root.lossyScale.x, cornerBuf[1].y + 30f * root.lossyScale.y);
            string text = g == PourGrade.Perfect ? RecipeStrings.PourPerfect : (g == PourGrade.Under ? RecipeStrings.PourUnder : RecipeStrings.PourGood);
            // "Chuẩn!" vàng / "Đủ" xanh / "Hơi ít" cam (FloatLayer: kiểu nhãn kẹo viền + bóng)
            Color c = g == PourGrade.Perfect ? HudBuilder.Accent : (g == PourGrade.Under ? new Color(1f, 0.54f, 0.24f, 1f) : new Color(0.55f, 0.9f, 0.5f, 1f));
            UiFx.FloatTextScreen(p, text, c);
            if (GraphicsQuality.ReduceMotion) return;
            if (g == PourGrade.Perfect) flashT = 0f;      // ly lóe sáng + 4 tia lấp lánh
            else if (g == PourGrade.Under) shakeT = 0f;   // ly rung nhẹ
        }

        /// Nhả nút Giữ sớm (chỉ ở chế độ giữ cũ UseLegacyHold): sai bước (trừ chạm nhầm quá ngắn chỉ nhắc).
        private void OnHoldReleased(BrewStep step, bool accidental)
        {
            if (accidental)
            {
                StepGestureUtil.Hint(RecipeStrings.ActionHint(step));
                return;
            }
            game.Station.FailStep(step);
        }

        private void OnAction()
        {
            CancelActivePour();
            game.Station.CancelOrDiscard();
        }

        private void OnDone()
        {
            game.Station.FinishExperiment();
        }

        public void Refresh()
        {
            if (!visible) return;
            BrewStation st = game.Station;
            RecipeDef r = st.Recipe;
            bool exp = st.Experiment;
            bool canTap;
            bool showDone = false;
            Mode want;

            // Lần rót đang chạy không còn khớp bước kế (hủy ly, khách bỏ đi...): hủy lặng lẽ
            if (activePour != null)
            {
                BrewStep ns;
                bool still = activePour.IsPouring && !exp && st.State == BrewStation.Phase.Brewing
                             && st.TryGetNextStep(out ns) && ns == activePour.Step;
                if (!still) CancelActivePour();
            }

            // Khay: Tier 1 = 5 nguyên liệu; Tier 2+ = tất cả
            int wantTray = (RecipeBook.CurrentTier >= 2) ? TrayOrder.Length : Tier1TrayCount;
            if (wantTray != trayCount) LayoutTray(wantTray);

            if (exp)
            {
                title.text = RecipeStrings.ExperimentTitle;
                hint.text = string.Format(RecipeStrings.ExperimentHintFmt, RecipeBalance.ExperimentIngredientXu);
                canTap = true;
                want = Mode.Strip;
                bool brewingNow = st.State == BrewStation.Phase.Brewing;
                showDone = brewingNow && st.ExperimentSteps.Count > 0;
            }
            else
            {
                switch (st.State)
                {
                    case BrewStation.Phase.Idle:
                        if (st.Selected == null || r == null)
                        {
                            title.text = Strings.PickOrder;
                            hint.text = "";
                            canTap = false;
                            want = tutorialReveal ? Mode.Strip : Mode.Hidden;
                        }
                        else
                        {
                            title.text = string.Format(Strings.RecipeTitleFmt, Strings.FullName(r), r.price);
                            hint.text = NextActionHint(r, 0);
                            canTap = true;
                            want = Mode.Strip;
                        }
                        break;

                    case BrewStation.Phase.Brewing:
                        title.text = string.Format(Strings.RecipeTitleFmt, Strings.FullName(r), r.price);
                        hint.text = activePour != null ? RecipeStrings.PourStopHint : NextActionHint(r, st.NextIndex);
                        canTap = true;
                        want = Mode.Strip;
                        break;

                    default: // Ready
                        pillTitle.text = r != null ? Strings.FullName(r) : "";
                        pillHint.text = Strings.ReadyHint;
                        canTap = false;
                        want = Mode.Pill;
                        break;
                }
            }

            // Chủ quán đang đi xa: chưa pha được, chờ về quầy
            bool ownerAway = game.Owner != null && !game.Owner.IsAtCart;
            if (canTap && ownerAway)
            {
                canTap = false;
                hint.text = Strings.OwnerComing;
            }
            else if (st.State == BrewStation.Phase.Ready && game.Owner != null && game.Owner.IsDelivering)
            {
                pillHint.text = Strings.OwnerDelivering;
            }

            // icon món
            if (exp) HudBuilder.SetIcon(recipeIcon, HudBuilder.T.iconCup);
            else HudBuilder.SetIcon(recipeIcon, r != null ? HudBuilder.T.IconFor(r) : null);
            HudBuilder.SetIcon(pillIcon, HudBuilder.T.iconCup != null ? HudBuilder.T.iconCup : (r != null ? HudBuilder.T.IconFor(r) : null));

            // đo lỗi mới để nháy dấu chéo
            int wrongNow = st.State == BrewStation.Phase.Brewing ? st.Wrong : 0;
            if (wrongNow > lastWrong) flashTimer = 0.8f;
            lastWrong = wrongNow;

            RefreshSlots();

            if (st.State == BrewStation.Phase.Ready)
            {
                for (int i = 0; i < stars.Length; i++)
                    stars[i].color = i < st.HeldQuality ? Color.white : new Color(0.35f, 0.28f, 0.24f, 0.45f);
            }

            for (int i = 0; i < stepButtons.Length; i++) stepButtons[i].SetInteractable(canTap);
            canTapNow = canTap;
            if (doneButton.gameObject.activeSelf != showDone) doneButton.gameObject.SetActive(showDone);
            bool showGauge = !exp && !StepActions.UseLegacyHold;
            if (gaugeRoot.gameObject.activeSelf != showGauge) gaugeRoot.gameObject.SetActive(showGauge);

            ApplyMode(want);

            // Tự cuộn khay tới nguyên liệu kế (chỉ khi pha theo đơn, không phải thử pha)
            autoScrollX = float.NaN;
            BrewStep next;
            if (canTap && st.TryGetNextStep(out next)) ScrollToStep(next);

            lastTimerPct = -1;
            toolMsgShown = false;
            Tick();
        }

        /// Chuyển dải / viên thuốc / ẩn. Hiện: bật lại và trượt lên từ ngoài màn hình; ẩn: trượt xuống rồi SetActive(false) (Tick).
        private void ApplyMode(Mode want)
        {
            Expanded = want == Mode.Strip;
            if (want == Mode.Hidden)
            {
                if (mode != Mode.Hidden)
                {
                    mode = Mode.Hidden;
                    targetY = -(root.sizeDelta.y + HiddenGap);
                }
                return;
            }

            bool wasHidden = mode == Mode.Hidden || !root.gameObject.activeSelf;
            mode = want;
            bool strip = want == Mode.Strip;
            if (stripGroup.gameObject.activeSelf != strip) stripGroup.gameObject.SetActive(strip);
            if (pillGroup.gameObject.activeSelf != !strip) pillGroup.gameObject.SetActive(!strip);
            root.sizeDelta = strip ? new Vector2(PanelW, PanelH) : new Vector2(PillW, PillH);
            panelImage.raycastTarget = strip; // viên thuốc: chỉ nút Đổ ly nhận chạm, nền không chặn
            if (wasHidden)
            {
                slideY = HiddenY;
                root.anchoredPosition = new Vector2(0f, slideY);
                root.gameObject.SetActive(true);
            }
            targetY = RestY;
        }

        /// Gợi ý thao tác cho bước kế (nút đúng đã được tô sáng nên nói luôn tên nguyên liệu và cách làm).
        private static string NextActionHint(RecipeDef r, int index)
        {
            if (r == null || r.steps == null || r.steps.Length == 0) return Strings.TapInOrder;
            BrewStep s = r.steps[Mathf.Clamp(index, 0, r.steps.Length - 1)];
            return RecipeStrings.ActionHint(s);
        }

        private void RefreshSlots()
        {
            BrewStation st = game.Station;
            if (st.Experiment)
            {
                RefreshExperimentSlots(st);
                return;
            }
            RecipeDef r = st.Recipe;
            int n = (r != null && r.steps != null && st.State != BrewStation.Phase.Ready) ? Mathf.Min(r.steps.Length, MaxSlots) : 0;
            bool brewing = st.State == BrewStation.Phase.Brewing;
            int cur = brewing ? st.NextIndex : 0;
            for (int i = 0; i < MaxSlots; i++)
            {
                Slot sl = slots[i];
                if (i >= n)
                {
                    sl.root.SetActive(false);
                    continue;
                }
                sl.root.SetActive(true);
                StepIconFallback.Apply(sl.icon, r.steps[i]);
                bool done = brewing && i < cur;
                bool current = i == cur;
                sl.disc.color = done ? HudBuilder.Mint : (current ? HudBuilder.Accent : HudBuilder.CreamDark);
                bool flash = brewing && current && flashTimer > 0f;
                if (done) HudBuilder.SetIcon(sl.badge, HudBuilder.T.iconCheck);
                else if (flash) HudBuilder.SetIcon(sl.badge, HudBuilder.T.iconCross);
                else HudBuilder.SetIcon(sl.badge, null);
            }
        }

        /// Thử pha: các nguyên liệu đã thêm (màu bạc hà) + một ô trống vàng cho nguyên liệu kế.
        private void RefreshExperimentSlots(BrewStation st)
        {
            var steps = st.ExperimentSteps;
            int cnt = steps.Count;
            int n = Mathf.Min(MaxSlots, cnt + (cnt < RecipeBalance.MaxExperimentSteps ? 1 : 0));
            for (int i = 0; i < MaxSlots; i++)
            {
                Slot sl = slots[i];
                if (i >= n)
                {
                    sl.root.SetActive(false);
                    continue;
                }
                sl.root.SetActive(true);
                HudBuilder.SetIcon(sl.badge, null);
                if (i < cnt)
                {
                    StepIconFallback.Apply(sl.icon, steps[i]);
                    sl.disc.color = HudBuilder.Mint;
                }
                else
                {
                    StepIconFallback.Clear(sl.icon);
                    sl.disc.color = HudBuilder.Accent;
                }
            }
        }

        // ---------------------------------------------------------------- tô sáng bước kế

        private void SetHighlight(int index)
        {
            if (index == highlightIndex) return;
            if (highlightIndex >= 0 && highlightIndex < stepButtons.Length)
                stepButtons[highlightIndex].Content.localScale = Vector3.one;
            highlightIndex = index;
            if (index < 0)
            {
                highlight.SetParent(trayContent, false);
                highlight.gameObject.SetActive(false);
                return;
            }
            highlight.SetParent(stepButtons[index].transform, false);
            HudBuilder.SetRect(highlight, Vector2.zero, Vector2.one, new Vector2(-4f, -4f), new Vector2(4f, 4f));
            highlight.SetAsLastSibling();
            highlight.gameObject.SetActive(true);
        }

        private void TickHighlight()
        {
            BrewStation st = game.Station;
            BrewStep next;
            int want = -1;
            if (mode == Mode.Strip && canTapNow && !st.Experiment && st.TryGetNextStep(out next))
            {
                want = TrayIndexOf(next);
                if (want >= trayCount) want = -1;
            }
            SetHighlight(want);
            TickNextArrow(want);
            TickNextSlot();
            if (want < 0) return;
            float k = 1f;
            if (!GraphicsQuality.ReduceMotion)
                k = 1f + HighlightScale * 0.5f * (1f - Mathf.Cos(Time.unscaledTime * HighlightHz * 2f * Mathf.PI));
            stepButtons[want].Content.localScale = new Vector3(k, k, 1f);
        }

        /// Mũi tên ▼ trên nút bước kế (nhún nhẹ); ẩn khi nút đã cuộn ra ngoài khay.
        private void TickNextArrow(int index)
        {
            if (nextArrow == null) return;
            bool on = index >= 0;
            if (on)
            {
                float left = trayX0 + index * (btnW + BtnGap) + trayContent.anchoredPosition.x;
                on = left >= -btnW * 0.25f && left + btnW <= trayViewW + btnW * 0.25f;
                if (on)
                {
                    float bob = GraphicsQuality.ReduceMotion ? 0f : 3f * Mathf.Sin(Time.unscaledTime * HighlightHz * 2f * Mathf.PI);
                    nextArrow.anchoredPosition = new Vector2(TrayLeft + left + btnW * 0.5f, -(TrayTop + 4f) + bob);
                }
            }
            if (nextArrow.gameObject.activeSelf != on) nextArrow.gameObject.SetActive(on);
        }

        /// Đĩa bước kế trong chuỗi bước: viền vàng nhấp nháy (phóng nhẹ).
        private void TickNextSlot()
        {
            BrewStation st = game.Station;
            int cur = (!st.Experiment && st.State == BrewStation.Phase.Brewing) ? st.NextIndex : (st.Selected != null ? 0 : -1);
            for (int i = 0; i < MaxSlots; i++)
            {
                RectTransform rt = slots[i].disc.rectTransform;
                float k = 1f;
                if (i == cur && slots[i].root.activeSelf && !GraphicsQuality.ReduceMotion)
                    k = 1f + 0.08f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
                if (!Mathf.Approximately(rt.localScale.x, k)) rt.localScale = new Vector3(k, k, 1f);
            }
        }

        // ---------------------------------------------------------------- mỗi frame

        /// Gọi mỗi frame: trượt bảng, nháy lỗi, đồng hồ khi đang pha, ly-thước, tô sáng, tự cuộn khay.
        public void Tick()
        {
            if (!visible) return;
            float dt = Time.unscaledDeltaTime;
            bool active = root.gameObject.activeSelf;

            if (active && Mathf.Abs(slideY - targetY) > 0.4f)
            {
                slideY = Mathf.Lerp(slideY, targetY, 1f - Mathf.Exp(-dt * 14f));
                if (Mathf.Abs(slideY - targetY) <= 0.4f) slideY = targetY;
                root.anchoredPosition = new Vector2(0f, slideY);
            }
            if (active && mode == Mode.Hidden && Mathf.Abs(slideY - targetY) <= 0.4f)
            {
                root.gameObject.SetActive(false); // đã trượt ra ngoài: tắt hẳn để không chặn chạm / không vẽ
                active = false;
                SetHighlight(-1);
            }

            // Báo camera phần màn hình bị bàn pha che (theo chiều cao canvas thật: màn hẹp hơn 16:9 match width nên canvas cao hơn HudRefHeight)
            if (game.CameraRig != null)
            {
                RectTransform canvasRt = root.parent as RectTransform;
                float canvasH = canvasRt != null && canvasRt.rect.height > 10f ? canvasRt.rect.height : HudBuilder.HudRefHeight;
                float top = active ? Mathf.Max(0f, slideY + root.sizeDelta.y) : 0f;
                game.CameraRig.SetBottomInset(Mathf.Min(BottomInsetOpen, top / canvasH));
            }
            if (!active) return;

            TickTray(dt);
            TickGauge(dt);
            TickHighlight();

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f)
                {
                    flashTimer = 0f;
                    RefreshSlots();
                }
            }

            BrewStation st = game.Station;
            bool brewing = st.State == BrewStation.Phase.Brewing && st.Recipe != null && !st.Experiment;
            if (!brewing)
            {
                SetTimerVisible(false, false);
                return;
            }

            // Món ẩn mà dụng cụ còn cấp 1: không thể 3 sao, thay thanh bằng lời nhắc
            if (!st.ThreeStarPossible)
            {
                SetTimerVisible(true, false);
                if (!toolMsgShown)
                {
                    toolMsgShown = true;
                    timer.text = RecipeStrings.NeedToolForThree;
                }
                return;
            }
            SetTimerVisible(true, true);
            toolMsgShown = false;

            float left = 1f - Mathf.Clamp01(st.BrewElapsed / Mathf.Max(0.1f, st.PerfectSeconds));
            int pct = Mathf.RoundToInt(left * 100f);
            if (pct == lastTimerPct) return;
            lastTimerPct = pct;
            // Co phần fill theo anchor để sprite 9-slice không bị méo
            speedFillRt.anchorMax = new Vector2(Mathf.Max(0.06f, left), 1f);
            speedFill.color = left > 0.5f ? HudBuilder.Good : (left > 0f ? HudBuilder.Accent : HudBuilder.Red);
            timer.text = left > 0f ? Strings.SpeedLabel : Strings.SpeedLate;
        }

        /// Ly-thước (hud_visual_polish §4): ui_gauge_glass trong suốt trên chất lỏng ui_gauge_mask (Filled Vertical, màu theo nguyên liệu),
        /// dải vàng "CHUẨN" trong lòng ly, mặt chất lỏng gợn khi rót. Chạm ly = dừng rót. Không có sprite thì vẽ khung trơn như cũ.
        private void BuildGauge()
        {
            Image hit = HudBuilder.Img(stripGroup, "Gauge", null, new Color(0f, 0f, 0f, 0f), false, true); // vùng chạm cả cột
            gaugeRoot = hit.rectTransform;
            HudBuilder.SetFixed(gaugeRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(GaugeX, -GaugeTop), new Vector2(GaugeW, GaugeH));
            var gaugeBtn = hit.gameObject.AddComponent<Button>();
            gaugeBtn.transition = Selectable.Transition.None;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            gaugeBtn.navigation = nav;
            gaugeBtn.onClick.AddListener(StopActivePour);

            bool sprites = HudBuilder.T.gaugeGlass != null && HudBuilder.T.gaugeMask != null;
            gaugeBody = HudBuilder.NewRect("Body", gaugeRoot);
            HudBuilder.SetFixed(gaugeBody, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(74f, 124f)); // 96x160 thu nhỏ

            float y0 = sprites ? FillBottom : 0.05f, y1 = sprites ? FillTop : 0.95f;
            if (!sprites)
            {
                Image bg = HudBuilder.Img(gaugeBody, "Back", null, new Color(1f, 1f, 1f, 0.35f), false, false);
                HudBuilder.Stretch(bg.rectTransform);
            }
            // chất lỏng
            gaugeFill = HudBuilder.Img(gaugeBody, "Fill", HudBuilder.T.gaugeMask, Color.white, false, false);
            gaugeFillRt = gaugeFill.rectTransform;
            if (sprites)
            {
                HudBuilder.Stretch(gaugeFillRt);
                gaugeFill.type = Image.Type.Filled;
                gaugeFill.fillMethod = Image.FillMethod.Vertical;
                gaugeFill.fillOrigin = (int)Image.OriginVertical.Bottom;
                gaugeFill.fillAmount = 0f;
            }
            else
            {
                HudBuilder.SetRect(gaugeFillRt, new Vector2(0.1f, y0), new Vector2(0.9f, y0), Vector2.zero, Vector2.zero);
            }
            gaugeFill.enabled = false;
            // mặt chất lỏng gợn sóng (dải elip sáng hơn ở mức đầy)
            gaugeSurfaceImg = HudBuilder.Img(gaugeBody, "Surface", HudBuilder.T.disc, Color.white, false, false);
            gaugeSurface = gaugeSurfaceImg.rectTransform;
            gaugeSurface.anchorMin = gaugeSurface.anchorMax = new Vector2(0.5f, y0);
            gaugeSurface.pivot = new Vector2(0.5f, 0.5f);
            gaugeSurface.sizeDelta = new Vector2(44f, 8f);
            gaugeSurfaceImg.enabled = false;

            // dải vàng = vùng "Chuẩn!" trong lòng ly (60-85% thời gian rót)
            float b0 = y0 + (y1 - y0) * StepActions.PerfectBandStart, b1 = y0 + (y1 - y0) * StepActions.PerfectBandEnd;
            Image band = HudBuilder.Img(gaugeBody, "Band", null, new Color(HudBuilder.Accent.r, HudBuilder.Accent.g, HudBuilder.Accent.b, 0.38f), false, false);
            HudBuilder.SetRect(band.rectTransform, new Vector2(0.22f, b0), new Vector2(0.78f, b1), Vector2.zero, Vector2.zero);
            for (int i = 0; i < 2; i++)
            {
                Image line = HudBuilder.Img(gaugeBody, "BandLine" + i, null, HudBuilder.Accent, false, false);
                float y = i == 0 ? b0 : b1;
                line.rectTransform.anchorMin = new Vector2(0.16f, y);
                line.rectTransform.anchorMax = new Vector2(0.84f, y);
                line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                line.rectTransform.sizeDelta = new Vector2(0f, 3f);
                line.rectTransform.anchoredPosition = Vector2.zero;
            }
            // ly thủy tinh vẽ trên chất lỏng
            if (sprites)
            {
                gaugeGlass = HudBuilder.Img(gaugeBody, "Glass", HudBuilder.T.gaugeGlass, Color.white, false, false);
                HudBuilder.Stretch(gaugeGlass.rectTransform);
                gaugeFlash = HudBuilder.Img(gaugeBody, "Flash", HudBuilder.T.gaugeMask, Color.white, false, false);
                HudBuilder.Stretch(gaugeFlash.rectTransform);
                gaugeFlash.enabled = false;
            }
            // chữ nhỏ "CHUẨN" dọc bên phải dải vàng
            Text tag = HudBuilder.AddText(gaugeRoot, "BandLabel", RecipeStrings.GaugeBand, 14, TextAnchor.MiddleCenter, HudBuilder.Accent, true, true);
            RectTransform tr = tag.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(60f, 16f);
            tr.anchoredPosition = new Vector2(82f, (((b0 + b1) * 0.5f) - 0.5f) * 124f);
            tr.localEulerAngles = new Vector3(0f, 0f, 90f);
            // 4 tia lấp lánh khi rót "Chuẩn!"
            for (int i = 0; i < sparkles.Length; i++)
            {
                Image sp = HudBuilder.Img(gaugeBody, "Sparkle" + i, HudBuilder.T.sparkle, Color.white, false, false);
                sp.rectTransform.sizeDelta = new Vector2(26f, 26f);
                sp.enabled = false;
                sparkles[i] = sp.rectTransform;
                sparkleImgs[i] = sp;
            }
            SetGlassFull(false);
        }

        /// Ly trống mờ 50% khi không rót.
        private void SetGlassFull(bool full)
        {
            if (glassFull == full) return;
            glassFull = full;
            if (gaugeGlass != null) gaugeGlass.color = new Color(1f, 1f, 1f, full ? 1f : 0.5f);
        }

        /// Ly-thước: mức đầy theo lần rót đang chạy; rót xong đọng lại một nhịp rồi cạn. Hiệu ứng chỉ chạy khi có rót / chấm điểm.
        private void TickGauge(float dt)
        {
            if (!gaugeRoot.gameObject.activeInHierarchy) return;
            float level;
            if (activePour != null && activePour.IsPouring)
            {
                level = activePour.Fill01;
            }
            else if (gaugeAfterTimer > 0f)
            {
                gaugeAfterTimer -= dt;
                level = gaugeAfter;
            }
            else
            {
                level = Mathf.MoveTowards(gaugeShown < 0f ? 0f : gaugeShown, 0f, dt * 3f);
            }
            bool pouring = activePour != null && activePour.IsPouring;
            SetGlassFull(pouring || level > 0.005f);
            TickGaugeFx(dt, level, pouring);
            if (Mathf.Abs(level - gaugeShown) < 0.002f) return;
            gaugeShown = level;
            float l = Mathf.Clamp01(level);
            bool sprites = gaugeGlass != null;
            float y0 = sprites ? FillBottom : 0.05f, y1 = sprites ? FillTop : 0.95f;
            float top = y0 + (y1 - y0) * l;
            if (sprites) gaugeFill.fillAmount = top;
            else gaugeFillRt.anchorMax = new Vector2(0.9f, top);
            gaugeFill.color = gaugeColor;
            gaugeFill.enabled = l > 0.005f;
            gaugeSurface.anchorMin = gaugeSurface.anchorMax = new Vector2(0.5f, top);
        }

        /// Mặt chất lỏng gợn (offset sin) khi đang rót; lóe sáng + tia khi "Chuẩn!"; rung khi "Hơi ít". Tắt khi "Giảm chuyển động".
        private void TickGaugeFx(float dt, float level, bool pouring)
        {
            bool wave = pouring && level > 0.02f && !GraphicsQuality.ReduceMotion;
            if (gaugeSurfaceImg.enabled != wave) gaugeSurfaceImg.enabled = wave;
            if (wave)
            {
                float w = 48f * Mathf.Lerp(0.85f, 1f, level);
                gaugeSurface.sizeDelta = new Vector2(w, 8f);
                gaugeSurface.anchoredPosition = new Vector2(3f * Mathf.Sin(Time.unscaledTime * 9f), 1.5f * Mathf.Sin(Time.unscaledTime * 13f));
                Color c = Color.Lerp(gaugeColor, Color.white, 0.35f);
                gaugeSurfaceImg.color = c;
            }

            if (flashT >= 0f)
            {
                flashT += dt;
                float k = Mathf.Clamp01(flashT / 0.45f);
                if (gaugeFlash != null)
                {
                    gaugeFlash.enabled = k < 1f;
                    gaugeFlash.color = new Color(1f, 1f, 0.9f, 0.85f * (1f - k));
                }
                for (int i = 0; i < sparkles.Length; i++)
                {
                    bool on = k < 1f && sparkleImgs[i].sprite != null;
                    if (sparkleImgs[i].enabled != on) sparkleImgs[i].enabled = on;
                    if (!on) continue;
                    float a = (45f + 90f * i) * Mathf.Deg2Rad;
                    float r = 18f + 34f * k;
                    sparkles[i].anchoredPosition = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 1.3f);
                    float s = Mathf.Sin(k * Mathf.PI);
                    sparkles[i].localScale = new Vector3(s, s, 1f);
                }
                if (k >= 1f) flashT = -1f;
            }

            if (shakeT >= 0f)
            {
                shakeT += dt;
                float k = Mathf.Clamp01(shakeT / 0.3f);
                gaugeBody.anchoredPosition = new Vector2(5f * Mathf.Sin(shakeT * 45f) * (1f - k), 0f);
                if (k >= 1f)
                {
                    shakeT = -1f;
                    gaugeBody.anchoredPosition = Vector2.zero;
                }
            }
        }

        private void SetTimerVisible(bool label, bool bar)
        {
            if (timer.gameObject.activeSelf != label)
            {
                timer.gameObject.SetActive(label);
                lastTimerPct = -1; // hiện lại thì vẽ lại ngay
            }
            if (speedTrack.gameObject.activeSelf != bar)
            {
                speedTrack.gameObject.SetActive(bar);
                lastTimerPct = -1;
            }
        }

        private void TickTray(float dt)
        {
            if (!trayOverflow) return;

            // Tự cuộn mượt tới nguyên liệu kế, nhường khi người chơi đang chạm
            if (!float.IsNaN(autoScrollX))
            {
                if (Input.GetMouseButton(0) || Input.touchCount > 0)
                {
                    autoScrollX = float.NaN;
                }
                else
                {
                    Vector2 p = trayContent.anchoredPosition;
                    float nx = Mathf.Lerp(p.x, autoScrollX, 1f - Mathf.Exp(-dt * 12f));
                    if (Mathf.Abs(nx - autoScrollX) < 0.5f)
                    {
                        nx = autoScrollX;
                        autoScrollX = float.NaN;
                    }
                    trayContent.anchoredPosition = new Vector2(nx, p.y);
                    trayScroll.velocity = Vector2.zero;
                }
            }

            // Chỉ báo cuộn
            float x = trayContent.anchoredPosition.x;
            if (!Mathf.Approximately(x, lastIndicatorX))
            {
                lastIndicatorX = x;
                float range = Mathf.Max(1f, trayContentW - trayViewW);
                float t = Mathf.Clamp01(-x / range);
                float track = scrollIndicator.rect.width;
                float hw = Mathf.Max(24f, track * trayViewW / trayContentW);
                scrollHandle.sizeDelta = new Vector2(hw, 0f);
                scrollHandle.anchoredPosition = new Vector2((track - hw) * t, 0f);
            }
        }
    }
}
