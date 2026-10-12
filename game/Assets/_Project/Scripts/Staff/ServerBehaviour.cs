using UnityEngine;

namespace Cafe
{
    /// Nhân viên Phục vụ: khi chủ quán pha xong một ly, cầm ly đi ngay (quầy rảnh để pha ly kế) và mang tới khách chờ
    /// lâu nhất đúng món đó. Khách bỏ đi giữa đường thì ly bị bỏ và ly sạch về kho.
    public sealed class ServerBehaviour : StaffBehaviour
    {
        private Customer serveTarget;
        private Order carriedOrder;
        private int carriedQuality;

        public ServerBehaviour(StaffWorker worker) : base(worker) { }

        /// Bỏ ly đang cầm (nếu còn) và nhả đơn đã giữ.
        public override void Release()
        {
            serveTarget = null;
            if (carriedOrder != null)
            {
                Order o = carriedOrder;
                carriedOrder = null;
                o.Release(worker);
                if (Game != null) GameEvents.RaiseOrderClaimChanged(o); // Game có thể đã bị hủy lúc đóng cảnh
            }
            if (carriedQuality > 0)
            {
                carriedQuality = 0;
                if (Game != null && Game.Stock != null) Game.Stock.Return();
            }
        }

        protected override void OnTick()
        {
            if (state == WorkerState.Idle)
            {
                TryPickUpDrink();
                return;
            }
            if (carriedOrder == null || serveTarget == null || !IsWaiting(carriedOrder) ||
                !ReferenceEquals(serveTarget.CurrentOrder, carriedOrder))
            {
                worker.StopWork(); // khách bỏ đi giữa đường
                return;
            }
            if (state == WorkerState.Moving && ReachedTarget())
            {
                if (serveTarget.Serve(carriedQuality))
                {
                    carriedQuality = 0; // ly sang tay khách
                    CountJob();
                }
                worker.StopWork();
            }
        }

        private void TryPickUpDrink()
        {
            BrewStation station = Game.Station;
            if (Time.time < retryAt || station == null || station.State != BrewStation.Phase.Ready) return;
            if (Game.Owner != null && Game.Owner.IsDelivering) return; // chủ quán đang tự giao ly này

            // Station.Selected đã bị xóa khi pha xong (BrewStation.Finish), nên chọn khách theo món đang cầm.
            Order pick = OrderPicker.NextForServer(Game.Orders.Items, station.Recipe, IsWaiting);
            Customer customer = pick != null ? pick.customer : null;
            Vector3 point;
            if (customer == null || customer.Seat == null ||
                !customer.Seat.TryGetApproachPoint(worker.transform.position, out point))
            {
                retryAt = Time.time + 0.5f;
                return;
            }

            // Cầm ly đi ngay; giữ chỗ đơn để Pha chế không làm trùng.
            if (!pick.TryClaim(worker)) { retryAt = Time.time + 0.5f; return; }
            int quality = station.TakeReadyDrink(pick.recipe);
            if (quality <= 0) { pick.Release(worker); retryAt = Time.time + 0.5f; return; }
            carriedOrder = pick;
            carriedQuality = quality;
            GameEvents.RaiseOrderClaimChanged(pick);
            serveTarget = customer;
            worker.Visual.SetCarry(true);
            if (!MoveTo(point)) { worker.StopWork(); retryAt = Time.time + 1f; }
            else BeginState(WorkerState.Moving);
        }
    }
}
