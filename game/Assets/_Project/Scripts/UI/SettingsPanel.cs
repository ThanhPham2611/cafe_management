using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Cài đặt: nút "ĐỒ HỌA" ở cột phải HUD và bảng chọn 3 mức Thấp / Vừa / Cao (GraphicsQuality), giảm chuyển động,
    /// nhạc nền / âm thanh / rung (AudioPrefs). Đổi có hiệu lực ngay và được nhớ cho lần chơi sau.
    public sealed class SettingsPanel
    {
        private const float CardW = 1000f;
        private const float CardH = 700f;
        private const float OptionW = 290f;
        private const float OptionH = 340f;
        private const float OptionGap = 24f;

        private sealed class OptionView
        {
            public GraphicsLevel level;
            public ClayButton button;
            public Text badge;
        }

        private const float RowH = 100f;        // chiều cao thêm vào thẻ cho mỗi hàng chọn FPS
        private const float RowTop = -130f - OptionH - 30f;   // hàng đầu nằm ngay dưới 3 mức đồ họa
        private const float FpsButtonW = 170f;
        private const float MotionButtonW = 220f;
        private const float VolButtonW = 130f;
        private const float VolGap = 12f;
        private const float FpsLabelW = 380f;
        private const float RowLeft = -(FpsLabelW + 3 * (FpsButtonW + OptionGap)) * 0.5f;   // hai hàng thẳng cột nhau

        private readonly GameObject root;
        private readonly OptionView[] options = new OptionView[GraphicsPresets.Count];
        private readonly Text recommend;
        private ClayButton motionButton;
        private Text motionText;
        private ClayButton[] fpsButtons = new ClayButton[0];
        private readonly ClayButton[] musicButtons = new ClayButton[AudioRules.MaxStep + 1];
        private readonly ClayButton[] sfxButtons = new ClayButton[AudioRules.MaxStep + 1];
        private ClayButton hapticsButton;
        private Text hapticsText;
        private int[] fpsChoices = new int[0];
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
            bool fpsRow = GraphicsQuality.FpsSelectable;
            bool hapticsRow = Haptics.Supported;
            int rows = (fpsRow ? 2 : 1) + 2 + (hapticsRow ? 1 : 0); // [FPS] + giảm chuyển động + nhạc + âm thanh + [rung]
            HudBuilder.SetFixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(CardW, CardH + RowH * rows));
            card.gameObject.AddComponent<FitInCanvas>(); // máy có thêm hàng chọn FPS: thẻ cao hơn màn hình thì tự thu nhỏ

            Text title = HudBuilder.AddText(card.transform, "Title", Strings.SettingsTitle, 46, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 60f));

            float total = GraphicsPresets.Count * OptionW + (GraphicsPresets.Count - 1) * OptionGap;
            for (int i = 0; i < options.Length; i++)
            {
                float x = -total * 0.5f + OptionW * 0.5f + i * (OptionW + OptionGap);
                options[i] = BuildOption(card.transform, GraphicsPresets.All[i], x);
            }

            if (fpsRow) BuildFpsRow(card.transform);
            float rowTop = RowTop - (fpsRow ? RowH : 0f);
            BuildMotionRow(card.transform, rowTop);
            BuildVolumeRow(card.transform, "Music", Strings.MusicLabel, rowTop - RowH, musicButtons, step => AudioPrefs.SetMusic(step));
            BuildVolumeRow(card.transform, "Sfx", Strings.SfxLabel, rowTop - 2 * RowH, sfxButtons, step => { AudioPrefs.SetSfx(step); Audio.Play(Sfx.UiTap, 1f, false); });
            if (hapticsRow) BuildHapticsRow(card.transform, rowTop - 3 * RowH);

            recommend = HudBuilder.AddText(card.transform, "Recommend", "", 28, TextAnchor.MiddleCenter, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(recommend.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 44f));
            HudBuilder.Fit(recommend, 20);

            Text closeText;
            ClayButton close = HudBuilder.AddLabelButton(card.transform, "Close", ClayStyle.Red, "Đóng", 32, () => SetOpen(false), out closeText);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 88f));

            // Xem lại hướng dẫn: đóng bảng rồi để TutorialDirector nhắc lại thao tác cơ bản + mở lại các gợi ý tình huống
            Text tutText;
            ClayButton tutorial = HudBuilder.AddLabelButton(card.transform, "Tutorial", ClayStyle.Gold, TutorialStrings.SettingsBtn, 32, () =>
            {
                SetOpen(false);
                if (game.Tutorial != null) game.Tutorial.Replay();
            }, out tutText);
            HudBuilder.SetFixed((RectTransform)tutorial.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 28f), new Vector2(300f, 88f));

            GraphicsQuality.Changed += Refresh;
            AudioPrefs.Changed += Refresh;
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

        /// Hàng "Khung hình/giây: 60 | 90 | 120" ngay dưới 3 mức đồ họa; chỉ dựng khi màn hình quét nhanh hơn 60 Hz.
        private void BuildFpsRow(Transform card)
        {
            fpsChoices = GraphicsQuality.FpsChoices;
            fpsButtons = new ClayButton[fpsChoices.Length];
            Text label = HudBuilder.AddText(card, "FpsLabel", "Khung hình/giây (mức Thấp luôn 30)", 30, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(RowLeft, RowTop), new Vector2(FpsLabelW, 84f));
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.Fit(label, 20);

            for (int i = 0; i < fpsChoices.Length; i++)
            {
                int fps = fpsChoices[i];
                Text text;
                fpsButtons[i] = HudBuilder.AddLabelButton(card, "Fps_" + fps, ClayStyle.Green, fps.ToString(), 40, () => GraphicsQuality.SetFps(fps), out text);
                HudBuilder.SetFixed((RectTransform)fpsButtons[i].transform, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                    new Vector2(RowLeft + FpsLabelW + OptionGap + i * (FpsButtonW + OptionGap), RowTop), new Vector2(FpsButtonW, 84f));
            }
        }

        /// Hàng "Giảm chuyển động: BẬT / TẮT" (camera bớt xoay gắt, chống chóng mặt). Luôn có, kể cả trên máy tính.
        private void BuildMotionRow(Transform card, float top)
        {
            Text label = HudBuilder.AddText(card, "MotionLabel", "Giảm chuyển động (đỡ chóng mặt)", 30, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(RowLeft, top), new Vector2(FpsLabelW, 84f));
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.Fit(label, 20);

            motionButton = HudBuilder.AddLabelButton(card, "MotionToggle", ClayStyle.Green, "TẮT", 40,
                () => GraphicsQuality.SetReduceMotion(!GraphicsQuality.ReduceMotion), out motionText);
            HudBuilder.SetFixed((RectTransform)motionButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                new Vector2(RowLeft + FpsLabelW + OptionGap, top), new Vector2(MotionButtonW, 84f));
        }

        /// Hàng "Nhạc nền / Âm thanh: Tắt | Nhỏ | Vừa | To" (4 bậc, AudioPrefs).
        private void BuildVolumeRow(Transform card, string key, string labelText, float top, ClayButton[] buttons, System.Action<int> onPick)
        {
            Text label = HudBuilder.AddText(card, key + "Label", labelText, 30, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(RowLeft, top), new Vector2(FpsLabelW, 84f));
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.Fit(label, 20);

            string[] names = { Strings.VolOff, Strings.VolLow, Strings.VolMed, Strings.VolHigh };
            for (int i = 0; i < buttons.Length; i++)
            {
                int step = i;
                Text text;
                buttons[i] = HudBuilder.AddLabelButton(card, key + "_" + i, ClayStyle.Green, names[i], 32, () => onPick(step), out text);
                HudBuilder.SetFixed((RectTransform)buttons[i].transform, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                    new Vector2(RowLeft + FpsLabelW + OptionGap + i * (VolButtonW + VolGap), top), new Vector2(VolButtonW, 84f));
            }
        }

        /// Hàng "Rung: BẬT / TẮT" (chỉ điện thoại).
        private void BuildHapticsRow(Transform card, float top)
        {
            Text label = HudBuilder.AddText(card, "HapticsLabel", Strings.HapticsLabel, 30, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(RowLeft, top), new Vector2(FpsLabelW, 84f));
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.Fit(label, 20);
            hapticsButton = HudBuilder.AddLabelButton(card, "HapticsToggle", ClayStyle.Green, Strings.VolOff, 40,
                () => AudioPrefs.SetHaptics(!AudioPrefs.Haptics), out hapticsText);
            HudBuilder.SetFixed((RectTransform)hapticsButton.transform, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                new Vector2(RowLeft + FpsLabelW + OptionGap, top), new Vector2(MotionButtonW, 84f));
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
            // Mức Thấp cố định 30 FPS nên không chọn nút nào; mức khác sáng đúng nút đang dùng
            for (int i = 0; i < fpsButtons.Length; i++)
            {
                bool on = current != GraphicsLevel.Low && fpsChoices[i] == GraphicsQuality.TargetFps;
                fpsButtons[i].SetTint(on ? Color.white : new Color(0.62f, 0.6f, 0.55f, 1f));
            }
            bool calm = GraphicsQuality.ReduceMotion;
            motionText.text = calm ? "BẬT" : "TẮT";
            motionButton.SetTint(calm ? Color.white : new Color(0.62f, 0.6f, 0.55f, 1f));
            for (int i = 0; i < musicButtons.Length; i++)
            {
                musicButtons[i].SetTint(i == AudioPrefs.MusicStep ? Color.white : new Color(0.62f, 0.6f, 0.55f, 1f));
                sfxButtons[i].SetTint(i == AudioPrefs.SfxStep ? Color.white : new Color(0.62f, 0.6f, 0.55f, 1f));
            }
            if (hapticsButton != null)
            {
                hapticsText.text = AudioPrefs.Haptics ? Strings.On : Strings.Off;
                hapticsButton.SetTint(AudioPrefs.Haptics ? Color.white : new Color(0.62f, 0.6f, 0.55f, 1f));
            }
            recommend.text = "Đổi mức có hiệu lực ngay. Máy giật thì chọn THẤP; máy mạnh chọn CAO.";
        }

        public void Dispose()
        {
            GraphicsQuality.Changed -= Refresh;
            AudioPrefs.Changed -= Refresh;
        }
    }
}
