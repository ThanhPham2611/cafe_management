using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Trang trí quán (cấp 2+): nút "TRANG TRÍ" (gom vào popup MENU) và bảng hai tab Sơn tường / Sàn nhà (DecorCatalog).
    /// Bảng nằm ở nửa dưới màn hình, nền gần trong suốt để thấy quán phía trên: chạm một mẫu = xem thử ngay (dựng lại tường / sàn),
    /// nút dưới = mua (Xu / Hạt) hoặc dùng mẫu đã có. Đóng bảng = quay lại mẫu đang dùng.
    public sealed class DecorPanel
    {
        private const float CardW = 1360f;
        private const float CardH = 500f;
        private const float OptW = 196f;
        private const float OptH = 236f;
        private const float OptGap = 16f;
        private const int MaxOptions = 6;

        private sealed class OptionView
        {
            public ClayButton button;
            public Image main, accent;
            public Text name, status;
            public GameObject selected;
        }

        private readonly GameBootstrap game;
        private readonly GameObject root;
        private readonly ClayButton hudButton;
        private readonly ClayButton tabWall, tabFloor;
        private readonly OptionView[] options = new OptionView[MaxOptions];
        private readonly Text info;
        private readonly ClayButton action;
        private readonly Text actionLabel;
        private bool open;
        private DecorSlot tab = DecorSlot.Wall;
        private string pickWall, pickFloor; // mẫu đang xem thử

        public DecorPanel(GameBootstrap game, Transform buttonParent, Transform overlayParent)
        {
            this.game = game;

            Text hudLabel;
            hudButton = HudBuilder.AddLabelButton(buttonParent, "DecorButton", ClayStyle.Gold, Strings.DecorButton, 30, () => SetOpen(true), out hudLabel);
            HudBuilder.SetFixed((RectTransform)hudButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -270f), new Vector2(230f, 86f));

            Image dim = HudBuilder.Dim(overlayParent, "DecorPanel", 0.12f); // chặn chạm vào cảnh nhưng vẫn thấy quán
            HudBuilder.Stretch(dim.rectTransform);
            root = dim.gameObject;
            ModalMarker.Mark(root);
            Image card = HudBuilder.Panel(dim.transform, "Card", true);
            HudBuilder.SetFixed(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(CardW, CardH));

            Text title = HudBuilder.AddText(card.transform, "Title", Strings.DecorTitle, 42, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(520f, 64f));

            Text tl;
            tabWall = HudBuilder.AddLabelButton(card.transform, "TabWall", ClayStyle.Cream, Strings.DecorTabWall, 32, () => SetTab(DecorSlot.Wall), out tl);
            HudBuilder.SetFixed((RectTransform)tabWall.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-300f, -14f), new Vector2(250f, 78f));
            tabFloor = HudBuilder.AddLabelButton(card.transform, "TabFloor", ClayStyle.Cream, Strings.DecorTabFloor, 32, () => SetTab(DecorSlot.Floor), out tl);
            HudBuilder.SetFixed((RectTransform)tabFloor.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -14f), new Vector2(250f, 78f));

            float total = MaxOptions * OptW + (MaxOptions - 1) * OptGap;
            for (int i = 0; i < MaxOptions; i++)
            {
                float x = -total * 0.5f + OptW * 0.5f + i * (OptW + OptGap);
                options[i] = BuildOption(card.transform, i, x);
            }

            info = HudBuilder.AddText(card.transform, "Info", "", 28, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(info.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 24f), new Vector2(680f, 84f));
            info.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.Fit(info, 18);

            action = HudBuilder.AddLabelButton(card.transform, "Action", ClayStyle.Green, Strings.DecorUse, 32, OnAction, out actionLabel);
            HudBuilder.SetFixed((RectTransform)action.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-280f, 20f), new Vector2(340f, 92f));

            Text closeText;
            ClayButton close = HudBuilder.AddLabelButton(card.transform, "Close", ClayStyle.Red, Strings.DecorClose, 32, () => SetOpen(false), out closeText);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(230f, 92f));

            GameEvents.MoneyChanged += OnMoney;
            if (game.Gems != null) game.Gems.Changed += OnMoney;
            if (game.Tier != null) game.Tier.TierChanged += OnTierChanged;
            root.SetActive(false);
            RefreshHud();
        }

        private OptionView BuildOption(Transform card, int index, float x)
        {
            var v = new OptionView();
            v.button = HudBuilder.AddClayButton(card, "Opt" + index, ClayStyle.Cream, () => OnPick(index));
            HudBuilder.SetFixed((RectTransform)v.button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(x, -104f), new Vector2(OptW, OptH));
            RectTransform c = v.button.Content;

            Image sel = HudBuilder.Img(c, "Selected", null, HudBuilder.Accent, false, false);
            HudBuilder.SetFixed(sel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(OptW - 28f, 118f));
            v.selected = sel.gameObject;

            v.main = HudBuilder.Img(c, "Main", null, Color.white, false, false);
            HudBuilder.SetFixed(v.main.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(OptW - 40f, 74f));
            v.accent = HudBuilder.Img(c, "Accent", null, Color.white, false, false);
            HudBuilder.SetFixed(v.accent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(OptW - 40f, 32f));

            v.name = HudBuilder.AddText(c, "Name", "", 25, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(v.name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(OptW - 16f, 56f));
            v.name.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.Fit(v.name, 16);

            v.status = HudBuilder.AddText(c, "Status", "", 24, TextAnchor.MiddleCenter, HudBuilder.InkSoft, true, false);
            HudBuilder.SetFixed(v.status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(OptW - 16f, 34f));
            HudBuilder.Fit(v.status, 16);
            return v;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugOpen(bool value, bool floorTab = false)
        {
            SetOpen(value);
            if (value) SetTab(floorTab ? DecorSlot.Floor : DecorSlot.Wall);
        }

        public void DebugPick(string id)
        {
            DecorOption o = DecorCatalog.Find(id);
            if (o == null) return;
            SetTab(o.slot);
            DecorOption[] list = DecorCatalog.For(o.slot);
            for (int i = 0; i < list.Length; i++) if (list[i].id == id) OnPick(i);
        }
#endif

        public bool IsOpen { get { return open; } }

        private TierManager Tier { get { return game.Tier; } }

        private void SetOpen(bool value)
        {
            TierManager tier = Tier;
            if (value && (tier == null || !tier.CanDecorate)) return;
            bool was = open;
            open = value;
            root.SetActive(open);
            if (open)
            {
                pickWall = tier.Decor.WallFor(tier.Current);
                pickFloor = tier.Decor.FloorFor(tier.Current);
                Refresh();
            }
            else if (was && tier != null)
            {
                tier.PreviewDecor(null, null); // bỏ mẫu đang xem thử
            }
        }

        private void SetTab(DecorSlot slot)
        {
            tab = slot;
            Refresh();
        }

        private string Picked { get { return tab == DecorSlot.Wall ? pickWall : pickFloor; } }

        private void OnPick(int index)
        {
            DecorOption[] list = DecorCatalog.For(tab);
            if (index < 0 || index >= list.Length || Tier == null) return;
            DecorOption o = list[index];
            if (Tier.Current < o.minTier) { GameEvents.RaiseToast(o.name + ": " + Strings.DecorNeedTier); return; }
            if (tab == DecorSlot.Wall) pickWall = o.id;
            else pickFloor = o.id;
            Tier.PreviewDecor(pickWall, pickFloor);
            Refresh();
        }

        private void OnAction()
        {
            TierManager tier = Tier;
            if (tier == null) return;
            string id = Picked;
            DecorOption o = DecorCatalog.Find(id);
            if (o == null || tier.Decor.Current(tab, tier.Current) == id) return;
            DecorCheck r = tier.BuyOrApplyDecor(id);
            if (r == DecorCheck.Ok || r == DecorCheck.Owned)
            {
                GameEvents.RaiseToast(string.Format(Strings.DecorAppliedFmt, o.name));
                tier.PreviewDecor(pickWall, pickFloor); // tab kia có thể còn đang xem thử
            }
            else
            {
                GameEvents.RaiseToast(o.name + ": " + Reason(o, r));
            }
            Refresh();
        }

        private string Reason(DecorOption o, DecorCheck r)
        {
            switch (r)
            {
                case DecorCheck.NeedTier: return Strings.DecorNeedTier;
                case DecorCheck.NeedLevel: return string.Format(Strings.DecorNeedLevelFmt, o.minLevel);
                case DecorCheck.NeedXu: return string.Format(Strings.DecorNeedXuFmt, Strings.FormatInt(o.priceXu - game.Wallet.Xu));
                case DecorCheck.NeedGems:
                    int gems = game.Gems != null ? game.Gems.Gems : 0;
                    return string.Format(Strings.DecorNeedGemsFmt, Strings.FormatInt(o.priceGems - gems));
                default: return "";
            }
        }

        private static string Price(DecorOption o)
        {
            if (o.priceGems > 0) return string.Format(Strings.DecorPriceGemsFmt, Strings.FormatInt(o.priceGems));
            if (o.priceXu > 0) return string.Format(Strings.DecorPriceXuFmt, Strings.FormatInt(o.priceXu));
            return Strings.DecorFree;
        }

        private void OnMoney(int _)
        {
            if (open) Refresh();
        }

        private void OnTierChanged()
        {
            if (open) SetOpen(false);
            RefreshHud();
        }

        /// Nút chỉ hiện khi có phòng để trang trí (cấp 2+).
        private void RefreshHud()
        {
            bool visible = Tier != null && Tier.Current >= 2;
            if (hudButton.gameObject.activeSelf != visible) hudButton.gameObject.SetActive(visible);
        }

        private void Refresh()
        {
            if (!open || Tier == null) return;
            TierManager tier = Tier;
            DecorState state = tier.Decor;
            int t = tier.Current;
            int level = game.Reputation.Level;
            int gems = game.Gems != null ? game.Gems.Gems : 0;
            Color on = Color.white, off = new Color(0.78f, 0.74f, 0.66f, 1f), locked = new Color(0.62f, 0.6f, 0.55f, 1f);
            Color disabled = new Color(0.6f, 0.6f, 0.6f, 1f);
            tabWall.SetTint(tab == DecorSlot.Wall ? on : off);
            tabFloor.SetTint(tab == DecorSlot.Floor ? on : off);

            DecorOption[] list = DecorCatalog.For(tab);
            string current = state.Current(tab, t);
            for (int i = 0; i < options.Length; i++)
            {
                OptionView v = options[i];
                bool has = i < list.Length;
                v.button.gameObject.SetActive(has);
                if (!has) continue;
                DecorOption o = list[i];
                v.main.color = o.previewMain;
                v.accent.color = o.previewAccent;
                v.name.text = o.name;
                v.selected.SetActive(o.id == Picked);
                DecorCheck c = state.CanBuy(o.id, t, level, game.Wallet.Xu, gems);
                bool usable = c == DecorCheck.Owned || c == DecorCheck.Ok || c == DecorCheck.NeedXu || c == DecorCheck.NeedGems;
                v.button.SetTint(c == DecorCheck.NeedTier || c == DecorCheck.NeedLevel ? locked : on);
                if (o.id == current) v.status.text = Strings.DecorInUse;
                else if (c == DecorCheck.Owned) v.status.text = Strings.DecorOwned;
                else if (!usable) v.status.text = Reason(o, c);
                else v.status.text = Price(o);
            }

            // dòng thông tin + nút hành động cho mẫu đang chọn
            DecorOption p = DecorCatalog.Find(Picked);
            DecorCheck pc = p != null ? state.CanBuy(p.id, t, level, game.Wallet.Xu, gems) : DecorCheck.Unknown;
            info.text = InfoLine(p, t, state);
            if (p == null || p.id == current)
            {
                actionLabel.text = Strings.DecorInUse;
                action.SetInteractable(false);
                action.SetTint(disabled);
            }
            else if (pc == DecorCheck.Owned)
            {
                actionLabel.text = Strings.DecorUse;
                action.SetInteractable(true);
                action.SetTint(on);
            }
            else
            {
                actionLabel.text = p.priceGems > 0
                    ? string.Format(Strings.DecorBuyGemsFmt, Strings.FormatInt(p.priceGems))
                    : string.Format(Strings.DecorBuyXuFmt, Strings.FormatInt(p.priceXu));
                action.SetInteractable(pc == DecorCheck.Ok);
                action.SetTint(pc == DecorCheck.Ok ? on : disabled);
                if (pc != DecorCheck.Ok) info.text = p.name + " · " + Reason(p, pc);
            }
        }

        /// "Vàng Hội An · Đông Dương · +10 phong cách · Hợp tông Đông Dương: +5" (điểm tính với mẫu đang xem thử của cả hai mặt).
        private static string InfoLine(DecorOption p, int tier, DecorState state)
        {
            if (p == null) return Strings.DecorHint;
            if (p.family == DecorFamily.None) return p.name + " · " + Strings.DecorHint;
            int slot = tier >= 3 ? DecorCatalog.SlotBonusT3 : DecorCatalog.SlotBonusT2;
            string s = p.name + " · " + DecorCatalog.FamilyName(p.family) + " · " + string.Format(Strings.DecorBonusFmt, slot);
            DecorSlot other = p.slot == DecorSlot.Wall ? DecorSlot.Floor : DecorSlot.Wall;
            DecorOption o = DecorCatalog.Resolve(other, state.Current(other, tier), tier);
            if (o.family == p.family)
                s += " · " + string.Format(Strings.DecorMatchFmt, DecorCatalog.FamilyName(p.family), tier >= 3 ? DecorCatalog.MatchBonusT3 : DecorCatalog.MatchBonusT2);
            return s;
        }

        public void Dispose()
        {
            GameEvents.MoneyChanged -= OnMoney;
            if (game != null && game.Gems != null) game.Gems.Changed -= OnMoney;
            if (game != null && game.Tier != null) game.Tier.TierChanged -= OnTierChanged;
        }
    }
}
