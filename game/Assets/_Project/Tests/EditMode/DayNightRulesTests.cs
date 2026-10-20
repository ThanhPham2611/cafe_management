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
        public void Night_IsMoonlit_BrightEnoughToPlay_ButDarkerThanDay()
        {
            DayNightLook day = DayNightRules.Sample(12f);
            DayNightLook night = DayNightRules.Sample(21f);
            Assert.That(night.sunIntensity, Is.InRange(0.8f, 1.0f), "đêm phải đủ sáng để nhìn rõ quán");
            float sunRatio = Lum(night.sun) * night.sunIntensity / (Lum(day.sun) * day.sunIntensity);
            Assert.That(sunRatio, Is.InRange(0.35f, 0.75f), "nắng đêm so với nắng ngày");
            Assert.That(Lum(night.equator) / Lum(day.equator), Is.InRange(0.4f, 0.8f), "ambient đêm so với ngày");
            Assert.That(Lum(night.sky), Is.LessThan(Lum(day.sky)));
            Assert.That(Lum(night.equator), Is.LessThan(Lum(day.equator)));
            Assert.That(Lum(night.ground), Is.LessThan(Lum(day.ground)));
            Assert.That(Lum(night.background), Is.LessThan(Lum(day.background)), "nền camera đêm vẫn tối hơn ngày");
            Assert.That(night.sun.b, Is.GreaterThan(night.sun.r), "ánh trăng ngả xanh");
        }

        [Test]
        public void Lamps_AreOffAtNoon_FadeInAtDusk_AndFullByNight()
        {
            Assert.That(DayNightRules.Sample(12f).night, Is.EqualTo(0f));
            Assert.That(DayNightRules.Sample(17.5f).night, Is.EqualTo(0f));
            float prev = 0f;
            for (float h = 17.5f; h <= 19f; h += 0.1f)
            {
                float n = DayNightRules.Sample(h).night;
                Assert.That(n, Is.GreaterThanOrEqualTo(prev), "h=" + h);
                prev = n;
            }
            Assert.That(DayNightRules.Sample(18.5f).night, Is.InRange(0.5f, 0.9f));
            Assert.That(DayNightRules.Sample(19.5f).night, Is.EqualTo(1f));
        }

        private static float Lum(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
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
