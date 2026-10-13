using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Nút "Cửa hàng" (dưới trái) + bảng bên phải liệt kê nội thất mua được, chia tab "Ghế & bàn" / "Trang trí".
    /// Chạm thẻ = bắt đầu đặt (BuildModeController.BeginPlace); chưa trừ Xu cho tới khi xác nhận trên lưới.
    public class ShopPanel
    {
        private const float PanelW = 720f;
        private const float PanelH = 800f;
        private const float CardH = 160f;
        private const float CardGap = 12f;
        private const float TopBar = 228f;

        private class CardView
        {
            public ClayButton button;
            public Image icon;
            public Text name;
            public Image coin;
            public Text price;
            public Image attractChip;
            public Text attractText;
            public Image comfortChip;
            public Text comfortText;
            public Text reason;
            public FurnitureDef def;
            public bool fromInventory;   // thẻ đồ trong kho: đặt miễn phí
        }

        private readonly GameBootstrap game;
        private readonly BuildModeController builder;
        private readonly ClayButton shopButton;
        private readonly RectTransform panelRoot;
        private readonly RectTransform content;
        private readonly ClayButton tabSeats;
        private readonly ClayButton tabDecor;
        private readonly List<CardView> cards = new List<CardView>();
        private readonly List<FurnitureDef> seatList = new List<FurnitureDef>();
        private readonly List<FurnitureDef> decorList = new List<FurnitureDef>();
        private readonly List<FurnitureDef> shownDefs = new List<FurnitureDef>();   // kho trước, rồi hàng bán (đã lọc theo cấp)
        private readonly List<bool> shownFromInv = new List<bool>();
        private readonly ScrollRect scroll;
        private bool decorTab;
        private bool visible = true;

        public ShopPanel(GameBootstrap game, RectTransform parent)
        {
            this.game = game;
            builder = game.Builder;

            if (game.catalog != null)
            {
                for (int i = 0; i < game.catalog.Length; i++)
                {
                    FurnitureDef d = game.catalog[i];
                    if (!ShopRules.IsShopItem(d)) continue;
                    if (d.kind == FurnitureKind.Decor) decorList.Add(d);
                    else seatList.Add(d);
                }
            }

            // --- Nút Cửa hàng (dưới trái, tránh cột order phía trên và bàn pha giữa-dưới)
            Text lbl;
            shopButton = HudBuilder.AddLabelButton(parent, "ShopButton", ClayStyle.Gold, Strings.Shop, 46, OnShopButton, out lbl);
            HudBuilder.SetFixed((RectTransform)shopButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 16f), new Vector2(300f, 100f));

            // --- Bảng
            Image panel = HudBuilder.Panel(parent, "ShopPanel", true);
            panelRoot = panel.rectTransform;
            HudBuilder.SetFixed(panelRoot, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(PanelW, PanelH));

            Text title = HudBuilder.AddText(panelRoot, "Title", Strings.Shop, 58, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -20f), new Vector2(460f, 80f));

            Text xl;
            ClayButton close = HudBuilder.AddLabelButton(panelRoot, "Close", ClayStyle.Red, "X", 52, OnCloseClicked, out xl);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -14f), new Vector2(100f, 100f));

            Text tl;
            tabSeats = HudBuilder.AddLabelButton(panelRoot, "TabSeats", ClayStyle.Cream, Strings.TabSeats, 40, OnTabSeats, out tl);
            HudBuilder.SetFixed((RectTransform)tabSeats.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -122f), new Vector2(325f, 96f));
            tabDecor = HudBuilder.AddLabelButton(panelRoot, "TabDecor", ClayStyle.Cream, Strings.TabDecor, 40, OnTabDecor, out tl);
            HudBuilder.SetFixed((RectTransform)tabDecor.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(365f, -122f), new Vector2(325f, 96f));

            // --- Danh sách cuộn
            RectTransform viewport = HudBuilder.NewRect("Viewport", panelRoot);
            HudBuilder.SetRect(viewport, Vector2.zero, Vector2.one, new Vector2(24f, 28f), new Vector2(-24f, -TopBar));
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

            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 60f;

            panelRoot.gameObject.SetActive(false);

            GameEvents.MoneyChanged += OnMoney;
            game.Grid.LayoutChanged += OnLayout;
            builder.Changed += SyncVisibility;

            RefreshTabs();
        }

        public void Dispose()
        {
            GameEvents.MoneyChanged -= OnMoney;
            if (game != null && game.Grid != null) game.Grid.LayoutChanged -= OnLayout;
            if (builder != null) builder.Changed -= SyncVisibility;
        }

        /// Ẩn/hiện cả nút (cuối ngày ẩn); ẩn thì đóng build mode.
        public void SetVisible(bool v)
        {
            visible = v;
            if (!v) builder.Close();
            shopButton.gameObject.SetActive(v);
            SyncVisibility();
        }

        // ---------------------------------------------------------------- sự kiện

        private void OnShopButton()
        {
            if (builder.IsOpen) builder.Close();
            else builder.Open();
        }

        private void OnCloseClicked()
        {
            builder.Close();
        }

        private void OnTabSeats()
        {
            SetTab(false);
        }

        private void OnTabDecor()
        {
            SetTab(true);
        }

        private void SetTab(bool decor)
        {
            decorTab = decor;
            scroll.verticalNormalizedPosition = 1f;
            RefreshTabs();
            Refresh();
        }

        private void RefreshTabs()
        {
            Color on = new Color(1f, 0.82f, 0.42f, 1f);
            Color off = new Color(0.82f, 0.78f, 0.7f, 1f);
            tabSeats.SetTint(decorTab ? off : on);
            tabDecor.SetTint(decorTab ? on : off);
        }

        private void OnMoney(int xu)
        {
            if (panelRoot != null && panelRoot.gameObject.activeSelf) Refresh();
        }

        private void OnLayout()
        {
            if (panelRoot != null && panelRoot.gameObject.activeSelf) Refresh();
        }

        private void SyncVisibility()
        {
            bool show = visible && builder.IsOpen && !builder.IsPlacing;
            if (panelRoot.gameObject.activeSelf == show) return;
            panelRoot.gameObject.SetActive(show);
            if (show) Refresh();
        }

        private void OnCardClicked(CardView cv)
        {
            if (cv.def == null) return;
            builder.BeginPlace(cv.def, cv.fromInventory);
        }

        // ---------------------------------------------------------------- vẽ danh sách

        private CardView EnsureCard(int index)
        {
            while (cards.Count <= index) cards.Add(CreateCard(cards.Count));
            return cards[index];
        }

        private CardView CreateCard(int index)
        {
            var cv = new CardView();
            ClayButton btn = HudBuilder.AddClayButton(content, "Card" + index, HudBuilder.T.card, null, HudBuilder.Cream, () => OnCardClicked(cards[index]));
            RectTransform rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -index * (CardH + CardGap));
            rt.sizeDelta = new Vector2(-8f, CardH);
            cv.button = btn;

            cv.icon = HudBuilder.Icon(btn.Content, "Icon", null, 104f);
            HudBuilder.SetFixed(cv.icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(76f, 0f), new Vector2(104f, 104f));

            cv.name = HudBuilder.AddText(btn.Content, "Name", "", 38, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(cv.name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(146f, -4f), new Vector2(480f, 50f));
            HudBuilder.Fit(cv.name, 24);

            cv.coin = HudBuilder.Icon(btn.Content, "Coin", HudBuilder.T.iconCoin, 44f);
            HudBuilder.SetFixed(cv.coin.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(168f, -78f), new Vector2(44f, 44f));
            cv.price = HudBuilder.AddText(btn.Content, "Price", "", 36, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(cv.price.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(196f, -56f), new Vector2(240f, 46f));

            cv.attractChip = HudBuilder.Chip(btn.Content, "AttractChip", false);
            cv.attractChip.color = HudBuilder.Mint;
            HudBuilder.SetFixed(cv.attractChip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(146f, -104f), new Vector2(170f, 34f));
            cv.attractText = HudBuilder.AddText(cv.attractChip.transform, "T", "", 22, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.Fit(cv.attractText, 16);

            cv.comfortChip = HudBuilder.Chip(btn.Content, "ComfortChip", false);
            cv.comfortChip.color = HudBuilder.CreamDark;
            HudBuilder.SetFixed(cv.comfortChip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(324f, -104f), new Vector2(200f, 34f));
            cv.comfortText = HudBuilder.AddText(cv.comfortChip.transform, "T", "", 22, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.Fit(cv.comfortText, 16);

            cv.reason = HudBuilder.AddText(btn.Content, "Reason", "", 26, TextAnchor.MiddleLeft, HudBuilder.Bad, true, false);
            HudBuilder.SetFixed(cv.reason.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(146f, -100f), new Vector2(480f, 40f));
            HudBuilder.Fit(cv.reason, 18);
            return cv;
        }

        /// Danh sách thẻ của tab hiện tại: đồ trong kho (đặt miễn phí) trước, rồi hàng bán đã lọc theo cấp quán (minTier / onlyTier).
        private List<FurnitureDef> BuildShownList()
        {
            shownDefs.Clear();
            shownFromInv.Clear();
            int tier = game.Tier != null ? game.Tier.Current : 1;

            if (game.Tier != null && game.catalog != null)
            {
                for (int i = 0; i < game.catalog.Length; i++)
                {
                    FurnitureDef d = game.catalog[i];
                    if (d == null || d.prefab == null || d.kind == FurnitureKind.BrewStation) continue;
                    if ((d.kind == FurnitureKind.Decor) != decorTab) continue;
                    if (game.Tier.InventoryCount(d.id) <= 0) continue;
                    shownDefs.Add(d);
                    shownFromInv.Add(true);
                }
            }

            List<FurnitureDef> shop = decorTab ? decorList : seatList;
            for (int i = 0; i < shop.Count; i++)
            {
                if (!ShopRules.AvailableInTier(shop[i], tier)) continue;
                shownDefs.Add(shop[i]);
                shownFromInv.Add(false);
            }
            return shownDefs;
        }

        private void Refresh()
        {
            List<FurnitureDef> list = BuildShownList();
            GridSystem grid = game.Grid;
            int xu = game.Wallet.Xu;

            for (int i = 0; i < list.Count; i++)
            {
                CardView cv = EnsureCard(i);
                FurnitureDef d = list[i];
                bool inv = shownFromInv[i];
                cv.def = d;
                cv.fromInventory = inv;
                cv.button.gameObject.SetActive(true);

                HudBuilder.SetIcon(cv.icon, d.icon != null ? d.icon : HudBuilder.T.iconCup);
                string dn = string.IsNullOrEmpty(d.displayName) ? d.id : d.displayName;
                cv.name.text = inv ? string.Format(Strings.InventoryNameFmt, dn, ShopRules.InventoryCount(d)) : dn;

                // Đồ trong kho: đặt miễn phí, không tính giới hạn mua / giá
                int price = inv ? 0 : ShopRules.Price(grid, d);
                bool maxed = !inv && ShopRules.AtMax(grid, d);
                bool poor = !inv && xu < price;
                cv.price.text = inv ? Strings.InventoryFree : Strings.FormatInt(price);
                cv.price.color = poor ? HudBuilder.Bad : (inv ? HudBuilder.GoodDark : HudBuilder.Ink);
                cv.coin.enabled = !inv && HudBuilder.T.iconCoin != null;

                bool blocked = maxed || poor;
                cv.reason.gameObject.SetActive(blocked);
                if (maxed) cv.reason.text = string.Format(Strings.MaxReachedFmt, d.maxCount);
                else if (poor) cv.reason.text = string.Format(Strings.NeedMoreXuFmt, Strings.FormatInt(price - xu));

                bool isMachine = !string.IsNullOrEmpty(d.machineId);
                bool hasAttract = d.attract > 0 || isMachine;
                bool hasComfort = d.comfort > 0 || d.style > 0;
                cv.attractChip.gameObject.SetActive(!blocked && hasAttract);
                if (hasAttract) cv.attractText.text = isMachine ? MachineRules.Effect(d.machineId) : string.Format(Strings.AttractBadgeFmt, d.attract);
                cv.comfortChip.gameObject.SetActive(!blocked && hasComfort);
                if (hasComfort)
                {
                    cv.comfortText.text = d.comfort > 0
                        ? string.Format(Strings.ComfortBadgeFmt, d.comfort)
                        : string.Format(Strings.StyleBadgeFmt, d.style);
                    // Chỉ có attract: dời chip comfort lên vị trí đầu
                    cv.comfortChip.rectTransform.anchoredPosition = new Vector2(hasAttract ? 324f : 146f, -104f);
                }

                cv.button.SetInteractable(!blocked);
            }

            for (int i = list.Count; i < cards.Count; i++) cards[i].button.gameObject.SetActive(false);
            content.sizeDelta = new Vector2(0f, Mathf.Max(0f, list.Count * (CardH + CardGap)));
        }
    }
}
