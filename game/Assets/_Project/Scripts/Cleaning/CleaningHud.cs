using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// HUD dọn dẹp: chip "cây chổi + CI%" (đỏ khi CI < 60) ở hàng chip trên cùng, và nút "Lau sàn"
    /// (hiện khi FloorDirt > 15) phía trên chip ly. Chỉ hiện khi CleaningRules.Enabled.
    /// HudController.Init gọi CleaningHud.Create(game, safeArea) một lần.
    public class CleaningHud : MonoBehaviour
    {
        private const float Margin = 24f;
        private const float ChipH = 96f;
        /// Chip thứ ba của hàng trên cùng: sau chip Xu (310) và chip Sao (440), cộng khoảng cách + nửa icon.
        private const float ChipX = Margin + 46f + 310f + 16f + 46f + 440f + 16f + 46f;
        private const float ChipW = 250f;

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

            // ---- chip độ sạch
            chip = HudBuilder.Chip(root, "CleanChip", false);
            HudBuilder.SetFixed(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(ChipX, -Margin), new Vector2(ChipW, ChipH));
            Image icon = HudBuilder.Icon(chip.transform, "Icon", CleaningRules.BroomIcon(HudBuilder.T), 112f);
            HudBuilder.SetFixed(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8f, 9f), new Vector2(112f, 112f));
            pctText = HudBuilder.AddText(chip.transform, "Pct", "", 44, TextAnchor.MiddleRight, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(pctText.rectTransform, Vector2.zero, Vector2.one, new Vector2(66f, 22f), new Vector2(-28f, -4f));
            HudBuilder.Fit(pctText, 26);

            // ---- nút lau sàn (trên chip ly, góc phải dưới)
            Text label;
            mopButton = HudBuilder.AddLabelButton(root, "MopBtn", ClayStyle.Cream, Strings.MopBtn, 32, OnMopClicked, out label);
            HudBuilder.SetFixed((RectTransform)mopButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Margin, Margin + ChipH + 16f), new Vector2(250f, 104f));
            label.rectTransform.offsetMin = new Vector2(84f, 0f);
            Image mopIcon = HudBuilder.Icon(mopButton.Content, "Icon", CleaningRules.BroomIcon(HudBuilder.T), 70f);
            HudBuilder.SetFixed(mopIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52f, 0f), new Vector2(70f, 70f));

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
            if (chip.gameObject.activeSelf != en) chip.gameObject.SetActive(en);

            int ci = Mathf.RoundToInt(model.CI);
            pctText.text = string.Format(Strings.CleanChipFmt, ci);
            pctText.color = model.CI < GameBalance.CiWalkAwayBelow ? HudBuilder.Red : HudBuilder.Ink;

            bool mop = en && model.WantsMop;
            if (mopButton.gameObject.activeSelf != mop) mopButton.gameObject.SetActive(mop);
        }
    }
}
