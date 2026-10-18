using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Sinh khách trong ngày: min(Demand_T1 × (1 + Σattract), sức chứa), rải đều có nhiễu, dừng gần giờ đóng cửa.
    public class CustomerSpawner : MonoBehaviour
    {
        private GameBootstrap game;
        private readonly List<Customer> active = new List<Customer>();
        private float[] schedule = new float[0];
        private int next;
        private float forceTimer;
        private int curDay;
        private int curStars;
        private RegularDefinition scheduledRegular;
        private float delay;          // thời gian lịch đã bị dời vì nghẽn
        private float lastSpawnClock = -100f;

        /// Bot tắt để so sánh A/B nhịp khách.
        public static bool ThrottleEnabled = true;

        public int ActiveCount { get { return active.Count; } }
        public int PlannedCount { get { return schedule.Length; } }
        public int SpawnedCount { get { return next; } }
        public IList<Customer> Active { get { return active; } }

        public void Init(GameBootstrap g)
        {
            game = g;
            GameEvents.CustomerLeft += OnCustomerLeft;
        }

        private void OnDestroy()
        {
            GameEvents.CustomerLeft -= OnCustomerLeft;
        }

        private int TargetCount(int seatCount)
        {
            if (seatCount <= 0) return 0;
            int attract = game != null && game.Grid != null ? ShopRules.AttractSum(game.Grid) : 0;
            // Cấp 1: DemandWithAttract(★, ngày) và ghế x 4 (y như cũ); cấp 2+: nhu cầu x1,5, sức chứa chỗ x 4,5 x (1 + 0,25·NV)
            int tier = game != null && game.Tier != null ? game.Tier.Current : 1;
            int staff = game != null && game.Tier != null ? game.Tier.StaffCount : 0;
            float zoneMult = game != null && game.Tier != null ? game.Tier.ZoneDemandMult : 1f;
            int demand = GameBalance.DemandWithAttract(tier, curStars, curDay, attract, zoneMult);
            if (game != null) demand = Mathf.Max(1, Mathf.RoundToInt(demand * game.TodayEvent.demand)); // sự kiện của ngày (DayEvents)
            return Mathf.Max(1, Mathf.Min(demand, GameBalance.CapacityFor(tier, seatCount, staff)));
        }

        /// Rải schedule[from..] đều trong [start, end] có nhiễu.
        private void Distribute(int from, float start, float end)
        {
            int n = schedule.Length - from;
            if (n <= 0) return;
            float span = end - start;
            float len = game.Day.Length;
            float w0 = GameBalance.SpawnStartFraction * len;
            float w1 = GameBalance.SpawnEndFraction * len;
            float xs = (start - w0) / (w1 - w0);
            float xe = (end - w0) / (w1 - w0);
            for (int i = 0; i < n; i++)
            {
                float jitter = Random.Range(-0.4f, 0.4f);
                float u = Mathf.Clamp01((i + 0.5f + jitter) / n);
                schedule[from + i] = w0 + ArrivalCurve.Map(u, xs, xe) * (w1 - w0);
            }
            System.Array.Sort(schedule, from, n);
        }

        public void BeginDay(int day, int stars, int seatCount)
        {
            curDay = day;
            curStars = stars;
            game.TopUpCups();
            int count = TargetCount(seatCount);

            schedule = new float[count];
            float len = game.Day.Length;
            Distribute(0, GameBalance.SpawnStartFraction * len, GameBalance.SpawnEndFraction * len);
            if (count > 0 && day == 1) schedule[0] = Mathf.Min(schedule[0], 2f); // FTUE: khách đầu tiên đến sớm

            next = 0;
            delay = 0f;
            lastSpawnClock = -100f;
            forceTimer = 0f;
            active.Clear();
            scheduledRegular = null;
            // Hướng dẫn ngày 1: chưa xếp khách quen (món của họ không bị ép)
            if (game.Regulars != null && !(TutorialGate.Active && day <= 1)) game.Regulars.TryScheduleVisit(day, game.Tier.Current, out scheduledRegular);
        }

        /// Co giãn thời điểm sinh của các khách chưa tới (schedule[from..]) theo tỉ lệ ratio.
        public static void RescaleFrom(float[] schedule, int from, float ratio)
        {
            if (schedule == null || ratio <= 0f) return;
            for (int i = Mathf.Max(0, from); i < schedule.Length; i++) schedule[i] *= ratio;
        }

        /// Lên cấp giữa ngày làm đổi độ dài ngày: dời các lượt khách chưa tới theo cùng tỉ lệ để ngày không bị vắng / dồn cục.
        public void RescaleRemaining(float ratio)
        {
            RescaleFrom(schedule, next, ratio);
        }

        /// Gọi khi bố cục quán đổi giữa ngày: tính lại số khách còn lại theo ghế + đồ hút khách mới.
        public void RefreshDemand()
        {
            if (game != null) game.TopUpCups(); // mua thêm ghế giữa ngày => thêm ly ngay
            if (game == null || game.Day == null || game.Grid == null || curDay <= 0) return;
            if (game.Day.CurrentPhase != DayCycle.Phase.Open) return;

            int target = Mathf.Max(next, TargetCount(game.Grid.Seats.Count));
            if (target == schedule.Length) return;

            float len = game.Day.Length;
            float end = GameBalance.SpawnEndFraction * len;
            float start = Mathf.Max(game.Day.Elapsed + 1f, GameBalance.SpawnStartFraction * len);
            if (start >= end) target = Mathf.Min(target, schedule.Length); // hết giờ rải: chỉ cho phép giảm
            if (target == schedule.Length) return;

            var arr = new float[target];
            int keep = Mathf.Min(next, schedule.Length);
            System.Array.Copy(schedule, arr, keep);
            schedule = arr;
            Distribute(keep, start, end);
        }

        private void Update()
        {
            if (game == null || game.Day == null) return;

            if (game.Day.CurrentPhase == DayCycle.Phase.Open)
            {
                float t = game.Day.Elapsed;
                // Chặn sinh khách chỉ ở cấp 1-2 (chủ quán pha một mình): ở cấp 3 bot đo kém đi (economy §24.3)
                bool throttled = ThrottleEnabled && !TutorialGate.Active && game.Tier != null && game.Tier.Current < 3;
                if (throttled && next < schedule.Length && schedule[next] <= t - delay)
                {
                    // Quán nghẽn: khách tiếp theo chờ ngoài cửa (dời lịch, tối đa ThrottleMaxDelay)
                    if (delay < GameBalance.ThrottleMaxDelay && IsCongested())
                    {
                        delay = Mathf.Min(GameBalance.ThrottleMaxDelay, delay + Time.deltaTime);
                        return;
                    }
                }
                while (next < schedule.Length && TutorialGate.SpawnIsDue(next, schedule[next], t - (throttled ? delay : 0f)))
                {
                    if (throttled && t - lastSpawnClock < GameBalance.MinSpawnGapSeconds) break;
                    Spawn();
                    lastSpawnClock = t;
                    next++;
                }
            }
            else if (game.Day.CurrentPhase == DayCycle.Phase.Closing && game.Day.ClosingSeconds > 30f)
            {
                forceTimer -= Time.deltaTime;
                if (forceTimer <= 0f)
                {
                    forceTimer = 1f;
                    ForceAllLeave();
                }
            }
        }

        private bool IsCongested()
        {
            int seatWait = 0, service = 0;
            for (int i = 0; i < active.Count; i++)
            {
                Customer c = active[i];
                if (c == null) continue;
                if (c.State == CustomerState.FindSeat) seatWait++;
                else if (c.IsWaitingForDrink) service++;
            }
            return SpawnThrottle.ShouldHold(seatWait, service, game.Grid != null ? game.Grid.Seats.Count : 0);
        }

        private void Spawn()
        {
            using var perf = PerfNote.Measure("sinh khách");
            var go = new GameObject("Customer");
            var c = go.AddComponent<Customer>();
            RegularDefinition regular = scheduledRegular;
            scheduledRegular = null;
            c.Init(game, regular);
            active.Add(c);
            Audio.Play(Sfx.DoorChime, 0.6f, false);
        }

        /// Hướng dẫn: thả thêm một khách ngoài lịch (khách kịch bản đã bỏ về). Không đụng tới hạn mức của TutorialGate.
        public void SpawnExtra()
        {
            Spawn();
            next++;
        }

        public void ForceAllLeave()
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i] != null) active[i].ForceLeave();
        }

        /// Xóa ngay mọi khách đang có mặt (chuyển cấp quán, khi màn hình đã tối). Không tính bỏ đi / RP.
        public void PurgeAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Customer c = active[i];
                if (c != null)
                {
                    c.gameObject.SetActive(false);
                    Destroy(c.gameObject);
                }
            }
            active.Clear();
        }

        private void OnCustomerLeft(Customer c)
        {
            active.Remove(c);
        }
    }
}
