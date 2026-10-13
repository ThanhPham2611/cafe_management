using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Bảng quản lý nhân viên (cấp quán 2+): mỗi nhân viên một hàng — thuê, nâng cấp, cho nghỉ.
    /// Số hàng theo GameBalance.StaffMaxCount (cấp 2: 1 Dọn dẹp + 1 Phục vụ; cấp 3: 3 Dọn dẹp + 2 Phục vụ + 3 Pha chế).
    public sealed class StaffPanel
    {
        private sealed class SlotView
        {
            public StaffRole role;
            public int index;          // người thứ index (từ 0) của vai trò
            public Image card;
            public Text title;
            public Text detail;
            public ClayButton primary;
            public Text primaryLabel;
            public ClayButton fire;
        }

        private const float PanelW = 700f;
        private const float RowH = 104f;
        private const float RowGap = 8f;
        private const float HeaderH = 104f;
        private const float FooterH = 56f;

        private readonly GameBootstrap game;
        private readonly ClayButton openButton;
        private readonly Image panel;
        private readonly Text footer;
        private readonly SlotView[] slots;
        private bool open;

        public StaffPanel(GameBootstrap game, Transform parent)
        {
            this.game = game;

            Text openLabel;
            openButton = HudBuilder.AddLabelButton(parent, "StaffButton", ClayStyle.Coffee,
                "NHÂN VIÊN", 25, Toggle, out openLabel);
            HudBuilder.SetFixed((RectTransform)openButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -462f), new Vector2(230f, 86f)); // cột phải, tránh khay nguyên liệu ở đáy

            panel = HudBuilder.Panel(parent, "StaffPanel", true);

            Text heading = HudBuilder.AddText(panel.transform, "Heading", "ĐỘI NGŨ QUÁN", 42,
                TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -54f), new Vector2(560f, 58f));

            Text closeLabel;
            ClayButton close = HudBuilder.AddLabelButton(panel.transform, "Close", ClayStyle.Red, "×", 34,
                () => SetOpen(false), out closeLabel);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-22f, -20f), new Vector2(64f, 60f));

            var list = new System.Collections.Generic.List<SlotView>();
            foreach (StaffRole role in StaffRoles.All)
            {
                int most = GameBalance.StaffMaxCount(role, 3);
                for (int k = 0; k < most; k++) list.Add(BuildSlot(role, k));
            }
            slots = list.ToArray();

            footer = HudBuilder.AddText(panel.transform, "Footer", "", 24, TextAnchor.MiddleCenter,
                HudBuilder.InkSoft, false, false);
            footer.horizontalOverflow = HorizontalWrapMode.Wrap;

            game.Staff.Changed += Refresh;
            GameEvents.MoneyChanged += OnMoneyChanged;
            game.Tier.TierChanged += Refresh;
            SetOpen(false);
            Refresh();
        }

        private SlotView BuildSlot(StaffRole role, int index)
        {
            var view = new SlotView { role = role, index = index };
            view.card = HudBuilder.Chip(panel.transform, role + "Card" + index, false);

            view.title = HudBuilder.AddText(view.card.transform, "Title", "", 30,
                TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(view.title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -10f), new Vector2(370f, 40f));
            HudBuilder.Fit(view.title, 22);

            view.detail = HudBuilder.AddText(view.card.transform, "Detail", "", 22,
                TextAnchor.UpperLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(view.detail.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -54f), new Vector2(390f, 40f));
            view.detail.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text primaryText;
            view.primary = HudBuilder.AddLabelButton(view.card.transform, "Primary", ClayStyle.Green, "", 24,
                () => OnPrimary(view), out primaryText);
            view.primaryLabel = primaryText;
            HudBuilder.SetFixed((RectTransform)view.primary.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-124f, 0f), new Vector2(240f, 70f));

            Text fireText;
            view.fire = HudBuilder.AddLabelButton(view.card.transform, "Fire", ClayStyle.Red, "NGHỈ", 22,
                () => OnFire(view), out fireText);
            HudBuilder.SetFixed((RectTransform)view.fire.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f), new Vector2(92f, 70f));
            return view;
        }

        private static string RoleTitle(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Server: return "PHỤC VỤ";
                case StaffRole.Brewer: return "PHA CHẾ";
                default: return "DỌN DẸP";
            }
        }

        private static string RoleHint(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Server: return "Mang ly bạn pha tới khách";
                case StaffRole.Brewer: return "Tự nhận đơn và pha món";
                default: return "Tự lau bàn, rửa ly, lau sàn";
            }
        }

        private void Toggle() { SetOpen(!open); }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugToggle() { Toggle(); }
