using UnityEngine;
using UnityEngine.AI;

namespace Cafe
{
    /// Một vai trò của nhân viên (dọn dẹp / phục vụ / pha chế): máy trạng thái riêng, dùng chung thân nhân vật StaffWorker
    /// (NavMeshAgent, hoạt ảnh). StaffWorker chỉ gọi Tick mỗi khung khi quán đang mở và Stop khi phải dừng việc.
    public abstract class StaffBehaviour
    {
        protected enum WorkerState { Idle, Moving, Working }

        protected readonly StaffWorker worker;
        protected WorkerState state;
        protected float stateTime;
        protected float retryAt;

        protected StaffBehaviour(StaffWorker worker)
        {
            this.worker = worker;
        }

        /// Số việc đã xong từ đầu ngày (để chẩn đoán và thống kê).
        public int JobsDone { get; private set; }

        protected GameBootstrap Game { get { return worker.Game; } }
        protected StaffSave Data { get { return worker.Data; } }

        public void Tick(float dt)
        {
            stateTime += dt;
            OnTick();
        }

        protected abstract void OnTick();

        /// Nhả mọi thứ đang giữ (việc, đơn, ly sạch). Gọi khi dừng việc hoặc khi nhân viên bị gỡ khỏi quán.
        public virtual void Release() { }

        /// Dừng việc đang làm và về trạng thái rảnh.
        public void Stop()
        {
            Release();
            state = WorkerState.Idle;
            stateTime = 0f;
        }

        public void ResetJobs()
        {
            JobsDone = 0;
        }

        protected void CountJob()
        {
            JobsDone++;
        }

        protected bool MoveTo(Vector3 point)
        {
            return worker.MoveTo(point);
        }

        /// Đã tới đích chưa. Hết thời gian / đường đi hỏng thì tự dừng việc và trả false.
        protected bool ReachedTarget()
        {
            NavMeshAgent agent = worker.Agent;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) { worker.StopWork(); return false; }
            if (stateTime > 25f || (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete))
            {
                worker.StopWork();
                retryAt = Time.time + 1f;
                return false;
            }
            return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.12f;
        }

        protected void BeginState(WorkerState next)
        {
            state = next;
            stateTime = 0f;
        }

        /// Khách còn ngồi chờ món và có ghế.
        protected static bool IsWaiting(Order o)
        {
            return o != null && o.customer != null && o.customer.IsWaitingForDrink && o.customer.Seat != null;
        }
    }
}
