using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Mở rộng nhà hàng (cấp 3): nút "MỞ RỘNG" ở cột phải HUD và bảng ba khu Sân vườn / Lầu / VIP (ZoneInfo.Buyable).
    /// Chạm một thẻ khu chưa mở lần đầu = chọn (hiện "Chạm lần nữa để mở"), lần hai = mua (tránh bấm nhầm 180.000 Xu).
    /// Mua xong bảng đóng lại để người chơi thấy khu mới. Nút chỉ hiện từ cấp 3.
    public sealed class ZonePanel
    {
        private const float CardW = 1180f;
        private const float CardH = 760f;
        private const float OptionW = 340f;
        private const float OptionH = 420f;
        private const float OptionGap = 24f;

        private sealed class OptionView
        {
            public ZoneId zone;
            public ClayButton button;
            public Text cost;
            public Text badge;
        }

        private readonly GameBootstrap game;
        private readonly GameObject root;
        private readonly ClayButton hudButton;
        private readonly Text hudLabel;
        private readonly OptionView[] options = new OptionView[ZoneInfo.Buyable.Length];
        private readonly Text hint;
        private bool open;
        private int hudState = -1; // -1 chưa vẽ, 0 chưa đủ tiền, 1 đủ tiền mở thêm một khu
        private ZoneId pending = ZoneId.Hall; // Hall = chưa chọn khu nào

        public ZonePanel(GameBootstrap game, Transform buttonParent, Transform overlayParent)
        {
            this.game = game;

            hudButton = HudBuilder.AddLabelButton(buttonParent, "ZonesButton", ClayStyle.Gold, Strings.ZoneButton, 30, () => SetOpen(true), out hudLabel);
            HudBuilder.SetFixed((RectTransform)hudButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -182f), new Vector2(230f, 86f)); // cột phải, ngay dưới tên quán (thẻ lên cấp đã ẩn ở cấp 3)

            Image dim = HudBuilder.Dim(overlayParent, "ZonesPanel", 0.62f);
            HudBuilder.Stretch(dim.rectTransform);
            root = dim.gameObject;
            ModalMarker.Mark(root);
            Image card = HudBuilder.Panel(dim.transform, "Card", true);
            HudBuilder.SetFixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW, CardH));

            Text title = HudBuilder.AddText(card.transform, "Title", Strings.ZoneTitle, 46, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 60f));

            float total = options.Length * OptionW + (options.Length - 1) * OptionGap;
            for (int i = 0; i < options.Length; i++)
            {
                float x = -total * 0.5f + OptionW * 0.5f + i * (OptionW + OptionGap);
                options[i] = BuildOption(card.transform, ZoneInfo.Buyable[i], x);
            }

            hint = HudBuilder.AddText(card.transform, "Hint", Strings.ZoneHint, 26, TextAnchor.MiddleCenter, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(1050f, 44f));
            HudBuilder.Fit(hint, 18);

            Text closeText;
            ClayButton close = HudBuilder.AddLabelButton(card.transform, "Close", ClayStyle.Red, "Đóng", 32, () => SetOpen(false), out closeText);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 88f));

            GameEvents.MoneyChanged += OnMoney;
            if (game.Tier != null)
            {
                game.Tier.TierChanged += RefreshHud;
                game.Tier.ZonesChanged += OnZonesChanged;
            }
            SetOpen(false);
            RefreshHud();
        }

        private OptionView BuildOption(Transform card, ZoneId zone, float x)
        {
            var v = new OptionView { zone = zone };
            v.button = HudBuilder.AddClayButton(card, "Zone_" + zone, ClayStyle.Green, () => OnZoneTapped(zone));
            HudBuilder.SetFixed((RectTransform)v.button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, -120f), new Vector2(OptionW, OptionH));
            RectTransform c = v.button.Content;

            Text name = HudBuilder.AddText(c, "Name", ZoneInfo.Name(zone), 54, TextAnchor.MiddleCenter, HudBuilder.Cream, true, true);
            HudBuilder.SetFixed(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(OptionW - 30f, 70f));
            HudBuilder.Fit(name, 30);

            Text desc = HudBuilder.AddText(c, "Desc", ZoneInfo.Description(zone), 25, TextAnchor.UpperCenter, HudBuilder.Cream, false, false);
            HudBuilder.SetFixed(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(OptionW - 40f, 170f));
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.Fit(desc, 17);

            v.cost = HudBuilder.AddText(c, "Cost", "", 44, TextAnchor.MiddleCenter, HudBuilder.Accent, true, true);
            HudBuilder.SetFixed(v.cost.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 84f), new Vector2(OptionW - 30f, 56f));
            HudBuilder.Fit(v.cost, 26);

            v.badge = HudBuilder.AddText(c, "Badge", "", 28, TextAnchor.MiddleCenter, HudBuilder.Cream, true, true);
            HudBuilder.SetFixed(v.badge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(OptionW - 24f, 46f));
            HudBuilder.Fit(v.badge, 18);
            return v;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugOpen(bool value) { SetOpen(value); }
