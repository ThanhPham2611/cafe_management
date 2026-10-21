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

    public class ArrivalPacerTests
    {
        [Test]
        public void LoadLimit_GrowsWithServiceRate_AndIsClamped()
        {
            Assert.That(ArrivalPacer.LoadLimit(0f, 20), Is.EqualTo(ArrivalPacer.MinLoad));
            Assert.That(ArrivalPacer.LoadLimit(0.25f, 40), Is.GreaterThan(ArrivalPacer.LoadLimit(0.09f, 40)));
            Assert.That(ArrivalPacer.LoadLimit(5f, 10), Is.EqualTo(6), "trần 60% số ghế");
            Assert.That(ArrivalPacer.LoadLimit(5f, 2), Is.EqualTo(ArrivalPacer.MinLoad), "quán rất nhỏ vẫn giữ mức tối thiểu");
        }

        [Test]
        public void MinGap_ShrinksWhenServingFaster()
        {
            Assert.That(ArrivalPacer.MinGap(0.01f), Is.EqualTo(8f));
            Assert.That(ArrivalPacer.MinGap(10f), Is.EqualTo(1.5f));
            Assert.That(ArrivalPacer.MinGap(0.25f), Is.LessThan(ArrivalPacer.MinGap(0.09f)));
        }

        [Test]
        public void UpdateMu_TracksServedRate_WithinBounds()
        {
            float prior = ArrivalPacer.Prior(2);
            float mu = prior;
            for (int i = 0; i < 2000; i++) mu = ArrivalPacer.UpdateMu(mu, prior, i % 10 == 0 ? 1 : 0, 0.1f); // 1 khách/giây
            Assert.That(mu, Is.EqualTo(3f * prior).Within(0.03f), "nhanh hơn 3x prior thì kẹp ở 3x");
            for (int i = 0; i < 2000; i++) mu = ArrivalPacer.UpdateMu(mu, prior, 0, 0.1f);
            Assert.That(mu, Is.EqualTo(0.5f * prior).Within(1e-4f), "không phục vụ được ai thì không tụt dưới 0,5x");
            mu = prior;
            for (int i = 0; i < 4000; i++) mu = ArrivalPacer.UpdateMu(mu, prior, i % 50 == 0 ? 1 : 0, 0.1f); // 0,2 khách/giây
            Assert.That(mu, Is.EqualTo(0.2f).Within(0.03f));
        }
    }
}
