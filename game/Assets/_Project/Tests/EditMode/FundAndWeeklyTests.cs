using NUnit.Framework;

namespace Cafe.Tests
{
    public class HouseFundTests
    {
        [TestCase(0, 18000, 0)]
        [TestCase(4499, 18000, 0)]
        [TestCase(4500, 18000, 1)]
        [TestCase(9000, 18000, 2)]
        [TestCase(13500, 18000, 3)]
        [TestCase(17999, 18000, 3)]
        [TestCase(18000, 18000, 4)]
        [TestCase(99999, 18000, 4)]
        [TestCase(-5, 18000, 0)]
        [TestCase(10, 0, 4)]                 // không cần Xu: coi như đủ
        [TestCase(int.MaxValue, 122000, 4)]  // không tràn số
        public void StageFor_SplitsTheBarIntoFourParts(int xu, int need, int expected)
        {
            Assert.That(HouseFund.StageFor(xu, need), Is.EqualTo(expected));
        }

        [Test]
        public void Rewards_AreGemsForTheFirstThreeStagesOnly_AndDoubleForTierThree()
        {
            Assert.That(HouseFund.RewardGems(1, 2), Is.EqualTo(5));
            Assert.That(HouseFund.RewardGems(2, 2), Is.EqualTo(10));
            Assert.That(HouseFund.RewardGems(3, 2), Is.EqualTo(15));
            Assert.That(HouseFund.RewardGems(4, 2), Is.Zero, "chặng 100% là nút lên cấp");
            Assert.That(HouseFund.RewardGems(0, 2), Is.Zero);
            Assert.That(HouseFund.RewardGems(1, 3), Is.EqualTo(10));
            Assert.That(HouseFund.RewardGems(3, 3), Is.EqualTo(30));
        }

        [Test]
        public void Tracker_PaysEachStageOnce_EvenWhenXuGoesUpAndDown()
        {
            var t = new HouseFundTracker();
            int from, to;
            Assert.That(t.Advance(2, 1000, 18000, out from, out to), Is.False);
            Assert.That(t.Advance(2, 5000, 18000, out from, out to), Is.True);
            Assert.That((from, to), Is.EqualTo((0, 1)));
            Assert.That(t.Advance(2, 800, 18000, out from, out to), Is.False, "tiêu bớt Xu: không mất chặng");
            Assert.That(t.Advance(2, 5000, 18000, out from, out to), Is.False, "lên lại cũng không thưởng lại");
            Assert.That(t.Advance(2, 14000, 18000, out from, out to), Is.True);
            Assert.That((from, to), Is.EqualTo((1, 3)), "nhảy qua chặng 2 và 3 cùng lúc");
        }

        [Test]
        public void Tracker_ANewTargetTierStartsOver()
        {
            var t = new HouseFundTracker();
            int from, to;
            t.Advance(2, 18000, 18000, out from, out to);
            Assert.That(t.Stage, Is.EqualTo(4));
            Assert.That(t.Advance(3, 20000, 122000, out from, out to), Is.False, "16%: chưa tới chặng 1 của quỹ mới");
            Assert.That(t.TargetTier, Is.EqualTo(3));
            Assert.That(t.Stage, Is.Zero, "quỹ cấp đích mới bắt đầu từ 0 dù quỹ cũ đã đầy");
            Assert.That(t.Advance(3, 40000, 122000, out from, out to), Is.True, "32%: đã qua chặng 1 thật");
            Assert.That((from, to), Is.EqualTo((0, 1)));
            Assert.That(t.Advance(3, 61000, 122000, out from, out to), Is.True);
            Assert.That(to, Is.EqualTo(2));
        }

        [Test]
        public void Tracker_ResetForAnOldSave_DoesNotBackPay()
        {
            var t = new HouseFundTracker();
            t.Reset(3, 70000, 122000);               // 57%: chặng 2 đã đạt từ trước
            int from, to;
            Assert.That(t.Stage, Is.EqualTo(2));
            Assert.That(t.Advance(3, 70000, 122000, out from, out to), Is.False);
            Assert.That(t.Advance(3, 92000, 122000, out from, out to), Is.True);
            Assert.That((from, to), Is.EqualTo((2, 3)));
        }

        [Test]
        public void Tracker_NoNextTier_IsInert()
        {
            var t = new HouseFundTracker();
            int from, to;
            Assert.That(t.Advance(0, 999999, 0, out from, out to), Is.False);
        }
    }

    public class WeeklyProjectTests
    {
        [TestCase(1, 0, 1)]
        [TestCase(7, 0, 7)]
        [TestCase(8, 1, 1)]
        [TestCase(14, 1, 7)]
        [TestCase(15, 2, 1)]
        public void WeekAndDayInWeek(int day, int week, int dayInWeek)
        {
            Assert.That(WeeklyProject.WeekOf(day), Is.EqualTo(week));
            Assert.That(WeeklyProject.DayInWeek(day), Is.EqualTo(dayInWeek));
        }

