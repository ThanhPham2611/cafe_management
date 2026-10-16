using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    public class CounterMountRulesTests
    {
        private static readonly Rect Area = new Rect(-0.1f, -0.3f, 1.0f, 0.6f);

        [Test]
        public void Contains_FreeHalfOfTheCounter()
        {
            Assert.That(CounterMountRules.Contains(Area, new Vector3(0.25f, 1.05f, 0.25f)), Is.True);
            Assert.That(CounterMountRules.Contains(Area, new Vector3(0.75f, 1.05f, -0.25f)), Is.True);
        }

        [Test]
        public void Contains_RejectsDisplayCabinetSide()
        {
            Assert.That(CounterMountRules.Contains(Area, new Vector3(-0.75f, 1.05f, 0.25f)), Is.False);
            Assert.That(CounterMountRules.Contains(Area, new Vector3(-0.25f, 1.05f, 0.25f)), Is.False);
        }

        [Test]
        public void Contains_IgnoresHeight()
        {
            Assert.That(CounterMountRules.Contains(Area, new Vector3(0.25f, 0f, 0.25f)), Is.True);
        }
    }
}
