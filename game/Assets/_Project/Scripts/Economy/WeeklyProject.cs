using System;

namespace Cafe
{
    public enum WeeklyKind { Serve, Tips, Profit }

    /// Dự án tuần: mỗi tuần game (7 ngày) một mục tiêu cộng dồn qua các ngày, đổi luân phiên Khách → Tip → Lãi. Mục tiêu theo cấp quán,
    /// khoảng 65% số bot đo được (người thật chậm hơn bot). Xong thì thưởng Xu + Hạt một lần. Cấp quán được chốt ở ngày đầu tuần
    /// để lên cấp giữa tuần không làm mục tiêu nhảy vọt. Thuần logic: GameBootstrap lo lưu và trả thưởng.
    public sealed class WeeklyProject
    {
        public const int DaysPerWeek = 7;

        public int Week { get; private set; }
        public int Progress { get; private set; }
        public bool Done { get; private set; }
        public int Tier { get; private set; } = 1;

        public WeeklyKind Kind { get { return KindOf(Week); } }
        public int Target { get { return TargetFor(Kind, Tier); } }

        public static int WeekOf(int day) { return Math.Max(0, (day - 1) / DaysPerWeek); }
        public static int DayInWeek(int day) { return (Math.Max(1, day) - 1) % DaysPerWeek + 1; }
        public static WeeklyKind KindOf(int week) { return (WeeklyKind)(Math.Max(0, week) % 3); }

        /// Mục tiêu cả tuần. Số bot đo (ngày 210 s): khách/ngày ~25 / 40-80 / 45-95; tip ~300 / 600 / 1.500; lãi ~2.000 / 4.000 / 5.500+.
        public static int TargetFor(WeeklyKind kind, int tier)
        {
            int t = Math.Max(1, Math.Min(tier, 3)) - 1;
            switch (kind)
            {
                case WeeklyKind.Serve: return ServeTargets[t];
                case WeeklyKind.Tips: return TipTargets[t];
                default: return ProfitTargets[t];
            }
        }

        private static readonly int[] ServeTargets = { 110, 180, 350 };
        private static readonly int[] TipTargets = { 1500, 2500, 6000 };
        private static readonly int[] ProfitTargets = { 7000, 16000, 24000 };
        private static readonly int[] RewardXus = { 800, 2000, 5000 };
        private static readonly int[] RewardGemsByTier = { 10, 15, 20 };

        public static int RewardXu(int tier) { return RewardXus[Math.Max(1, Math.Min(tier, 3)) - 1]; }
        public static int RewardGems(int tier) { return RewardGemsByTier[Math.Max(1, Math.Min(tier, 3)) - 1]; }

        public void Load(int week, int progress, bool done, int tier)
        {
            Week = Math.Max(0, week);
            Progress = Math.Max(0, progress);
            Done = done;
            Tier = Math.Max(1, tier);
        }

        /// Sang tuần mới (theo `day`) thì làm lại từ 0 và chốt cấp quán hiện tại. Gọi khi bắt đầu ngày và trước khi ghi nhận.
        public void Sync(int day, int tier)
        {
            int week = WeekOf(day);
            if (week == Week) return;
            Week = week;
            Progress = 0;
            Done = false;
            Tier = Math.Max(1, tier);
        }

        /// Ghi nhận một ngày vừa kết thúc. Trả true nếu CHÍNH ngày này làm xong dự án (để trả thưởng đúng một lần).
        public bool RecordDay(int day, int tier, int served, int tips, int profit)
        {
            Sync(day, tier);
            if (Done) return false;
            int add;
            switch (Kind)
            {
                case WeeklyKind.Serve: add = served; break;
                case WeeklyKind.Tips: add = tips; break;
                default: add = profit; break;
            }
            Progress += Math.Max(0, add);
            if (Progress < Target) return false;
            Done = true;
            return true;
        }
    }
}
