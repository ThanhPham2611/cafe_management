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

        public StaffSave Data { get { return data; } }
        public StaffRole Role { get { return role; } }

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
                if (job == null || !lease.TryClaim(job)) { retryAt = Time.time + 0.5f; return; }

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
            float seconds = current != null && current.type == WorkJobType.WashCup
                ? GameBalance.WashSecondsPerCup
                : StaffWorkerRules.CleanSeconds(data.level);
            if (stateTime < seconds) return;

            if (current != null && current.type == WorkJobType.CleanTable && current.table != null)
            {
                int cups = current.table.FinishClean(4);
                if (cups > 0 && CleaningSystem.Instance != null) CleaningSystem.Instance.DropAtSink(cups);
                lease.Complete();
            }
            else if (current != null && CleaningSystem.Instance != null)
            {
                CleaningSystem.Instance.WashOne(current);
                lease.Complete();
            }
            StopWork();
        }

        private void UpdateServer()
        {
            if (state == WorkerState.Idle)
            {
                if (Time.time < retryAt || game.Station == null || game.Station.State != BrewStation.Phase.Ready) return;
                if (game.Owner != null && game.Owner.IsDelivering) return;
                Order selected = game.Station.Selected;
                Customer customer = selected != null ? selected.customer : null;
                Vector3 point;
                if (customer == null || !customer.IsWaitingForDrink || customer.Seat == null ||
                    !customer.Seat.TryGetApproachPoint(transform.position, out point))
                {
                    retryAt = Time.time + 0.5f;
                    return;
                }
                serveTarget = customer;
                visual.SetCarry(true);
                if (!MoveTo(point)) { StopWork(); retryAt = Time.time + 1f; }
                else BeginState(WorkerState.Moving);
                return;
            }
            if (serveTarget == null || !serveTarget.IsWaitingForDrink || game.Station.State != BrewStation.Phase.Ready)
            {
                StopWork();
                return;
            }
            if (state == WorkerState.Moving && ReachedTarget())
            {
                game.Station.TryServe(serveTarget);
                StopWork();
            }
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
            Vector3 start = game != null && game.Owner != null ? game.Owner.CartPoint : transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(start, out hit, 3f, NavMesh.AllAreas) && agent != null && agent.enabled)
                agent.Warp(hit.position);
        }

        public void StopWork()
        {
            if (lease != null) lease.Release();
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
        }
    }
}
