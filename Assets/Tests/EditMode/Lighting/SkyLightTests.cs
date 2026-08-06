using MyWorld.Core.Lighting;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Lighting
{
    [TestFixture]
    public class SkyLightTests
    {
        private const int Size = 16;

        [Test]
        public void OpenSky_LightsEveryCellAtFullLevel()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);

            LightPropagator.PropagateSkyLight(volume);

            Assert.That(volume.GetLight(8, 15, 8), Is.EqualTo(15));
            Assert.That(volume.GetLight(8, 0, 8), Is.EqualTo(15), "无遮挡时天空光垂直向下不衰减");
        }

        [Test]
        public void FullRoof_LeavesEverythingBelowDark()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            FillRoof(volume, y: 10, fromX: 0, toX: Size - 1);

            LightPropagator.PropagateSkyLight(volume);

            Assert.That(volume.GetLight(8, 12, 8), Is.EqualTo(15), "屋顶之上仍是全亮");
            Assert.That(volume.GetLight(8, 5, 8), Is.EqualTo(0), "完全封闭的屋顶下应全黑");
        }

        [Test]
        public void PartialRoof_LetsLightSpreadSidewaysUnderTheOverhang()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            FillRoof(volume, y: 10, fromX: 0, toX: 7);

            LightPropagator.PropagateSkyLight(volume);

            // x>=8 处天空敞开为 15；屋檐下每深入一格衰减 1
            Assert.That(volume.GetLight(8, 5, 8), Is.EqualTo(15));
            Assert.That(volume.GetLight(7, 5, 8), Is.EqualTo(14));
            Assert.That(volume.GetLight(6, 5, 8), Is.EqualTo(13));
        }

        [Test]
        public void DeepUnderAnOverhang_FadesToDarkness()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            FillRoof(volume, y: 10, fromX: 0, toX: 7);

            LightPropagator.PropagateSkyLight(volume);

            Assert.That(volume.GetLight(0, 5, 8), Is.EqualTo(7), "距离洞口 8 格处应衰减到 7");
        }

        private static void FillRoof(ArrayLightVolume volume, int y, int fromX, int toX)
        {
            for (int x = fromX; x <= toX; x++)
            {
                for (var z = 0; z < Size; z++)
                {
                    volume.SetOpaque(x, y, z, true);
                }
            }
        }
    }
}
