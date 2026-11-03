using UnityEngine;

namespace Cafe
{
    /// Luật thuần của combo pha chuẩn (docs/combo_rush_spec.md §1): mỗi ly 3 sao chủ quán tự pha +1 (ly chuẩn vị +2),
    /// bậc combo cho tip thưởng = giá ly × hệ số, có trần theo doanh thu trong ngày. Không đụng Unity runtime, có test EditMode.
    public static class ComboRules
    {
        /// Combo tối thiểu của bậc 1 / 2 / 3 ("Khéo tay" / "Pha như gió" / "Barista!").
        public const int Tier1At = 3;
        public const int Tier2At = 6;
        public const int Tier3At = 10;

        /// Tip combo = giá ly × hệ số của bậc (bậc 0 không thưởng).
        private static readonly float[] Rates = { 0f, 0.10f, 0.20f, 0.30f };

        /// Trần tip combo của cả ngày = Floor + Fraction × doanh thu đồ uống tới lúc đó (giữ tổng ≲ 5% doanh thu, §1.2).
        /// Bot pha 3 sao gần như mọi ly: 60 + 5% cho 6,5–7,9% doanh thu ở cấp 1 (2026-11-03), nên hạ còn 30 + 4% (≈ 5,7% ở 1.800 Xu/ngày).
        public const int DailyCapFloor = 30;
        public const float DailyCapFraction = 0.04f;

        /// Badge combo chỉ hiện từ mức này (x1 dễ gây rối mắt).
        public const int ShowAt = 2;

        public static int Tier(int combo)
        {
            if (combo >= Tier3At) return 3;
            if (combo >= Tier2At) return 2;
            if (combo >= Tier1At) return 1;
            return 0;
        }

        public static float Rate(int tier)
        {
            return Rates[Mathf.Clamp(tier, 0, Rates.Length - 1)];
        }

        /// Số combo một ly vừa pha xong cộng thêm: dưới 3 sao = 0 (làm đứt combo), 3 sao = 1, chuẩn vị = 2.
        public static int Gain(int quality, bool perfect)
        {
            if (quality < 3) return 0;
            return perfect ? 2 : 1;
        }

        /// Tip thưởng (chưa áp trần) cho một ly giá price khi combo đã lên tới combo. rushMult dành cho giờ đông (§2, mặc định 1).
        public static int RawBonus(int price, int combo, float rushMult = 1f)
        {
            if (price <= 0) return 0;
            return Mathf.RoundToInt(price * Rate(Tier(combo)) * Mathf.Max(0f, rushMult));
        }

        /// Trần tip combo của ngày khi doanh thu đồ uống đã đạt revenueSoFar.
        public static int DailyCap(int revenueSoFar)
        {
            return DailyCapFloor + Mathf.RoundToInt(DailyCapFraction * Mathf.Max(0, revenueSoFar));
        }

        /// Phần tip được trả thật sau khi áp trần (đã trả paidSoFar trong ngày).
        public static int Capped(int raw, int paidSoFar, int revenueSoFar)
        {
            if (raw <= 0) return 0;
            return Mathf.Clamp(DailyCap(revenueSoFar) - Mathf.Max(0, paidSoFar), 0, raw);
        }
    }
}
