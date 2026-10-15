using NUnit.Framework;

namespace Cafe.Tests
{
    public class CupCollectRulesTests
    {
        [Test]
        public void Urgency_GrowsFromZeroToOneOverBlockTime()
        {
            Assert.That(CupCollectRules.Urgency(100f, 100f, 60f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(CupCollectRules.Urgency(130f, 100f, 60f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(CupCollectRules.Urgency(500f, 100f, 60f), Is.EqualTo(1f).Within(1e-4f), "kẹp ở 1");
            Assert.That(CupCollectRules.Urgency(90f, 100f, 60f), Is.EqualTo(0f).Within(1e-4f), "không âm");
        }

        [Test]
        public void Urgency_ZeroBlockTimeIsAlwaysUrgent()
        {
            Assert.That(CupCollectRules.Urgency(0f, 0f, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void Warning_StartsAtWarnFraction()
        {
            Assert.That(CupCollectRules.IsWarning(CupCollectRules.WarnFraction - 0.01f), Is.False);
            Assert.That(CupCollectRules.IsWarning(CupCollectRules.WarnFraction), Is.True);
        }

        [Test]
        public void PulseSpeed_IncreasesWithUrgency()
        {
            Assert.That(CupCollectRules.PulseSpeed(1f), Is.GreaterThan(CupCollectRules.PulseSpeed(0f)));
            Assert.That(CupCollectRules.PulseSpeed(5f), Is.EqualTo(CupCollectRules.PulseSpeed(1f)), "kẹp");
        }
    }
}
