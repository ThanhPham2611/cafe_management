using UnityEngine;
using UnityEngine.AI;

namespace Cafe
{
    public static class StaffWorkerRules
    {
        private static readonly float[] CleanTimes = { 6f, 5f, 4f, 3f, 2.2f };

        public static float CleanSeconds(int level)
        {
            return CleanTimes[Mathf.Clamp(level, 1, 5) - 1];
        }

        /// Lau sàn tại chỗ: cùng tỉ lệ với chủ quán (lau bàn 1,2 s : lau sàn 2,0 s) nhưng theo tốc độ dọn của nhân viên.
        public static float MopSeconds(int level)
        {
            return GameBalance.MopSecondsOwner * CleanSeconds(level) / GameBalance.CleanSecondsOwner;
        }

        public static float MoveSpeed(int level)
        {
            int clamped = Mathf.Clamp(level, 1, 5);
            float bonus = clamped >= 3 ? 0.30f : (clamped == 2 ? 0.15f : 0f);
            return 1.65f * (1f + bonus);
        }
    }

    /// Quyền sở hữu một WorkQueue job. Release không thể vô tình nhả job đã bị chủ quán cướp.
    public sealed class StaffJobLease
    {
        private readonly object worker;
        private WorkJob job;

        public StaffJobLease(object worker)
        {
            this.worker = worker;
        }

        public WorkJob Job { get { return job; } }
        public bool StillOwned { get { return job != null && !job.finished && job.claimedBy == worker; } }

        public bool TryClaim(WorkJob candidate)
        {
            Release();
            if (!WorkQueue.Claim(candidate, worker)) return false;
            job = candidate;
            return true;
        }

        public void Release()
        {
            if (job != null) WorkQueue.Cancel(job, worker);
            job = null;
        }

        public void Complete()
        {
            if (StillOwned) WorkQueue.Complete(job);
            job = null;
        }
    }

    /// Nhân viên ngoài thế giới: dọn/rửa từ WorkQueue hoặc mang ly đã pha tới khách.
    public sealed class StaffWorker : MonoBehaviour
    {
        private enum WorkerState { Idle, Moving, Working }

        private GameBootstrap game;
        private StaffSave data;
        private StaffRole role;
        private CustomerVisual visual;
        private NavMeshAgent agent;
        private StaffJobLease lease;
        private WorkerState state;
        private Customer serveTarget;
        private Vector3 target;
        private float stateTime;
        private float retryAt;

        // Pha chế
        private enum BrewPhase { Idle, Brewing, Delivering, Returning }
        private BrewPhase brewPhase;
        private Order brewOrder;
        private bool holdsCup;
        private float brewElapsed;
        private float brewTotal;
        private int brewQuality;
        private Vector3 home;

        // Phục vụ: ly đang cầm đi giao
        private Order carriedOrder;
        private int carriedQuality;

        public StaffSave Data { get { return data; } }
        public StaffRole Role { get { return role; } }
        /// Số việc đã xong từ đầu ngày (dọn bàn / rửa ly / mang ly / pha ly): để chẩn đoán và thống kê.
        public int JobsDoneToday { get; private set; }
        /// Đơn Pha chế đang làm (null khi rảnh).
        public Order BrewingOrder { get { return brewOrder; } }

        public static StaffWorker Create(GameBootstrap game, StaffSave data, int index)
        {
            var go = new GameObject("Staff_" + data.role + "_" + index);
            var worker = go.AddComponent<StaffWorker>();
            worker.Init(game, data, index);
            return worker;
        }

        private void Init(GameBootstrap g, StaffSave saved, int index)
        {
            game = g;
            data = saved;
            if (!StaffRoles.TryParse(saved.role, out role)) role = StaffRole.Cleaner;
            lease = new StaffJobLease(this);

            Vector3 start = g.Owner != null ? g.Owner.CartPoint : g.SpawnPosition;
            start += new Vector3(0.35f * index, 0f, 0.35f);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(start, out hit, 3f, NavMesh.AllAreas)) start = hit.position;
            transform.position = start;
            home = start;

