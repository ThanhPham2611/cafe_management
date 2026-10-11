using System;
using UnityEngine;

namespace Cafe
{
    /// Chỉ số Sạch sẽ (CI) của quán - docs/economy_monetization.md §5 (một zone duy nhất ở T2):
    ///   CI = 100 - 40·(bàn_bẩn / tổng_bàn) - 0,4·FloorDirt - 5·(số bàn bẩn quá 26 s thực)      (kẹp 0..100)
    ///   FloorDirt += 0,6 mỗi khách phục vụ; -= 8 mỗi lần lau sàn; sang ngày mới: max(0, FloorDirt - 10).
    /// Hệ quả: C của S = CI/100; walk-away +15% mỗi 10 CI dưới 60; tip ×0,5 dưới 60; -1 RP mỗi giờ in-game khi CI < 40.
    /// Mô hình thuần dữ liệu; CleaningSystem gọi Tick() ~4 lần/giây.
    public class CleanlinessModel
    {
        private readonly GameBootstrap game;

        public float FloorDirt { get; private set; }
        public float CI { get; private set; }
        public int DirtyTables { get; private set; }
        public int OldTables { get; private set; }
        public int TotalTables { get; private set; }

        /// CI trung bình trong ngày (chỉ tính lúc mở cửa / đóng cửa, khi luật dọn bật).
        public float AverageCI { get { return avgTime > 0.01f ? avgSum / avgTime : CI; } }

        /// Con số đổi (CI làm tròn, số bàn bẩn, FloorDirt, bật/tắt luật): HUD nghe để vẽ lại.
        public event Action Changed;

        private float avgSum;
        private float avgTime;
        private float lowCiInGameSeconds;
        private int shownCi = -1;
        private int shownDirty = -1;
        private int shownFloor = -1;
        private bool shownEnabled;

        public CleanlinessModel(GameBootstrap g)
        {
            game = g;
            CI = 100f;
        }

        // ------------------------------------------------------------------ truy vấn

        /// Xác suất khách bỏ đi khi tới ghế: +15% mỗi 10 CI dưới 60 (liên tục).
        public float WalkAwayChance
        {
            get
            {
                if (CI >= GameBalance.CiWalkAwayBelow) return 0f;
                return Mathf.Clamp01(GameBalance.CiWalkAwayPer10 * (GameBalance.CiWalkAwayBelow - CI) / 10f);
            }
        }

        /// Hệ số tip: ×0,5 khi CI < 60.
        public float TipMultiplier
        {
            get { return CI < GameBalance.CiWalkAwayBelow ? GameBalance.CiTipMultiplier : 1f; }
        }

        /// Hiện nút "Lau sàn" khi sàn đủ bẩn.
        public bool WantsMop
        {
            get { return CleaningRules.Enabled && FloorDirt > GameBalance.FloorDirtMopChip; }
        }

        // ------------------------------------------------------------------ sự kiện

        public void AddServedCustomer()
        {
            if (!CleaningRules.Enabled) return;
            FloorDirt += GameBalance.FloorDirtPerCustomer;
            Recompute(0f);
        }

        /// Lau sàn xong (chủ quán/nhân viên).
        public void Mop()
        {
            FloorDirt = Mathf.Max(0f, FloorDirt - GameBalance.MopFloorDirtReduce);
            Recompute(0f);
        }

        public void OnNewDay()
        {
            FloorDirt = Mathf.Max(0f, FloorDirt - GameBalance.FloorDirtDailyDecay);
            avgSum = 0f;
            avgTime = 0f;
            lowCiInGameSeconds = 0f;
            Recompute(0f);
        }

        // ------------------------------------------------------------------ cập nhật

        /// dt = giây (thời gian game) kể từ lần gọi trước.
        public void Tick(float dt)
        {
            Recompute(dt);
        }

        private void Recompute(float dt)
        {
            bool enabled = CleaningRules.Enabled;
            int total = 0, dirty = 0, old = 0;
            if (game != null && game.Grid != null)
            {
                var tables = game.Grid.Tables;
                total = tables.Count;
                for (int i = 0; i < tables.Count; i++)
                {
                    PlacedFurniture t = tables[i];
                    TableDirt td = t != null ? t.Dirt : null;
                    if (td == null || !td.IsDirty) continue;
                    dirty++;
                    if (td.IsOld) old++;
                }
            }
            TotalTables = total;
            DirtyTables = dirty;
            OldTables = old;

            float ci = 100f;
            if (enabled)
            {
                float ratio = total > 0 ? (float)dirty / total : 0f;
                ci = 100f
                     - GameBalance.CiDirtyTableWeight * ratio
                     - GameBalance.CiFloorDirtWeight * FloorDirt
                     - GameBalance.OldTableCiPenalty * old;
                ci = Mathf.Clamp(ci, 0f, 100f);
            }
            CI = ci;

            if (enabled && dt > 0f && game != null && game.Day != null)
            {
                DayCycle.Phase ph = game.Day.CurrentPhase;
                if (ph == DayCycle.Phase.Open || ph == DayCycle.Phase.Closing)
                {
                    avgSum += CI * dt;
                    avgTime += dt;
                    if (game.Day.Summary != null) game.Day.Summary.cleanliness = AverageCI;

                    // -1 RP mỗi giờ in-game khi CI < 40
                    if (CI < GameBalance.CiRpPenaltyBelow)
                    {
                        lowCiInGameSeconds += dt * GameBalance.InGameSecondsPerRealSecond;
                        while (lowCiInGameSeconds >= 3600f)
                        {
                            lowCiInGameSeconds -= 3600f;
                            if (game.Reputation != null)
                            {
                                float rp = game.Reputation.Add(-GameBalance.CiRpPenaltyPerInGameHour);
                                if (game.Day.Summary != null) game.Day.Summary.rpGained += rp;
                            }
                        }
                    }
                }
            }

            // Job lau sàn mở khi sàn bẩn, gỡ khi sàn đã sạch (chưa ai nhận)
            if (enabled)
            {
                if (FloorDirt > GameBalance.FloorDirtMopChip) WorkQueue.PostMopFloor();
                else
                {
                    WorkJob mj = WorkQueue.Peek(WorkJobType.MopFloor, null);
                    if (mj != null) WorkQueue.Discard(mj);
                }
            }

            int ci10 = Mathf.RoundToInt(CI);
            int fl = Mathf.RoundToInt(FloorDirt);
            if (ci10 != shownCi || dirty != shownDirty || fl != shownFloor || enabled != shownEnabled)
            {
                shownCi = ci10;
                shownDirty = dirty;
                shownFloor = fl;
                shownEnabled = enabled;
                if (Changed != null) Changed();
            }
        }
    }
}
