using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Danh sách nhân viên và toàn bộ giao dịch tuyển/nâng cấp/lương.
    /// Điều khiển nhân vật ngoài thế giới nằm ở StaffWorker.
    public sealed class StaffRoster
    {
        private readonly Wallet wallet;
        private readonly RentLedger debtLedger;
        private readonly Func<int> tierProvider;
        private readonly Func<int> zonesProvider;
        private readonly Func<int> stationsProvider;
        private const int MaxTier = 3; // Load giữ tối đa theo cấp cao nhất: save cấp 2 có Pha chế cũ không bị mất
        private readonly List<StaffSave> members = new List<StaffSave>(8);
        private readonly List<StaffWorker> workers = new List<StaffWorker>(8);
        private GameBootstrap game;
        private GridSystem grid; // lưới đang nghe LayoutChanged (gán lại chỗ ở quầy pha phụ)

        public event Action Changed;

        /// zonesProvider: số khu phụ đã mở (mỗi khu nới thêm 1 chỗ mỗi vai trò, chỉ ở cấp 3); mặc định 0.
        /// stationsProvider: số quầy pha phụ đang đặt (mỗi quầy +1 chỗ Pha chế, chỉ ở cấp 3, tối đa 2); mặc định 0.
        public StaffRoster(Wallet wallet, RentLedger debtLedger, Func<int> tierProvider = null, Func<int> zonesProvider = null,
                           Func<int> stationsProvider = null)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.debtLedger = debtLedger ?? throw new ArgumentNullException(nameof(debtLedger));
            this.tierProvider = tierProvider ?? (() => int.MaxValue);
            this.zonesProvider = zonesProvider ?? (() => 0);
            this.stationsProvider = stationsProvider ?? (() => 0);
        }

        /// Số quầy pha phụ đang được tính cho trần thuê Pha chế (StaffPanel đọc để hiện số hàng).
        public int BrewStations { get { return stationsProvider(); } }

        /// Trần của một vai trò theo cấp, số khu đang mở và số quầy pha phụ.
        public int MaxFor(StaffRole role)
        {
            return GameBalance.StaffMaxCount(role, tierProvider(), zonesProvider(), stationsProvider());
        }

        public IReadOnlyList<StaffSave> Members { get { return members; } }
        public IReadOnlyList<StaffWorker> Workers { get { return workers; } }
        public int Count { get { return members.Count; } }

        public void Attach(GameBootstrap owner)
        {
            if (game == owner) return;
            DisposeWorkers();
            if (game != null && game.Tier != null) game.Tier.TierChanged -= OnTierChanged;
            if (grid != null) grid.LayoutChanged -= OnLayoutChanged;
            game = owner;
            grid = game != null ? game.Grid : null;
            if (game != null && game.Tier != null) game.Tier.TierChanged += OnTierChanged;
            if (grid != null) grid.LayoutChanged += OnLayoutChanged;
            SyncWorkers();
        }

        public void Load(SaveData data)
        {
            members.Clear();
            StaffSave[] saved = data != null ? data.staff : null;
            if (saved != null)
            {
                for (int i = 0; i < saved.Length; i++)
                {
                    StaffRole role;
                    if (!StaffRoles.TryParse(saved[i].role, out role) || CountOf(role) >= GameBalance.StaffMaxCount(role, MaxTier, ZoneInfo.Buyable.Length, GameBalance.BrewStationMaxCount)) continue;
                    StaffSave item = saved[i];
                    item.role = StaffRoles.ToId(role);
                    item.preset = Mathf.Clamp(item.preset, 0, 3);
                    item.level = Mathf.Clamp(item.level, 1, 5);
                    if (string.IsNullOrWhiteSpace(item.name)) item.name = StaffRoles.DefaultName(role, CountOf(role));
                    else item.name = item.name.Trim();
                    members.Add(item);
                }
            }
            RaiseChanged();
        }

        public bool HasRole(StaffRole role)
        {
            return CountOf(role) > 0;
        }

        public int CountOf(StaffRole role)
        {
            string id = StaffRoles.ToId(role);
            int n = 0;
            for (int i = 0; i < members.Count; i++)
                if (members[i].role == id) n++;
            return n;
        }

        /// Chỉ số (trong Members) của người thứ k (từ 0) của vai trò; -1 nếu chưa có.
        public int IndexOf(StaffRole role, int k)
        {
            string id = StaffRoles.ToId(role);
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].role != id) continue;
                if (k-- == 0) return i;
            }
            return -1;
        }

        public bool CanHire(StaffRole role)
        {
            return tierProvider() >= 2 && CountOf(role) < MaxFor(role);
        }

        public bool Hire(StaffRole role, int preset, string name)
        {
            if (!CanHire(role)) return false;
            int fee = GameBalance.StaffHireFee(role);
            if (wallet.Xu < fee) return false;
            wallet.Spend(fee);
            int n = CountOf(role);
            members.Add(new StaffSave
            {
                role = StaffRoles.ToId(role),
                preset = Mathf.Clamp(preset, 0, 3),
                level = 1,
                name = string.IsNullOrWhiteSpace(name) ? StaffRoles.DefaultName(role, n) : name.Trim()
            });
            // Chỉ thêm người mới: những người khác đang pha / mang ly giữa ngày không bị hủy việc
            if (WorkersTracked && workers.Count == members.Count - 1)
            {
                workers.Add(StaffWorker.Create(game, members[members.Count - 1], members.Count - 1));
                AssignBrewSpots();
            }
            else
            {
                SyncWorkers();
            }
            RaiseChangedKeepingWorkers();
            return true;
        }

        public bool Fire(int index)
        {
            if (index < 0 || index >= members.Count) return false;
            members.RemoveAt(index);
            if (WorkersTracked && workers.Count == members.Count + 1)
            {
                if (workers[index] != null) UnityEngine.Object.Destroy(workers[index].gameObject);
                workers.RemoveAt(index);
                AssignBrewSpots(); // Pha chế nghỉ: người thuê sau dồn lên chỗ trống
            }
            else
            {
                SyncWorkers();
            }
            RaiseChangedKeepingWorkers();
            return true;
        }

        public bool Upgrade(int index)
        {
            if (index < 0 || index >= members.Count) return false;
            StaffSave item = members[index];
            if (item.level >= 5 || (item.level == 4 && tierProvider() < 3)) return false;
            int cost = GameBalance.StaffUpgradeCost(item.level);
            if (wallet.Xu < cost) return false;
            wallet.Spend(cost);
            item.level++;
            members[index] = item;
            // Chỉ cập nhật người được nâng cấp (tốc độ đi, tốc độ pha / chất lượng đọc từ Data.level lúc nhận việc kế)
            if (WorkersTracked && workers.Count == members.Count && workers[index] != null) workers[index].UpdateData(item);
            else SyncWorkers();
            RaiseChangedKeepingWorkers();
            return true;
        }

        /// Trả lương bằng số Xu đang có; phần thiếu được cộng vào sổ nợ thuê/lương chung.
        public int PayDailyWages(DaySummary summary)
        {
            int total = 0;
            for (int i = 0; i < members.Count; i++)
            {
                StaffRole role;
                if (StaffRoles.TryParse(members[i].role, out role)) total += GameBalance.StaffDailyWage(role);
            }
            int paid = wallet.Spend(total);
            debtLedger.AddDebt(total - paid);
            if (summary != null) summary.staffWages += paid;
            return paid;
        }

        public void WriteToSave(SaveData data)
        {
            if (data == null) return;
            data.staff = members.ToArray();
        }

        public void ResetForNewDay()
        {
            for (int i = 0; i < workers.Count; i++)
                if (workers[i] != null) workers[i].ResetForNewDay();
        }

        public void Dispose()
        {
            if (game != null && game.Tier != null) game.Tier.TierChanged -= OnTierChanged;
            if (grid != null) grid.LayoutChanged -= OnLayoutChanged;
            grid = null;
            DisposeWorkers();
            game = null;
        }

        private void OnTierChanged()
        {
            SyncWorkers();
        }

        /// Đặt / gỡ / dời đồ: tính lại chỗ ở quầy pha phụ (không dựng lại cả đội; người đang pha dở pha xong mới đi).
        private void OnLayoutChanged()
        {
            AssignBrewSpots();
        }

        /// Gán Pha chế theo thứ tự thuê vào các chỗ làm ở quầy pha phụ (StationRules.SpotIndexFor); không có chỗ thì về hàng cũ.
        private void AssignBrewSpots()
        {
            if (workers.Count == 0) return;
            List<Vector3> spots = grid != null && game != null && game.Tier != null && game.Tier.Current >= 3
                ? grid.BrewSpots() : null;
            int spotCount = spots != null ? spots.Count : 0;
            int k = 0;
            for (int i = 0; i < workers.Count; i++)
            {
                StaffWorker w = workers[i];
                if (w == null || w.Role != StaffRole.Brewer) continue;
                int spot = StationRules.SpotIndexFor(k++, spotCount);
                w.AssignBrewSpot(spot >= 0, spot >= 0 ? spots[spot] : Vector3.zero);
            }
        }

        private void SyncWorkers()
        {
            DisposeWorkers();
            if (game == null || game.Tier == null || game.Tier.Current < 2)
            {
                if (game != null && game.Tier != null) game.Tier.StaffCount = 0;
                return;
            }
            for (int i = 0; i < members.Count; i++)
                workers.Add(StaffWorker.Create(game, members[i], i));
            game.Tier.StaffCount = members.Count;
            AssignBrewSpots();
        }

        private void DisposeWorkers()
        {
            for (int i = 0; i < workers.Count; i++)
                if (workers[i] != null) UnityEngine.Object.Destroy(workers[i].gameObject);
            workers.Clear();
            if (game != null && game.Tier != null) game.Tier.StaffCount = 0;
        }

        private bool WorkersTracked
        {
            get { return game != null && game.Tier != null && game.Tier.Current >= 2; }
        }

        private void RaiseChanged()
        {
            SyncWorkers();
            if (Changed != null) Changed();
        }

        /// Báo thay đổi khi các StaffWorker đã được cập nhật từng người (không dựng lại cả đội).
        private void RaiseChangedKeepingWorkers()
        {
            if (game != null && game.Tier != null) game.Tier.StaffCount = WorkersTracked ? members.Count : 0;
            if (Changed != null) Changed();
        }
    }
}
