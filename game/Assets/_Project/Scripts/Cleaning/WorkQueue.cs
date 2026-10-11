using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    // =====================================================================================================
    //  WorkQueue - hàng việc dọn dẹp CHUNG (chủ quán + nhân viên tương lai cùng lấy việc từ đây).
    //
    //  Loại việc (WorkJobType):
    //    CleanTable : lau một bàn bẩn (job.table). Tự được đăng khi bàn có khách rời đi (TableDirt.AddDish).
    //    WashCup    : rửa MỘT ly bẩn đang nằm ở bồn rửa (đăng khi ai đó đặt ly bẩn xuống bồn: CleaningSystem.DropAtSink).
    //    MopFloor   : lau sàn (duy nhất một job mở; tự đăng khi FloorDirt > ngưỡng, tự huỷ khi sàn đã sạch).
    //
    //  Vòng đời một job:   Post  ->  (Peek)  ->  Claim  ->  Complete                    (thành công, job biến mất)
    //                                              \->  Cancel (bỏ dở, nhả claim, job quay lại trạng thái "mở")
    //                      Discard: job không còn hợp lệ (bàn bị bán/dọn tự động qua đêm) -> bỏ hẳn.
    //
    //  API tĩnh (đơn luồng, chỉ gọi từ main thread):
    //    WorkJob Post(type, table)              đăng job; CleanTable/MopFloor được chống trùng (trả job cũ)
    //    WorkJob PostCleanTable(table) / PostWashCup() / PostMopFloor()
    //    WorkJob Peek(type, worker)             job MỞ cũ nhất của loại này (chưa ai nhận, hoặc đã do chính worker nhận)
    //    WorkJob PeekNearest(type, from, worker) như Peek nhưng chọn job gần `from` nhất (nhân viên dùng)
    //    bool    Claim(job, worker, steal=false) nhận job. false nếu người khác đang giữ (trừ khi steal = true).
    //                                            Chủ quán dùng steal = true (tay chủ quán luôn ưu tiên);
    //                                            nhân viên bị cướp claim phải kiểm tra job.claimedBy != mình rồi bỏ.
    //    void    Complete(job)                  xong việc: gỡ job khỏi hàng
    //    void    Cancel(job, worker = null)     nhả claim (nếu worker != null thì chỉ nhả khi đúng worker đang giữ)
    //    void    Discard(job)                   gỡ job không còn hợp lệ
    //    int     Count(type, bool openOnly)     đếm job
    //    IReadOnlyList<WorkJob> Jobs            toàn bộ job (đọc)
    //    event Action Changed                   hàng đổi (thêm/gỡ/đổi claim)
    //
    //  Nhân viên (Staff agent): vòng lặp mẫu
    //      var j = WorkQueue.PeekNearest(WorkJobType.CleanTable, transform.position, this);
    //      if (j != null && WorkQueue.Claim(j, this)) { ...đi tới j.table, lau (6 s)...;  int cups = j.table.FinishClean(4);
    //                                                   ...mang ly tới bồn: CleaningSystem.Instance.DropAtSink(cups);
    //                                                   WorkQueue.Complete(j); }
    //      Rửa:  var w = WorkQueue.Peek(WorkJobType.WashCup, this); Claim -> đợi WashSecondsPerCup -> CleaningSystem.Instance.WashOne(w).
    //  Mọi job gắn claimedBy (object): chủ quán = OwnerController, nhân viên = Staff.
    // =====================================================================================================

    public enum WorkJobType { CleanTable, WashCup, MopFloor }

    public sealed class WorkJob
    {
        public readonly WorkJobType type;
        /// CleanTable: bàn cần lau.
        public readonly TableDirt table;
        /// Người đang nhận việc (OwnerController, Staff...). null = chưa ai nhận.
        public object claimedBy;
        /// Giờ (Time.time) lúc đăng việc.
        public readonly float createdAt;
        /// Đã xong hoặc đã bị gỡ.
        public bool finished;

        public bool IsClaimed { get { return claimedBy != null; } }

        public WorkJob(WorkJobType type, TableDirt table)
        {
            this.type = type;
            this.table = table;
            createdAt = Time.time;
        }

        /// Vị trí thế giới của việc (để chọn việc gần nhất).
        public Vector3 Position
        {
            get
            {
                if (table != null) return table.transform.position;
                CleaningSystem s = CleaningSystem.Instance;
                if (type == WorkJobType.WashCup && s != null && s.Sink != null) return s.Sink.transform.position;
                return Vector3.zero;
            }
        }
    }

    public static class WorkQueue
    {
        private static readonly List<WorkJob> jobs = new List<WorkJob>(16);

        public static event Action Changed;

        public static IReadOnlyList<WorkJob> Jobs { get { return jobs; } }

        // ------------------------------------------------------------------ đăng việc

        public static WorkJob Post(WorkJobType type, TableDirt table)
        {
            if (type == WorkJobType.CleanTable)
            {
                if (table == null) return null;
                for (int i = 0; i < jobs.Count; i++)
                    if (jobs[i].type == type && jobs[i].table == table) return jobs[i];
            }
            else if (type == WorkJobType.MopFloor)
            {
                for (int i = 0; i < jobs.Count; i++)
                    if (jobs[i].type == type) return jobs[i];
            }
            var j = new WorkJob(type, table);
            jobs.Add(j);
            Raise();
            return j;
        }

        public static WorkJob PostCleanTable(TableDirt table)
        {
            return Post(WorkJobType.CleanTable, table);
        }

        public static WorkJob PostWashCup()
        {
            return Post(WorkJobType.WashCup, null);
        }

        public static WorkJob PostMopFloor()
        {
            return Post(WorkJobType.MopFloor, null);
        }

        // ------------------------------------------------------------------ tìm / nhận

        private static bool IsOpenFor(WorkJob j, object worker)
        {
            return !j.finished && (j.claimedBy == null || (worker != null && j.claimedBy == worker));
        }

        /// Job MỞ cũ nhất của loại này (chưa ai nhận).
        public static WorkJob Peek(WorkJobType type)
        {
            return Peek(type, null);
        }

        public static WorkJob Peek(WorkJobType type, object worker)
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                WorkJob j = jobs[i];
                if (j.type == type && IsOpenFor(j, worker)) return j;
            }
            return null;
        }

        public static WorkJob PeekNearest(WorkJobType type, Vector3 from, object worker)
        {
            WorkJob best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < jobs.Count; i++)
            {
                WorkJob j = jobs[i];
                if (j.type != type || !IsOpenFor(j, worker)) continue;
                float d = (j.Position - from).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = j;
                }
            }
            return best;
        }

        public static bool Claim(WorkJob job, object worker, bool steal)
        {
            if (job == null || job.finished || worker == null) return false;
            if (job.claimedBy == worker) return true;
            if (job.claimedBy != null && !steal) return false;
            job.claimedBy = worker;
            Raise();
            return true;
        }

        public static bool Claim(WorkJob job, object worker)
        {
            return Claim(job, worker, false);
        }

        // ------------------------------------------------------------------ kết thúc

        public static void Complete(WorkJob job)
        {
            if (job == null || job.finished) return;
            job.finished = true;
            job.claimedBy = null;
            jobs.Remove(job);
            Raise();
        }

        public static void Cancel(WorkJob job, object worker)
        {
            if (job == null || job.finished || job.claimedBy == null) return;
            if (worker != null && job.claimedBy != worker) return;
            job.claimedBy = null;
            Raise();
        }

        public static void Cancel(WorkJob job)
        {
            Cancel(job, null);
        }

        public static void Discard(WorkJob job)
        {
            if (job == null || job.finished) return;
            job.finished = true;
            job.claimedBy = null;
            jobs.Remove(job);
            Raise();
        }

        public static void Clear()
        {
            for (int i = 0; i < jobs.Count; i++)
            {
                jobs[i].finished = true;
                jobs[i].claimedBy = null;
            }
            jobs.Clear();
            Raise();
        }

        public static int Count(WorkJobType type, bool openOnly)
        {
            int n = 0;
            for (int i = 0; i < jobs.Count; i++)
            {
                WorkJob j = jobs[i];
                if (j.type != type) continue;
                if (openOnly && j.claimedBy != null) continue;
                n++;
            }
            return n;
        }

        private static void Raise()
        {
            if (Changed != null) Changed();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            jobs.Clear();
            Changed = null;
        }
    }
}
