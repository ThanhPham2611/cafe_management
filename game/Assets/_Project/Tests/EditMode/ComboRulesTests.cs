using NUnit.Framework;

namespace Cafe.Tests
{
    /// Combo pha chuẩn (docs/combo_rush_spec.md §1): bậc, cộng / đứt, tip thưởng và trần theo doanh thu ngày.
    public class ComboRulesTests
    {
        [TearDown]
        public void Clear()
        {
            GameEvents.ComboChanged -= OnChanged;
            GameEvents.ComboBroken -= OnBroken;
        }

        private int changedCalls, brokenCalls, lastBroken, lastBonus;
        private bool lastTierUp;
        private void OnChanged(int count, int tier, int gain, int bonus, bool tierUp) { changedCalls++; lastBonus = bonus; lastTierUp = tierUp; }
        private void OnBroken(int prev) { brokenCalls++; lastBroken = prev; }

        [Test]
        public void Tier_Thresholds()
        {
            Assert.That(ComboRules.Tier(0), Is.EqualTo(0));
            Assert.That(ComboRules.Tier(2), Is.EqualTo(0));
            Assert.That(ComboRules.Tier(3), Is.EqualTo(1));
            Assert.That(ComboRules.Tier(5), Is.EqualTo(1));
            Assert.That(ComboRules.Tier(6), Is.EqualTo(2));
            Assert.That(ComboRules.Tier(9), Is.EqualTo(2));
            Assert.That(ComboRules.Tier(10), Is.EqualTo(3));
            Assert.That(ComboRules.Tier(99), Is.EqualTo(3));
        }

        [Test]
        public void Gain_OnlyThreeStars_PerfectCountsDouble()
        {
            Assert.That(ComboRules.Gain(1, false), Is.EqualTo(0));
            Assert.That(ComboRules.Gain(2, true), Is.EqualTo(0));
            Assert.That(ComboRules.Gain(3, false), Is.EqualTo(1));
            Assert.That(ComboRules.Gain(3, true), Is.EqualTo(2));
        }

        [Test]
        public void RawBonus_ByTier_AndRushMultiplier()
        {
            Assert.That(ComboRules.RawBonus(100, 2), Is.EqualTo(0));
            Assert.That(ComboRules.RawBonus(100, 3), Is.EqualTo(10));
            Assert.That(ComboRules.RawBonus(100, 7), Is.EqualTo(20));
            Assert.That(ComboRules.RawBonus(100, 12), Is.EqualTo(30));
            Assert.That(ComboRules.RawBonus(100, 12, 1.5f), Is.EqualTo(45));
            Assert.That(ComboRules.RawBonus(0, 12), Is.EqualTo(0));
            Assert.That(ComboRules.RawBonus(100, 12, -1f), Is.EqualTo(0));
        }

        [Test]
        public void Capped_NeverExceedsDailyCap()
        {
            int cap = ComboRules.DailyCap(1000); // 30 + 40
            Assert.That(cap, Is.EqualTo(70));
            Assert.That(ComboRules.Capped(30, 0, 1000), Is.EqualTo(30));
            Assert.That(ComboRules.Capped(30, 60, 1000), Is.EqualTo(10));
            Assert.That(ComboRules.Capped(30, 70, 1000), Is.EqualTo(0));
            Assert.That(ComboRules.Capped(30, 500, 1000), Is.EqualTo(0));
            Assert.That(ComboRules.Capped(0, 0, 1000), Is.EqualTo(0));
        }

        [Test]
        public void Tracker_CountsMaxAndBreaks()
        {
            GameEvents.ComboChanged += OnChanged;
            GameEvents.ComboBroken += OnBroken;
            var t = new ComboTracker();

            Assert.That(t.OnDrink(3, false, 100, 0), Is.EqualTo(0)); // x1
            Assert.That(t.OnDrink(3, false, 100, 100), Is.EqualTo(0)); // x2
            Assert.That(t.OnDrink(3, true, 100, 200), Is.EqualTo(10)); // x4: bậc 1
            Assert.That(lastTierUp, Is.True);
            Assert.That(t.Count, Is.EqualTo(4));
            Assert.That(changedCalls, Is.EqualTo(3));

            Assert.That(t.OnDrink(2, false, 100, 300), Is.EqualTo(0)); // 2 sao: đứt
            Assert.That(t.Count, Is.EqualTo(0));
            Assert.That(t.Max, Is.EqualTo(4));
            Assert.That(brokenCalls, Is.EqualTo(1));
            Assert.That(lastBroken, Is.EqualTo(4));

            t.Break(); // đã 0: không phát lần nữa
            Assert.That(brokenCalls, Is.EqualTo(1));
        }

        [Test]
        public void Tracker_PaysBeforeAnnouncing()
        {
            var order = new System.Collections.Generic.List<string>();
            System.Action<int, int, int, int, bool> h = (c, t, g, b, up) => order.Add("event");
            GameEvents.ComboChanged += h;
            try
            {
                var tr = new ComboTracker();
                for (int i = 0; i < 2; i++) tr.OnDrink(3, false, 100, 1000, 1f, b => order.Add("pay"));
                order.Clear();
                tr.OnDrink(3, false, 100, 1000, 1f, b => order.Add("pay" + b)); // x3: thưởng 10
                Assert.That(order, Is.EqualTo(new[] { "pay10", "event" }));
            }
            finally { GameEvents.ComboChanged -= h; }
        }

        [Test]
        public void Tracker_BonusRespectsDailyCap_AndResetClears()
        {
            var t = new ComboTracker();
            int paid = 0;
            for (int i = 0; i < 40; i++) paid += t.OnDrink(3, true, 300, 0); // doanh thu 0: trần = 60
            Assert.That(paid, Is.EqualTo(ComboRules.DailyCapFloor));
            Assert.That(t.BonusPaid, Is.EqualTo(paid));

            t.Reset();
            Assert.That(t.Count, Is.EqualTo(0));
            Assert.That(t.Max, Is.EqualTo(0));
            Assert.That(t.BonusPaid, Is.EqualTo(0));
        }
    }
}
