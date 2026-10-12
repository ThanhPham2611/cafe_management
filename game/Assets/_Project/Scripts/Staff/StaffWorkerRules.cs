using UnityEngine;

namespace Cafe
{
    /// Công thức tốc độ của nhân viên theo cấp kỹ năng 1..5 (economy §5–6).
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
}
