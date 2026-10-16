using UnityEngine;

namespace Cafe
{
    /// Luật thuần cho tín hiệu "ly chờ thu": mức khẩn cấp theo thời gian tới lúc ghế bị chặn. Không phụ thuộc Unity scene nên test EditMode được.
    public static class CupCollectRules
    {
        /// Từ mức này (0..1 của thời gian tới lúc chặn ghế) bong bóng chuyển sang cảnh báo.
        public const float WarnFraction = 0.6f;

        /// 0 = vừa uống xong, 1 = ghế sắp/đã bị chặn.
        public static float Urgency(float inGameNow, float collectableSince, float blockInGameSeconds)
        {
            if (blockInGameSeconds <= 0f) return 1f;
            return Mathf.Clamp01((inGameNow - collectableSince) / blockInGameSeconds);
        }

        public static bool IsWarning(float urgency) { return urgency >= WarnFraction; }

        /// Tốc độ nhịp đập của ly / bong bóng: nhanh dần khi khẩn cấp.
        public static float PulseSpeed(float urgency) { return 5f + 7f * Mathf.Clamp01(urgency); }
    }
}
