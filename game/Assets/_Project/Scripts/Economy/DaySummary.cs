namespace Cafe
{
    /// Số liệu tổng kết một ngày.
    public class DaySummary
    {
        public int day;
        public int revenue;        // tiền đồ uống
        public int tips;
        public int scrap;          // tiền thu ly
        public int ingredientCost;
        public int fee;
        public int served;
        public int lost;
        public float rpGained;
        public float satisfactionSum;
        public int stars;
        /// CI trung bình trong ngày (T2+). -1 = không áp dụng (T1).
        public float cleanliness = -1f;

        // Cấp 2 (thuê mặt bằng). tier = cấp quán lúc đóng cửa; các số Xu đều là tiền mặt thực trả trong ngày.
        public int tier = 1;
        public int rent;           // tiền thuê nhà đã trừ sáng nay (0 nếu chưa tới kỳ)
        public int debtPaid;       // đã trả nợ cuối ngày (30% doanh thu)
        public int debt;           // nợ còn lại sau khi trả
        public int staffWages;     // lương nhân viên thực trả trong ngày

        public int NetProfit
        {
            get { return revenue + tips + scrap - ingredientCost - fee - rent - debtPaid - staffWages; }
        }

        public float AverageSatisfaction
        {
            get { return served > 0 ? satisfactionSum / served : 0f; }
        }
    }
}
