using System;
using NUnit.Framework;

namespace Cafe.Tests
{
    public class StepActionsTests
    {
        [Test]
        public void HoldSeconds_AreShortButNeverTapLike()
        {
            foreach (BrewStep s in Enum.GetValues(typeof(BrewStep)))
            {
                float h = StepActions.HoldSeconds(s);
                if (StepActions.For(s) != StepAction.Hold) { Assert.That(h, Is.EqualTo(0f), s.ToString()); continue; }
                Assert.That(h, Is.InRange(0.3f, 0.7f), s.ToString());
                Assert.That(h, Is.GreaterThanOrEqualTo(3f * StepActions.AccidentalHoldSeconds), s.ToString());
            }
        }
    }
}
