using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Sổ khách quen: tiến độ thân thiết và số chương đã hoàn thành.
    public sealed class RegularsBookPanel
    {
        private readonly GameBootstrap game;
        private readonly ClayButton button;
        private readonly GameObject root;
        private const float RowH = 108f;
        private const float RowStep = 116f;
        private readonly Text[] rows;

        public RegularsBookPanel(GameBootstrap game, Transform buttonParent, Transform overlayParent)
        {
            this.game = game;
            int count = game.Regulars != null ? Mathf.Max(3, game.Regulars.Definitions.Count) : 3;
            rows = new Text[count];
            Text label;
            button = HudBuilder.AddLabelButton(buttonParent, "RegularsButton", ClayStyle.Coffee,
                "KHÁCH QUEN", 25, () => SetOpen(true), out label);
            HudBuilder.SetFixed((RectTransform)button.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -366f), new Vector2(230f, 86f)); // cột phải, tránh khay nguyên liệu ở đáy

            Image dim = HudBuilder.Dim(overlayParent, "RegularsBook", 0.62f);
            HudBuilder.Stretch(dim.rectTransform);
            root = dim.gameObject;
            ModalMarker.Mark(root);
            Image card = HudBuilder.Panel(dim.transform, "Card", true);
            // đầu 125 + số hàng x 116 + chân (nút ĐÓNG) 130; nhiều người thì thẻ cao hơn, FitInCanvas thu nhỏ vừa màn hình
            HudBuilder.SetFixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(920f, 255f + count * RowStep));
            card.gameObject.AddComponent<FitInCanvas>();
            Text title = HudBuilder.AddText(card.transform, "Title", "SỔ KHÁCH QUEN", 48,
                TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -45f), new Vector2(760f, 64f));

            for (int i = 0; i < rows.Length; i++)
            {
                Image row = HudBuilder.Chip(card.transform, "Regular" + i, false);
                HudBuilder.SetFixed(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -(125f + i * RowStep)), new Vector2(760f, RowH));
                rows[i] = HudBuilder.AddText(row.transform, "Text", "", 28, TextAnchor.MiddleLeft,
                    HudBuilder.Ink, false, false);
                rows[i].rectTransform.offsetMin = new Vector2(28f, 6f);
                rows[i].rectTransform.offsetMax = new Vector2(-28f, -6f);
                HudBuilder.Fit(rows[i], 18);
            }

            Text closeText;
            ClayButton close = HudBuilder.AddLabelButton(card.transform, "Close", ClayStyle.Red, "ĐÓNG", 32,
                () => SetOpen(false), out closeText);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 32f), new Vector2(300f, 92f));

            game.Regulars.Changed += Refresh;
            SetOpen(false);
            Refresh();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugOpen(bool value) { SetOpen(value); }
#endif

        private void SetOpen(bool open)
        {
            root.SetActive(open);
            if (open) Refresh();
        }

        private void Refresh()
        {
            bool enabled = game.Regulars != null && game.Regulars.Enabled;
            button.gameObject.SetActive(enabled);
            for (int i = 0; i < rows.Length; i++)
            {
                if (!enabled || i >= game.Regulars.Definitions.Count) { rows[i].text = ""; continue; }
                RegularDefinition def = game.Regulars.Definitions[i];
                RegularProgress p = game.Regulars.GetProgress(def.id);
                rows[i].text = def.displayName.ToUpperInvariant() + "  •  " + def.role + "\n" + DetailLine(def, p);
            }
        }

        /// Dòng 2 của hàng: chưa tới cấp quán thì ghi khi nào xuất hiện; thiếu công thức món ruột thì ghi cần gì; còn lại là tiến độ + món ruột.
        private string DetailLine(RegularDefinition def, RegularProgress p)
        {
            int tier = game.Tier != null ? game.Tier.Current : 1;
            if (def.unlockTier > tier) return string.Format(Strings.RegularLockedTierFmt, Strings.TierShortName(def.unlockTier));
            RecipeDef fav = game.Recipes != null ? game.Recipes.Get(def.favoriteRecipe) : null;
            string favName = fav != null && !string.IsNullOrEmpty(fav.displayName) ? fav.displayName : def.favoriteRecipe;
            if (fav != null && !game.Recipes.IsKnown(fav)) return string.Format(Strings.RegularNeedRecipeFmt, favName);
            return "Thân thiết " + p.Bond + "/10  •  Chuyện " + p.ChaptersDone + "/" + RegularsManager.ChapterCount(def)
                + "  •  " + string.Format(Strings.RegularFavoriteFmt, favName);
        }

        public void Dispose()
        {
            if (game != null && game.Regulars != null) game.Regulars.Changed -= Refresh;
        }
    }
}
