using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    public class TierRulesTests
    {
        [Test]
        public void UpgradeCost_Tier2IsDeposit_Tier3IsPriceMinusRefundedDeposit()
        {
            Assert.That(GameBalance.UpgradeCost(2, 0), Is.EqualTo(GameBalance.Tier2Deposit));
            Assert.That(GameBalance.UpgradeCost(3, GameBalance.Tier2Deposit), Is.EqualTo(122000), "140.000 - cọc 18.000 (economy §3.5, §19)");
            Assert.That(GameBalance.UpgradeCost(3, 0), Is.EqualTo(GameBalance.Tier3Price));
            Assert.That(GameBalance.UpgradeCost(3, 999999), Is.EqualTo(0), "cọc không bao giờ làm giá âm");
        }

        [Test]
        public void UpgradeStarRp_FollowsEconomyThresholds()
        {
            Assert.That(GameBalance.UpgradeStarRP(2), Is.EqualTo(GameBalance.StarThresholds[1]));
            Assert.That(GameBalance.UpgradeStarRP(3), Is.EqualTo(GameBalance.StarThresholds[2]));
        }

        [Test]
        public void StarThresholds_UseTierConstants_AndStayAscending()
        {
            Assert.That(GameBalance.StarThresholds[1], Is.EqualTo(GameBalance.Tier2StarRP));
            Assert.That(GameBalance.StarThresholds[2], Is.EqualTo(GameBalance.Tier3StarRP));
            for (int i = 1; i < GameBalance.StarThresholds.Length; i++)
                Assert.That(GameBalance.StarThresholds[i], Is.GreaterThan(GameBalance.StarThresholds[i - 1]));
        }

        [Test]
        public void StoryChapterRp_GrowsWithChapter()
        {
            Assert.That(GameBalance.StoryChapterRp(0), Is.EqualTo(60));
            Assert.That(GameBalance.StoryChapterRp(1), Is.EqualTo(80));
            Assert.That(GameBalance.StoryChapterRp(2), Is.EqualTo(100));
        }

        [Test]
        public void FixedCost_PerTier()
        {
            Assert.That(GameBalance.FixedCostFor(1), Is.EqualTo(60));
            Assert.That(GameBalance.FixedCostFor(2), Is.EqualTo(150));
            Assert.That(GameBalance.FixedCostFor(3), Is.EqualTo(700));
        }

        [Test]
        public void StaffMaxCount_BrewerOnlyFromTier3_AndGrowsWithTier()
        {
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Cleaner, 1), Is.Zero);
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 2), Is.Zero);
            Assert.That(GameBalance.StaffMaxCount(StaffRole.Brewer, 3), Is.GreaterThan(0));
            foreach (StaffRole role in StaffRoles.All)
                Assert.That(GameBalance.StaffMaxCount(role, 3), Is.GreaterThanOrEqualTo(GameBalance.StaffMaxCount(role, 2)));

            int t2 = 0, t3 = 0;
            foreach (StaffRole role in StaffRoles.All)
            {
                t2 += GameBalance.StaffMaxCount(role, 2);
                t3 += GameBalance.StaffMaxCount(role, 3);
            }
            Assert.That(t2, Is.EqualTo(2), "cấp 2: 1 Dọn dẹp + 1 Phục vụ");
            Assert.That(t3, Is.InRange(4, 10), "economy §3.1: cấp 3 có 4–10 nhân viên");
        }

        [Test]
        public void RoomSpec_Tier2KeepsOriginalGridAndPoints()
        {
            RoomSpec r = RoomLayout.For(2);
            Assert.That(r.GridSize, Is.EqualTo(new Vector2Int(12, 16)));
            Assert.That(r.GridOrigin, Is.EqualTo(new Vector3(-3f, 0f, -4f)));
            Assert.That(r.SpawnPoint.x, Is.EqualTo(-8.5f).Within(0.001f));
            Assert.That(r.ExitPoint.x, Is.EqualTo(8.5f).Within(0.001f));
        }

        [Test]
        public void RoomSpec_Tier3IsBiggerAndGridMatchesRoom()
        {
            RoomSpec t2 = RoomLayout.For(2), t3 = RoomLayout.For(3);
            Assert.That(t3.GridSize, Is.EqualTo(new Vector2Int(20, 24)));
            Assert.That(t3.GridSize.x * t3.GridSize.y, Is.GreaterThan(t2.GridSize.x * t2.GridSize.y * 2));
            Assert.That(t3.DoorHalfWidth, Is.GreaterThan(t2.DoorHalfWidth));
            // Khách đi vào / ra nằm trên vỉa hè, ngoài tường hông, trong vùng đi được
            Assert.That(Mathf.Abs(t3.SpawnPoint.x), Is.GreaterThan(t3.Width * 0.5f));
            Assert.That(Mathf.Abs(t3.SpawnPoint.x), Is.LessThan(t3.SidewalkHalfWidthCollider));
            Assert.That(RoomLayout.For(1), Is.SameAs(RoomLayout.For(2)), "chưa có phòng cấp 1: dùng phòng nhỏ nhất");
            Assert.That(RoomLayout.For(4), Is.SameAs(t3));
        }

        [Test]
        public void RentLedger_Tier3ClearsRentCycleButKeepsDebt()
        {
            var ledger = new RentLedger();
            ledger.Load(1200, 5);
            ledger.ClearRentCycle();
            Assert.That(ledger.Debt, Is.EqualTo(1200));
            Assert.That(ledger.DaysSinceRent, Is.Zero);
        }

        [Test]
        public void Capacity_Tier3UsesRestaurantTurnsAndSmallerStaffBonus()
        {
            Assert.That(GameBalance.CapacityFor(3, 30, 0), Is.EqualTo(150));
            Assert.That(GameBalance.CapacityFor(3, 30, 4), Is.EqualTo(Mathf.RoundToInt(30 * 5f * 1.6f)));
            Assert.That(GameBalance.CapacityFor(3, 30, 0), Is.GreaterThan(GameBalance.CapacityFor(2, 30, 0)), "cùng số ghế, chưa có nhân viên: nhà hàng quay vòng nhanh hơn");
            Assert.That(GameBalance.FRefFor(3), Is.GreaterThan(GameBalance.FRefFor(2)));
        }

        [Test]
        public void Cups_ScaleWithSeatsAtRestaurantSize()
        {
            Assert.That(GameBalance.CupsFor(3, 30), Is.EqualTo(45));
            Assert.That(GameBalance.CupsFor(3, 0), Is.EqualTo(GameBalance.StartCups));
        }
    }
}
