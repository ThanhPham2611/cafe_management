using UnityEngine;

namespace Cafe
{
    /// Kết quả một lần rót (docs/hud_brew_redesign_2026-10-18.md B1).
    /// Under = "Hơi ít" (chạm dừng sớm: bước vẫn xong, ly tối đa 2 sao), Good = "Đủ" (tự đầy / chạm muộn), Perfect = "Chuẩn!" (vạch vàng).
    public enum PourGrade { Under, Good, Perfect }

    /// Luật thuần của thao tác rót (không đụng Unity runtime, có test EditMode): chấm điểm theo thời điểm chạm dừng,
    /// giới hạn sao của ly và ngân sách thời gian 3 sao. Thông số nằm ở StepActions.
    public static class PourRules
    {
        /// Ngân sách thời gian (giây) mỗi kiểu bước để tính hạn 3 sao tối thiểu (B3).
        public const float TapBudgetSeconds = 0.6f;
        public const float SwipeBudgetSeconds = 0.8f;
        public const float SpinBudgetSeconds = 1.2f;
        /// Thời gian phản xạ cộng một lần cho cả ly.
        public const float ReactionSeconds = 1.0f;
        /// Hạn 3 sao phải >= hệ số này x tổng ngân sách.
        public const float PerfectBudgetFactor = 1.3f;

        /// Giây bắt đầu vạch vàng của một lần rót dài pourSeconds.
        public static float BandStartSeconds(float pourSeconds)
        {
            return pourSeconds * StepActions.PerfectBandStart;
        }

        /// Giây kết thúc vạch vàng (chưa cộng PerfectGraceSeconds).
        public static float BandEndSeconds(float pourSeconds)
        {
            return pourSeconds * StepActions.PerfectBandEnd;
        }

        /// Chạm dừng trước mốc này (giây) = chạm nhầm, bỏ qua.
        public static float IgnoreSeconds(float pourSeconds)
        {
            return pourSeconds * StepActions.UnderIgnoreFraction;
        }

        /// Mức đầy của ly (0..1) sau elapsed giây.
        public static float Fill(float elapsed, float pourSeconds)
        {
            if (pourSeconds <= 0f) return 1f;
            return Mathf.Clamp01(elapsed / pourSeconds);
        }

        /// Đã rót đầy (tự xong, kết quả "Đủ").
        public static bool IsFull(float elapsed, float pourSeconds)
        {
            return pourSeconds <= 0f || elapsed >= pourSeconds;
        }

        /// Người chơi chạm dừng sau elapsed giây. Trả false nếu chạm quá sớm (< UnderIgnoreFraction): bỏ qua, ly vẫn rót tiếp.
        /// Ngược lại grade: sớm hơn vạch vàng = Under; trong vạch vàng (+ bù trễ) = Perfect; sau đó = Good.
        public static bool TryGradeStop(float elapsed, float pourSeconds, out PourGrade grade)
        {
            grade = PourGrade.Good;
            if (pourSeconds <= 0f) return true;
            if (elapsed < IgnoreSeconds(pourSeconds)) return false;
            if (elapsed < BandStartSeconds(pourSeconds)) grade = PourGrade.Under;
            else if (elapsed <= BandEndSeconds(pourSeconds) + StepActions.PerfectGraceSeconds) grade = PourGrade.Perfect;
            else grade = PourGrade.Good;
            return true;
        }

        /// Sao của ly vừa pha xong: sai bước -> 1; có lần rót "Hơi ít" hoặc chậm -> 2; còn lại 3.
        /// (Sai >= 2 bước thì hỏng ly, BrewStation xử lý trước khi tới đây. "Hơi ít" trong hướng dẫn không được đếm.)
        public static int Quality(int wrong, int underCount, bool fast)
        {
            if (wrong > 0) return 1;
            if (underCount > 0 || !fast) return 2;
            return 3;
        }

        /// Ly "chuẩn vị": có ít nhất một bước rót, mọi bước rót đều "Chuẩn!", và ly đạt 3 sao.
        public static bool IsPerfectPourDrink(int pourSteps, int perfectPours, int quality)
        {
            return pourSteps > 0 && perfectPours >= pourSteps && quality >= 3;
        }

        /// Ngân sách thời gian (giây) cho một bước theo kiểu thao tác.
        public static float StepBudgetSeconds(BrewStep s)
        {
            switch (StepActions.For(s))
            {
                case StepAction.Hold: return StepActions.PourSeconds(s);
                case StepAction.Swipe: return SwipeBudgetSeconds;
                case StepAction.Spin: return SpinBudgetSeconds;
                default: return TapBudgetSeconds;
            }
        }

        /// Hạn 3 sao tối thiểu của một chuỗi bước: 1,3 x (tổng ngân sách + 1 s phản xạ). Rỗng -> 0.
        public static float MinPerfectSeconds(BrewStep[] steps)
        {
            if (steps == null || steps.Length == 0) return 0f;
            float sum = ReactionSeconds;
            for (int i = 0; i < steps.Length; i++) sum += StepBudgetSeconds(steps[i]);
            return PerfectBudgetFactor * sum;
        }
    }
}