            visual = gameObject.AddComponent<CustomerVisual>();
            GameObject prefab = null;
            if (g.staffPrefabs != null && g.staffPrefabs.Length > 0)
                prefab = g.staffPrefabs[Mathf.Clamp(saved.preset, 0, g.staffPrefabs.Length - 1)];
            visual.Build(prefab, g.toonMaterial);

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.radius = 0.28f;
            col.height = 1.2f;
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = NavMeshBaker.AgentRadius;
            agent.height = 1.2f;
            agent.speed = StaffWorkerRules.MoveSpeed(saved.level);
            agent.acceleration = 10f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.08f;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            state = WorkerState.Idle;
        }

        private void Update()
        {
            if (game == null || game.Day == null || game.Day.CurrentPhase != DayCycle.Phase.Open)
            {
                StopWork();
                TickVisual();
                return;
            }

            stateTime += Time.deltaTime;
            if (role == StaffRole.Cleaner) UpdateCleaner();
            else if (role == StaffRole.Brewer) UpdateBrewer();
            else UpdateServer();
            TickVisual();
        }

        private void UpdateCleaner()
        {
            if (state != WorkerState.Idle && !lease.StillOwned)
            {
                StopWork();
                return;
            }
            if (state == WorkerState.Idle)
            {
                if (Time.time < retryAt) return;
                WorkJob job = WorkQueue.PeekNearest(WorkJobType.CleanTable, transform.position, this);
                if (job == null) job = WorkQueue.PeekNearest(WorkJobType.WashCup, transform.position, this);
                if (job == null) job = WorkQueue.PeekNearest(WorkJobType.MopFloor, transform.position, this); // sàn bẩn: ưu tiên thấp nhất
                if (job == null || !lease.TryClaim(job)) { retryAt = Time.time + 0.5f; return; }

                if (job.type == WorkJobType.MopFloor)
                {
                    // Lau sàn tại chỗ (như chủ quán), không cần đi tới đâu
                    if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
                    BeginState(WorkerState.Working);
                    return;
                }

                bool found;
                if (job.type == WorkJobType.CleanTable)
                    found = job.table != null && job.table.IsDirty && job.table.TryGetApproachPoint(transform.position, out target);
                else
                    found = CleaningSystem.Instance != null && CleaningSystem.Instance.TryGetSinkApproach(transform.position, out target);
                if (!found || !MoveTo(target)) { StopWork(); retryAt = Time.time + 1f; }
                else BeginState(WorkerState.Moving);
                return;
            }
            if (state == WorkerState.Moving)
            {
                if (!ReachedTarget()) return;
                agent.isStopped = true;
                BeginState(WorkerState.Working);
                return;
            }

            WorkJob current = lease.Job;
            if (current != null && current.type == WorkJobType.CleanTable && current.table != null)
                current.table.MarkCleaning();
            float seconds;
            if (current != null && current.type == WorkJobType.WashCup) seconds = GameBalance.WashSecondsPerCup;
            else if (current != null && current.type == WorkJobType.MopFloor) seconds = StaffWorkerRules.MopSeconds(data.level);
            else seconds = StaffWorkerRules.CleanSeconds(data.level);
            if (stateTime < seconds) return;

            if (current != null && current.type == WorkJobType.CleanTable && current.table != null)
            {
                int cups = current.table.FinishClean(4);
                if (cups > 0 && CleaningSystem.Instance != null) CleaningSystem.Instance.DropAtSink(cups);
                lease.Complete();
                JobsDoneToday++;
            }
            else if (current != null && current.type == WorkJobType.MopFloor)
            {
                CleaningSystem cs = CleaningSystem.Instance;
                if (cs != null && cs.Model != null) cs.Model.Mop();
                lease.Complete();
                JobsDoneToday++;
                UiFx.FloatText(transform.position + Vector3.up * 1.7f, Strings.FloorSparkling, HudBuilder.Mint);
            }
            else if (current != null && CleaningSystem.Instance != null)
            {
                CleaningSystem.Instance.WashOne(current);
                lease.Complete();
                JobsDoneToday++;
            }
            StopWork();
        }

