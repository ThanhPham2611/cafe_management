using UnityEngine;

namespace Cafe
{
    /// Ví Xu. Không cho âm.
    public class Wallet
    {
        public int Xu { get; private set; }

        public void Init(int xu)
        {
            Xu = Mathf.Max(0, xu);
            GameEvents.RaiseMoneyChanged(Xu);
        }

        public void Earn(int amount)
        {
            if (amount <= 0) return;
            Xu += amount;
            GameEvents.RaiseMoneyChanged(Xu);
        }

        /// Trừ tối đa bằng số dư hiện có; trả về số thực trừ.
        public int Spend(int amount)
        {
            int paid = Mathf.Min(Xu, Mathf.Max(0, amount));
            Xu -= paid;
            GameEvents.RaiseMoneyChanged(Xu);
            return paid;
        }
    }
}
