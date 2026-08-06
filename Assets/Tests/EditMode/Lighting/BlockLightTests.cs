using MyWorld.Core.Lighting;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Lighting
{
    [TestFixture]
    public class BlockLightTests
    {
        private const int Size = 16;

        [Test]
        public void Source_LightsItsOwnCellAtFullLevel()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);

            LightPropagator.AddBlockLight(volume, 8, 8, 8, 14);

            Assert.That(volume.GetLight(8, 8, 8), Is.EqualTo(14));
        }

        [Test]
        public void Light_FallsOffByOnePerBlock()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);

            LightPropagator.AddBlockLight(volume, 8, 8, 8, 14);

            Assert.That(volume.GetLight(9, 8, 8), Is.EqualTo(13));
            Assert.That(volume.GetLight(11, 8, 8), Is.EqualTo(11));
            Assert.That(volume.GetLight(8, 12, 8), Is.EqualTo(10));
        }

        [Test]
        public void Light_ReachesZeroAtTheEdgeOfItsRange()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);

            LightPropagator.AddBlockLight(volume, 8, 8, 8, 5);

            Assert.That(volume.GetLight(12, 8, 8), Is.EqualTo(1));
            Assert.That(volume.GetLight(13, 8, 8), Is.EqualTo(0));
        }

        [Test]
        public void Light_DoesNotPassThroughOpaqueWall()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            SealPlaneAtX(volume, 9);

            LightPropagator.AddBlockLight(volume, 8, 8, 8, 15);

            Assert.That(volume.GetLight(10, 8, 8), Is.EqualTo(0), "墙后应保持全黑");
            Assert.That(volume.GetLight(8, 8, 8), Is.EqualTo(15));
        }

        [Test]
        public void Light_TravelsAroundCornersRatherThanInStraightLines()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            SealPlaneAtX(volume, 9);
            volume.SetOpaque(9, 10, 8, false); // 墙上开一个洞

            LightPropagator.AddBlockLight(volume, 8, 8, 8, 15);

            // 路径: (8,8,8)→(8,9,8)→(8,10,8)→洞(9,10,8)→(10,10,8)，共 4 步
            Assert.That(volume.GetLight(10, 10, 8), Is.EqualTo(11));
        }

        [Test]
        public void TwoSources_LeaveTheBrighterValueAtOverlap()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);

            LightPropagator.AddBlockLight(volume, 4, 8, 8, 15);
            LightPropagator.AddBlockLight(volume, 12, 8, 8, 8);

            // (10,8,8) 距离强光源 6 格得 9，距离弱光源 2 格得 6，应取较亮者
            Assert.That(volume.GetLight(10, 8, 8), Is.EqualTo(9));
        }

        [Test]
        public void RemovingTheOnlySource_RestoresDarkness()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            LightPropagator.AddBlockLight(volume, 8, 8, 8, 14);

            LightPropagator.RemoveBlockLight(volume, 8, 8, 8);

            Assert.That(volume.GetLight(8, 8, 8), Is.EqualTo(0));
            Assert.That(volume.GetLight(10, 8, 8), Is.EqualTo(0));
            Assert.That(volume.GetLight(8, 11, 8), Is.EqualTo(0));
        }

        [Test]
        public void RemovingOneOfTwoSources_KeepsTheOtherIntact()
        {
            var volume = new ArrayLightVolume(Size, Size, Size);
            LightPropagator.AddBlockLight(volume, 4, 8, 8, 15);
            LightPropagator.AddBlockLight(volume, 12, 8, 8, 15);

            LightPropagator.RemoveBlockLight(volume, 12, 8, 8);

            Assert.That(volume.GetLight(4, 8, 8), Is.EqualTo(15), "保留的光源应仍然满亮");
            Assert.That(volume.GetLight(12, 8, 8), Is.EqualTo(7), "被移除处应只剩另一光源照到的亮度");
        }

        private static void SealPlaneAtX(ArrayLightVolume volume, int x)
        {
            for (var y = 0; y < Size; y++)
            for (var z = 0; z < Size; z++)
            {
                volume.SetOpaque(x, y, z, true);
            }
        }
    }
}
