using NUnit.Framework;

namespace Cafe.Tests
{
    public class CanvasAspectMatchTests
    {
        [Test]
        public void WideScreens_MatchHeight()
        {
            Assert.That(CanvasAspectMatch.MatchFor(1920, 1080), Is.EqualTo(1f), "16:9");
            Assert.That(CanvasAspectMatch.MatchFor(2340, 1080), Is.EqualTo(1f), "19.5:9 điện thoại");
            Assert.That(CanvasAspectMatch.MatchFor(1560, 720), Is.EqualTo(1f), "20:9");
        }

        [Test]
        public void NarrowScreens_MatchWidth()
        {
            Assert.That(CanvasAspectMatch.MatchFor(1024, 768), Is.EqualTo(0f), "4:3 iPad");
            Assert.That(CanvasAspectMatch.MatchFor(1920, 1200), Is.EqualTo(0f), "16:10");
        }

        [Test]
        public void InvalidSize_DefaultsToHeight()
        {
            Assert.That(CanvasAspectMatch.MatchFor(0, 0), Is.EqualTo(1f));
        }
    }
}
