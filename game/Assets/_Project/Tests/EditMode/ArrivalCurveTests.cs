using NUnit.Framework;

namespace Cafe.Tests
{
    public class ArrivalCurveTests
    {
        [Test]
        public void Map_StaysInsideSubWindowAndIsMonotonic()
        {
            float prev = 0.2f;
            for (int i = 0; i <= 20; i++)
            {
                float x = ArrivalCurve.Map(i / 20f, 0.2f, 0.7f);
                Assert.That(x, Is.InRange(0.2f, 0.7f));
                Assert.That(x, Is.GreaterThanOrEqualTo(prev - 1e-5f));
                prev = x;
            }
        }

        [Test]
        public void Map_EndpointsMatchWindow()
        {
            Assert.That(ArrivalCurve.Map(0f, 0.2f, 0.7f), Is.EqualTo(0.2f).Within(1e-3f));
            Assert.That(ArrivalCurve.Map(1f, 0.2f, 0.7f), Is.EqualTo(0.7f).Within(1e-3f));
        }

        [Test]
        public void Cdf_FirstTenthHoldsLessThanTenPercent()
        {
            Assert.That(ArrivalCurve.Cdf(0.1f), Is.LessThan(0.08f), "vào chậm đầu ngày");
            Assert.That(ArrivalCurve.Cdf(1f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Inverse_RoundTripsCdf()
        {
            Assert.That(ArrivalCurve.Inverse(ArrivalCurve.Cdf(0.55f)), Is.EqualTo(0.55f).Within(0.02f));
        }

        [Test]
        public void Map_DegenerateWindowReturnsStart()
        {
            Assert.That(ArrivalCurve.Map(0.5f, 0.4f, 0.4f), Is.EqualTo(0.4f));
        }
    }

    public class SpawnThrottleTests
    {
        [Test]
        public void HoldsWhenSeatQueueFull()
        {
            Assert.That(SpawnThrottle.ShouldHold(GameBalance.ThrottleSeatWait - 1, 0, 4), Is.False);
            Assert.That(SpawnThrottle.ShouldHold(GameBalance.ThrottleSeatWait, 0, 4), Is.True);
        }

        [Test]
        public void ServiceLimitScalesWithSeats()
        {
            Assert.That(SpawnThrottle.ShouldHold(0, 3, 4), Is.True, "quán nhỏ: 3 đơn chờ là nghẽn");
            Assert.That(SpawnThrottle.ShouldHold(0, 10, 40), Is.False, "quán lớn: 20 đơn mới nghẽn");
            Assert.That(SpawnThrottle.ShouldHold(0, 20, 40), Is.True);
        }
    }
}