#endif

        private void SetOpen(bool value)
        {
            open = value;
            pending = ZoneId.Hall;
            root.SetActive(open);
            if (open) Refresh();
        }

        private void OnZoneTapped(ZoneId zone)
        {
            TierManager tier = game.Tier;
            if (tier == null || tier.IsZoneOpen(zone)) return;
            if (!tier.CanUnlockZone(zone)) return;
            if (!tier.CanAffordZone(zone))
            {
                GameEvents.RaiseToast(string.Format(Strings.ZoneNeedFmt, Strings.FormatInt(ZoneInfo.Cost(zone)), ZoneInfo.Name(zone)));
                return;
            }
            if (pending != zone)
            {
                pending = zone;
                Refresh();
                return;
            }
            pending = ZoneId.Hall;
            if (tier.UnlockZone(zone)) SetOpen(false);
        }

        private void OnMoney(int xu)
        {
            if (open) Refresh();
            RefreshHud();
        }

        private void OnZonesChanged()
        {
            Refresh();
            RefreshHud();
        }

        /// Nút trên HUD: chỉ hiện ở cấp 3; có chữ "!" khi đủ tiền mở một khu nữa.
        private void RefreshHud()
        {
            TierManager tier = game.Tier;
            bool visible = tier != null && tier.Current >= 3;
            if (hudButton.gameObject.activeSelf != visible) hudButton.gameObject.SetActive(visible);
            if (!visible) return;
            bool afford = false;
            for (int i = 0; i < ZoneInfo.Buyable.Length && !afford; i++)
            {
                ZoneId z = ZoneInfo.Buyable[i];
                afford = !tier.IsZoneOpen(z) && tier.CanAffordZone(z);
            }
            int state = afford ? 1 : 0;
            if (state == hudState) return;
            hudState = state;
            hudLabel.text = afford ? Strings.ZoneButton + "!" : Strings.ZoneButton;
        }

        private void Refresh()
        {
            if (!open) return;
            TierManager tier = game.Tier;
            for (int i = 0; i < options.Length; i++)
            {
                OptionView v = options[i];
                bool isOpen = tier != null && tier.IsZoneOpen(v.zone);
                int cost = ZoneInfo.Cost(v.zone);
                int xu = game.Wallet.Xu;
                v.cost.text = isOpen ? "" : string.Format(Strings.ZoneCostFmt, Strings.FormatInt(cost));
                if (isOpen)
                {
                    v.button.SetTint(new Color(0.62f, 0.6f, 0.55f, 1f));
                    v.badge.text = Strings.ZoneOpened;
                }
                else if (xu < cost)
                {
                    v.button.SetTint(new Color(0.62f, 0.6f, 0.55f, 1f));
                    v.badge.text = "Thiếu " + Strings.FormatInt(cost - xu) + " Xu";
                }
                else
                {
                    v.button.SetTint(Color.white);
                    v.badge.text = pending == v.zone ? "Chạm lần nữa để mở" : "Chạm để chọn";
                }
            }
        }

        public void Dispose()
        {
            GameEvents.MoneyChanged -= OnMoney;
            if (game != null && game.Tier != null)
            {
                game.Tier.TierChanged -= RefreshHud;
                game.Tier.ZonesChanged -= OnZonesChanged;
            }
        }
    }
}
