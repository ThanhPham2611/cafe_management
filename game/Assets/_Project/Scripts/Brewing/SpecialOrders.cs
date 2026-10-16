namespace Cafe
{
    /// Đơn đặc biệt (từ Nhà hàng): khách gọi một món mà CHỈ chủ quán được pha (nhân viên Pha chế bỏ qua), thưởng x2 cả giá ly lẫn tip.
    /// Cho chủ quán một việc riêng ở cấp 3, khi nhân viên đã lo gần hết ly thường. Hiếm (10%), tối đa 2 đơn cùng lúc để một mình
    /// chủ quán không bị quá tải; khách chờ lâu hơn 1,5 lần.
    public static class SpecialOrders
    {
        public const float Chance = 0.10f;
        public const float RewardMult = 2f;
        public const float PatienceMult = 1.5f;
        public const int MaxActive = 2;
        public const int MinTier = 3;

        /// Đơn vừa gọi có phải đơn đặc biệt không. `roll01` = số ngẫu nhiên 0..1 (truyền vào để kiểm thử được).
        public static bool ShouldBeSpecial(int tier, int activeSpecial, float roll01)
        {
            return tier >= MinTier && activeSpecial < MaxActive && roll01 < Chance;
        }

        public static int Count(System.Collections.Generic.IReadOnlyList<Order> orders)
        {
            int n = 0;
            if (orders != null)
                for (int i = 0; i < orders.Count; i++)
                    if (orders[i] != null && orders[i].ownerOnly) n++;
            return n;
        }
    }
}