#endif

        private void SetOpen(bool value)
        {
            open = value && game.Tier.Current >= 2;
            panel.gameObject.SetActive(open);
            if (open) Refresh();
        }

        private void OnPrimary(SlotView view)
        {
            int member = game.Staff.IndexOf(view.role, view.index);
            int cost = member < 0 ? GameBalance.StaffHireFee(view.role) : GameBalance.StaffUpgradeCost(game.Staff.Members[member].level);
            bool changed = member < 0
                ? game.Staff.Hire(view.role, StaffRoles.DefaultPreset(view.role, view.index), StaffRoles.DefaultName(view.role, view.index))
                : game.Staff.Upgrade(member);
            if (changed)
            {
                game.NotifyShopTransaction(-cost);
                game.Hud.ShowToast(member < 0 ? "Đã thuê nhân viên!" : "Nhân viên đã lên cấp!");
            }
            Refresh();
        }

        private void OnFire(SlotView view)
        {
            int member = game.Staff.IndexOf(view.role, view.index);
            if (member >= 0) game.Staff.Fire(member);
            Refresh();
        }

        private void OnMoneyChanged(int value) { Refresh(); }

        /// Hàng có hiện không: còn chỗ ở cấp này, hoặc đang có người (vd. Pha chế thuê từ trước khi cấp 3 mới mở).
        private bool IsVisible(SlotView view, int tier)
        {
            return view.index < GameBalance.StaffMaxCount(view.role, tier) || game.Staff.IndexOf(view.role, view.index) >= 0;
        }

        private void Refresh()
        {
            if (game == null || game.Staff == null) return;
            int tier = game.Tier.Current;
            bool unlocked = tier >= 2;
            openButton.gameObject.SetActive(unlocked);
            if (!unlocked) SetOpen(false);

            int shown = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                SlotView view = slots[i];
                bool visible = unlocked && IsVisible(view, tier);
                view.card.gameObject.SetActive(visible);
                if (!visible) continue;

                HudBuilder.SetFixed(view.card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -(HeaderH + shown * (RowH + RowGap))), new Vector2(PanelW - 50f, RowH));
                shown++;
                RefreshSlot(view, tier);
            }

            bool moreAtT3 = tier < 3;
            footer.gameObject.SetActive(unlocked && moreAtT3);
            footer.text = "Pha chế và thêm người cho mỗi vai trò mở khi mua nhà hàng (cấp 3)";
            float height = HeaderH + shown * (RowH + RowGap) + (moreAtT3 ? FooterH : 12f);
            HudBuilder.SetFixed(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-28f, 0f), new Vector2(PanelW, height));
            HudBuilder.SetFixed(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(PanelW - 60f, FooterH - 8f));
        }

        private void RefreshSlot(SlotView view, int tier)
        {
            int member = game.Staff.IndexOf(view.role, view.index);
            int wage = GameBalance.StaffDailyWage(view.role);
            string number = GameBalance.StaffMaxCount(view.role, 3) > 1 ? " " + (view.index + 1) : "";
            if (member < 0)
            {
                int fee = GameBalance.StaffHireFee(view.role);
                view.title.text = RoleTitle(view.role) + number;
                view.detail.text = "Lương " + wage + "/ngày • " + RoleHint(view.role);
                view.primaryLabel.text = "THUÊ • " + fee + " XU";
                view.primary.SetInteractable(game.Staff.CanHire(view.role) && game.Wallet.Xu >= fee);
                view.fire.gameObject.SetActive(false);
                return;
            }

            StaffSave item = game.Staff.Members[member];
            int cost = GameBalance.StaffUpgradeCost(item.level);
            bool max = item.level >= GameBalance.StaffMaxLevel;
            bool tierLocked = item.level == 4 && tier < 3;
            view.title.text = RoleTitle(view.role) + number + " • " + item.name;
            view.detail.text = "Cấp " + item.level + " • Lương " + wage + "/ngày";
            view.primaryLabel.text = max ? "TỐI ĐA" : (tierLocked ? "CẦN CẤP 3" : "NÂNG • " + cost + " XU");
            view.primary.SetInteractable(!max && !tierLocked && game.Wallet.Xu >= cost);
            view.fire.gameObject.SetActive(true);
        }

        public void Dispose()
        {
            if (game != null)
            {
                if (game.Staff != null) game.Staff.Changed -= Refresh;
                if (game.Tier != null) game.Tier.TierChanged -= Refresh;
            }
            GameEvents.MoneyChanged -= OnMoneyChanged;
        }
    }
}
