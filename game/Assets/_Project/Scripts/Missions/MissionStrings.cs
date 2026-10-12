namespace Cafe
{
    /// Chuỗi tiếng Việt của nhiệm vụ ngày.
    public static class MissionStrings
    {
        public const string Button = "NHIỆM VỤ";
        public const string Title = "NHIỆM VỤ HÔM NAY";
        public const string Claim = "NHẬN";
        public const string Claimed = "ĐÃ NHẬN";
        public const string InProgress = "ĐANG LÀM";
        public const string ChestTitle = "RƯƠNG HẠT";
        public const string ChestLockedHint = "Nhận đủ 3 nhiệm vụ để mở rương";
        public const string ChestReadyHint = "Rương đã sẵn sàng!";
        public const string ChestOpenedHint = "Đã mở rương hôm nay";
        public const string OpenChest = "MỞ RƯƠNG";
        public const string Close = "ĐÓNG";
        public const string GemsFmt = "Hạt: {0}";
        public const string ResetFmt = "Nhiệm vụ mới sau {0}";
        public const string RewardXuFmt = "+{0} Xu";
        public const string ChestRewardFmt = "+{0} Hạt  +{1} RP";
        public const string CompletedFmt = "Xong nhiệm vụ: {0}";
        public const string ChestOpenedFmt = "Mở rương: +{0} Hạt, +{1} RP!";
        public const string ButtonProgressFmt = "NHIỆM VỤ {0}/{1}";
        public const string SummaryRow = "Nhiệm vụ";
        public const string SummaryHasReward = " • nhận!";

        public static string Describe(MissionState m)
        {
            MissionTemplate t = MissionCatalog.Find(m.id);
            if (t == null) return m.id;
            switch (t.kind)
            {
                case MissionKind.Serve: return "Phục vụ " + m.target + " khách";
                case MissionKind.SellRecipe: return "Bán " + m.target + " ly " + t.label;
                case MissionKind.ThreeStar: return "Pha " + m.target + " ly đạt 3★";
                case MissionKind.SellHidden: return "Bán " + m.target + " ly món ẩn";
                case MissionKind.NoWalkaway: return "Một ngày không khách nào bỏ về";
                case MissionKind.CleanDay: return "Giữ độ sạch từ 80 trở lên cả ngày";
                case MissionKind.EarnTips: return "Nhận " + m.target + " Xu tiền tip";
                default: return m.id;
            }
        }

        /// "2 giờ 15 phút" / "45 phút".
        public static string Duration(double seconds)
        {
            int minutes = (int)System.Math.Ceiling(System.Math.Max(0.0, seconds) / 60.0);
            int h = minutes / 60;
            int m = minutes % 60;
            return h > 0 ? h + " giờ " + m + " phút" : m + " phút";
        }
    }
}
