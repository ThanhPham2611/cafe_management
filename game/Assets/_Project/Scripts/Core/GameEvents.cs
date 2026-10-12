using System;
using UnityEngine;

namespace Cafe
{
    /// Sự kiện toàn cục (static). Hệ thống giao tiếp qua đây thay vì gọi chéo.
    public static class GameEvents
    {
        public static event Action<int> MoneyChanged;
        public static event Action<float, int> ReputationChanged; // rp, stars
        public static event Action<Order> OrderAdded;
        public static event Action<Order> OrderRemoved;
        /// Đơn vừa được nhân viên Pha chế nhận / nhả (thẻ order đổi màu).
        public static event Action<Order> OrderClaimChanged;
        public static event Action<int> DayStarted;
        public static event Action DayClosing;
        public static event Action<DaySummary> DayEnded;
        public static event Action<Customer> CustomerServed;
        public static event Action<Customer> CustomerLeft;
        public static event Action<string> Toast;

        public static void RaiseMoneyChanged(int xu) { if (MoneyChanged != null) MoneyChanged(xu); }
        public static void RaiseReputationChanged(float rp, int stars) { if (ReputationChanged != null) ReputationChanged(rp, stars); }
        public static void RaiseOrderAdded(Order o) { if (OrderAdded != null) OrderAdded(o); }
        public static void RaiseOrderRemoved(Order o) { if (OrderRemoved != null) OrderRemoved(o); }
        public static void RaiseOrderClaimChanged(Order o) { if (OrderClaimChanged != null) OrderClaimChanged(o); }
        public static void RaiseDayStarted(int day) { if (DayStarted != null) DayStarted(day); }
        public static void RaiseDayClosing() { if (DayClosing != null) DayClosing(); }
        public static void RaiseDayEnded(DaySummary s) { if (DayEnded != null) DayEnded(s); }
        public static void RaiseCustomerServed(Customer c) { if (CustomerServed != null) CustomerServed(c); }
        public static void RaiseCustomerLeft(Customer c) { if (CustomerLeft != null) CustomerLeft(c); }
        public static void RaiseToast(string msg) { if (Toast != null) Toast(msg); }

        // Tắt Domain Reload vẫn sạch event cũ.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MoneyChanged = null;
            ReputationChanged = null;
            OrderAdded = null;
            OrderRemoved = null;
            OrderClaimChanged = null;
            DayStarted = null;
            DayClosing = null;
            DayEnded = null;
            CustomerServed = null;
            CustomerLeft = null;
            Toast = null;
        }
    }
}
