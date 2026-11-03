namespace Cafe
{
    /// Trạng thái combo trong một ngày (docs/combo_rush_spec.md §1.1). Lớp thuần: BrewStation báo ly xong / lỗi, Customer báo khách
    /// bỏ về; HUD nghe GameEvents.ComboChanged / ComboBroken. Hết ngày về 0, không lưu save.
    public class ComboTracker
    {
        public int Count { get; private set; }
        public int Max { get; private set; }
        /// Tổng tip combo đã trả hôm nay (để áp trần).
        public int BonusPaid { get; private set; }
        public int Tier { get { return ComboRules.Tier(Count); } }

        public void Reset()
        {
            Count = 0;
            Max = 0;
            BonusPaid = 0;
        }

        /// Chủ quán pha xong một ly. Trả tip combo (đã áp trần); 0 nếu ly dưới 3 sao (combo đứt) hoặc chưa tới bậc.
        /// pay (nếu có) được gọi với tip combo TRƯỚC khi phát ComboChanged, để tiền đã vào ví khi HUD cho xu bay.
        public int OnDrink(int quality, bool perfect, int price, int revenueSoFar, float rushMult = 1f, System.Action<int> pay = null)
        {
            int gain = ComboRules.Gain(quality, perfect);
            if (gain <= 0)
            {
                Break();
                return 0;
            }
            int prevTier = Tier;
            Count += gain;
            if (Count > Max) Max = Count;
            int bonus = ComboRules.Capped(ComboRules.RawBonus(price, Count, rushMult), BonusPaid, revenueSoFar);
            BonusPaid += bonus;
            if (bonus > 0 && pay != null) pay(bonus);
            GameEvents.RaiseComboChanged(Count, Tier, gain, bonus, Tier > prevTier);
            return bonus;
        }

        /// Đứt combo (bước sai, hỏng / đổ ly, khách bỏ về). Không có combo thì không phát sự kiện.
        public void Break()
        {
            if (Count <= 0) return;
            int prev = Count;
            Count = 0;
            GameEvents.RaiseComboBroken(prev);
        }
    }
}
