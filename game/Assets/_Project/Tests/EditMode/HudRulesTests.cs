using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    public class HudRulesTests
    {
        [TestCase(0f, 0f)]
        [TestCase(1f, 1f)]
        [TestCase(-0.5f, 0f)]
        [TestCase(1.7f, 1f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(0.009f, 0f)]    // dưới nửa bậc (0,01) về 0
        [TestCase(0.011f, 0.02f)] // trên nửa bậc lên bậc kế
        [TestCase(0.9999f, 1f)]
        public void QuantizeRing_SnapsToFiftySteps(float v, float expected)
        {
            Assert.That(HudRules.QuantizeRing(v), Is.EqualTo(expected).Within(1e-5f));
        }

        [Test]
        public void QuantizeRing_NearbyValuesShareOneStep()
        {
            Assert.That(HudRules.QuantizeRing(0.4101f), Is.EqualTo(HudRules.QuantizeRing(0.4139f)));
        }

        [Test]
        public void SnapPosition_RoundsToHalfPixel()
        {
            Assert.That(HudRules.SnapPosition(new Vector2(100.1f, 200.3f)), Is.EqualTo(new Vector2(100f, 200.5f)));
            Assert.That(HudRules.SnapPosition(new Vector2(-10.26f, 0.74f)), Is.EqualTo(new Vector2(-10.5f, 0.5f)));
            Assert.That(HudRules.SnapPosition(new Vector2(0.5f, 0f)), Is.EqualTo(new Vector2(0.5f, 0f)));
        }
    }
}
