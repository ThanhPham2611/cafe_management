using UnityEngine;

namespace Cafe
{
    /// Nhân viên Dọn dẹp: nhận việc từ WorkQueue theo thứ tự lau bàn > rửa ly > lau sàn (lau sàn ngay tại chỗ).
    /// Chủ quán luôn cướp được việc (WorkQueue.Claim steal): khi bị cướp thì nhả việc và quay về rảnh.
    public sealed class CleanerBehaviour : StaffBehaviour
    {
        private readonly StaffJobLease lease;
        private Vector3 target;

        public CleanerBehaviour(StaffWorker worker) : base(worker)
        {
            lease = new StaffJobLease(worker);
        }

        public override void Release()
        {
            lease.Release();
        }

        protected override void OnTick()
        {
            if (state != WorkerState.Idle && !lease.StillOwned)
            {
                worker.StopWork();
                return;
            }
            if (state == WorkerState.Idle)
            {
                StartNextJob();
                return;
            }
            if (state == WorkerState.Moving)
            {
                if (!ReachedTarget()) return;
                worker.HoldPosition();
                BeginState(WorkerState.Working);
                return;
            }
            Work();
        }

        private void StartNextJob()
        {
            if (Time.time < retryAt) return;
            Vector3 pos = worker.transform.position;
            WorkJob job = WorkQueue.PeekNearest(WorkJobType.CleanTable, pos, worker);
            if (job == null) job = WorkQueue.PeekNearest(WorkJobType.WashCup, pos, worker);
            if (job == null) job = WorkQueue.PeekNearest(WorkJobType.MopFloor, pos, worker); // sàn bẩn: ưu tiên thấp nhất
            if (job == null || !lease.TryClaim(job)) { retryAt = Time.time + 0.5f; return; }

            if (job.type == WorkJobType.MopFloor)
            {
                // Lau sàn tại chỗ (như chủ quán), không cần đi tới đâu
                worker.HoldPosition();
                BeginState(WorkerState.Working);
                return;
            }

            bool found;
            if (job.type == WorkJobType.CleanTable)
                found = job.table != null && job.table.IsDirty && job.table.TryGetApproachPoint(pos, out target);
            else
                found = CleaningSystem.Instance != null && CleaningSystem.Instance.TryGetSinkApproach(pos, out target);
            if (!found || !MoveTo(target)) { worker.StopWork(); retryAt = Time.time + 1f; }
            else BeginState(WorkerState.Moving);
        }

        private void Work()
        {
            WorkJob current = lease.Job;
            if (current != null && current.type == WorkJobType.CleanTable && current.table != null)
                current.table.MarkCleaning();
            if (stateTime < SecondsFor(current)) return;

            CleaningSystem cs = CleaningSystem.Instance;
            if (current != null && current.type == WorkJobType.CleanTable && current.table != null)
            {
                int cups = current.table.FinishClean(4);
                if (cups > 0 && cs != null) cs.DropAtSink(cups);
                CompleteJob();
            }
            else if (current != null && current.type == WorkJobType.MopFloor)
            {
                if (cs != null && cs.Model != null) cs.Model.Mop();
                CompleteJob();
                UiFx.FloatText(worker.transform.position + Vector3.up * 1.7f, Strings.FloorSparkling, HudBuilder.Mint);
            }
            else if (current != null && cs != null)
            {
                cs.WashOne(current);
                CompleteJob();
            }
            worker.StopWork();
        }

        private float SecondsFor(WorkJob job)
        {
            if (job != null && job.type == WorkJobType.WashCup) return GameBalance.WashSecondsPerCup;
            if (job != null && job.type == WorkJobType.MopFloor) return StaffWorkerRules.MopSeconds(Data.level);
            return StaffWorkerRules.CleanSeconds(Data.level);
        }

        private void CompleteJob()
        {
            lease.Complete();
            CountJob();
        }
    }
}
