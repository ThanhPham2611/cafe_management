using NUnit.Framework;

namespace Cafe.Tests
{
    public class RegularsManagerTests
    {
        [Test]
        public void Progress_LoadClampsBondAndChapterCount()
        {
            var progress = RegularProgress.FromSave(
                new RegularSave { id = "ba_sau", bond = 99, chaptersDone = -3, lastVisitDay = 4 }, 4);

            Assert.That(progress.Bond, Is.EqualTo(10));
            Assert.That(progress.ChaptersDone, Is.Zero);
            Assert.That(progress.LastVisitDay, Is.EqualTo(4));
        }

        [TestCase(true, 3, 10f, 100f, 1)]
        [TestCase(true, 2, 35f, 75f, 0)]
        [TestCase(false, 3, 10f, 100f, 0)]
        [TestCase(false, 1, 80f, 30f, 0)]
        public void BondGain_UsesFavoriteQualitySpeedAndCleanliness(
            bool favorite, int quality, float wait, float cleanliness, int expected)
        {
            Assert.That(RegularProgressRules.BondGain(favorite, quality, wait, cleanliness), Is.EqualTo(expected));
        }

        [Test]
        public void Tokens_ExpandOwnerShopAndSpeakerName()
        {
            string text = RegularText.Expand("{owner} ở {shop}, chào {name}!", "An", "Cà Phê Hẻm", "Bà Sáu");
            Assert.That(text, Is.EqualTo("An ở Cà Phê Hẻm, chào Bà Sáu!"));
        }

        [Test]
        public void CompletingChapter_IsIdempotent()
        {
            var progress = RegularProgress.FromSave(new RegularSave { id = "minh" }, 4);
            Assert.That(progress.TryCompleteChapter(0), Is.True);
            Assert.That(progress.TryCompleteChapter(0), Is.False);
            Assert.That(progress.ChaptersDone, Is.EqualTo(1));
        }

        [Test]
        public void InitialMilestone_ShipsOnlyThreeChaptersPerRegular()
        {
            var definition = new RegularDefinition { chapters = new StoryChapterDefinition[4] };
            Assert.That(RegularsManager.ChapterCount(definition), Is.EqualTo(3));
        }
    }
}
