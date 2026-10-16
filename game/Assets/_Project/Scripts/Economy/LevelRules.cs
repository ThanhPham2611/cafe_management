using System.Collections.Generic;

namespace Cafe
{
    /// Level uy tín: các bậc nhỏ chia đều giữa hai mốc sao liên tiếp (dựng từ GameBalance.StarThresholds nên đổi mốc sao thì
    /// level đổi theo). Level lên thường xuyên cho người chơi thấy tiến bộ; mốc sao là level đặc biệt, thưởng lớn hơn.
    /// Thưởng mỗi level: Xu = 100 + 40·level (x3 ở level trùng mốc sao) + Hạt ở level mốc sao (economy §22).
    public static class LevelRules
    {
        /// Số level chia trong từng hạng sao: ★1→★2, ★2→★3, ★3→★4, ★4→★5.
        private static readonly int[] StepsPerStar = { 5, 10, 15, 10 };
        /// Hạt thưởng khi đạt ★1..★5 (economy §4.2); ★1 là mức khởi đầu nên không có.
        private static readonly int[] StarGems = { 0, 50, 100, 200, 400 };

        /// RP bắt đầu của từng level (chỉ số 0 = level 1).
        public static readonly int[] Thresholds = Build();

        public static int MaxLevel { get { return Thresholds.Length; } }

        private static int[] Build()
        {
            int[] th = GameBalance.StarThresholds;
            var list = new List<int>();
            for (int s = 0; s < StepsPerStar.Length && s + 1 < th.Length; s++)
                for (int i = 0; i < StepsPerStar[s]; i++)
                    list.Add(th[s] + (th[s + 1] - th[s]) * i / StepsPerStar[s]);
            list.Add(th[th.Length - 1]); // level cuối = ★ cao nhất
            return list.ToArray();
        }

        /// Level (từ 1) ứng với số RP.
        public static int LevelFor(float rp)
        {
            int level = 1;
            for (int i = 1; i < Thresholds.Length; i++)
                if (rp >= Thresholds[i]) level = i + 1;
            return level;
        }

        /// RP bắt đầu của `level`.
        public static int RpFor(int level)
        {
            return Thresholds[System.Math.Max(1, System.Math.Min(level, MaxLevel)) - 1];
        }

        /// RP cần cho level kế tiếp; -1 nếu đã tối đa.
        public static int NextRp(int level)
        {
            return level >= MaxLevel ? -1 : Thresholds[level];
        }

        /// Level này có trùng đúng mốc một hạng sao không (★2..★5); star = số sao đạt được.
        public static bool IsStarLevel(int level, out int star)
        {
            star = 0;
            if (level <= 1) return false;
            int rp = RpFor(level);
            int[] th = GameBalance.StarThresholds;
            for (int s = 1; s < th.Length; s++)
                if (th[s] == rp) { star = s + 1; return true; }
            return false;
        }

        public static int RewardXu(int level)
        {
            int xu = 100 + 40 * level;
            int star;
            return IsStarLevel(level, out star) ? xu * 3 : xu;
        }

        public static int RewardGems(int level)
        {
            int star;
            return IsStarLevel(level, out star) ? StarGems[System.Math.Min(star, StarGems.Length) - 1] : 0;
        }
    }
}
