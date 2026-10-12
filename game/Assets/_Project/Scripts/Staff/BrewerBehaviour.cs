using UnityEngine;
using UnityEngine.AI;

namespace Cafe
{
    /// Nhân viên Pha chế: nhận đơn khách đang chờ (chưa ai làm) -> pha tại quầy (mất nguyên liệu + 1 ly sạch như chủ quán,
    /// thời gian và chất lượng theo BrewerRules) -> mang tới khách -> về chỗ đứng.
    public sealed class BrewerBehaviour : StaffBehaviour
    {
        private enum BrewPhase { Idle, Brewing, Delivering, Returning }

        private BrewPhase phase;
        private Order order;
        private bool holdsCup;
        private float elapsed;
        private float total;
        private int quality;

        public BrewerBehaviour(StaffWorker worker) : base(worker) { }

        /// Hủy việc pha dở: nhả đơn, trả ly sạch (nguyên liệu đã dùng thì mất).
        public override void Release()
        {
            ReleaseClaim();
            if (holdsCup)
            {
                holdsCup = false;
                if (Game != null && Game.Stock != null) Game.Stock.Return();
            }
            phase = BrewPhase.Idle;
            elapsed = 0f;
        }

        protected override void OnTick()
        {
            switch (phase)
            {
                case BrewPhase.Idle: TryStart(); break;
                case BrewPhase.Brewing: TickBrewing(); break;
                case BrewPhase.Delivering: TickDelivering(); break;
                case BrewPhase.Returning: TickReturning(); break;
            }
        }

        private void TryStart()
        {
            if (Time.time < retryAt || Game.Orders == null || Game.Station == null) return;
            BrewStation st = Game.Station;
            Order pick = OrderPicker.NextForBrewer(Game.Orders.Items, st.Selected,
                st.State == BrewStation.Phase.Ready ? st.Recipe : null, IsWaiting);
            if (pick == null || pick.recipe == null) { retryAt = Time.time + 0.4f; return; }

            RecipeDef recipe = pick.recipe;
            if (Game.Wallet.Xu < recipe.ingredientCost || !Game.Stock.TryTake()) { retryAt = Time.time + 1f; return; }
            if (!pick.TryClaim(worker)) { Game.Stock.Return(); retryAt = Time.time + 0.5f; return; }

            holdsCup = true;
            Game.Wallet.Spend(recipe.ingredientCost);
            if (Game.Day.Summary != null) Game.Day.Summary.ingredientCost += recipe.ingredientCost;

            order = pick;
            elapsed = 0f;
            total = BrewerRules.BrewSeconds(recipe.brewSeconds, Data.level);
            quality = BrewerRules.Quality(Data.level, Game.Recipes == null || Game.Recipes.ToolAllowsThreeStars(recipe));
            phase = BrewPhase.Brewing;
            GameEvents.RaiseOrderClaimChanged(pick);
        }

        private bool OrderStillValid()
        {
            return IsWaiting(order) && ReferenceEquals(order.customer.CurrentOrder, order);
        }

        private void TickBrewing()
        {
            if (!OrderStillValid())
            {
                worker.StopWork(); // khách bỏ đi giữa chừng: mất nguyên liệu, ly sạch về kho
                return;
            }
            elapsed += Time.deltaTime;
            if (elapsed < total) return;

            Vector3 point;
            Customer c = order.customer;
            if (c.Seat == null || !c.Seat.TryGetApproachPoint(worker.transform.position, out point) || !MoveTo(point))
            {
                worker.StopWork();
                retryAt = Time.time + 1f;
                return;
            }
            worker.Visual.SetCarry(true);
            phase = BrewPhase.Delivering;
            BeginState(WorkerState.Moving);
        }

        private void TickDelivering()
        {
            if (!OrderStillValid())
            {
                worker.StopWork();
                return;
            }
            if (!ReachedTarget()) return;

            if (order.customer.Serve(quality))
            {
                holdsCup = false; // ly đã sang tay khách (thành ly bẩn sau khi uống)
                CountJob();
                ReleaseClaim();
                worker.Visual.SetCarry(false);
                if (MoveTo(worker.Home)) { phase = BrewPhase.Returning; BeginState(WorkerState.Moving); }
                else worker.StopWork();
            }
            else
            {
                worker.StopWork();
            }
        }

        private void TickReturning()
        {
            NavMeshAgent agent = worker.Agent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) { worker.StopWork(); return; }
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;
            if (arrived || stateTime > 12f)
            {
                phase = BrewPhase.Idle;
                agent.isStopped = true;
                agent.ResetPath();
                BeginState(WorkerState.Idle);
            }
        }

        private void ReleaseClaim()
        {
            if (order == null) return;
            Order o = order;
            order = null;
            o.Release(worker);
            if (Game != null) GameEvents.RaiseOrderClaimChanged(o); // Game có thể đã bị hủy lúc đóng cảnh
        }
    }
}
