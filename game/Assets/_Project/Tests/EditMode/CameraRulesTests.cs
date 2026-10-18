using NUnit.Framework;
using UnityEngine;

namespace Cafe.Tests
{
    public class CameraRulesTests
    {
        [Test]
        public void ClampPitch_FreeAllowsEyeLevel_ButNotBelowHardMin()
        {
            Assert.That(CameraRules.ClampPitch(8f, false, false), Is.EqualTo(8f));
            Assert.That(CameraRules.ClampPitch(0f, false, false), Is.EqualTo(CameraRules.HardMinPitch));
            Assert.That(CameraRules.ClampPitch(200f, false, false), Is.EqualTo(CameraRules.MaxPitch));
        }

        [Test]
        public void ClampPitch_BuildAndTutorialFloorsRaiseTheMinimum()
        {
            Assert.That(CameraRules.ClampPitch(8f, true, false), Is.EqualTo(CameraRules.BuildMinPitch));
            Assert.That(CameraRules.ClampPitch(8f, false, true), Is.EqualTo(CameraRules.TutorialMinPitch));
            Assert.That(CameraRules.ClampPitch(8f, true, true), Is.EqualTo(CameraRules.BuildMinPitch), "lấy sàn cao nhất");
            Assert.That(CameraRules.ClampPitch(55f, true, true), Is.EqualTo(55f));
        }

        [Test]
        public void PivotY_TopDownKeepsTableHeight_EyeLevelIsRaisedAndMonotonic()
        {
            Assert.That(CameraRules.PivotY(48f), Is.EqualTo(CameraRules.PivotYTop).Within(1e-4f));
            Assert.That(CameraRules.PivotY(30f), Is.EqualTo(CameraRules.PivotYTop).Within(1e-4f));
            Assert.That(CameraRules.PivotY(8f), Is.EqualTo(CameraRules.PivotYEye).Within(1e-4f));
            float prev = CameraRules.PivotY(6f);
            for (float p = 7f; p <= 70f; p += 1f)
            {
                float y = CameraRules.PivotY(p);
                Assert.That(y, Is.LessThanOrEqualTo(prev + 1e-5f), "p=" + p);
                prev = y;
            }
        }

        [Test]
        public void CameraHeight_EyePreset_NeverBelowAdultHeadHeight()
        {
            for (float dist = 5f; dist <= 23.4f; dist += 0.5f)
                Assert.That(CameraRules.CameraHeight(CameraRules.PivotY(CameraRules.PresetEye), dist, CameraRules.PresetEye), Is.GreaterThanOrEqualTo(1.2f), "dist=" + dist);
        }

