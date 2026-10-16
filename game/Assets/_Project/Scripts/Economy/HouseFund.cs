using UnityEngine;

namespace Cafe
{
    /// Quỹ lên cấp quán: thanh Xu của thẻ "Thuê mặt bằng" / "Mua nhà hàng" chia 4 chặng (25 / 50 / 75 / 100%). Mỗi chặng
    /// đầu (25 / 50 / 75%) thưởng Hạt một lần cho mỗi cấp đích, lấp đoạn dài chỉ gom tiền; chặng 100% là chính nút lên cấp.
    public static class HouseFund
    {
        public const int Stages = 4;

        /// Chặng đã đạt (0..4) khi có `xu` trên tổng `need`.
        public static int StageFor(int xu, int need)
        {
            if (need <= 0) return Stages;
            if (xu <= 0) return 0;
            return Mathf.Clamp((int)((long)xu * Stages / need), 0, Stages);
        }

        /// Hạt thưởng khi đạt chặng 1..3 của quỹ lên cấp `toTier`: 5 / 10 / 15 (cấp 2), gấp đôi cho cấp 3. Chặng 4: không (là nút lên cấp).
        public static int RewardGems(int stage, int toTier)
        {
            if (stage < 1 || stage >= Stages) return 0;
            return (toTier >= 3 ? 10 : 5) * stage;
        }
    }

    /// Nhớ chặng quỹ đã nhận thưởng để mỗi chặng chỉ thưởng một lần cho mỗi cấp đích, kể cả khi Xu lên xuống.
    public sealed class HouseFundTracker
    {
        public int TargetTier { get; private set; }
        public int Stage { get; private set; }

        public void Load(int targetTier, int stage)
        {
            TargetTier = targetTier;
            Stage = Mathf.Clamp(stage, 0, HouseFund.Stages);
        }

        /// Đặt cấp đích và chặng hiện tại mà không thưởng (save cũ chưa có quỹ: không thưởng bù hàng loạt).
        public void Reset(int nextTier, int xu, int need)
        {
            TargetTier = nextTier;
            Stage = nextTier > 0 ? HouseFund.StageFor(xu, need) : 0;
        }

        /// Cập nhật theo Xu hiện có. Đổi cấp đích thì quỹ mới bắt đầu từ 0. Trả true nếu vừa đạt chặng mới (from, to] cần thưởng.
        public bool Advance(int nextTier, int xu, int need, out int from, out int to)
        {
            from = to = Stage;
            if (nextTier <= 0) return false;
            if (nextTier != TargetTier)
            {
                TargetTier = nextTier;
                Stage = 0;
                from = to = 0;
            }
            int s = HouseFund.StageFor(xu, need);
            if (s <= Stage) return false;
            from = Stage;
            Stage = s;
            to = s;
            return true;
        }
    }
}