        [Test]
        public void Kinds_RotateServeTipsProfit()
        {
            Assert.That(WeeklyProject.KindOf(0), Is.EqualTo(WeeklyKind.Serve));
            Assert.That(WeeklyProject.KindOf(1), Is.EqualTo(WeeklyKind.Tips));
            Assert.That(WeeklyProject.KindOf(2), Is.EqualTo(WeeklyKind.Profit));
            Assert.That(WeeklyProject.KindOf(3), Is.EqualTo(WeeklyKind.Serve));
        }

        [Test]
        public void ServeWeek_AccumulatesAcrossDays_AndPaysExactlyOnce()
        {
            var w = new WeeklyProject();
            w.Sync(1, 1);
            Assert.That(w.Target, Is.EqualTo(110));
            for (int day = 1; day <= 4; day++) Assert.That(w.RecordDay(day, 1, 25, 0, 0), Is.False);
            Assert.That(w.Progress, Is.EqualTo(100));
            Assert.That(w.RecordDay(5, 1, 25, 0, 0), Is.True, "ngày thứ 5 chạm 125 >= 110");
            Assert.That(w.Done, Is.True);
            Assert.That(w.RecordDay(6, 1, 25, 0, 0), Is.False, "đã xong: không thưởng lại");
        }

        [Test]
        public void NewWeek_ResetsProgressAndSwitchesKind()
        {
            var w = new WeeklyProject();
            w.Sync(1, 1);
            w.RecordDay(1, 1, 30, 100, 900);
            w.RecordDay(7, 1, 30, 100, 900);
            Assert.That(w.Progress, Is.EqualTo(60));
            w.Sync(8, 1);
            Assert.That(w.Week, Is.EqualTo(1));
            Assert.That(w.Kind, Is.EqualTo(WeeklyKind.Tips));
            Assert.That(w.Progress, Is.Zero);
            Assert.That(w.Done, Is.False);
        }

        [Test]
        public void TipsAndProfitWeeks_CountTheirOwnNumbers_NotNegativeProfit()
        {
            var w = new WeeklyProject();
            w.Sync(8, 2);                                  // tuần 1 = Tip, cấp 2: mục tiêu 2.500
            w.RecordDay(8, 2, 40, 900, -1500);
            Assert.That(w.Progress, Is.EqualTo(900));
            w.Sync(15, 2);                                 // tuần 2 = Lãi
            w.RecordDay(15, 2, 40, 900, -1500);
            Assert.That(w.Progress, Is.Zero, "ngày lỗ không làm tiến độ lùi");
            w.RecordDay(16, 2, 40, 900, 5000);
            Assert.That(w.Progress, Is.EqualTo(5000));
        }

        [Test]
        public void TierIsLockedAtTheStartOfTheWeek()
        {
            var w = new WeeklyProject();
            w.Sync(1, 1);
            Assert.That(w.Target, Is.EqualTo(110));
            w.RecordDay(3, 3, 30, 0, 0);                   // lên nhà hàng giữa tuần
            Assert.That(w.Tier, Is.EqualTo(1));
            Assert.That(w.Target, Is.EqualTo(110), "mục tiêu không nhảy vọt giữa tuần");
            w.Sync(8, 3);
            Assert.That(w.Tier, Is.EqualTo(3));
        }

        [Test]
        public void TargetsAndRewards_GrowWithTier()
        {
            foreach (WeeklyKind k in new[] { WeeklyKind.Serve, WeeklyKind.Tips, WeeklyKind.Profit })
            {
                Assert.That(WeeklyProject.TargetFor(k, 2), Is.GreaterThan(WeeklyProject.TargetFor(k, 1)));
                Assert.That(WeeklyProject.TargetFor(k, 3), Is.GreaterThan(WeeklyProject.TargetFor(k, 2)));
            }
            Assert.That(WeeklyProject.RewardXu(3), Is.GreaterThan(WeeklyProject.RewardXu(1)));
            Assert.That(WeeklyProject.RewardGems(3), Is.GreaterThan(WeeklyProject.RewardGems(1)));
            Assert.That(WeeklyProject.TargetFor(WeeklyKind.Serve, 9), Is.EqualTo(WeeklyProject.TargetFor(WeeklyKind.Serve, 3)), "cấp 4 chưa có: dùng cấp 3");
        }

        [Test]
        public void LoadedState_IsKept_UntilTheWeekChanges()
        {
            var w = new WeeklyProject();
            w.Load(2, 12000, false, 2);
            w.Sync(16, 2);                                 // ngày 16 vẫn là tuần 2
            Assert.That(w.Progress, Is.EqualTo(12000));
            Assert.That(w.Kind, Is.EqualTo(WeeklyKind.Profit));
            Assert.That(w.RecordDay(16, 2, 0, 0, 5000), Is.True, "12.000 + 5.000 >= 16.000");
        }
    }
}