        private void UpdateServer()
        {
            if (state == WorkerState.Idle)
            {
                if (Time.time < retryAt || game.Station == null || game.Station.State != BrewStation.Phase.Ready) return;
                if (game.Owner != null && game.Owner.IsDelivering) return;
                // Station.Selected đã bị xóa khi pha xong (BrewStation.Finish), nên chọn khách theo món đang cầm.
                Order pick = OrderPicker.NextForServer(game.Orders.Items, game.Station.Recipe, IsWaiting);
                Customer customer = pick != null ? pick.customer : null;
                Vector3 point;
                if (customer == null || customer.Seat == null ||
                    !customer.Seat.TryGetApproachPoint(transform.position, out point))
                {
                    retryAt = Time.time + 0.5f;
                    return;
                }
                // Cầm ly đi ngay: quầy rảnh để chủ quán pha ly kế. Giữ chỗ đơn để Pha chế không làm trùng.
                if (!pick.TryClaim(this)) { retryAt = Time.time + 0.5f; return; }
                int quality = game.Station.TakeReadyDrink(pick.recipe);
                if (quality <= 0) { pick.Release(this); retryAt = Time.time + 0.5f; return; }
                carriedOrder = pick;
                carriedQuality = quality;
                GameEvents.RaiseOrderClaimChanged(pick);
                serveTarget = customer;
                visual.SetCarry(true);
                if (!MoveTo(point)) { StopWork(); retryAt = Time.time + 1f; }
                else BeginState(WorkerState.Moving);
                return;
            }
            if (carriedOrder == null || serveTarget == null || !IsWaiting(carriedOrder) ||
                !ReferenceEquals(serveTarget.CurrentOrder, carriedOrder))
            {
                StopWork(); // khách bỏ đi giữa đường: ly bỏ, ly sạch về kho
                return;
            }
            if (state == WorkerState.Moving && ReachedTarget())
            {
                if (serveTarget.Serve(carriedQuality))
                {
                    carriedQuality = 0; // ly sang tay khách
                    JobsDoneToday++;
                }
                StopWork();
            }
        }

        /// Bỏ ly đang cầm (nếu còn) và nhả đơn đã giữ.
        private void AbortCarry()
        {
            if (carriedOrder != null)
            {
                Order o = carriedOrder;
                carriedOrder = null;
                o.Release(this);
                if (game != null) GameEvents.RaiseOrderClaimChanged(o);
            }
            if (carriedQuality > 0)
            {
                carriedQuality = 0;
                if (game != null && game.Stock != null) game.Stock.Return();
            }
        }

        private static bool IsWaiting(Order o)
        {
            return o != null && o.customer != null && o.customer.IsWaitingForDrink && o.customer.Seat != null;
        }

        // ------------------------------------------------------------------ Pha chế

        /// Nhận đơn khách đang chờ -> pha tại quầy (mất nguyên liệu + 1 ly sạch như chủ quán) -> mang tới khách -> về quầy.
        private void UpdateBrewer()
        {
            switch (brewPhase)
            {
                case BrewPhase.Idle: TryStartBrew(); break;
                case BrewPhase.Brewing: TickBrewing(); break;
                case BrewPhase.Delivering: TickDelivering(); break;
                case BrewPhase.Returning: TickReturning(); break;
            }
        }

        private void TryStartBrew()
        {
            if (Time.time < retryAt || game.Orders == null || game.Station == null) return;
            BrewStation st = game.Station;
            Order pick = OrderPicker.NextForBrewer(game.Orders.Items, st.Selected,
                st.State == BrewStation.Phase.Ready ? st.Recipe : null, IsWaiting);
            if (pick == null || pick.recipe == null) { retryAt = Time.time + 0.4f; return; }

            RecipeDef recipe = pick.recipe;
            if (game.Wallet.Xu < recipe.ingredientCost || !game.Stock.TryTake()) { retryAt = Time.time + 1f; return; }
            if (!pick.TryClaim(this)) { game.Stock.Return(); retryAt = Time.time + 0.5f; return; }

            holdsCup = true;
            game.Wallet.Spend(recipe.ingredientCost);
            if (game.Day.Summary != null) game.Day.Summary.ingredientCost += recipe.ingredientCost;

            brewOrder = pick;
            brewElapsed = 0f;
            brewTotal = BrewerRules.BrewSeconds(recipe.brewSeconds, data.level);
            brewQuality = BrewerRules.Quality(data.level, game.Recipes == null || game.Recipes.ToolAllowsThreeStars(recipe));
            brewPhase = BrewPhase.Brewing;
            GameEvents.RaiseOrderClaimChanged(pick);
        }

