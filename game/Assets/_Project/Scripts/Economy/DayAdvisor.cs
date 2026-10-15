namespace Cafe
{
    public enum DayAdvice { None, BuySeats, MoreStaff, CleanUp, HireCleaner, Debt, ClaimMission }

    public struct DayAdvisorInput
    {
        public DaySummary summary;
        public int tier;
        public bool hasCleaner;
        public int debt;
        public bool missionReady;
        public DayAdvice lastAdvice;
    }

    /// Chọn tối đa MỘT gợi ý ngắn đầu ngày mới dựa trên tổng kết hôm qua (khách bỏ về vì sao, quán có bẩn không...).
    /// Thuần logic: UI chỉ hiện Text(Pick(...)). Không lặp lại cùng một gợi ý hai ngày liền để khỏi gây phiền.
    public static class DayAdvisor
    {
        public const int LostThreshold = 6;
        public const float DirtyCleanliness = 70f;
        public const int DirtyLostThreshold = 3;

        public static DayAdvice Pick(DayAdvisorInput i)
        {
            DaySummary s = i.summary;
            if (s == null) return DayAdvice.None;

            // Vấn đề vận hành trước, rồi tới nhắc nhở. Mỗi mục chỉ xét nếu chưa là gợi ý ngày hôm trước.
            if (s.lostNoSeat >= LostThreshold)
            {
                // Cấp 3 bot đo: số khách phục vụ theo số nhân viên (2 NV ~43, 8 NV ~90), không theo ghế (36 -> 43 ghế không đổi)
                // => hết chỗ là do tay nghề phục vụ, mua thêm ghế không giúp
                DayAdvice a = i.tier >= 3 ? DayAdvice.MoreStaff : DayAdvice.BuySeats;
                if (Allowed(a, i)) return a;
            }
            if (s.lostPatience >= LostThreshold && Allowed(DayAdvice.MoreStaff, i)) return DayAdvice.MoreStaff;
            bool measured = s.cleanliness >= 0f;
            if (measured && (s.cleanliness < DirtyCleanliness || s.lostDirty >= DirtyLostThreshold) && Allowed(DayAdvice.CleanUp, i))
                return DayAdvice.CleanUp;
            if (i.tier >= 2 && !i.hasCleaner && s.day >= 3 && Allowed(DayAdvice.HireCleaner, i)) return DayAdvice.HireCleaner;
            if (i.debt > 0 && Allowed(DayAdvice.Debt, i)) return DayAdvice.Debt;
            if (i.missionReady && Allowed(DayAdvice.ClaimMission, i)) return DayAdvice.ClaimMission;
            return DayAdvice.None;
        }

        private static bool Allowed(DayAdvice a, DayAdvisorInput i)
        {
            return i.lastAdvice != a;
        }

        public static string Text(DayAdvice a)
        {
            switch (a)
            {
                case DayAdvice.BuySeats: return "Nhiều khách bỏ về vì hết chỗ: mua thêm bàn ghế trong Cửa hàng.";
                case DayAdvice.MoreStaff: return "Khách chờ lâu rồi bỏ về: thuê hoặc nâng cấp nhân viên Pha chế / Phục vụ.";
                case DayAdvice.CleanUp: return "Quán hơi bẩn: nâng cấp nhân viên Dọn dẹp để bàn sạch nhanh hơn.";
                case DayAdvice.HireCleaner: return "Thuê nhân viên Dọn dẹp để bạn rảnh tay pha chế.";
                case DayAdvice.Debt: return "Còn nợ tiền thuê: mỗi ngày trừ 30% doanh thu cho tới khi hết nợ.";
                case DayAdvice.ClaimMission: return "Có nhiệm vụ đã xong: mở NHIỆM VỤ để nhận thưởng.";
                default: return "";
            }
        }
    }
}
