using UnityEngine;
using UnityEngine.AI;

namespace Cafe
{
    /// Thân nhân vật nhân viên ngoài thế giới: vị trí, NavMeshAgent, hoạt ảnh. Việc làm cụ thể nằm ở StaffBehaviour
    /// (CleanerBehaviour / ServerBehaviour / BrewerBehaviour); quán chỉ mở cửa thì mới làm việc.
    public sealed class StaffWorker : MonoBehaviour
    {
        private StaffBehaviour behaviour;

        public GameBootstrap Game { get; private set; }
        public StaffSave Data { get; private set; }
        public StaffRole Role { get; private set; }
        public CustomerVisual Visual { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        /// Chỗ đứng chờ việc (cạnh quầy); Pha chế về đây sau mỗi ly.
        public Vector3 Home { get; private set; }
        /// Số việc đã xong từ đầu ngày: để chẩn đoán và thống kê.
        public int JobsDoneToday { get { return behaviour != null ? behaviour.JobsDone : 0; } }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string DebugState { get { return behaviour != null ? behaviour.DebugState() : ""; } }
#endif

        public static StaffWorker Create(GameBootstrap game, StaffSave data, int index)
        {
            var go = new GameObject("Staff_" + data.role + "_" + index);
            var worker = go.AddComponent<StaffWorker>();
            worker.Init(game, data, index);
            return worker;
        }

        private void Init(GameBootstrap g, StaffSave saved, int index)
        {
            Game = g;
            Data = saved;
            StaffRole role;
            if (!StaffRoles.TryParse(saved.role, out role)) role = StaffRole.Cleaner;
            Role = role;

            Vector3 start = g.Owner != null ? g.Owner.CartPoint : g.SpawnPosition;
            start += new Vector3(0.35f * index, 0f, 0.35f);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(start, out hit, 3f, NavMesh.AllAreas)) start = hit.position;
            transform.position = start;
            Home = start;

            Visual = gameObject.AddComponent<CustomerVisual>();
            GameObject prefab = null;
            if (g.staffPrefabs != null && g.staffPrefabs.Length > 0)
                prefab = g.staffPrefabs[Mathf.Clamp(saved.preset, 0, g.staffPrefabs.Length - 1)];
            Visual.Build(prefab, g.toonMaterial);

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.radius = 0.28f;
            col.height = 1.2f;
            var body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            Agent = gameObject.AddComponent<NavMeshAgent>();
            Agent.radius = NavMeshBaker.AgentRadius;
            Agent.height = 1.2f;
            Agent.speed = StaffWorkerRules.MoveSpeed(saved.level);
            Agent.acceleration = 10f;
            Agent.angularSpeed = 540f;
            Agent.stoppingDistance = 0.08f;
            Agent.autoBraking = true;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            behaviour = CreateBehaviour(role);
        }

        private StaffBehaviour CreateBehaviour(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Server: return new ServerBehaviour(this);
                case StaffRole.Brewer: return new BrewerBehaviour(this);
                default: return new CleanerBehaviour(this);
            }
        }

        private void Update()
        {
            if (Game == null || Game.Day == null || Game.Day.CurrentPhase != DayCycle.Phase.Open)
            {
                StopWork();
                TickVisual();
                return;
            }

            behaviour.Tick(Time.deltaTime);
            TickVisual();
        }

        public void ResetForNewDay()
        {
            StopWork();
            behaviour.ResetJobs();
            Vector3 start = Game != null && Game.Owner != null ? Game.Owner.CartPoint : transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(start, out hit, 3f, NavMesh.AllAreas) && Agent != null && Agent.enabled)
                Agent.Warp(hit.position);
        }

        /// Dừng mọi việc: nhả việc / đơn / ly đang giữ, đứng lại, về trạng thái rảnh.
        public void StopWork()
        {
            if (behaviour != null) behaviour.Stop();
            if (Visual != null) Visual.SetCarry(false);
            HaltAgent();
        }

        /// Đi tới điểm; false nếu không có đường.
        public bool MoveTo(Vector3 point)
        {
            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh) return false;
            Agent.isStopped = false;
            return Agent.SetDestination(point);
        }

        /// Đứng yên tại chỗ (lau sàn, tới nơi làm việc).
        public void HoldPosition()
        {
            if (Agent != null && Agent.enabled && Agent.isOnNavMesh) Agent.isStopped = true;
        }

        private void HaltAgent()
        {
            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh) return;
            Agent.isStopped = true;
            Agent.ResetPath();
        }

        private void TickVisual()
        {
            if (Visual == null) return;
            float speed = Agent != null && Agent.enabled && Agent.isOnNavMesh ? Agent.velocity.magnitude : 0f;
            Visual.Tick(speed, false, Time.deltaTime);
        }

        private void OnDisable()
        {
            if (behaviour != null) behaviour.Release(); // bị gỡ / đóng cảnh giữa chừng: trả việc, đơn, ly
        }
    }
}
