using System;
using System.Collections.Generic;

namespace Cafe
{
    /// Một nhiệm vụ đang chạy trong ngày.
    public struct MissionState
    {
        public string id;
        public int target;
        public int progress;
        public int rewardXu;
        public bool claimed;

        public bool IsDone { get { return progress >= target; } }
    }

    /// Nhiệm vụ ngày + rương (economy §11.1). Thuần logic: thưởng đi ra qua ba callback để kiểm thử độc lập,
    /// GameBootstrap nối callback vào ví Xu / Hạt / Danh tiếng.
    /// Tiến độ cộng dồn qua các ngày in-game trong cùng một ngày thật (đổi lúc 04:00 giờ VN).
    public sealed class MissionBook
    {
        public const int DailyCount = 3;
        public const int ChestGems = 10;
        public const float ChestRp = 30f;
        /// Ngày in-game phải phục vụ ít nhất ngần này khách mới tính nhiệm vụ "cả ngày" (tránh ngày vắng trống). Cấp 1.
        public const int MinServedForDayMission = 8;

        /// Cấp 2 đo ~38 khách/ngày, cấp 3 ~53-88: ngưỡng theo cấp để "ngày trọn vẹn" không thành nhiệm vụ rỗng.
        public static int MinServedFor(int tier)
        {
            return tier >= 3 ? 40 : (tier == 2 ? 25 : MinServedForDayMission);
        }
        public const float CleanDayThreshold = 80f;

        private readonly Action<int> giveXu;
        private readonly Action<int> giveGems;
        private readonly Action<float> giveRp;
        private readonly List<MissionState> items = new List<MissionState>(DailyCount);

        public IReadOnlyList<MissionState> Items { get { return items; } }
        public int DayKey { get; private set; }
        public bool ChestClaimed { get; private set; }

        /// Thay đổi bất kỳ (tiến độ, nhận thưởng, ngày mới): UI vẽ lại.
        public event Action Changed;
        /// Vừa đạt mục tiêu nhiệm vụ thứ i (một lần duy nhất mỗi nhiệm vụ).
        public event Action<int> Completed;

        public MissionBook(Action<int> giveXu, Action<int> giveGems, Action<float> giveRp)
        {
            this.giveXu = giveXu;
            this.giveGems = giveGems;
            this.giveRp = giveRp;
        }

        public bool ChestReady
        {
            get
            {
                if (ChestClaimed || items.Count == 0) return false;
                for (int i = 0; i < items.Count; i++)
                    if (!items[i].claimed) return false;
                return true;
            }
        }

        /// Có gì để nhận (nhiệm vụ xong chưa nhận hoặc rương sẵn sàng): chấm đỏ trên HUD.
        public bool HasClaimable
        {
            get
            {
                for (int i = 0; i < items.Count; i++)
                    if (items[i].IsDone && !items[i].claimed) return true;
                return ChestReady;
            }
        }

        // ------------------------------------------------------------------ sinh nhiệm vụ

