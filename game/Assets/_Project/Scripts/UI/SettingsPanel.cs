using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Cài đặt đồ họa: nút "ĐỒ HỌA" ở cột phải HUD và bảng chọn 3 mức Thấp / Vừa / Cao (GraphicsQuality).
    /// Đổi mức có hiệu lực ngay (bóng đổ, độ nét, khung hình/giây, viền chữ) và được nhớ cho lần chơi sau.
    public sealed class SettingsPanel
    {
        private const float CardW = 1000f;
        private const float CardH = 700f;
        private const float OptionW = 290f;
        private const float OptionH = 360f;
        private const float OptionGap = 24f;

        private sealed class OptionView
        {
            public GraphicsLevel level;
            public ClayButton button;
            public Text badge;
        }

        private readonly GameObject root;
        private readonly OptionView[] options = new OptionView[GraphicsPresets.Count];
        private readonly Text recommend;
        private bool open;

        public SettingsPanel(GameBootstrap game, Transform buttonParent, Transform overlayParent)
        {
            Text buttonLabel;
            ClayButton button = HudBuilder.AddLabelButton(buttonParent, "GraphicsButton", ClayStyle.Coffee,
                "ĐỒ HỌA", 24, () => SetOpen(true), out buttonLabel);
            HudBuilder.SetFixed((RectTransform)button.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -654f), new Vector2(230f, 76f)); // cột phải, dưới nút Nhiệm vụ

            Image dim = HudBuilder.Dim(overlayParent, "GraphicsSettings", 0.62f);
            HudBuilder.Stretch(dim.rectTransform);
            root = dim.gameObject;
            Image card = HudBuilder.Panel(dim.transform, "Card", true);
            HudBuilder.SetFixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW, CardH));

            Text title = HudBuilder.AddText(card.transform, "Title", "Cài đặt đồ họa", 46, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 60f));

            float total = GraphicsPresets.Count * OptionW + (GraphicsPresets.Count - 1) * OptionGap;
            for (int i = 0; i < options.Length; i++)
            {
                float x = -total * 0.5f + OptionW * 0.5f + i * (OptionW + OptionGap);
                options[i] = BuildOption(card.transform, GraphicsPresets.All[i], x);
            }

            recommend = HudBuilder.AddText(card.transform, "Recommend", "", 28, TextAnchor.MiddleCenter, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(recommend.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 44f));
            HudBuilder.Fit(recommend, 20);

            Text closeText;
            ClayButton close = HudBuilder.AddLabelButton(card.transform, "Close", ClayStyle.Red, "Đóng", 32, () => SetOpen(false), out closeText);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 88f));

            GraphicsQuality.Changed += Refresh;
            SetOpen(false);
        }

        private OptionView BuildOption(Transform card, GraphicsLevel level, float x)
        {
            var v = new OptionView { level = level };
            v.button = HudBuilder.AddClayButton(card, "Option_" + level, ClayStyle.Green, () => GraphicsQuality.Set(level));
            HudBuilder.SetFixed((RectTransform)v.button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(x, -130f), new Vector2(OptionW, OptionH));
            RectTransform c = v.button.Content;

            Text name = HudBuilder.AddText(c, "Name", GraphicsPresets.Name(level), 56, TextAnchor.MiddleCenter, HudBuilder.Cream, true, true);
            HudBuilder.SetFixed(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(OptionW - 30f, 76f));

            Text desc = HudBuilder.AddText(c, "Desc", GraphicsPresets.Description(level), 26, TextAnchor.UpperCenter, HudBuilder.Cream, false, false);
            HudBuilder.SetFixed(desc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(OptionW - 44f, 170f));
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.Fit(desc, 18);

            v.badge = HudBuilder.AddText(c, "Badge", "", 30, TextAnchor.MiddleCenter, HudBuilder.Accent, true, true);
            HudBuilder.SetFixed(v.badge.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(OptionW - 30f, 44f));
            return v;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugOpen(bool value) { SetOpen(value); }
#endif

        private void SetOpen(bool value)
        {
            open = value;
            root.SetActive(open);
            if (open) Refresh();
        }

        private void Refresh()
        {
            if (!open) return;
            GraphicsLevel current = GraphicsQuality.Current;
            for (int i = 0; i < options.Length; i++)
            {
                OptionView v = options[i];
                bool selected = v.level == current;
                v.button.SetTint(selected ? Color.white : new Color(0.62f, 0.6f, 0.55f, 1f));
                v.badge.text = selected ? "ĐANG DÙNG" : (v.level == GraphicsQuality.Recommended ? "Gợi ý cho máy bạn" : "");
            }
            recommend.text = "Đổi mức có hiệu lực ngay. Máy giật thì chọn THẤP; máy mạnh chọn CAO.";
        }

        public void Dispose()
        {
            GraphicsQuality.Changed -= Refresh;
        }
    }
}
