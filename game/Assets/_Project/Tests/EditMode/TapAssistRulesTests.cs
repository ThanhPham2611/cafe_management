using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    public class TapAssistRulesTests
    {
        [Test]
        public void Nearest_PicksClosestWithinRadius()
        {
            var pts = new[] { new Vector2(100, 0), new Vector2(40, 0) };
            Assert.That(TapAssistRules.Nearest(Vector2.zero, pts, 60f), Is.EqualTo(1));
        }

        [Test]
        public void Nearest_NoneOutsideRadius()
        {
            var pts = new[] { new Vector2(100, 0) };
            Assert.That(TapAssistRules.Nearest(Vector2.zero, pts, 60f), Is.EqualTo(-1));
        }

        [Test]
        public void Nearest_TieKeepsLowestIndex()
        {
            var pts = new[] { new Vector2(30, 0), new Vector2(-30, 0) };
            Assert.That(TapAssistRules.Nearest(Vector2.zero, pts, 60f), Is.EqualTo(0));
        }

        [Test]
        public void Radius_HasFloor()
        {
            Assert.That(TapAssistRules.RadiusPx(400f), Is.EqualTo(TapAssistRules.MinRadiusPx));
            Assert.That(TapAssistRules.RadiusPx(2000f), Is.EqualTo(120f).Within(1e-3f));
        }
    }
}
