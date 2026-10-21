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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string lastBlock = "";
        public override string DebugState()
        {
            return phase + (order != null ? " có-đơn" : "") + (holdsCup ? " cầm-ly" : "") + " chặn=" + lastBlock +
                   " thử-lại=" + Mathf.Max(0f, retryAt - Time.time).ToString("0.0");
        }
#endif

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
                case BrewPhase.Idle: if (!TryGoToNewHome()) TryStart(); break;
                case BrewPhase.Brewing: TickBrewing(); break;
                case BrewPhase.Delivering: TickDelivering(); break;
                case BrewPhase.Returning: TickReturning(); break;
            }
        }

        /// Rảnh mà vừa được gán chỗ mới (quầy pha phụ đặt / gỡ, đổi người): đi tới Home mới trước rồi mới nhận đơn.
        private bool TryGoToNewHome()
        {
            if (!worker.ApplyPendingHome() || !MoveTo(worker.Home)) return false;
            phase = BrewPhase.Returning;
            BeginState(WorkerState.Moving);
            return true;
        }

        private void TryStart()
        {
            if (Time.time < retryAt || Game.Orders == null || Game.Station == null) return;
            Game.Shelf.Prune(IsWaiting, o => OrderPicker.SameRecipeWaiting(Game.Orders.Items, o.recipe, IsWaiting)); // ly của khách đã bỏ đi: trả ly sạch về kho
            BrewStation st = Game.Station;
            float timeFactor = Game.BrewTimeFactor * StationRules.BrewFactor(worker.HasBrewSpot);
            Order pick = OrderPicker.NextForBrewer(Game.Orders.Items, st.Selected,
                st.State == BrewStation.Phase.Ready ? st.Recipe : null, IsWaiting,
                o => o.recipe != null && o.customer != null &&
                     BrewerRules.CanFinish(o.customer.PatienceSecondsLeft, BrewerRules.BrewSeconds(o.recipe.brewSeconds, Data.level, timeFactor)));
            if (pick == null || pick.recipe == null) { Block("khôngCóĐơn"); retryAt = Time.time + 0.4f; return; }

            RecipeDef recipe = pick.recipe;
            if (Game.Wallet.Xu < recipe.ingredientCost || !Game.Stock.TryTake()) { Block("hếtLy/Xu"); retryAt = Time.time + 1f; return; }
            if (!pick.TryClaim(worker)) { Block("claimLỗi"); Game.Stock.Return(); retryAt = Time.time + 0.5f; return; }
            Block("");

            holdsCup = true;
            Game.Wallet.Spend(recipe.ingredientCost);
            if (Game.Day.Summary != null) { Game.Day.Summary.ingredientCost += recipe.ingredientCost; Game.Day.Summary.brewedStaff++; }

            order = pick;
            elapsed = 0f;
            // Có chỗ ở quầy pha phụ: × BrewSpotFactor (không có chỗ thì giữ nguyên, docs/stations_spec.md §1)
            total = BrewerRules.BrewSeconds(recipe.brewSeconds, Data.level, Game.BrewTimeFactor * StationRules.BrewFactor(worker.HasBrewSpot));
            quality = BrewerRules.Quality(Data.level, Game.Recipes == null || Game.Recipes.ToolAllowsThreeStars(recipe));
            phase = BrewPhase.Brewing;
            GameEvents.RaiseOrderClaimChanged(pick);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void Block(string why)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            lastBlock = why;
#endif
        }

        private bool OrderStillValid()
        {
            return IsWaiting(order) && ReferenceEquals(order.customer.CurrentOrder, order);
        }

        private void TickBrewing()
        {
            if (!OrderStillValid() && !RetargetSameRecipe())
            {
                worker.StopWork(); // khách bỏ đi giữa chừng, không ai chờ cùng món: mất nguyên liệu, ly sạch về kho
                return;
            }
            elapsed += Time.deltaTime;
            if (elapsed < total) return;

            if (TryHandOffToServer()) return;

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

        /// Có nhân viên Phục vụ và quầy giao còn chỗ: để ly lên quầy rồi pha ly kế, không tự đi giao.
        private bool TryHandOffToServer()
        {
            if (Game.Shelf == null || Game.Staff == null || !Game.Staff.HasRole(StaffRole.Server)) return false;
            if (!Game.Shelf.HasRoom) { StationStats.ShelfFull++; return false; } // kệ đầy: phải tự đi giao (bot đo quầy line)
            Order o = order;
            if (!Game.Shelf.TryAdd(o, quality, worker)) return false;
            order = null;
            holdsCup = false; // ly đã nằm trên quầy giao
            CountJob();
            GameEvents.RaiseOrderClaimChanged(o);
            phase = BrewPhase.Idle;
            elapsed = 0f;
            return true;
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
                StationStats.SelfDelivered++;
                ReleaseClaim();
                worker.Visual.SetCarry(false);
                worker.ApplyPendingHome(); // trong lúc đi giao được gán chỗ mới: về thẳng chỗ mới
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

        /// Khách của ly đang pha bỏ đi: chuyển ly cho khách khác đang chờ cùng món (giữ nguyên tiến độ pha).
        private bool RetargetSameRecipe()
        {
            Order alt = order != null ? OrderPicker.SameRecipeWaiting(Game.Orders.Items, order.recipe, IsWaiting) : null;
            if (alt == null || !alt.TryClaim(worker)) return false;
            ReleaseClaim();
            order = alt;
            GameEvents.RaiseOrderClaimChanged(alt);
            return true;
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
