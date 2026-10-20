using UnityEngine;

namespace Cafe
{
    /// Luật làm tròn cho HUD: uGUI dựng lại cả canvas mỗi khi một Image đổi fillAmount / một RectTransform đổi vị trí,
    /// nên làm tròn để những thay đổi mắt không thấy được không làm canvas bẩn mỗi khung. Thuần logic để kiểm thử được.
    public static class HudRules
    {
        /// Số bậc của vòng tròn tiến độ (vòng kiên nhẫn đi hết trong 30-60 s: 50 bậc = 1-2 lần đổi mỗi giây, mỗi bậc 7° nên không thấy khấc).
        public const int RingSteps = 50;
        /// Bước làm tròn vị trí (đơn vị canvas, px).
        public const float PositionStep = 0.5f;

        public static float QuantizeRing(float v)
        {
            return Mathf.Round(Mathf.Clamp01(v) * RingSteps) / RingSteps;
        }

        public static Vector2 SnapPosition(Vector2 p)
        {
            return new Vector2(Mathf.Round(p.x / PositionStep) * PositionStep, Mathf.Round(p.y / PositionStep) * PositionStep);
        }

        /// Đặt vị trí neo đã làm tròn, chỉ ghi khi khác vị trí hiện tại.
        public static void Place(RectTransform rt, Vector2 p)
        {
            p = SnapPosition(p);
            if (rt.anchoredPosition != p) rt.anchoredPosition = p;
        }
    }
}
