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
        /// Nhả khi đã đạt tỉ lệ này của thời gian giữ thì vẫn tính thành công.
        public const float HoldReleaseForgiveness = 0.8f;
        /// Ngón trượt ra khỏi nút ngắn hơn mức này (giây) thì chỉ tạm dừng, không hủy.
        public const float HoldSlipGraceSeconds = 0.15f;

        // ---- Rót kiểu mới (docs/hud_brew_redesign_2026-10-18.md B1): chạm để rót, ly tự đầy sau PourSeconds;
        // chạm lại trong vạch vàng = "Chuẩn!", không chạm = "Đủ", chạm sớm = "Hơi ít" (vẫn xong bước, ly tối đa 2 sao).
        /// Mép trái vạch vàng (tỉ lệ của thời gian rót T).
        public const float PerfectBandStart = 0.60f;
        /// Mép phải vạch vàng (tỉ lệ của T).
        public const float PerfectBandEnd = 0.85f;
        /// Cộng thêm sau mép phải để bù độ trễ cảm ứng (giây).
        public const float PerfectGraceSeconds = 0.05f;
        /// Chạm dừng trước tỉ lệ này của T = chạm nhầm: bỏ qua, ly vẫn rót tiếp.
        public const float UnderIgnoreFraction = 0.15f;
        /// Bật lại thao tác giữ cũ (HoldButtonBehaviour: nhả sớm = sai bước). Mặc định tắt.
        public static bool UseLegacyHold;

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

        /// Thời gian tự rót đầy T (giây) cho bước kiểu Hold (rót); 0 với kiểu khác.
        public static float PourSeconds(BrewStep s)
        {
            switch (s)
            {
                case BrewStep.Phin: return 1.0f;
                case BrewStep.CotDua: return 0.9f;
                case BrewStep.SuaDac:
                case BrewStep.SuaTuoi:
                case BrewStep.Tra:
                    return 0.8f;
                default:
                    return 0f;
            }
        }

        /// Thời gian giữ (giây) cho bước kiểu Hold ở chế độ giữ cũ (UseLegacyHold); 0 với kiểu khác.
        public static float HoldSeconds(BrewStep s)
        {
            switch (s)
            {
                case BrewStep.Phin: return 0.5f;
                case BrewStep.CotDua: return 0.45f;
                case BrewStep.SuaDac:
                case BrewStep.SuaTuoi:
                case BrewStep.Tra:
                    return 0.4f;
                default:
                    return 0f;
            }
        }
    }
}
