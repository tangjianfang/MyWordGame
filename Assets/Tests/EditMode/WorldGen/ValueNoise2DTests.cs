using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    [TestFixture]
    public class ValueNoise2DTests
    {
        [Test]
        public void Sample_WithSameSeedAndPosition_IsDeterministic()
        {
            var a = new ValueNoise2D(12345);
            var b = new ValueNoise2D(12345);

            Assert.That(a.Sample(3.7f, -8.2f), Is.EqualTo(b.Sample(3.7f, -8.2f)));
        }

        [Test]
        public void Sample_WithDifferentSeeds_Diverges()
        {
            var a = new ValueNoise2D(1);
            var b = new ValueNoise2D(2);

            Assert.That(a.Sample(3.7f, -8.2f), Is.Not.EqualTo(b.Sample(3.7f, -8.2f)));
        }

        [Test]
        public void Sample_StaysWithinMinusOneToOne()
        {
            var noise = new ValueNoise2D(99);

            for (var i = -200; i < 200; i++)
            {
                float value = noise.Sample(i * 0.37f, i * -0.11f);
                Assert.That(value, Is.InRange(-1f, 1f));
            }
        }

        [Test]
        public void Sample_IsContinuousBetweenNeighbouringPoints()
        {
            var noise = new ValueNoise2D(7);

            float previous = noise.Sample(0f, 0f);
            for (var i = 1; i <= 100; i++)
            {
                float current = noise.Sample(i * 0.01f, 0f);
                Assert.That(System.Math.Abs(current - previous), Is.LessThan(0.2f),
                    "相邻采样点之间不应出现突变，否则地形会出现尖刺");
                previous = current;
            }
        }

        [Test]
        public void SampleFbm_AccumulatesOctavesWithinRange()
        {
            var noise = new ValueNoise2D(2024);

            for (var i = -100; i < 100; i++)
            {
                float value = noise.SampleFbm(i * 0.13f, i * 0.29f, octaves: 4, lacunarity: 2f, gain: 0.5f);
                Assert.That(value, Is.InRange(-1f, 1f));
            }
        }
    }
}
