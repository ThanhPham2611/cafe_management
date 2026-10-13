using UnityEngine;

namespace Cafe
{
    /// Sổ thuê nhà cấp 2 (economy §3.4): tiền thuê mỗi 7 ngày in-game, trừ tự động đầu ngày khi tới kỳ;
    /// thiếu Xu thì ghi nợ phần thiếu và mỗi cuối ngày trừ 30% doanh thu vào nợ cho tới khi hết. Không mất quán, không phạt ★.
    /// Logic thuần (không MonoBehaviour) để dễ kiểm tra; TierManager giữ một bản và lưu/đọc qua SaveData.
    public class RentLedger
    {
        /// Nợ hiện tại (Xu): tiền thuê thiếu + lương thiếu...
        public int Debt { get; private set; }
        /// Số ngày đã hoàn tất kể từ lần thu thuê gần nhất.
        public int DaysSinceRent { get; private set; }

        /// Còn bao nhiêu ngày nữa thì thu tiền thuê (0 = sáng mai).
        public int DaysUntilRent
        {
            get { return Mathf.Max(0, GameBalance.RentIntervalDays - DaysSinceRent); }
        }

        public bool RentDue
        {
            get { return DaysSinceRent >= GameBalance.RentIntervalDays; }
        }

        public void Load(int debt, int daysSinceRent)
        {
            Debt = Mathf.Max(0, debt);
            DaysSinceRent = Mathf.Max(0, daysSinceRent);
        }

        /// Bắt đầu kỳ thuê mới (vừa đặt cọc): tuần đầu đã nằm trong tiền cọc.
        public void Reset()
        {
            Debt = 0;
            DaysSinceRent = 0;
        }

        /// Mua nhà hàng (cấp 3): hết tiền thuê hàng tuần. Nợ còn lại vẫn phải trả dần.
        public void ClearRentCycle()
        {
            DaysSinceRent = 0;
        }

        public void AddDebt(int amount)
        {
            if (amount > 0) Debt += amount;
        }

        /// Đầu ngày. Tới kỳ thì thu tiền thuê; phần Xu không đủ trở thành nợ.
        /// Trả về tiền đã thu thật (Xu), shortfall = phần ghi nợ; ghi vào summary.rent.
        public int OnDayBegin(Wallet wallet, DaySummary summary, out int shortfall)
        {
            shortfall = 0;
            if (!RentDue) return 0;
            DaysSinceRent = 0;

            int rent = GameBalance.Tier2RentPerWeek;
            int paid = wallet.Spend(rent);
            shortfall = rent - paid;
            Debt += shortfall;
            if (summary != null) summary.rent += paid;
            return paid;
        }

        /// Cuối ngày: đếm ngày, trừ 30% doanh thu hôm nay vào nợ (không quá nợ, không quá số Xu đang có).
        /// Ghi summary.debtPaid / summary.debt. Trả về số Xu đã trả nợ.
        public int OnDayEnd(Wallet wallet, DaySummary summary)
        {
            DaysSinceRent++;
            int paid = 0;
            if (Debt > 0)
            {
                int income = summary != null ? Mathf.Max(0, summary.revenue + summary.tips) : 0;
                int due = Mathf.Min(Debt, Mathf.RoundToInt(income * GameBalance.DebtRevenueShare));
                paid = wallet.Spend(due);
                Debt -= paid;
            }
            if (summary != null)
            {
                summary.debtPaid = paid;
                summary.debt = Debt;
            }
            return paid;
        }
    }
}
