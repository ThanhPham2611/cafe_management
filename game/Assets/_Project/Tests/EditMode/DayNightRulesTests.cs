using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    public class DayNightRulesTests
    {
        [Test]
        public void Noon_MatchesBakedScene()
        {
            DayNightLook l = DayNightRules.Sample(12f);
            Assert.That(l.sunIntensity, Is.EqualTo(1.15f).Within(1e-4f));
            Assert.That(l.pitch, Is.EqualTo(38f).Within(1e-4f));
            Assert.That(l.yaw, Is.EqualTo(-35f).Within(1e-4f));
            Assert.That(l.night, Is.EqualTo(0f));
        }

        [Test]
        public void Night_IsLitAtClosing_AndOffByDay()
        {
            Assert.That(DayNightRules.Sample(22f).night, Is.EqualTo(1f));
            for (float h = 8.5f; h <= 15f; h += 0.5f) Assert.That(DayNightRules.Sample(h).night, Is.EqualTo(0f), "h=" + h);
        }

        [Test]
        public void SunStaysReadable_AndPitchClamped()
        {
            for (float h = 5f; h <= 23f; h += 0.1f)
            {
                DayNightLook l = DayNightRules.Sample(h);
                Assert.That(l.pitch, Is.InRange(DayNightRules.MinPitch, DayNightRules.MaxPitch), "h=" + h);
                Assert.That(l.sunIntensity, Is.GreaterThanOrEqualTo(DayNightRules.MinSunIntensity), "h=" + h);
            }
        }

        [Test]
        public void Look_IsContinuous()
        {
            DayNightLook prev = DayNightRules.Sample(6f);
            for (float h = 6.05f; h <= 22f; h += 0.05f)
            {
                DayNightLook l = DayNightRules.Sample(h);
                Assert.That(Mathf.Abs(l.sunIntensity - prev.sunIntensity), Is.LessThan(0.1f), "h=" + h);
                Assert.That(Mathf.Abs(l.pitch - prev.pitch), Is.LessThan(5f), "h=" + h);
                prev = l;
            }
        }

        [Test]
        public void OutOfRangeHours_AreClamped()
        {
            Assert.That(DayNightRules.Sample(0f).sunIntensity, Is.EqualTo(DayNightRules.Sample(6f).sunIntensity));
            Assert.That(DayNightRules.Sample(30f).night, Is.EqualTo(1f));
        }
    }
}
