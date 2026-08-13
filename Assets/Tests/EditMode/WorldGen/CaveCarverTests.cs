using MyWorld.Core.WorldGen;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    public class CaveCarverTests
    {
        [Test]
        public void Carve_SkipsBedrockLayer()
        {
            var carver = new CaveCarver(seed: 42);
            // bedrock y=-64 → 不论噪声值如何都不挖
            bool carved = carver.ShouldCarve(new Float3(0, -64, 0), 0f);
            Assert.That(carved, Is.False, "基岩层永不挖空");
        }

        [Test]
        public void Carve_ReturnsBool()
        {
            var carver = new CaveCarver(seed: 42);
            // 验证 API 形状（不卡具体值）
            bool _ = carver.ShouldCarve(new Float3(10, 70, 10), 0f);
            Assert.Pass();  // 跑通即可
        }

        [Test]
        public void Carve_DeterministicForSameSeed()
        {
            var c1 = new CaveCarver(seed: 42);
            var c2 = new CaveCarver(seed: 42);
            bool a = c1.ShouldCarve(new Float3(15, 80, 15), 0.5f);
            bool b = c2.ShouldCarve(new Float3(15, 80, 15), 0.5f);
            Assert.That(a, Is.EqualTo(b), "同 seed 同坐标同 density → 同结果");
        }

        [Test]
        public void Carve_DifferentSeedProducesDifferentResults()
        {
            var c1 = new CaveCarver(seed: 42);
            var c2 = new CaveCarver(seed: 99);
            int diffs = 0;
            for (int i = 0; i < 50; i++)
            {
                if (c1.ShouldCarve(new Float3(i * 7, 70, i * 3), 0.5f) !=
                    c2.ShouldCarve(new Float3(i * 7, 70, i * 3), 0.5f))
                    diffs++;
            }
            Assert.That(diffs, Is.GreaterThan(20), "不同 seed 应差异显著（>40%）");
        }
    }
}
