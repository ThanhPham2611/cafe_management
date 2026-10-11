using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Bảng quản lý nhân viên cấp quán 2: thuê, nâng cấp và cho nghỉ.
    public sealed class StaffPanel
    {
        private sealed class RoleView
        {
            public StaffRole role;
            public Text title;
            public Text detail;
            public ClayButton primary;
            public Text primaryLabel;
            public ClayButton fire;
        }

        private readonly GameBootstrap game;
        private readonly ClayButton openButton;
        private readonly Image panel;
        private readonly RoleView[] views = new RoleView[2];
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
            HudBuilder.SetFixed(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-28f, 0f), new Vector2(650f, 680f));

            Text heading = HudBuilder.AddText(panel.transform, "Heading", "ĐỘI NGŨ QUÁN", 42,
                TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -54f), new Vector2(560f, 58f));

            Text closeLabel;
            ClayButton close = HudBuilder.AddLabelButton(panel.transform, "Close", ClayStyle.Red, "×", 34,
                () => SetOpen(false), out closeLabel);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-22f, -20f), new Vector2(64f, 60f));

            views[0] = BuildRole(StaffRole.Cleaner, 118f);
            views[1] = BuildRole(StaffRole.Server, 378f);

            game.Staff.Changed += Refresh;
            GameEvents.MoneyChanged += OnMoneyChanged;
            game.Tier.TierChanged += Refresh;
            SetOpen(false);
            Refresh();
        }

        private RoleView BuildRole(StaffRole role, float y)
        {
            var view = new RoleView { role = role };
            Image card = HudBuilder.Chip(panel.transform, role + "Card", false);
            HudBuilder.SetFixed(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -y), new Vector2(570f, 225f));

            view.title = HudBuilder.AddText(card.transform, "Title",
                role == StaffRole.Cleaner ? "DỌN DẸP" : "PHỤC VỤ", 34,
                TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(view.title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -22f), new Vector2(360f, 46f));

            view.detail = HudBuilder.AddText(card.transform, "Detail", "", 25,
                TextAnchor.UpperLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(view.detail.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(28f, -72f), new Vector2(510f, 66f));
            view.detail.horizontalOverflow = HorizontalWrapMode.Wrap;

            Text primaryText;
            view.primary = HudBuilder.AddLabelButton(card.transform, "Primary", ClayStyle.Green, "", 24,
                () => OnPrimary(role), out primaryText);
            view.primaryLabel = primaryText;
            HudBuilder.SetFixed((RectTransform)view.primary.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(26f, 18f), new Vector2(345f, 68f));

            Text fireText;
            view.fire = HudBuilder.AddLabelButton(card.transform, "Fire", ClayStyle.Red, "CHO NGHỈ", 22,
                () => OnFire(role), out fireText);
            HudBuilder.SetFixed((RectTransform)view.fire.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-26f, 18f), new Vector2(165f, 68f));
            return view;
        }

        private void Toggle() { SetOpen(!open); }

        private void SetOpen(bool value)
        {
            open = value && game.Tier.Current >= 2;
            panel.gameObject.SetActive(open);
            if (open) Refresh();
        }

        private void OnPrimary(StaffRole role)
        {
            int index = Find(role);
            int cost = index < 0 ? GameBalance.StaffHireFee(role) : GameBalance.StaffUpgradeCost(game.Staff.Members[index].level);
            bool changed = index < 0
                ? game.Staff.Hire(role, role == StaffRole.Cleaner ? 2 : 0, StaffRoles.DefaultName(role))
                : game.Staff.Upgrade(index);
            if (changed)
            {
                game.NotifyShopTransaction(-cost);
                game.Hud.ShowToast(index < 0 ? "Đã thuê nhân viên!" : "Nhân viên đã lên cấp!");
            }
            Refresh();
        }

        private void OnFire(StaffRole role)
        {
            int index = Find(role);
            if (index >= 0) game.Staff.Fire(index);
            Refresh();
        }

        private int Find(StaffRole role)
        {
            string id = StaffRoles.ToId(role);
            for (int i = 0; i < game.Staff.Members.Count; i++)
                if (game.Staff.Members[i].role == id) return i;
            return -1;
        }

        private void OnMoneyChanged(int value) { Refresh(); }

        private void Refresh()
        {
            if (game == null || game.Staff == null) return;
            bool unlocked = game.Tier.Current >= 2;
            openButton.gameObject.SetActive(unlocked);
            if (!unlocked) SetOpen(false);

            for (int i = 0; i < views.Length; i++)
            {
                RoleView view = views[i];
                int index = Find(view.role);
                if (index < 0)
                {
                    int fee = GameBalance.StaffHireFee(view.role);
                    int wage = GameBalance.StaffDailyWage(view.role);
                    view.detail.text = "Chưa tuyển • Lương " + wage + " Xu/ngày";
                    view.primaryLabel.text = "THUÊ • " + fee + " XU";
                    view.primary.SetInteractable(unlocked && game.Wallet.Xu >= fee);
                    view.fire.gameObject.SetActive(false);
                }
                else
                {
                    StaffSave member = game.Staff.Members[index];
                    int cost = GameBalance.StaffUpgradeCost(member.level);
                    bool max = member.level >= 5;
                    bool tierLocked = member.level == 4 && game.Tier.Current < 3;
                    view.detail.text = member.name + " • Cấp " + member.level + " • Lương " +
                        GameBalance.StaffDailyWage(view.role) + " Xu/ngày";
                    view.primaryLabel.text = max ? "ĐÃ TỐI ĐA" : (tierLocked ? "CẦN QUÁN CẤP 3" : "NÂNG CẤP • " + cost + " XU");
                    view.primary.SetInteractable(!max && !tierLocked && game.Wallet.Xu >= cost);
                    view.fire.gameObject.SetActive(true);
                }
            }
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
