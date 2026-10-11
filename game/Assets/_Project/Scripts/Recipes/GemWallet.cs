using UnityEngine;

namespace Cafe
{
    /// Ví Hạt cà phê vàng (tiền cứng, economy §1.2). Không đổi ra Xu. Lưu ở SaveData.gems.
    /// TIER/Nhiệm vụ... cấp Hạt bằng GemWallet.Add; gợi ý công thức trừ bằng TrySpend.
    public class GemWallet
    {
        public int Gems { get; private set; }

        /// Gems đã đổi (số dư mới).
        public event System.Action<int> Changed;

        public void Init(int gems)
        {
            Gems = Mathf.Max(0, gems);
            Raise();
        }

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Gems += amount;
            Raise();
        }

        public bool CanAfford(int amount)
        {
            return amount <= Gems;
        }

        /// Trừ đúng amount hoặc không trừ gì cả.
        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Gems) return false;
            Gems -= amount;
            Raise();
            return true;
        }

        private void Raise()
        {
            if (Changed != null) Changed(Gems);
        }
    }
}
