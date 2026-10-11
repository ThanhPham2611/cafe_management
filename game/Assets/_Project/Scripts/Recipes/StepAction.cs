namespace Cafe
{
    /// Kiểu thao tác của một bước pha (phase2_spec.md §3): chạm, giữ (rót), vuốt (đổ), xoay vòng (đánh/lắc).
    public enum StepAction { Tap, Hold, Swipe, Spin }

    /// Bảng ánh xạ BrewStep -> thao tác + thông số. Autopilot/Station luôn gọi thẳng TapStep, không đi qua đây.
    public static class StepActions
    {
        /// Vuốt lên tối thiểu (px canvas tham chiếu 1920x1080) để tính là một lần đổ.
        public const float SwipeDistancePx = 120f;
        /// Số vòng quay tối thiểu cho thao tác xoay (1 vòng).
        public const float SpinTurns = 1f;
        /// Tổng góc rẽ cần đo được: đường kéo tay thực tế luôn hụt ~30 độ so với 360 do đầu/cuối vòng chưa khép.
        public const float SpinRequiredDegrees = 360f * SpinTurns - 30f;
        /// Bấm giữ ngắn hơn mức này coi là chạm nhầm/cuộn (không bị tính sai bước).
        public const float AccidentalHoldSeconds = 0.12f;

        public static StepAction For(BrewStep s)
        {
            switch (s)
            {
                case BrewStep.Phin:
                case BrewStep.SuaDac:
                case BrewStep.SuaTuoi:
                case BrewStep.Tra:
                case BrewStep.CotDua:
                    return StepAction.Hold;
                case BrewStep.KemMuoi:
                case BrewStep.Cacao:
                    return StepAction.Swipe;
                case BrewStep.Lac:
                case BrewStep.Danh:
                    return StepAction.Spin;
                default: // Da, Duong, Muoi, Trung, Chanh
                    return StepAction.Tap;
            }
        }

        /// Thời gian giữ (giây) cho bước kiểu Hold; 0 với kiểu khác.
        public static float HoldSeconds(BrewStep s)
        {
            switch (s)
            {
                case BrewStep.Phin: return 0.8f;
                case BrewStep.SuaDac:
                case BrewStep.SuaTuoi:
                case BrewStep.Tra:
                case BrewStep.CotDua:
                    return 0.6f;
                default:
                    return 0f;
            }
        }
    }
}
