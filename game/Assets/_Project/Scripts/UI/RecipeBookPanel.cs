using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// "Sổ công thức": nút trên HUD (cưỡi lên mép bàn pha) + bảng clay liệt kê công thức.
    ///  - Món đã biết: icon, giá/vốn, các bước theo thứ tự, nút bật/tắt trong menu hôm nay (tối đa 6 món ở Tier 2).
    ///  - Món ẩn: bóng đen + gợi ý chữ + nút "Gợi ý 30 Hạt" (mua rồi mới thấy thứ tự bước; thiếu Hạt thì khóa kèm lý do).
    ///  - "Thử pha tự do": đóng sổ và đưa BrewStation vào chế độ thử (khớp công thức ẩn thì khám phá).
    ///  - Dụng cụ: nâng cấp phin / máy đánh trứng / bình lắc (cấp >= 2 mới được 3 sao với món ẩn).
    public class RecipeBookPanel
    {
        private const float PanelW = 1560f;
        private const float PanelH = 940f;
        private const float HeaderH = 124f;
        private const float FooterH = 150f;
        private const float CardH = 150f;
        private const float CardGap = 12f;
        private const float BtnW = 300f;
        private const float BtnH = 96f;
        private const int MaxSteps = 6;
        private const int MaxTools = 3;

        private class CardView
        {
            public RectTransform root;
            public Image icon;
            public Text name;
            public Text sub;
            public Text hint;
            public Image[] steps = new Image[MaxSteps];
            public ClayButton toggle;
            public Text toggleLabel;
            public ClayButton hintButton;
            public Text hintLabel;
            public RecipeDef def;
        }

        private class ToolView
        {
            public ClayButton button;
            public Text label;
            public string id;
        }

        private readonly GameBootstrap game;
        private readonly BrewPanel brew;
        private readonly ClayButton openButton;
        private readonly RectTransform openRt;
        private readonly RectTransform overlay;
        private readonly RectTransform content;
        private readonly Text gemsText;
        private readonly Text menuText;
        private readonly Text teaser;
        private readonly ClayButton experimentButton;
        private readonly Text experimentLabel;
        private readonly List<CardView> cards = new List<CardView>();
        private readonly List<ToolView> tools = new List<ToolView>();
        private readonly RectTransform footer;
        private float lastButtonY = float.NaN;
        private bool visible = true;

        public bool IsOpen { get { return overlay.gameObject.activeSelf; } }

        public RecipeBookPanel(GameBootstrap game, RectTransform parent, BrewPanel brew)
        {
            this.game = game;
            this.brew = brew;

            // --- Nút trên HUD, ngay trên mép bàn pha (Tick bám theo khi bàn pha trượt)
            Text lbl;
            openButton = HudBuilder.AddLabelButton(parent, "RecipeBookButton", ClayStyle.Cream, RecipeStrings.Book, 36, Open, out lbl);
            openRt = (RectTransform)openButton.transform;
            HudBuilder.SetFixed(openRt, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(-540f, 160f), new Vector2(320f, BtnH));

            // --- Lớp phủ: nền mờ (chạm để đóng) + bảng
            RectTransform dim = HudBuilder.Panel(parent, "RecipeBookOverlay", new Color(0.1f, 0.06f, 0.04f, 0.55f), true);
            overlay = dim;
            HudBuilder.Stretch(dim);
            var dimBtn = dim.gameObject.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);

            Image panel = HudBuilder.Panel(dim, "Panel", true);
            RectTransform pr = panel.rectTransform;
            // Chạm vào nền bảng không được "xuyên" lên lớp mờ (sẽ đóng sổ): Button rỗng nuốt sự kiện click
            var absorb = panel.gameObject.AddComponent<Button>();
            absorb.transition = Selectable.Transition.None;
            HudBuilder.SetFixed(pr, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PanelW, PanelH));

            Text title = HudBuilder.AddText(pr, "Title", RecipeStrings.Book, 58, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(520f, 80f));

            menuText = HudBuilder.AddText(pr, "MenuCount", "", 34, TextAnchor.MiddleLeft, HudBuilder.InkSoft, true, false);
            HudBuilder.SetFixed(menuText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(580f, -30f), new Vector2(520f, 56f));
            HudBuilder.Fit(menuText, 22);

            gemsText = HudBuilder.AddText(pr, "Gems", "", 36, TextAnchor.MiddleRight, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(gemsText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-160f, -30f), new Vector2(260f, 56f));
            HudBuilder.Fit(gemsText, 22);

            Text xl;
            ClayButton close = HudBuilder.AddLabelButton(pr, "Close", ClayStyle.Red, "X", 52, Close, out xl);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -14f), new Vector2(100f, 100f));

            // --- Danh sách cuộn dọc
            RectTransform viewport = HudBuilder.NewRect("Viewport", pr);
            HudBuilder.SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(24f, FooterH), new Vector2(-24f, -HeaderH));
            var vpImg = viewport.gameObject.AddComponent<Image>();
            vpImg.color = new Color(0f, 0f, 0f, 0f);
            vpImg.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            content = HudBuilder.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 100f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 60f;

            teaser = HudBuilder.AddText(content, "Teaser", RecipeStrings.TeaserT1, 28, TextAnchor.MiddleCenter, HudBuilder.InkSoft, false, false);
            teaser.rectTransform.anchorMin = new Vector2(0f, 1f);
            teaser.rectTransform.anchorMax = new Vector2(1f, 1f);
            teaser.rectTransform.pivot = new Vector2(0.5f, 1f);
            teaser.rectTransform.sizeDelta = new Vector2(-8f, 70f);
            teaser.horizontalOverflow = HorizontalWrapMode.Wrap;
            teaser.gameObject.SetActive(false);

            // --- Chân bảng: thử pha tự do + dụng cụ
            footer = HudBuilder.NewRect("Footer", pr);
            HudBuilder.SetRect(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 18f), new Vector2(-24f, FooterH - 12f));

            experimentButton = HudBuilder.AddLabelButton(footer, "Experiment", ClayStyle.Gold, RecipeStrings.Experiment, 38, OnExperiment, out experimentLabel);
            HudBuilder.SetFixed((RectTransform)experimentButton.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(380f, 110f));

            int toolCount = Mathf.Min(MaxTools, game.Recipes != null ? game.Recipes.ToolIds.Count : 0);
            for (int i = 0; i < toolCount; i++)
            {
                var tv = new ToolView();
                tv.id = game.Recipes.ToolIds[i];
                string toolId = tv.id;
                tv.button = HudBuilder.AddLabelButton(footer, "Tool_" + toolId, ClayStyle.Cream, "", 28, () => OnUpgradeTool(toolId), out tv.label);
                HudBuilder.SetFixed((RectTransform)tv.button.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-(toolCount - 1 - i) * (BtnW + 14f), 0f), new Vector2(BtnW, 110f));
                tools.Add(tv);
            }

            overlay.gameObject.SetActive(false);

            if (game.Recipes != null) game.Recipes.Changed += OnChanged;
            if (game.Gems != null) game.Gems.Changed += OnGems;
            GameEvents.MoneyChanged += OnMoney;
        }

        public void Dispose()
        {
            if (game.Recipes != null) game.Recipes.Changed -= OnChanged;
            if (game.Gems != null) game.Gems.Changed -= OnGems;
            GameEvents.MoneyChanged -= OnMoney;
        }

        // ---------------------------------------------------------------- mở / đóng

        public void SetVisible(bool v)
        {
            visible = v;
            openButton.gameObject.SetActive(v);
            if (!v) Close();
        }

        public void Open()
        {
            if (!visible) return;
            overlay.gameObject.SetActive(true);
            overlay.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            if (overlay.gameObject.activeSelf) overlay.gameObject.SetActive(false);
        }

        /// Gọi mỗi frame (HudController.Update): nút Sổ nằm trên mép bàn pha đang trượt.
        public void Tick()
        {
            if (!visible || brew == null) return;
            float y = brew.TopY + 14f;
            if (float.IsNaN(lastButtonY) || Mathf.Abs(y - lastButtonY) > 0.5f)
            {
                lastButtonY = y;
                openRt.anchoredPosition = new Vector2(-540f, y);
            }
        }

        // ---------------------------------------------------------------- sự kiện

        private void OnChanged()
        {
            if (IsOpen) Refresh();
        }

        private void OnGems(int g)
        {
            if (IsOpen) Refresh();
        }

        private void OnMoney(int xu)
        {
            if (IsOpen) RefreshFooter();
        }

        private void OnExperiment()
        {
            RecipeBook rb = game.Recipes;
            if (rb == null || !rb.CanExperiment)
            {
                GameEvents.RaiseToast(RecipeStrings.ExperimentLockedT2);
                return;
            }
            if (game.Station.BeginExperiment()) Close();
        }

        private void OnUpgradeTool(string toolId)
        {
            if (game.Recipes != null) game.Recipes.TryUpgradeTool(toolId);
        }

        private void OnToggle(CardView cv)
        {
            if (cv.def == null || game.Recipes == null) return;
            bool now = game.Recipes.InMenu(cv.def);
            game.Recipes.SetOnMenu(cv.def.id, !now);
            Refresh();
        }

        private void OnHint(CardView cv)
        {
            if (cv.def == null || game.Recipes == null) return;
            game.Recipes.BuyHint(cv.def.id);
            Refresh();
        }

        // ---------------------------------------------------------------- vẽ

        private CardView EnsureCard(int index)
        {
            while (cards.Count <= index) cards.Add(CreateCard(cards.Count));
            return cards[index];
        }

        private CardView CreateCard(int index)
        {
            var cv = new CardView();
            Image bg = HudBuilder.Img(content, "Card" + index, HudBuilder.T.card, HudBuilder.Cream, true, false);
            RectTransform rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -index * (CardH + CardGap));
            rt.sizeDelta = new Vector2(-8f, CardH);
            cv.root = rt;

            cv.icon = HudBuilder.Icon(rt, "Icon", null, 100f);
            HudBuilder.SetFixed(cv.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(70f, 4f), new Vector2(100f, 100f));

            cv.name = HudBuilder.AddText(rt, "Name", "", 42, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(cv.name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, -14f), new Vector2(480f, 56f));
            HudBuilder.Fit(cv.name, 24);

            cv.sub = HudBuilder.AddText(rt, "Sub", "", 26, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(cv.sub.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(140f, -76f), new Vector2(480f, 40f));
            HudBuilder.Fit(cv.sub, 20);

            cv.hint = HudBuilder.AddText(rt, "Hint", "", 28, TextAnchor.MiddleLeft, HudBuilder.Ink, false, false);
            HudBuilder.SetFixed(cv.hint.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(640f, 0f), new Vector2(520f, 110f));
            cv.hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.Fit(cv.hint, 20);

            for (int i = 0; i < MaxSteps; i++)
            {
                Image s = HudBuilder.Icon(rt, "Step" + i, null, 60f);
                HudBuilder.SetFixed(s.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(670f + i * 72f, 0f), new Vector2(60f, 60f));
                s.enabled = false;
                cv.steps[i] = s;
            }

            CardView self = cv;
            cv.toggle = HudBuilder.AddLabelButton(rt, "Toggle", ClayStyle.Green, "", 32, () => OnToggle(self), out cv.toggleLabel);
            HudBuilder.SetFixed((RectTransform)cv.toggle.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(BtnW, 104f));

            cv.hintButton = HudBuilder.AddLabelButton(rt, "HintButton", ClayStyle.Gold, "", 32, () => OnHint(self), out cv.hintLabel);
            HudBuilder.SetFixed((RectTransform)cv.hintButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(BtnW, 104f));
            return cv;
        }

        public void Refresh()
        {
            RecipeBook rb = game.Recipes;
            if (rb == null) return;
            int tier = RecipeBook.CurrentTier;

            // Tiêu đề phụ
            gemsText.text = string.Format(RecipeStrings.GemsFmt, game.Gems != null ? game.Gems.Gems : 0);
            menuText.text = tier <= 1
                ? string.Format(RecipeStrings.MenuAllFmt, rb.MenuCount)
                : string.Format(RecipeStrings.MenuCountFmt, rb.MenuCount, rb.MenuLimit);

            // Thẻ công thức
            int n = 0;
            IReadOnlyList<RecipeDef> all = rb.Ordered;
            for (int i = 0; i < all.Count; i++)
            {
                RecipeDef r = all[i];
                if (!rb.IsListed(r)) continue;
                Bind(EnsureCard(n), r, rb, tier);
                n++;
            }
            for (int i = n; i < cards.Count; i++) cards[i].root.gameObject.SetActive(false);

            float h = n * (CardH + CardGap);
            bool showTeaser = tier <= 1;
            teaser.gameObject.SetActive(showTeaser);
            if (showTeaser)
            {
                teaser.rectTransform.anchoredPosition = new Vector2(0f, -h - 6f);
                h += 80f;
            }
            content.sizeDelta = new Vector2(0f, Mathf.Max(h, 100f));

            RefreshFooter();
        }

        private void Bind(CardView cv, RecipeDef r, RecipeBook rb, int tier)
        {
            cv.def = r;
            cv.root.gameObject.SetActive(true);
            bool known = rb.IsKnown(r);
            bool hinted = rb.HasHint(r.id);

            if (known)
            {
                StepIconFallback.ApplyRecipe(cv.icon, r, false);
                cv.name.text = Strings.FullName(r);
                cv.sub.text = string.Format(RecipeStrings.RecipeCostFmt, r.price, r.ingredientCost);
                if (!rb.MachineReady(r)) cv.sub.text = string.Format(RecipeStrings.NeedMachineFmt, MachineRules.DisplayName(r.requiredMachine));
                cv.hint.gameObject.SetActive(false);
                BindSteps(cv, r, true);

                cv.toggle.gameObject.SetActive(true);
                cv.hintButton.gameObject.SetActive(false);
                bool on = rb.InMenu(r);
                if (tier <= 1)
                {
                    cv.toggleLabel.text = RecipeStrings.OnMenu;
                    cv.toggle.SetInteractable(false);
                    cv.toggle.SetTint(ToggleTint(true));
                }
                else
                {
                    cv.toggleLabel.text = on ? RecipeStrings.OnMenu : RecipeStrings.OffMenu;
                    cv.toggle.SetInteractable(true);
                    cv.toggle.SetTint(ToggleTint(on));
                }
            }
            else
            {
                // Món ẩn chưa mở: bóng đen + gợi ý chữ; mua gợi ý thì lộ thứ tự bước
                StepIconFallback.ApplyRecipe(cv.icon, r, true);
                cv.name.text = RecipeStrings.UnknownName;
                cv.sub.text = string.IsNullOrEmpty(r.hint) ? "" : r.hint;
                cv.toggle.gameObject.SetActive(false);

                if (hinted)
                {
                    cv.hint.gameObject.SetActive(false);
                    BindSteps(cv, r, true);
                    cv.hintButton.gameObject.SetActive(false);
                }
                else
                {
                    cv.hint.gameObject.SetActive(true);
                    cv.hint.text = string.IsNullOrEmpty(r.hint) ? "" : r.hint;
                    cv.sub.text = "";
                    BindSteps(cv, r, false);
                    cv.hintButton.gameObject.SetActive(true);
                    int cost = Mathf.Max(0, r.hintCostGems);
                    int have = game.Gems != null ? game.Gems.Gems : 0;
                    if (have >= cost)
                    {
                        cv.hintLabel.text = string.Format(RecipeStrings.HintCostFmt, cost);
                        cv.hintButton.SetInteractable(true);
                    }
                    else
                    {
                        cv.hintLabel.text = string.Format(RecipeStrings.NeedGemsFmt, cost - have);
                        cv.hintButton.SetInteractable(false);
                    }
                }
            }
        }

        private static void BindSteps(CardView cv, RecipeDef r, bool show)
        {
            int n = (show && r.steps != null) ? Mathf.Min(r.steps.Length, MaxSteps) : 0;
            for (int i = 0; i < MaxSteps; i++)
            {
                Image s = cv.steps[i];
                if (i < n) StepIconFallback.Apply(s, r.steps[i]);
                else StepIconFallback.Clear(s);
            }
        }

        private static Color ToggleTint(bool on)
        {
            bool themed = HudBuilder.T.btnGreen != null;
            if (themed) return on ? Color.white : new Color(0.72f, 0.7f, 0.64f, 1f);
            return on ? HudBuilder.Good : HudBuilder.CreamDark;
        }

        private void RefreshFooter()
        {
            RecipeBook rb = game.Recipes;
            if (rb == null) return;

            bool can = rb.CanExperiment;
            experimentLabel.text = can ? RecipeStrings.Experiment : RecipeStrings.ExperimentLockedT2;
            experimentButton.SetInteractable(can);

            int xu = game.Wallet.Xu;
            for (int i = 0; i < tools.Count; i++)
            {
                ToolView tv = tools[i];
                int lv = rb.GetToolLevel(tv.id);
                int cost = rb.ToolUpgradeCost(tv.id);
                string name = RecipeStrings.ToolName(tv.id);
                if (cost < 0)
                {
                    tv.label.text = string.Format(RecipeStrings.ToolMaxFmt, name);
                    tv.button.SetInteractable(false);
                }
                else
                {
                    tv.label.text = name + "  cấp " + lv + "\nNâng: " + Strings.FormatInt(cost) + " Xu";
                    tv.button.SetInteractable(xu >= cost);
                }
            }
        }
    }
}
