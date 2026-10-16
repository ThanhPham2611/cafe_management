using NUnit.Framework;

namespace Cafe.Tests
{
    public class BootRulesTests
    {
        [Test]
        public void CanDismiss_NeedsBuildAndMinimumDisplayTime()
        {
            Assert.That(BootRules.CanDismiss(0.5f, true), Is.False, "chưa đủ 1,5 s");
            Assert.That(BootRules.CanDismiss(3f, false), Is.False, "Build chưa xong");
            Assert.That(BootRules.CanDismiss(1.5f, true), Is.True);
        }

        [Test]
        public void CanDismiss_GivesUpAfterMaxWait()
        {
            Assert.That(BootRules.CanDismiss(BootRules.MaxWaitSeconds, false), Is.True);
        }

        [Test]
        public void DisplayProgress_MapsSceneThenBuild()
        {
            Assert.That(BootRules.DisplayProgress(0f, false, false), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(BootRules.DisplayProgress(0.45f, false, false), Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(BootRules.DisplayProgress(1f, true, false), Is.EqualTo(0.9f).Within(1e-4f), "dựng quán: dừng ở 90%");
            Assert.That(BootRules.DisplayProgress(1f, true, true), Is.EqualTo(1f));
        }

        [Test]
        public void Smooth_OnlyMovesForward_AndCapsAtTarget()
        {
            Assert.That(BootRules.Smooth(0.5f, 0.2f, 1f, 1f), Is.EqualTo(0.5f));
            Assert.That(BootRules.Smooth(0.1f, 0.9f, 0.1f, 1f), Is.EqualTo(0.2f).Within(1e-4f));
            Assert.That(BootRules.Smooth(0.1f, 0.15f, 1f, 1f), Is.EqualTo(0.15f).Within(1e-4f));
        }

        [Test]
        public void PickTip_WrapsAndHandlesEmpty()
        {
            var tips = new[] { "a", "b", "c" };
            Assert.That(BootRules.PickTip(tips, 4), Is.EqualTo("b"));
            Assert.That(BootRules.PickTip(tips, -1), Is.EqualTo("c"));
            Assert.That(BootRules.PickTip(new string[0], 3), Is.EqualTo(""));
            Assert.That(BootRules.PickTip(null, 3), Is.EqualTo(""));
            Assert.That(BootStrings.Tips.Length, Is.GreaterThan(2));
        }

        [Test]
        public void LogoPopScale_Goes08_To105_To1()
        {
            Assert.That(BootRules.LogoPopScale(0f), Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(BootRules.LogoPopScale(0.6f), Is.EqualTo(1.05f).Within(1e-4f));
            Assert.That(BootRules.LogoPopScale(1f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(BootRules.LogoPopScale(5f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ShouldSkipTitle_ForBotsAndBatchmode_ButNotForceFlag()
        {
            Assert.That(BootRules.ShouldSkipTitle(new[] { "game.exe" }, false), Is.False);
            Assert.That(BootRules.ShouldSkipTitle(new[] { "game.exe", "-freshsave" }, false), Is.False);
            Assert.That(BootRules.ShouldSkipTitle(new[] { "game.exe" }, true), Is.True);
            Assert.That(BootRules.ShouldSkipTitle(new[] { "game.exe", "-autoplay" }, false), Is.True);
            Assert.That(BootRules.ShouldSkipTitle(new[] { "game.exe", "-autoshot", "dir" }, false), Is.True);
            Assert.That(BootRules.ShouldSkipTitle(new[] { "game.exe", "-cheatxu" }, false), Is.True);
            Assert.That(BootRules.ShouldSkipTitle(new[] { "game.exe", "-perf" }, false), Is.True);
            Assert.That(BootRules.ShouldSkipTitle(new[] { "game.exe", "-autoshot", "d", "-title" }, true), Is.False, "-title ép hiện");
        }
    }
}
