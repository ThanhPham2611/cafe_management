using NUnit.Framework;

namespace Cafe.Tests
{
    public class GpuTiersTests
    {
        [TestCase("Adreno (TM) 506", GraphicsLevel.Low)]
        [TestCase("Adreno (TM) 610", GraphicsLevel.Medium)]
        [TestCase("Adreno (TM) 619", GraphicsLevel.Medium)]
        [TestCase("Adreno (TM) 642L", GraphicsLevel.Medium)]
        [TestCase("Adreno (TM) 650", GraphicsLevel.High)]
        [TestCase("Adreno (TM) 662", GraphicsLevel.Medium)]
        [TestCase("Adreno (TM) 710", GraphicsLevel.Medium)]
        [TestCase("Adreno (TM) 740", GraphicsLevel.High)]
        [TestCase("Adreno (TM) 830", GraphicsLevel.High)]
        [TestCase("Adreno", GraphicsLevel.Medium)]            // không có số
        [TestCase("Mali-T880", GraphicsLevel.Low)]
        [TestCase("Mali-G52 MC2", GraphicsLevel.Medium)]
        [TestCase("Mali-G57 MC2", GraphicsLevel.Medium)]
        [TestCase("Mali-G78", GraphicsLevel.Medium)]
        [TestCase("Mali-G715", GraphicsLevel.High)]
        [TestCase("Immortalis-G715", GraphicsLevel.High)]
        [TestCase("Xclipse 940", GraphicsLevel.High)]
        [TestCase("PowerVR Rogue GE8320", GraphicsLevel.Low)]
        [TestCase("Apple A15 GPU", GraphicsLevel.High)]
        [TestCase("Apple M2 GPU", GraphicsLevel.High)]
        [TestCase("ADRENO (TM) 740", GraphicsLevel.High)]     // không phân biệt hoa thường
        public void Phone_ByGpuName(string gpu, GraphicsLevel expected)
        {
            Assert.That(GpuTiers.MaxLevel(gpu, true), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Some Unknown GPU")]
        public void Phone_UnknownGpu_IsMedium(string gpu)
        {
            Assert.That(GpuTiers.MaxLevel(gpu, true), Is.EqualTo(GraphicsLevel.Medium));
        }

        [TestCase(null)]
        [TestCase("Adreno (TM) 506")]
        [TestCase("Intel(R) UHD Graphics")]
        public void Desktop_IsNeverCapped(string gpu)
        {
            Assert.That(GpuTiers.MaxLevel(gpu, false), Is.EqualTo(GraphicsLevel.High));
        }

        [Test]
        public void Cap_OnlyLowersTheLevel()
        {
            Assert.That(GpuTiers.Cap(GraphicsLevel.High, "Adreno (TM) 610", true), Is.EqualTo(GraphicsLevel.Medium));
            Assert.That(GpuTiers.Cap(GraphicsLevel.Low, "Adreno (TM) 740", true), Is.EqualTo(GraphicsLevel.Low));
            Assert.That(GpuTiers.Cap(GraphicsLevel.Medium, "Mali-T880", true), Is.EqualTo(GraphicsLevel.Low));
            Assert.That(GpuTiers.Cap(GraphicsLevel.High, "Adreno (TM) 740", true), Is.EqualTo(GraphicsLevel.High));
        }

        // Đầu-cuối: RAM + nhân + GPU như GraphicsQuality.Init.
        [TestCase(6000, 8, "Adreno (TM) 610", GraphicsLevel.Medium)] // máy 6 GB GPU yếu: không còn Cao
        [TestCase(8000, 8, "Adreno (TM) 740", GraphicsLevel.High)]
        [TestCase(8000, 8, "", GraphicsLevel.Medium)]                // GPU lạ: Vừa
        [TestCase(2048, 8, "Adreno (TM) 740", GraphicsLevel.Low)]    // RAM thấp vẫn thắng
        public void Recommend_WithGpu(int ramMb, int cores, string gpu, GraphicsLevel expected)
        {
            Assert.That(GraphicsPresets.Recommend(ramMb, cores, true, gpu), Is.EqualTo(expected));
        }
    }
}
