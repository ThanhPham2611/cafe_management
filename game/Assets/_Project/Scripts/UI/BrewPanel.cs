using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cafe
{
    /// Bàn pha giữa-dưới: hàng tiến độ các bước (icon + dấu tích/chéo), khay nguyên liệu cuộn ngang (nút >= 96 px),
    /// nút Hủy/Đổ ly. Thu gọn thành dải nhỏ khi chưa chọn đơn / đang cầm ly; trượt lên (220 ms) khi chọn đơn.
    /// Khay: Tier 1 hiện 5 nút (như cũ); Tier 2+ hiện tất cả nguyên liệu, kéo ngang để xem thêm.
    /// Mỗi nút làm theo kiểu của bước (StepActions): chạm / giữ / vuốt / xoay. Thành công -> OnStep -> BrewStation.TapStep.
    public class BrewPanel
    {
        private const float PanelW = 960f;
        private const float PanelH = 372f;
        private const float StripVisible = 128f;
        private const float RestY = 24f;
        private const int MaxSlots = 6;

        // Khay nguyên liệu
        private const float TrayPad = 30f;
        private const float TrayTop = 204f;
        private const float TrayH = 156f;
        private const float BtnW = 160f; // 5 nút Tier 1 + khe 22 = 888 < khay 900: vừa, không phải cuộn
        private const float BtnH = 136f;
        private const float BtnGap = 22f; // ~10 pt: giữ nút liền kề không bị chạm nhầm
        private const float BtnTop = 10f;
        private const int Tier1TrayCount = 5;

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
        private readonly Image recipeIcon;
        private readonly Text title;
        private readonly Text hint;
        private readonly Text timer;
        private readonly GameObject starGroup;
        private readonly Image[] stars = new Image[3];
        private readonly Slot[] slots = new Slot[MaxSlots];
        private readonly ClayButton[] stepButtons = new ClayButton[TrayOrder.Length];
        private readonly ClayButton actionButton;
        private readonly Text actionLabel;
        private readonly ClayButton doneButton;

        private readonly RectTransform trayContent;
        private readonly ScrollRect trayScroll;
        private readonly RectTransform scrollIndicator;
        private readonly RectTransform scrollHandle;
        private int trayCount = -1;
        private float trayX0;
        private float trayContentW;
        private float trayViewW;
        private bool trayOverflow;
        private float autoScrollX = float.NaN;
        private float lastIndicatorX = float.NaN;

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

        /// Mép trên của bàn pha (px canvas tham chiếu, tính từ đáy màn hình): nút Sổ công thức cưỡi lên trên.
        public float TopY { get { return slideY + PanelH; } }

        public BrewPanel(GameBootstrap game, Transform parent)
        {
            this.game = game;

            Image panel = HudBuilder.Panel(parent, "BrewPanel", true);
            root = panel.rectTransform;
            HudBuilder.SetFixed(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, RestY), new Vector2(PanelW, PanelH));

            // --- đầu bảng
            recipeIcon = HudBuilder.Icon(root, "RecipeIcon", null, 76f);
            HudBuilder.SetFixed(recipeIcon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(76f, 76f));

            title = HudBuilder.AddText(root, "Title", "", 42, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -14f), new Vector2(510f, 54f));
            HudBuilder.Fit(title, 24);

            hint = HudBuilder.AddText(root, "Hint", "", 26, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(hint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -66f), new Vector2(510f, 36f));
            HudBuilder.Fit(hint, 20);

            // Thanh "pha nhanh": đầy lúc bắt đầu, cạn dần tới hạn 3 sao (hết = mất 3★)
            timer = HudBuilder.AddText(root, "SpeedLabel", Strings.SpeedLabel, 24, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(timer.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-272f, -14f), new Vector2(240f, 34f));
            HudBuilder.Fit(timer, 16);
            speedTrack = HudBuilder.Img(root, "SpeedTrack", HudBuilder.T.barTrack, HudBuilder.CreamDark, true, false);
            HudBuilder.SetFixed(speedTrack.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-272f, -52f), new Vector2(240f, 34f));
            speedFill = HudBuilder.Img(speedTrack.transform, "SpeedFill", HudBuilder.T.barFill, HudBuilder.Mint, true, false);
            speedFillRt = speedFill.rectTransform;
            HudBuilder.SetRect(speedFillRt, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));

            RectTransform sg = HudBuilder.NewRect("Quality", root);
            HudBuilder.SetFixed(sg, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-272f, -34f), new Vector2(150f, 52f));
            starGroup = sg.gameObject;
            for (int i = 0; i < 3; i++)
            {
                Image s = HudBuilder.Icon(sg, "Star" + i, HudBuilder.T.iconStar, 48f);
                HudBuilder.SetFixed(s.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * 50f, 0f), new Vector2(48f, 48f));
                stars[i] = s;
            }
            starGroup.SetActive(false);

            actionButton = HudBuilder.AddLabelButton(root, "Action", ClayStyle.Coffee, Strings.Cancel, 34, OnAction, out actionLabel);
            HudBuilder.SetFixed((RectTransform)actionButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(220f, 112f));

            // Nút "Xong" của thử pha tự do: nằm chỗ thanh pha nhanh (thanh đó ẩn khi thử pha)
            Text doneLabel;
            doneButton = HudBuilder.AddLabelButton(root, "Done", ClayStyle.Green, RecipeStrings.Done, 36, OnDone, out doneLabel);
            HudBuilder.SetFixed((RectTransform)doneButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-256f, -12f), new Vector2(210f, 112f));
            doneButton.gameObject.SetActive(false);

            // --- hàng tiến độ
            for (int i = 0; i < MaxSlots; i++)
            {
                var sl = new Slot();
                Image d = HudBuilder.Img(root, "Slot" + i, HudBuilder.T.disc, HudBuilder.CreamDark, false, false);
                HudBuilder.SetFixed(d.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f + i * 90f, -128f), new Vector2(70f, 70f));
                sl.disc = d;
                sl.root = d.gameObject;
                sl.icon = HudBuilder.Icon(d.transform, "Icon", null, 48f);
                HudBuilder.SetFixed(sl.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(48f, 48f));
                sl.badge = HudBuilder.Icon(d.transform, "Badge", null, 34f);
                HudBuilder.SetFixed(sl.badge.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-4f, 6f), new Vector2(34f, 34f));
                sl.root.SetActive(false);
                slots[i] = sl;
            }

            // --- khay nguyên liệu: viewport cắt (RectMask2D) + ScrollRect ngang
            trayViewW = PanelW - 2f * TrayPad;
            RectTransform viewport = HudBuilder.NewRect("Tray", root);
            HudBuilder.SetFixed(viewport, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(TrayPad, -TrayTop), new Vector2(trayViewW, TrayH));
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

            // Chỉ báo cuộn mảnh dưới khay (chỉ hiện khi khay tràn)
            Image track = HudBuilder.Img(root, "ScrollTrack", null, new Color(HudBuilder.Ink.r, HudBuilder.Ink.g, HudBuilder.Ink.b, 0.18f), false, false);
            scrollIndicator = track.rectTransform;
            HudBuilder.SetFixed(scrollIndicator, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(TrayTop + TrayH - 4f)), new Vector2(360f, 6f));
            Image handle = HudBuilder.Img(scrollIndicator, "Handle", null, new Color(HudBuilder.Ink.r, HudBuilder.Ink.g, HudBuilder.Ink.b, 0.6f), false, false);
            scrollHandle = handle.rectTransform;
            scrollHandle.anchorMin = new Vector2(0f, 0f);
            scrollHandle.anchorMax = new Vector2(0f, 1f);
            scrollHandle.pivot = new Vector2(0f, 0.5f);
            scrollIndicator.gameObject.SetActive(false);

            slideY = RestY - (PanelH - StripVisible);
            targetY = slideY;
            root.anchoredPosition = new Vector2(0f, slideY);

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

        private ClayButton CreateStepButton(int index)
        {
            BrewStep step = TrayOrder[index];
            StepAction action = StepActions.For(step);
            ClayStyle style = StyleFor(step);

            // Chạm: onClick như cũ. Giữ/vuốt/xoay: không gắn onClick, component thao tác tự gọi OnStep khi đủ.
            UnityAction click = null;
            if (action == StepAction.Tap) click = () => OnStep(step);
            ClayButton b = HudBuilder.AddClayButton(trayContent, "Step_" + step, style, click);
            HudBuilder.SetFixed((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -BtnTop), new Vector2(BtnW, BtnH));
            if (step == BrewStep.Da && HudBuilder.T.Button(style, false) != null) b.SetTint(new Color(0.78f, 0.94f, 1f, 1f));

            Image ic = HudBuilder.Icon(b.Content, "Icon", null, 66f);
            HudBuilder.SetFixed(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(66f, 66f));
            StepIconFallback.Apply(ic, step);

            bool dark = style == ClayStyle.Coffee || style == ClayStyle.Green;
            Text lab = HudBuilder.AddText(b.Content, "Label", Strings.StepName(step), 32, TextAnchor.MiddleCenter, dark ? HudBuilder.Cream : HudBuilder.Ink, true, dark);
            HudBuilder.SetRect(lab.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(6f, 2f), new Vector2(-6f, 40f));
            HudBuilder.Fit(lab, 20);

            // Nhãn kiểu thao tác ở góc trên trái (Giữ / Vuốt / Xoay)
            if (action != StepAction.Tap)
            {
                Image pill = HudBuilder.Chip(b.Content, "ActionPill", false);
                pill.color = HudBuilder.Cream;
                HudBuilder.SetFixed(pill.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -4f), new Vector2(64f, 30f));
                Text pt = HudBuilder.AddText(pill.transform, "T", RecipeStrings.ActionName(action), 22, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
                HudBuilder.Fit(pt, 16);
            }

            // Component thao tác (tạo sau cùng để lớp phủ tiến độ nằm trên icon/nhãn)
            switch (action)
            {
                case StepAction.Hold:
                    HoldButtonBehaviour.Attach(b, step, StepActions.HoldSeconds(step), OnStep, OnHoldReleased);
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
            float needed = count * BtnW + (count - 1) * BtnGap;
            trayContentW = Mathf.Max(trayViewW, needed + 4f);
            trayX0 = (trayContentW - needed) * 0.5f;
            trayContent.sizeDelta = new Vector2(trayContentW, TrayH);
            for (int i = 0; i < stepButtons.Length; i++)
            {
                bool on = i < count;
                stepButtons[i].gameObject.SetActive(on);
                if (on) ((RectTransform)stepButtons[i].transform).anchoredPosition = new Vector2(trayX0 + i * (BtnW + BtnGap), -BtnTop);
            }
            trayOverflow = trayContentW > trayViewW + 1f;
            if (!trayOverflow) trayContent.anchoredPosition = new Vector2(0f, trayContent.anchoredPosition.y);
            scrollIndicator.gameObject.SetActive(trayOverflow);
            lastIndicatorX = float.NaN;
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
            float left = trayX0 + i * (BtnW + BtnGap);
            float curX = trayContent.anchoredPosition.x;
            if (left + curX >= 0f && left + BtnW + curX <= trayViewW) return; // đã thấy đủ
            float minX = -(trayContentW - trayViewW);
            autoScrollX = Mathf.Clamp(-(left - (trayViewW - BtnW) * 0.5f), minX, 0f);
        }

        // ---------------------------------------------------------------- hiển thị

        /// Ẩn hẳn (khi hiện bảng tổng kết).
        public void SetVisible(bool v)
        {
            visible = v;
            if (!v && game.CameraRig != null) game.CameraRig.SetBottomInset(0f);
            root.gameObject.SetActive(v);
            if (v) Refresh();
        }

        private void OnStep(BrewStep step)
        {
            game.Station.TapStep(step);
        }

        /// Nhả nút Giữ sớm: sai bước (trừ chạm nhầm quá ngắn chỉ nhắc).
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
            bool showAction;
            bool expanded;
            bool showDone = false;

            // Khay: Tier 1 = 5 nguyên liệu; Tier 2+ = tất cả
            int wantTray = (RecipeBook.CurrentTier >= 2) ? TrayOrder.Length : Tier1TrayCount;
            if (wantTray != trayCount) LayoutTray(wantTray);

            if (exp)
            {
                title.text = RecipeStrings.ExperimentTitle;
                hint.text = string.Format(RecipeStrings.ExperimentHintFmt, RecipeBalance.ExperimentIngredientXu);
                canTap = true;
                showAction = true;
                expanded = true;
                bool brewingNow = st.State == BrewStation.Phase.Brewing;
                actionLabel.text = brewingNow ? Strings.Cancel : RecipeStrings.Exit;
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
                            showAction = false;
                            expanded = false;
                        }
                        else
                        {
                            title.text = string.Format(Strings.RecipeTitleFmt, Strings.FullName(r), r.price);
                            hint.text = NextActionHint(r, 0);
                            canTap = true;
                            showAction = true;
                            expanded = true;
                            actionLabel.text = Strings.Cancel;
                        }
                        break;

                    case BrewStation.Phase.Brewing:
                        title.text = string.Format(Strings.RecipeTitleFmt, Strings.FullName(r), r.price);
                        hint.text = NextActionHint(r, st.NextIndex);
                        canTap = true;
                        showAction = true;
                        expanded = true;
                        actionLabel.text = Strings.Cancel;
                        break;

                    default: // Ready
                        title.text = string.Format(Strings.ReadyTitleFmt, Strings.FullName(r));
                        hint.text = Strings.ReadyHint;
                        canTap = false;
                        showAction = true;
                        expanded = false;
                        actionLabel.text = Strings.DiscardCup;
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
                hint.text = Strings.OwnerDelivering;
            }

            // icon món
            if (exp) HudBuilder.SetIcon(recipeIcon, HudBuilder.T.iconCup);
            else HudBuilder.SetIcon(recipeIcon, r != null ? HudBuilder.T.IconFor(r) : null);

            // đo lỗi mới để nháy dấu chéo
            int wrongNow = st.State == BrewStation.Phase.Brewing ? st.Wrong : 0;
            if (wrongNow > lastWrong) flashTimer = 0.8f;
            lastWrong = wrongNow;

            RefreshSlots();

            bool ready = st.State == BrewStation.Phase.Ready;
            starGroup.SetActive(ready);
            if (ready)
            {
                for (int i = 0; i < stars.Length; i++)
                    stars[i].color = i < st.HeldQuality ? Color.white : new Color(0.35f, 0.28f, 0.24f, 0.45f);
            }

            for (int i = 0; i < stepButtons.Length; i++) stepButtons[i].SetInteractable(canTap);
            actionButton.gameObject.SetActive(showAction);
            if (doneButton.gameObject.activeSelf != showDone) doneButton.gameObject.SetActive(showDone);

            targetY = expanded ? RestY : RestY - (PanelH - StripVisible);

            // Tự cuộn khay tới nguyên liệu kế (chỉ khi pha theo đơn, không phải thử pha)
            autoScrollX = float.NaN;
            if (!exp && canTap && r != null && r.steps != null && r.steps.Length > 0)
            {
                int idx = st.State == BrewStation.Phase.Brewing ? Mathf.Min(st.NextIndex, r.steps.Length - 1) : 0;
                ScrollToStep(r.steps[idx]);
            }

            lastTimerPct = -1;
            toolMsgShown = false;
            Tick();
        }

        /// Gợi ý thao tác cho bước kế: chạm thì giữ câu chung (không lộ đáp án), giữ/vuốt/xoay thì nói cách làm.
        private static string NextActionHint(RecipeDef r, int index)
        {
            if (r == null || r.steps == null || r.steps.Length == 0) return Strings.TapInOrder;
            BrewStep s = r.steps[Mathf.Clamp(index, 0, r.steps.Length - 1)];
            return StepActions.For(s) == StepAction.Tap ? Strings.TapInOrder : RecipeStrings.ActionHint(s);
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

        /// Gọi mỗi frame: trượt bảng, nháy lỗi, đồng hồ khi đang pha, tự cuộn khay.
        public void Tick()
        {
            if (!visible) return;
            float dt = Time.unscaledDeltaTime;

            if (Mathf.Abs(slideY - targetY) > 0.4f)
            {
                slideY = Mathf.Lerp(slideY, targetY, 1f - Mathf.Exp(-dt * 14f));
                if (Mathf.Abs(slideY - targetY) <= 0.4f) slideY = targetY;
                root.anchoredPosition = new Vector2(0f, slideY);
            }
            // Báo camera phần màn hình bị bàn pha che (canvas HUD ref cao HudRefHeight, match height)
            if (game.CameraRig != null)
                game.CameraRig.SetBottomInset(Mathf.Max(0f, slideY + PanelH) / HudBuilder.HudRefHeight * 0.85f);

            TickTray(dt);

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
