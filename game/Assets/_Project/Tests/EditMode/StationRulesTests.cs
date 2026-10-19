using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    /// Quầy pha chế phụ + Quầy line (docs/stations_spec.md): trần thuê Pha chế, sức chứa kệ giao, gán chỗ làm, hệ số pha.
    public class StationRulesTests
    {
        [Test]
        public void StaffMaxCount_EachBrewStationAddsOneBrewer_OnlyAtTier3_CappedAtTwo()
        {
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 3, 3), Is.EqualTo(6), "không có quầy: như cũ");
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 3, 3, 1), Is.EqualTo(7));
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 3, 3, 2), Is.EqualTo(8), "đủ 3 khu + 2 quầy: 6 -> 8");
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 3, 3, 5), Is.EqualTo(8), "tối đa 2 quầy được tính");
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 3, 0, 2), Is.EqualTo(5));
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 3, 3, -1), Is.EqualTo(6), "số âm không làm giảm trần");
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 2, 0, 2), Is.Zero, "cấp 2 không có Pha chế");
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 1, 3, 2), Is.Zero);
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Server, 3, 3, 2), Is.EqualTo(GameBalance.StaffMaxCount(StaffRole.Server, 3, 3)));
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Cleaner, 3, 3, 2), Is.EqualTo(GameBalance.StaffMaxCount(StaffRole.Cleaner, 3, 3)));
        }

        [Test]
        public void StationsCounted_OnlyTier3_ClampedAndSwitchable()
        {
            Assert.That(StationRules.StationsCounted(2, 2), Is.Zero);
            Assert.That(StationRules.StationsCounted(3, 0), Is.Zero);
            Assert.That(StationRules.StationsCounted(3, 1), Is.EqualTo(1));
            Assert.That(StationRules.StationsCounted(3, 3), Is.EqualTo(2));
            Assert.That(StationRules.StationsCounted(3, 2, false), Is.Zero, "-nostationslots: không thêm chỗ thuê");
        }

        [Test]
        public void Roster_BrewerCapFollowsStationsProvider_AndHiresUpToIt()
        {
            var wallet = new Wallet();
            wallet.Init(100000);
            var ledger = new RentLedger();
            ledger.Reset();
            int stations = 0;
            var roster = new StaffRoster(wallet, ledger, () => 3, () => 3, () => stations);
            roster.Load(null);

            Assert.That(roster.MaxFor(StaffRole.Brewer), Is.EqualTo(6));
            for (int i = 0; i < 6; i++) Assert.That(roster.Hire(StaffRole.Brewer, 0, null), Is.True);
            Assert.That(roster.CanHire(StaffRole.Brewer), Is.False);

            stations = 2;
            Assert.That(roster.BrewStations, Is.EqualTo(2));
            Assert.That(roster.MaxFor(StaffRole.Brewer), Is.EqualTo(8));
            Assert.That(roster.Hire(StaffRole.Brewer, 0, null), Is.True);
            Assert.That(roster.Hire(StaffRole.Brewer, 0, null), Is.True);
            Assert.That(roster.Hire(StaffRole.Brewer, 0, null), Is.False, "tối đa 8 Pha chế");

            stations = 0; // gỡ quầy: người đã thuê vẫn giữ, chỉ không thuê thêm
            Assert.That(roster.CountOf(StaffRole.Brewer), Is.EqualTo(8));
            Assert.That(roster.CanHire(StaffRole.Brewer), Is.False);

            var names = new HashSet<string>();
            for (int i = 0; i < roster.Count; i++) names.Add(roster.Members[i].name);
            Assert.That(names.Count, Is.EqualTo(8), "8 Pha chế: mỗi người một tên mặc định");
        }

        [Test]
        public void Roster_Load_KeepsBrewersHiredWithStations()
        {
            var save = new SaveData();
            var list = new List<StaffSave>();
            for (int i = 0; i < 9; i++) list.Add(new StaffSave { role = "brew", level = 1, name = "B" + i });
            save.staff = list.ToArray();

            var roster = new StaffRoster(new Wallet(), new RentLedger(), () => 3, () => 3);
            roster.Load(save);

            Assert.That(roster.CountOf(StaffRole.Brewer), Is.EqualTo(8), "save có quầy pha phụ: giữ đủ 8, bỏ phần vượt trần lớn nhất");
        }

        [Test]
        public void ShelfCapacity_BasePlusPassCounterSlots()
        {
            Assert.That(StationRules.ShelfCapacity(0), Is.EqualTo(6));
            Assert.That(StationRules.ShelfCapacity(4), Is.EqualTo(10));
            Assert.That(StationRules.ShelfCapacity(8), Is.EqualTo(14));
            Assert.That(StationRules.ShelfCapacity(-3), Is.EqualTo(6));
        }

        [Test]
        public void ReadyShelf_DefaultCapacityIsSix()
        {
            var shelf = new ReadyShelf(null);
            Assert.That(shelf.Capacity, Is.EqualTo(6));
            Assert.That(ReadyShelf.DefaultCapacity, Is.EqualTo(6));
        }

        [Test]
        public void ReadyShelf_CapacityIsReadLive_FromProvider()
        {
            int slots = 4;
            int discarded = 0;
            var shelf = new ReadyShelf(() => discarded++, () => StationRules.ShelfCapacity(slots));
            var brewer = new object();
            Assert.That(shelf.Capacity, Is.EqualTo(10));

            for (int i = 0; i < 10; i++)
            {
                var o = new Order { id = i };
                o.TryClaim(brewer);
                Assert.That(shelf.TryAdd(o, 2, brewer), Is.True, "ly thứ " + (i + 1));
            }
            Assert.That(shelf.HasRoom, Is.False);
            var extra = new Order { id = 99 };
            extra.TryClaim(brewer);
            Assert.That(shelf.TryAdd(extra, 2, brewer), Is.False, "đầy ở 10");

            slots = 0; // gỡ quầy line: ly đang có giữ nguyên, không nhận thêm cho tới khi xuống dưới 6
            Assert.That(shelf.Capacity, Is.EqualTo(6));
            Assert.That(shelf.Count, Is.EqualTo(10));
            Assert.That(shelf.HasRoom, Is.False);
            Assert.That(discarded, Is.Zero);

            var server = new object();
            ReadyShelf.Drink d;
            for (int i = 0; i < 5; i++) Assert.That(shelf.TryTake(server, null, out d), Is.True);
            Assert.That(shelf.Count, Is.EqualTo(5));
            Assert.That(shelf.HasRoom, Is.True);
        }

        [Test]
        public void SpotAssignment_FollowsHireOrder_OneBrewerPerSpot()
        {
            Assert.That(StationRules.Assign(3, 2), Is.EqualTo(new[] { 0, 1, -1 }), "người thuê sau cùng không có chỗ: đứng hàng cũ");
            Assert.That(StationRules.Assign(2, 4), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(StationRules.Assign(0, 4), Is.Empty);
            Assert.That(StationRules.Assign(2, 0), Is.EqualTo(new[] { -1, -1 }), "không có quầy: không ai có chỗ");
            Assert.That(StationRules.SpotIndexFor(-1, 4), Is.EqualTo(-1));
            Assert.That(StationRules.SpotIndexFor(4, 4), Is.EqualTo(-1));
        }

        [Test]
        public void SpotLocal_SpreadsAlongWidth_OnStaffSide_OutsideFootprint()
        {
            var fp = new Vector2Int(4, 2); // 2 x 1 m
            Vector3 a = StationRules.SpotLocal(fp, 2, 0);
            Vector3 b = StationRules.SpotLocal(fp, 2, 1);
            Assert.That(a.x, Is.EqualTo(-0.5f).Within(1e-4));
            Assert.That(b.x, Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(a.z, Is.EqualTo(StationRules.StaffSide * (0.5f + StationRules.StandOffset)).Within(1e-4), "phía nhân viên (như chỗ chủ quán ở quầy chính), ngoài mép quầy");
            Assert.That(a.y, Is.Zero);

            var spots = new List<Vector3>();
            float h = Mathf.Sqrt(0.5f); // xoay 90° quanh Y (Quaternion.Euler gọi native, không chạy ngoài Unity)
            StationRules.AppendSpots(spots, new Vector3(10f, 0f, 5f), new Quaternion(0f, h, 0f, h), fp, 2);
            Assert.That(spots.Count, Is.EqualTo(2));
            // Xoay 90°: Z cục bộ thành X thế giới
            Assert.That(spots[0].x, Is.EqualTo(10f + a.z).Within(1e-3));
            Assert.That(spots[1].x, Is.EqualTo(10f + a.z).Within(1e-3));
            Assert.That(Mathf.Abs(spots[0].z - spots[1].z), Is.EqualTo(1f).Within(1e-3));
        }

        [Test]
        public void BrewFactor_SpotIsFasterOnly_NoPenaltyWithoutSpot()
        {
            Assert.That(StationRules.BrewFactor(false), Is.EqualTo(1f));
            Assert.That(StationRules.BrewFactor(true), Is.EqualTo(0.85f).Within(1e-5));
            Assert.That(BrewerRules.BrewSeconds(10f, 5, StationRules.BrewFactor(true)), Is.EqualTo(8.5f).Within(1e-4));
            Assert.That(BrewerRules.BrewSeconds(1f, 5, StationRules.BrewFactor(true)), Is.EqualTo(1f), "sàn 1 s giữ nguyên");
        }

        [Test]
        public void Effect_DescribesStationsOnly()
        {
            Assert.That(StationRules.Effect(2, 0), Is.Not.Empty);
            Assert.That(StationRules.Effect(0, 4), Does.Contain("4"));
            Assert.That(StationRules.Effect(0, 0), Is.Empty);
        }
    }
}
