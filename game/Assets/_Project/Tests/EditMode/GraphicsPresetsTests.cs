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
            var only60 = new[] { 60 };
            Assert.That(GraphicsPresets.TargetFps(GraphicsLevel.Low, 60, only60), Is.EqualTo(30));
            Assert.That(GraphicsPresets.TargetFps(GraphicsLevel.Medium, 60, only60), Is.EqualTo(60));
            Assert.That(GraphicsPresets.TargetFps(GraphicsLevel.High, 60, only60), Is.EqualTo(60));
            Assert.That(GraphicsPresets.UiOutlines(GraphicsLevel.Low), Is.False);
            Assert.That(GraphicsPresets.UiOutlines(GraphicsLevel.Medium), Is.True);
            Assert.That(GraphicsPresets.UiOutlines(GraphicsLevel.High), Is.True);
        }

        [TestCase(60, new[] { 60, 90, 120 }, 60)]    // màn 120 Hz nhưng người chơi giữ 60 (mặc định)
        [TestCase(90, new[] { 60, 90, 120 }, 90)]
        [TestCase(120, new[] { 60, 90, 120 }, 120)]
        [TestCase(120, new[] { 60, 90 }, 90)]        // lựa chọn đã lưu cao hơn màn hình mới: hạ về mức có thật
        [TestCase(90, new[] { 60, 120 }, 60)]        // 90 đã lưu nhưng máy này không có 90: không chọn bừa 120
        [TestCase(120, new[] { 60 }, 60)]
        [TestCase(30, new[] { 60, 120 }, 60)]        // giá trị lưu lạ không bao giờ xuống dưới 60
        public void TargetFps_UsesHighestChoiceNotAbovePreference(int preferred, int[] choices, int expected)
        {
            Assert.That(GraphicsPresets.TargetFps(GraphicsLevel.High, preferred, choices), Is.EqualTo(expected));
            Assert.That(GraphicsPresets.TargetFps(GraphicsLevel.Low, preferred, choices), Is.EqualTo(30));
        }

        [TestCase(new[] { 60 }, false, new[] { 60 })]
        [TestCase(new[] { 0 }, false, new[] { 60 })]                      // đọc tần số quét thất bại
        [TestCase(new[] { 60, 90 }, false, new[] { 60, 90 })]             // Android 90 Hz
        [TestCase(new[] { 120, 60, 90, 120 }, false, new[] { 60, 90, 120 })]
        [TestCase(new[] { 60, 120 }, false, new[] { 60, 120 })]           // Android chỉ có chế độ 60 / 120: không tự thêm 90
        [TestCase(new[] { 60, 90, 120, 144 }, false, new[] { 60, 120, 144 })] // 3 nút là đủ: giữ hai mức cao nhất
        [TestCase(new[] { 120 }, true, new[] { 60, 120 })]                // iPhone ProMotion
        [TestCase(new[] { 60, 90, 120 }, true, new[] { 60, 120 })]        // iOS không có 90
        [TestCase(new[] { 90 }, true, new[] { 60 })]
        [TestCase(new[] { 60 }, true, new[] { 60 })]
        public void FpsChoices_OnlyOffersRatesTheDisplayReallyHas(int[] rates, bool ios, int[] expected)
        {
            Assert.That(GraphicsPresets.FpsChoices(rates, ios), Is.EqualTo(expected));
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
                Assert.That(GraphicsPresets.LightingNote(level), Is.Not.Empty);
            }
        }

        [Test]
        public void LampLights_LowHasNone_AndGrowWithLevel()
        {
            Assert.That(GraphicsPresets.LampLights(GraphicsLevel.Low), Is.EqualTo(0));
            Assert.That(GraphicsPresets.LampRange(GraphicsLevel.Low), Is.EqualTo(0f));
            Assert.That(GraphicsPresets.LampIntensity(GraphicsLevel.Low), Is.EqualTo(0f));
            Assert.That(GraphicsPresets.LampLights(GraphicsLevel.Medium), Is.GreaterThan(0));
            Assert.That(GraphicsPresets.LampLights(GraphicsLevel.Medium), Is.LessThanOrEqualTo(GraphicsPresets.LampLights(GraphicsLevel.High)));
            Assert.That(GraphicsPresets.LampRange(GraphicsLevel.Medium), Is.GreaterThan(0f));
            Assert.That(GraphicsPresets.LampRange(GraphicsLevel.Medium), Is.LessThanOrEqualTo(GraphicsPresets.LampRange(GraphicsLevel.High)));
            Assert.That(GraphicsPresets.LampIntensity(GraphicsLevel.Medium), Is.GreaterThan(0f));
            Assert.That(GraphicsPresets.LampSoftness(GraphicsLevel.Medium), Is.LessThanOrEqualTo(GraphicsPresets.LampSoftness(GraphicsLevel.High)));
            foreach (GraphicsLevel level in GraphicsPresets.All)
                Assert.That(GraphicsPresets.LampSoftness(level), Is.InRange(0f, 1f));
        }

        [Test]
        public void LightingNote_MentionsTheLampCountOfTheLevel()
        {
            Assert.That(GraphicsPresets.LightingNote(GraphicsLevel.Medium), Does.Contain(GraphicsPresets.LampLights(GraphicsLevel.Medium).ToString()));
            Assert.That(GraphicsPresets.LightingNote(GraphicsLevel.High), Does.Contain(GraphicsPresets.LampLights(GraphicsLevel.High).ToString()));
        }

        [TestCase("Lamp_Bulb", true)]
        [TestCase("Lamp_Desk", true)]
        [TestCase("Lamp_Floor", true)]
        [TestCase("Lamp_Pendant", true)]
        [TestCase("Lantern_Hoian", true)]
        [TestCase("Chandelier_Brass", true)]
        [TestCase("Fan_Ceiling", false)]
        [TestCase("", false)]
        public void IsLamp_MatchesTheLampItems(string id, bool expected)
        {
            Assert.That(LampLights.IsLamp(id), Is.EqualTo(expected));
        }

        [Test]
        public void LampLights_FitTheUrpPerObjectLimit()
        {
            // UrpSetup đặt m_AdditionalLightsPerObjectLimit = max(4, đèn); URP chỉ nhận tối đa 8 đèn phụ mỗi vật
            foreach (GraphicsLevel level in GraphicsPresets.All)
                Assert.That(GraphicsPresets.LampLights(level), Is.LessThanOrEqualTo(8));
        }
    }
}
