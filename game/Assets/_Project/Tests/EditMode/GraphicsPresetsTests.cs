using NUnit.Framework;

namespace Cafe.Tests
{
    public class GraphicsPresetsTests
    {
        [Test]
        public void ThreeLevels_MatchQualityIndexOrder()
        {
            Assert.That(GraphicsPresets.Count, Is.EqualTo(3));
            Assert.That((int)GraphicsLevel.Low, Is.EqualTo(0));
            Assert.That((int)GraphicsLevel.Medium, Is.EqualTo(1));
            Assert.That((int)GraphicsLevel.High, Is.EqualTo(2));
            Assert.That(GraphicsPresets.All, Is.EqualTo(new[] { GraphicsLevel.Low, GraphicsLevel.Medium, GraphicsLevel.High }));
        }

        [Test]
        public void Clamp_KeepsIndexInRange()
        {
            Assert.That(GraphicsPresets.Clamp(-5), Is.EqualTo(GraphicsLevel.Low));
            Assert.That(GraphicsPresets.Clamp(1), Is.EqualTo(GraphicsLevel.Medium));
            Assert.That(GraphicsPresets.Clamp(99), Is.EqualTo(GraphicsLevel.High));
        }

        [Test]
        public void LowIsTheLightest_HighIsTheRichest()
        {
            Assert.That(GraphicsPresets.TargetFps(GraphicsLevel.Low), Is.EqualTo(30));
            Assert.That(GraphicsPresets.TargetFps(GraphicsLevel.Medium), Is.EqualTo(60));
            Assert.That(GraphicsPresets.TargetFps(GraphicsLevel.High), Is.EqualTo(60));
            Assert.That(GraphicsPresets.UiOutlines(GraphicsLevel.Low), Is.False);
            Assert.That(GraphicsPresets.UiOutlines(GraphicsLevel.Medium), Is.True);
            Assert.That(GraphicsPresets.UiOutlines(GraphicsLevel.High), Is.True);
        }

        [Test]
        public void Recommend_DesktopIsAlwaysHigh()
        {
            Assert.That(GraphicsPresets.Recommend(2000, 2, false), Is.EqualTo(GraphicsLevel.High));
            Assert.That(GraphicsPresets.Recommend(32000, 16, false), Is.EqualTo(GraphicsLevel.High));
        }

        [TestCase(2048, 8, GraphicsLevel.Low)]    // ít RAM
        [TestCase(3200, 8, GraphicsLevel.Low)]
        [TestCase(8192, 4, GraphicsLevel.Low)]    // ít nhân
        [TestCase(4096, 8, GraphicsLevel.Medium)] // máy tầm trung
        [TestCase(6000, 6, GraphicsLevel.Medium)] // RAM nhiều nhưng ít nhân
        [TestCase(5400, 8, GraphicsLevel.Medium)]
        [TestCase(5500, 8, GraphicsLevel.High)]
        [TestCase(12000, 8, GraphicsLevel.High)]
        public void Recommend_PhoneByRamAndCores(int ramMb, int cores, GraphicsLevel expected)
        {
            Assert.That(GraphicsPresets.Recommend(ramMb, cores, true), Is.EqualTo(expected));
        }

        [Test]
        public void EveryLevelHasNameAndDescription()
        {
            foreach (GraphicsLevel level in GraphicsPresets.All)
            {
                Assert.That(GraphicsPresets.Name(level), Is.Not.Empty);
                Assert.That(GraphicsPresets.Description(level), Is.Not.Empty);
            }
        }
    }
}
