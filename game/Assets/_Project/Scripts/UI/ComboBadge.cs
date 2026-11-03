using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Huy hiệu combo pha chuẩn (docs/combo_rush_spec.md §1.3): "x7" + tên bậc, cưỡi trên mép phải bàn pha (đối xứng nút Sổ công thức
    /// ở mép trái). Ẩn khi combo < ComboRules.ShowAt. Mỗi lần tăng thì nảy, lên bậc đổi màu + chữ nổi + âm; đứt combo hiện chữ xám nhẹ.
    /// Tip combo bay về chip Xu từ huy hiệu. "Giảm chuyển động": không nảy. Không bắt chạm.
    public class ComboBadge
    {
        private const float W = 170f;
        private const float H = 74f;
        private const float PulseSeconds = 0.25f;

        private static readonly Color Orange = new Color32(0xF0, 0x84, 0x2C, 0xFF);
        private static readonly Color BrokenGrey = new Color(0.55f, 0.5f, 0.46f, 1f);

        private readonly GameBootstrap game;
        private readonly HudController hud;
        private readonly BrewPanel brew;
        private readonly RectTransform root;
        private readonly Image bg;
        private readonly Text count;
        private readonly Text label;
        private float pulseT = -1f;
        private float pulseAmp;
        private bool capShown;

        public ComboBadge(GameBootstrap game, HudController hud, Transform parent, BrewPanel brew)
        {
            this.game = game;
            this.hud = hud;
            this.brew = brew;

            bg = HudBuilder.Chip(parent, "ComboBadge", false);
            root = bg.rectTransform;
            HudBuilder.SetFixed(root, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(brew.Width * 0.5f, 20f), new Vector2(W, H));
            count = HudBuilder.AddText(root, "Count", "", 40, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(count.rectTransform, Vector2.zero, new Vector2(0.48f, 1f), new Vector2(14f, 0f), Vector2.zero);
            label = HudBuilder.AddText(root, "Tier", "", 20, TextAnchor.MiddleLeft, HudBuilder.InkSoft, true, false);
            HudBuilder.SetRect(label.rectTransform, new Vector2(0.48f, 0f), Vector2.one, new Vector2(2f, 0f), new Vector2(-10f, 0f));
            HudBuilder.Fit(label, 14);
            root.gameObject.SetActive(false);

            GameEvents.ComboChanged += OnChanged;
            GameEvents.ComboBroken += OnBroken;
            GameEvents.DayStarted += OnDayStarted;
        }

        public void Dispose()
        {
            GameEvents.ComboChanged -= OnChanged;
            GameEvents.ComboBroken -= OnBroken;
            GameEvents.DayStarted -= OnDayStarted;
        }

        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 1: return Strings.ComboTier1;
                case 2: return Strings.ComboTier2;
                case 3: return Strings.ComboTier3;
                default: return "Combo";
            }
        }

        private static Color TierColor(int tier)
        {
            switch (tier)
            {
                case 1: return HudBuilder.Accent;
                case 2: return Orange;
                case 3: return HudBuilder.Red;
                default: return new Color(HudBuilder.Cream.r, HudBuilder.Cream.g, HudBuilder.Cream.b, 0.95f);
            }
        }

        private void OnDayStarted(int day)
        {
            capShown = false;
            root.gameObject.SetActive(false);
        }

        private void OnChanged(int value, int tier, int gain, int bonus, bool tierUp)
        {
            if (value < ComboRules.ShowAt) return;
            bool wasHidden = !root.gameObject.activeSelf;
            root.gameObject.SetActive(true);
            count.text = "x" + value;
            label.text = TierName(tier);
            bg.color = TierColor(tier);
            Color ink = tier >= 3 ? HudBuilder.Cream : HudBuilder.Ink;
            count.color = ink;
            label.color = tier >= 3 ? HudBuilder.Cream : HudBuilder.InkSoft;
            Pulse(tierUp ? 0.4f : 0.22f);
            if (wasHidden) Place();

            Vector2 at = ScreenPos(H + 10f);
            string text = null;
            if (tierUp) text = TierName(tier);
            else if (gain >= 2) text = string.Format(Strings.ComboPerfectFmt, value);
            if (bonus > 0)
            {
                string xu = string.Format(Strings.ComboBonusFmt, bonus);
                text = text == null ? xu : text + " " + xu;
                hud.FlyXu(ScreenPos(H * 0.5f), bonus);
            }
            else if (tier >= 1 && !capShown && game.Combo != null && game.Combo.BonusPaid > 0)
            {
                capShown = true; // bậc có thưởng mà không được trả = chạm trần ngày
                text = Strings.ComboCapped;
            }
            if (text != null) UiFx.FloatTextScreen(at, text, tierUp ? HudBuilder.Accent : HudBuilder.Cream);
            Audio.Play(tierUp ? Sfx.ComboTier : Sfx.ComboUp, 1f, tierUp);
        }

        private void OnBroken(int prev)
        {
            if (!root.gameObject.activeSelf) return;
            root.gameObject.SetActive(false);
            if (prev < ComboRules.Tier1At) return; // x2 đứt: lặng lẽ, khỏi làm ồn
            UiFx.FloatTextScreen(ScreenPos(H * 0.5f), string.Format(Strings.ComboBrokenFmt, prev), BrokenGrey);
            Audio.Play(Sfx.ComboBreak, 1f, false);
        }

        private void Pulse(float amp)
        {
            if (GraphicsQuality.ReduceMotion) return;
            pulseAmp = amp;
            pulseT = 0f;
        }

        /// Toạ độ màn hình (pixel) của điểm giữa huy hiệu, nâng lên dy đơn vị canvas.
        private Vector2 ScreenPos(float dy)
        {
            Place();
            Vector3 p = root.TransformPoint(new Vector3(-W * 0.5f, dy, 0f));
            return RectTransformUtility.WorldToScreenPoint(null, p);
        }

        private void Place()
        {
            float y = Mathf.Max(brew.TopY, 12f) + 8f;
            Vector2 pos = new Vector2(brew.Width * 0.5f, y);
            if (root.anchoredPosition != pos) root.anchoredPosition = pos;
        }

        public void Tick(float dt)
        {
            if (!root.gameObject.activeSelf) return;
            Place();
            if (pulseT < 0f) return;
            pulseT += dt;
            float k = Mathf.Clamp01(pulseT / PulseSeconds);
            float s = 1f + pulseAmp * (1f - k) * (1f - k);
            root.localScale = new Vector3(s, s, 1f);
            if (k >= 1f)
            {
                pulseT = -1f;
                root.localScale = Vector3.one;
            }
        }
    }
}
