using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// HUD dọn dẹp: chip "cây chổi + CI%" (đỏ khi CI < 60; ẩn khi CI >= 90, hud_brew_redesign A2) và nút "Lau sàn"
    /// (hiện khi FloorDirt > 15), xếp phía trên chip ly / nút "Thu hết ly" ở góc phải dưới. Chỉ hiện khi CleaningRules.Enabled.
    /// HudController.Init gọi CleaningHud.Create(game, safeArea) một lần.
    public class CleaningHud : MonoBehaviour
    {
        private const float Margin = 20f;
        /// Chip ly (70) + khe 10 + nút "Thu hết ly" (70) + khe 10: nút Lau sàn bắt đầu từ đây (canvas HUD 900).
        private const float StackBase = Margin + 70f + 10f + 70f + 10f;
        private const float ChipH = 65f;   // 120x52 (x1,25)
        private const float ChipW = 150f;
        private const float MopH = 80f;
        private const float MopW = 250f;
        /// Độ sạch từ mức này trở lên thì ẩn chip (không có gì phải làm).
        private const float HideChipAtCi = 90f;

        private GameBootstrap game;
        private CleanlinessModel model;
        private Image chip;
        private Text pctText;
        private ClayButton mopButton;

        public RectTransform ChipRect { get { return chip != null ? chip.rectTransform : null; } }

        public static CleaningHud Create(GameBootstrap g, RectTransform safe)
        {
            CleaningSystem cs = CleaningSystem.Ensure(g);
            if (cs == null) return null;
            RectTransform root = HudBuilder.NewRect("CleaningHud", safe);
            HudBuilder.Stretch(root);
            var hud = root.gameObject.AddComponent<CleaningHud>();
            hud.Build(g, cs, root);
            return hud;
        }

        private void Build(GameBootstrap g, CleaningSystem cs, RectTransform root)
        {
            game = g;
            model = cs.Model;

            // ---- chip độ sạch: góc phải dưới, trên nút lau sàn (hàng trên ở canvas rộng 1600 không đủ chỗ, chip đè lên nút MENU)
            chip = HudBuilder.Chip(root, "CleanChip", false);
            HudBuilder.SetFixed(chip.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, StackBase + MopH + 10f), new Vector2(ChipW, ChipH));
            Image icon = HudBuilder.Icon(chip.transform, "Icon", CleaningRules.BroomIcon(HudBuilder.T), 65f);
            HudBuilder.SetFixed(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8f, 4f), new Vector2(65f, 65f));
            pctText = HudBuilder.AddText(chip.transform, "Pct", "", 34, TextAnchor.MiddleRight, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(pctText.rectTransform, Vector2.zero, Vector2.one, new Vector2(44f, 12f), new Vector2(-16f, -3f));
            HudBuilder.Fit(pctText, 20);

            // ---- nút lau sàn (trên chip ly, góc phải dưới)
            Text label;
            mopButton = HudBuilder.AddLabelButton(root, "MopBtn", ClayStyle.Cream, Strings.MopBtn, 28, OnMopClicked, out label);
            HudBuilder.SetFixed((RectTransform)mopButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Margin, StackBase), new Vector2(MopW, MopH));
            label.rectTransform.offsetMin = new Vector2(72f, 0f);
            Image mopIcon = HudBuilder.Icon(mopButton.Content, "Icon", CleaningRules.BroomIcon(HudBuilder.T), 56f);
            HudBuilder.SetFixed(mopIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(42f, 0f), new Vector2(56f, 56f));

            model.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (model != null) model.Changed -= Refresh;
        }

        private void OnMopClicked()
        {
            if (game != null && game.Owner != null) game.Owner.QueueMopFloor();
        }

        private void Refresh()
        {
            if (model == null || chip == null) return;
            bool en = CleaningRules.Enabled;
            bool showChip = en && model.CI < HideChipAtCi; // quán sạch: ẩn chip cho HUD gọn
            if (chip.gameObject.activeSelf != showChip) chip.gameObject.SetActive(showChip);

            int ci = Mathf.RoundToInt(model.CI);
            pctText.text = string.Format(Strings.CleanChipFmt, ci);
            pctText.color = model.CI < GameBalance.CiWalkAwayBelow ? HudBuilder.Red : HudBuilder.Ink;

            bool mop = en && model.WantsMop;
            if (mopButton.gameObject.activeSelf != mop) mopButton.gameObject.SetActive(mop);
        }
    }
}