        private void TickBrewing()
        {
            if (!IsWaiting(brewOrder) || !ReferenceEquals(brewOrder.customer.CurrentOrder, brewOrder))
            {
                StopWork(); // khách bỏ đi giữa chừng: mất nguyên liệu, ly sạch về kho
                return;
            }
            brewElapsed += Time.deltaTime;
            if (brewElapsed < brewTotal) return;

            Vector3 point;
            Customer c = brewOrder.customer;
            if (c.Seat == null || !c.Seat.TryGetApproachPoint(transform.position, out point) || !MoveTo(point))
            {
                StopWork();
                retryAt = Time.time + 1f;
                return;
            }
            visual.SetCarry(true);
            brewPhase = BrewPhase.Delivering;
            BeginState(WorkerState.Moving);
        }

        private void TickDelivering()
        {
            if (!IsWaiting(brewOrder) || !ReferenceEquals(brewOrder.customer.CurrentOrder, brewOrder))
            {
                StopWork();
                return;
            }
            if (!ReachedTarget()) return;

            Customer c = brewOrder.customer;
            if (c.Serve(brewQuality))
            {
                holdsCup = false; // ly đã sang tay khách (thành ly bẩn sau khi uống)
                JobsDoneToday++;
                ReleaseBrewClaim();
                visual.SetCarry(false);
                if (MoveTo(home)) { brewPhase = BrewPhase.Returning; BeginState(WorkerState.Moving); }
                else StopWork();
            }
            else
            {
                StopWork();
            }
        }

        private void TickReturning()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) { StopWork(); return; }
            bool arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;
            if (arrived || stateTime > 12f)
            {
                brewPhase = BrewPhase.Idle;
                agent.isStopped = true;
                agent.ResetPath();
                BeginState(WorkerState.Idle);
            }
        }

        private void ReleaseBrewClaim()
        {
            if (brewOrder != null)
            {
                Order o = brewOrder;
                brewOrder = null;
                o.Release(this);
                if (game != null) GameEvents.RaiseOrderClaimChanged(o);
            }
        }

        /// Hủy việc pha dở: nhả đơn, trả ly sạch (nguyên liệu đã dùng thì mất).
        private void AbortBrew()
        {
            ReleaseBrewClaim();
            if (holdsCup)
            {
                holdsCup = false;
                if (game != null && game.Stock != null) game.Stock.Return();
            }
            brewPhase = BrewPhase.Idle;
            brewElapsed = 0f;
        }

        private bool MoveTo(Vector3 point)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            agent.isStopped = false;
            return agent.SetDestination(point);
        }

        private bool ReachedTarget()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) { StopWork(); return false; }
            if (stateTime > 25f || (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete))
            {
                StopWork();
                retryAt = Time.time + 1f;
                return false;
            }
            return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.12f;
        }

        private void BeginState(WorkerState next)
        {
            state = next;
            stateTime = 0f;
        }

        public void ResetForNewDay()
        {
            StopWork();
            JobsDoneToday = 0;
            Vector3 start = game != null && game.Owner != null ? game.Owner.CartPoint : transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(start, out hit, 3f, NavMesh.AllAreas) && agent != null && agent.enabled)
                agent.Warp(hit.position);
        }

        public void StopWork()
        {
            if (lease != null) lease.Release();
            AbortBrew();
            AbortCarry();
            serveTarget = null;
            if (visual != null) visual.SetCarry(false);
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            state = WorkerState.Idle;
            stateTime = 0f;
        }

        private void TickVisual()
        {
            if (visual == null) return;
            float speed = agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            visual.Tick(speed, false, Time.deltaTime);
        }

        private void OnDisable()
        {
            if (lease != null) lease.Release();
            AbortBrew();
            AbortCarry();
        }
    }
}
