using NUnit.Framework;

namespace Cafe.Tests
{
    public class DayEventsTests
    {
        [Test]
        public void FirstTwoDays_AreAlwaysNormal_ForTheTutorial()
        {
            Assert.That(DayEvents.For(1), Is.SameAs(DayEvents.Normal));
            Assert.That(DayEvents.For(2), Is.SameAs(DayEvents.Normal));
        }

        [Test]
        public void SameDay_AlwaysGivesTheSameEvent_SoItCanBeAnnouncedInAdvance()
        {
            for (int day = 1; day <= 400; day++)
                Assert.That(DayEvents.For(day), Is.SameAs(DayEvents.For(day)));
        }

        [Test]
        public void Weekends_AreEveryDayCongruentSixOrZeroMod7()
        {
            for (int day = 3; day <= 400; day++)
            {
                bool weekend = day % 7 == 6 || day % 7 == 0;
                Assert.That(DayEvents.For(day) == DayEvents.Weekend, Is.EqualTo(weekend), "ngày " + day);
            }
        }

        [Test]
        public void Over700Days_EveryEventAppears_AndAverageDemandStaysNearOne()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            double demand = 0, tip = 0;
            int n = 0;
            for (int day = 1; day <= 700; day++)
            {
                DayEvent e = DayEvents.For(day);
                seen.Add(e.id);
                demand += e.demand;
                tip += e.tip;
                n++;
            }
            Assert.That(seen.Count, Is.EqualTo(DayEvents.All.Length), "mọi sự kiện đều xuất hiện");
            Assert.That(demand / n, Is.InRange(0.98, 1.08), "nhu cầu trung bình không lệch nhịp §21");
            Assert.That(tip / n, Is.InRange(0.98, 1.05));
        }

        [Test]
        public void FavoredRecipes_GetExtraWeight_OthersDoNot()
        {
            Assert.That(DayEvents.Heat.WeightFor("tra_da"), Is.EqualTo(2f));
            Assert.That(DayEvents.Heat.WeightFor("ca_phe_den"), Is.EqualTo(1f));
            Assert.That(DayEvents.Normal.WeightFor("tra_da"), Is.EqualTo(1f));
        }

        [Test]
        public void EveryEventHasANameAndBlurb_ExceptNormal()
        {
            foreach (DayEvent e in DayEvents.All)
            {
                Assert.That(e.name, Is.Not.Empty);
                if (!e.IsNormal) Assert.That(e.blurb, Is.Not.Empty);
            }
        }
    }
}