        /// Sinh bộ nhiệm vụ mới nếu sang ngày thật mới (hoặc chưa có). Chỉnh giờ lùi thì giữ nguyên. Trả true nếu đã sinh.
        public bool EnsureToday(int dayKey, int tier, MissionContext ctx)
        {
            if (items.Count > 0 && dayKey <= DayKey) return false;

            DayKey = dayKey;
            ChestClaimed = false;
            items.Clear();

            tier = Math.Max(1, tier);
            int scale = MissionCatalog.TierScale(tier);
            var pool = new List<MissionTemplate>(MissionCatalog.All.Length);
            for (int i = 0; i < MissionCatalog.All.Length; i++)
                if (MissionCatalog.IsEligible(MissionCatalog.All[i], tier, ctx)) pool.Add(MissionCatalog.All[i]);

            var rng = new Random(unchecked(dayKey * 7919 + tier * 104729));
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                MissionTemplate tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            // Mỗi khe (vận hành / tay nghề / quán và khách quen) một nhiệm vụ để ngày nào cũng chạm đủ các trụ cột;
            // rồi mỗi loại tối đa một nhiệm vụ; thiếu thì lấy thêm bất kể loại
            var usedKinds = new HashSet<MissionKind>();
            var picked = new List<MissionTemplate>(DailyCount);
            for (int slot = 0; slot < DailyCount; slot++)
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i].slot != slot || !usedKinds.Add(pool[i].kind)) continue;
                    picked.Add(pool[i]);
                    break;
                }
            for (int i = 0; i < pool.Count && picked.Count < DailyCount; i++)
            {
                if (picked.Contains(pool[i]) || !usedKinds.Add(pool[i].kind)) continue;
                picked.Add(pool[i]);
            }
            for (int i = 0; i < pool.Count && picked.Count < DailyCount; i++)
                if (!picked.Contains(pool[i])) picked.Add(pool[i]);

            for (int i = 0; i < picked.Count; i++)
            {
                MissionTemplate t = picked[i];
                items.Add(new MissionState { id = t.id, target = t.TargetFor(tier), rewardXu = t.rewardBase * scale });
            }
            Raise();
            return true;
        }

        // ------------------------------------------------------------------ ghi nhận tiến độ

        /// Khách vừa được phục vụ. tip = Xu tiền tip của khách đó; favorite = khách quen nhận đúng món ruột.
        public void RecordServed(string recipeId, int quality, bool hidden, int tip, bool favorite = false)
        {
            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                MissionTemplate t = MissionCatalog.Find(items[i].id);
                if (t == null) continue;
                int add = 0;
                switch (t.kind)
                {
                    case MissionKind.Serve: add = 1; break;
                    case MissionKind.SellRecipe: add = recipeId == t.recipeId ? 1 : 0; break;
                    case MissionKind.ThreeStar: add = quality >= 3 ? 1 : 0; break;
                    case MissionKind.SellHidden: add = hidden ? 1 : 0; break;
                    case MissionKind.EarnTips: add = Math.Max(0, tip); break;
                    case MissionKind.RegularFavorite: add = favorite ? 1 : 0; break;
                }
                if (add > 0 && Advance(i, add)) changed = true;
            }
            if (changed) Raise();
        }

        /// Vừa mua một món đồ (tốn Xu) cho quán.
        public void RecordPurchase() { RecordKind(MissionKind.BuyItems); }

        /// Vừa chốt một lần thử nghiệm công thức ở Sổ công thức.
        public void RecordExperiment() { RecordKind(MissionKind.Experiment); }

        private void RecordKind(MissionKind kind)
        {
            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                MissionTemplate t = MissionCatalog.Find(items[i].id);
                if (t != null && t.kind == kind && Advance(i, 1)) changed = true;
            }
            if (changed) Raise();
        }

        /// Hết một ngày in-game. tier = cấp quán lúc đóng cửa (quyết định số khách tối thiểu của nhiệm vụ "cả ngày").
        public void RecordDay(int served, int lost, float cleanliness, int tier = 1)
        {
            if (served < MinServedFor(tier)) return;
            bool changed = false;
            for (int i = 0; i < items.Count; i++)
            {
                MissionTemplate t = MissionCatalog.Find(items[i].id);
                if (t == null) continue;
                bool hit = (t.kind == MissionKind.NoWalkaway && lost == 0) ||
                           (t.kind == MissionKind.CleanDay && cleanliness >= CleanDayThreshold);
                if (hit && Advance(i, 1)) changed = true;
            }
            if (changed) Raise();
        }

        private bool Advance(int index, int amount)
        {
            MissionState m = items[index];
            if (m.IsDone) return false;
            m.progress = Math.Min(m.target, m.progress + amount);
            items[index] = m;
            if (m.IsDone && Completed != null) Completed(index);
            return true;
        }

        // ------------------------------------------------------------------ nhận thưởng

        public bool Claim(int index)
        {
            if (index < 0 || index >= items.Count) return false;
            MissionState m = items[index];
            if (!m.IsDone || m.claimed) return false;
            m.claimed = true;
            items[index] = m;
            if (m.rewardXu > 0 && giveXu != null) giveXu(m.rewardXu);
            MissionTemplate t = MissionCatalog.Find(m.id);
            if (t != null && t.rewardGems > 0 && giveGems != null) giveGems(t.rewardGems);
            if (t != null && t.rewardRp > 0 && giveRp != null) giveRp(t.rewardRp);
            Raise();
            return true;
        }

        public bool ClaimChest()
        {
            if (!ChestReady) return false;
            ChestClaimed = true;
            if (giveGems != null) giveGems(ChestGems);
            if (giveRp != null) giveRp(ChestRp);
            Raise();
            return true;
        }

        // ------------------------------------------------------------------ lưu / đọc

        public void Load(SaveData data)
        {
            items.Clear();
            DayKey = 0;
            ChestClaimed = false;
            if (data != null)
            {
                DayKey = data.missionDayKey;
                ChestClaimed = data.missionChestClaimed;
                MissionSave[] saved = data.missions;
                for (int i = 0; saved != null && i < saved.Length && items.Count < DailyCount; i++)
                {
                    MissionTemplate t = MissionCatalog.Find(saved[i].id);
                    if (t == null) continue;
                    int target = saved[i].target > 0 ? saved[i].target : t.TargetFor(1);
                    int progress = Math.Max(0, Math.Min(saved[i].progress, target));
                    if (saved[i].claimed) progress = target;
                    items.Add(new MissionState
                    {
                        id = t.id,
                        target = target,
                        progress = progress,
                        rewardXu = Math.Max(0, saved[i].rewardXu),
                        claimed = saved[i].claimed
                    });
                }
            }
            Raise();
        }

        public void WriteToSave(SaveData data)
        {
            if (data == null) return;
            data.missionDayKey = DayKey;
            data.missionChestClaimed = ChestClaimed;
            var arr = new MissionSave[items.Count];
            for (int i = 0; i < items.Count; i++)
                arr[i] = new MissionSave
                {
                    id = items[i].id,
                    target = items[i].target,
                    progress = items[i].progress,
                    rewardXu = items[i].rewardXu,
                    claimed = items[i].claimed
                };
            data.missions = arr;
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }
}