        [Test]
        public void PanForwardScale_IsCapped()
        {
            Assert.That(CameraRules.PanForwardScale(6f), Is.LessThanOrEqualTo(1.82f));
            Assert.That(CameraRules.PanForwardScale(90f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void DragPitchScale_HalvedBelowTwenty()
        {
            Assert.That(CameraRules.DragPitchScale(10f), Is.EqualTo(0.5f));
            Assert.That(CameraRules.DragPitchScale(19.9f), Is.EqualTo(0.5f));
            Assert.That(CameraRules.DragPitchScale(20f), Is.EqualTo(1f));
            Assert.That(CameraRules.DragPitchScale(48f), Is.EqualTo(1f));
        }

        [Test]
        public void WallDropped_HasHysteresis()
        {
            Assert.That(CameraRules.WallDropped(0.1f, false), Is.False, "chưa ra đủ xa phía ngoài");
            Assert.That(CameraRules.WallDropped(0.3f, false), Is.True);
            Assert.That(CameraRules.WallDropped(0.0f, true), Is.True, "đang hạ thì giữ đến khi vào hẳn trong");
            Assert.That(CameraRules.WallDropped(-0.3f, true), Is.False);
        }

        [Test]
        public void WallDropped_CameraInside_NeverDrops()
        {
            for (float m = -20f; m <= 0f; m += 0.5f)
            {
                Assert.That(CameraRules.WallDropped(m, false), Is.False, "m=" + m);
                if (m < -0.2f) Assert.That(CameraRules.WallDropped(m, true), Is.False, "m=" + m);
            }
        }

        [Test]
        public void HideOverhead_HasHysteresis()
        {
            Assert.That(CameraRules.HideOverhead(10f, false), Is.True);
            Assert.That(CameraRules.HideOverhead(15f, false), Is.False, "chưa tụt dưới 14°");
            Assert.That(CameraRules.HideOverhead(15f, true), Is.True, "đang ẩn thì giữ đến 18°");
            Assert.That(CameraRules.HideOverhead(19f, true), Is.False);
            Assert.That(CameraRules.HideOverhead(48f, false), Is.False);
        }

        [Test]
        public void DistanceFactor_PullsInAtEyeLevel_AndKeepsTopDownZoom()
        {
            Assert.That(CameraRules.DistanceFactor(8f), Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(CameraRules.DistanceFactor(30f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(CameraRules.DistanceFactor(48f), Is.EqualTo(1f));
            float prev = CameraRules.DistanceFactor(6f);
            for (float p = 7f; p <= 70f; p += 1f)
            {
                float f = CameraRules.DistanceFactor(p);
                Assert.That(f, Is.GreaterThanOrEqualTo(prev - 1e-5f), "p=" + p);
                prev = f;
            }
        }

        [Test]
        public void HideNearCamera_OnlyAtLowPitchAndClose_WithHysteresis()
        {
            Assert.That(CameraRules.HideNearCamera(1.0f, 8f, false), Is.True);
            Assert.That(CameraRules.HideNearCamera(1.8f, 8f, false), Is.False, "chưa sát đủ");
            Assert.That(CameraRules.HideNearCamera(1.8f, 8f, true), Is.True, "đang ẩn thì giữ đến 2,0 m");
            Assert.That(CameraRules.HideNearCamera(2.2f, 8f, true), Is.False);
            Assert.That(CameraRules.HideNearCamera(0.5f, 48f, false), Is.False, "nhìn từ trên thì không ẩn");
        }

        [Test]
        public void DecideAxis_HorizontalLocksYaw_VerticalLocksPitch_ShortIsUndecided()
        {
            Assert.That(CameraRules.DecideAxis(new Vector2(5f, 2f), 40f), Is.EqualTo(CameraRules.DragAxis.Undecided));
            Assert.That(CameraRules.DecideAxis(new Vector2(80f, 30f), 40f), Is.EqualTo(CameraRules.DragAxis.Yaw));
            Assert.That(CameraRules.DecideAxis(new Vector2(20f, 80f), 40f), Is.EqualTo(CameraRules.DragAxis.Pitch));
            Assert.That(CameraRules.DecideAxis(new Vector2(60f, 55f), 40f), Is.EqualTo(CameraRules.DragAxis.Pitch), "chéo gần 45° vẫn chưa đủ trội ngang");
            Assert.That(CameraRules.DecideAxis(new Vector2(-90f, 40f), 40f), Is.EqualTo(CameraRules.DragAxis.Yaw), "kéo trái cũng là ngang");
        }

        [Test]
        public void NextPreset_CyclesTopMidEyeTop()
        {
            Assert.That(CameraRules.NextPreset(CameraRules.PresetTop), Is.EqualTo(CameraRules.PresetMid));
            Assert.That(CameraRules.NextPreset(CameraRules.PresetMid), Is.EqualTo(CameraRules.PresetEye));
            Assert.That(CameraRules.NextPreset(CameraRules.PresetEye), Is.EqualTo(CameraRules.PresetTop));
            Assert.That(CameraRules.NextPreset(52f), Is.EqualTo(CameraRules.PresetEye), "gần Chéo nhất");
            Assert.That(CameraRules.NextPreset(6f), Is.EqualTo(CameraRules.PresetTop));
        }

        [Test]
        public void PresetIndex_NearestPreset()
        {
            Assert.That(CameraRules.PresetIndex(70f), Is.EqualTo(0));
            Assert.That(CameraRules.PresetIndex(48f), Is.EqualTo(1));
            Assert.That(CameraRules.PresetIndex(15f), Is.EqualTo(2));
        }
    }

    public class TapAssistDepthTests
    {
        [Test]
        public void Nearest_PrefersNearerCupWhenScreenPositionsOverlap()
        {
            var pts = new[] { new Vector2(100, 100), new Vector2(106, 100) };
            var depths = new[] { 9f, 5f };
            Assert.That(TapAssistRules.Nearest(new Vector2(100, 100), pts, 60f, depths), Is.EqualTo(1), "ly gần camera thắng khi chỉ lệch 6 px");
        }

        [Test]
        public void Nearest_FarApartOnScreen_KeepsNearestOnScreen()
        {
            var pts = new[] { new Vector2(100, 100), new Vector2(150, 100) };
            var depths = new[] { 9f, 5f };
            Assert.That(TapAssistRules.Nearest(new Vector2(100, 100), pts, 60f, depths), Is.EqualTo(0));
        }
    }
}
