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
        private readonly Text[] rows = new Text[3];

        public RegularsBookPanel(GameBootstrap game, Transform buttonParent, Transform overlayParent)
        {
            this.game = game;
            Text label;
            button = HudBuilder.AddLabelButton(buttonParent, "RegularsButton", ClayStyle.Coffee,
                "KHÁCH QUEN", 25, () => SetOpen(true), out label);
            HudBuilder.SetFixed((RectTransform)button.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -366f), new Vector2(230f, 86f)); // cột phải, tránh khay nguyên liệu ở đáy

            Image dim = HudBuilder.Dim(overlayParent, "RegularsBook", 0.62f);
            HudBuilder.Stretch(dim.rectTransform);
            root = dim.gameObject;
            Image card = HudBuilder.Panel(dim.transform, "Card", true);
            HudBuilder.SetFixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(920f, 700f));
            Text title = HudBuilder.AddText(card.transform, "Title", "SỔ KHÁCH QUEN", 48,
                TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -45f), new Vector2(760f, 64f));

            for (int i = 0; i < rows.Length; i++)
            {
                Image row = HudBuilder.Chip(card.transform, "Regular" + i, false);
                HudBuilder.SetFixed(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -(145f + i * 145f)), new Vector2(760f, 120f));
                rows[i] = HudBuilder.AddText(row.transform, "Text", "", 30, TextAnchor.MiddleLeft,
                    HudBuilder.Ink, false, false);
                rows[i].rectTransform.offsetMin = new Vector2(32f, 10f);
                rows[i].rectTransform.offsetMax = new Vector2(-32f, -10f);
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
                rows[i].text = def.displayName.ToUpperInvariant() + "  •  " + def.role + "\n" +
                    "Thân thiết: " + p.Bond + "/10    Chuyện: " + p.ChaptersDone + "/" + RegularsManager.ChapterCount(def);
            }
        }

        public void Dispose()
        {
            if (game != null && game.Regulars != null) game.Regulars.Changed -= Refresh;
        }
    }
}
