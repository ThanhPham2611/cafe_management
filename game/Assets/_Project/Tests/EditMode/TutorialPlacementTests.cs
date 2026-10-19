using NUnit.Framework;

namespace Cafe.Tests
{
    /// Thẻ lời thoại bà Sáu khi bảng modal (tổng kết) đang mở: cạnh bảng nếu đủ chỗ, không thì đáy màn hình né vùng chỉ
    /// (docs/hud_visual_polish_2026-10-18.md §6).
    public class TutorialPlacementTests
    {
        [Test]
        public void SideFreeWidth_UsesWiderSide_MinusMargins()
        {
            Assert.That(TutorialRules.SideFreeWidth(360f, 1560f, 1920f, 10f), Is.EqualTo(340f).Within(0.01f));
            Assert.That(TutorialRules.SideFreeWidth(100f, 1500f, 1920f, 10f), Is.EqualTo(400f).Within(0.01f));
            Assert.That(TutorialRules.SideFreeWidth(0f, 1440f, 1440f, 10f), Is.LessThan(0f));
        }

        [Test]
        public void BottomCenter_NoHole_IsScreenCenter()
        {
            Assert.That(TutorialRules.PickBottomCenterX(false, 0f, 0f, 1920f, 600f, 16f), Is.EqualTo(960f));
        }

        [Test]
        public void BottomCenter_PrefersLeftOfHole_ThenRight()
        {
            // nút "Ngày mới" giữa đáy (672..1248): trái đủ chỗ cho thẻ 600
            float cx = TutorialRules.PickBottomCenterX(true, 672f, 1248f, 1920f, 600f, 16f);
            Assert.That(cx, Is.EqualTo(316f).Within(0.01f));
            Assert.That(cx + 300f, Is.LessThanOrEqualTo(672f), "thẻ không đè lên vùng chỉ");
            // lỗ sát trái: thẻ sang phải
            float cx2 = TutorialRules.PickBottomCenterX(true, 40f, 500f, 1920f, 600f, 16f);
            Assert.That(cx2, Is.EqualTo(1920f - 16f - 300f).Within(0.01f));
            Assert.That(cx2 - 300f, Is.GreaterThanOrEqualTo(500f));
        }

        [Test]
        public void BottomCenter_NoSideFits_PicksWiderSide_StaysOnScreen()
        {
            float cx = TutorialRules.PickBottomCenterX(true, 500f, 900f, 1440f, 600f, 16f);
            Assert.That(cx, Is.EqualTo(1440f - 16f - 300f).Within(0.01f)); // phải rộng hơn (540 > 500)
            Assert.That(cx + 300f, Is.LessThanOrEqualTo(1440f));
        }
    }
}
