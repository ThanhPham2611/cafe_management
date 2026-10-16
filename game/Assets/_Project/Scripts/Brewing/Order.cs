namespace Cafe
{
    /// Một đơn gọi món của khách đang chờ.
    public class Order
    {
        public int id;
        public Customer customer;
        public RecipeDef recipe;
        public float timeOrdered;
        /// Đơn đặc biệt (SpecialOrders): chỉ chủ quán pha, nhân viên Pha chế không nhận.
        public bool ownerOnly;
        /// Hệ số thưởng giá ly và tip (1 = thường, SpecialOrders.RewardMult = đặc biệt).
        public float rewardMult = 1f;
        /// Người đang làm đơn này (nhân viên Pha chế). Chủ quán không giữ chỗ: chủ quán dùng BrewStation.Selected.
        public object claimedBy;

        public bool IsClaimed { get { return claimedBy != null; } }

        /// Giữ chỗ độc quyền; gọi lại bởi chính người giữ vẫn thành công.
        public bool TryClaim(object by)
        {
            if (by == null) return false;
            if (claimedBy != null && !ReferenceEquals(claimedBy, by)) return false;
            claimedBy = by;
            return true;
        }

        /// Chỉ người đang giữ mới nhả được.
        public void Release(object by)
        {
            if (claimedBy != null && ReferenceEquals(claimedBy, by)) claimedBy = null;
        }
    }
}
