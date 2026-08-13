#if UNITY_EDITOR
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Visual
{
    /// <summary>
    /// Task C5：biome 视觉回归测试。
    /// 验证 WorldGenerator 用的气候噪声 + BiomeSelector 的组合能命中全部 4 种 biome，
    /// 并验证 BiomeSelector 在阈值边界处的过渡正确。
    /// 这是后续 biome 美术差异化（沙漠黄沙 / 森林密林 / 山地碎石）的几何基础。
    /// </summary>
    /// <remarks>
    /// 整个 fixture 不依赖 UnityEngine，理论上 dotnet 测试链也能跑，但放在
    /// <c>Assets/Tests/EditMode/Visual/</c> 下保持「视觉测试 = EditMode-only」的约定，
    /// 加上 <c>#if UNITY_EDITOR</c> 与同目录其它测试一致。
    /// </remarks>
    [TestFixture]
    public class BiomeVisualTests
    {
        private const int Seed = 20260806;

        /// <summary>
        /// 与 WorldGenerator 完全一致的参数派生气候噪声（<c>seed ^ 0x4F1A2C3B</c>，
        /// FBM(2 octaves) * 0.5 + 0.5），验证归一化后能命中 BiomeSelector 的全部 4 个分支。
        /// 跨多个种子避免单 seed 偏置锁死 biome 分布。
        /// </summary>
        [Test]
        public void WorldGenerator_ClimateNoise_HitsAllFourBiomesAcrossMultipleSeeds()
        {
            int[] seeds = { 1, 2, 3, 4, 5, 6, 7, 8, 42, 100, 999, 20260806 };
            int[] biomeCounts = new int[4];  // Plains / Desert / Forest / Mountains
            foreach (var seed in seeds)
            {
                // WorldGenerator._climateNoise 派生公式：seed ^ 0x4F1A2C3B
                var noise = new ValueNoise2D(seed ^ unchecked((int)0x4F1A2C3B));
                for (int x = -200; x <= 200; x += 4)
                for (int z = -200; z <= 200; z += 4)
                {
                    // 与 WorldGenerator.SampleClimate 完全一致：
                    //   ClimateScale = 0.004f, octaves=2, lacunarity=2, gain=0.5
                    //   raw * 0.5 + 0.5 归一化到 [0, 1]
                    // 温度通道偏移 0，湿度通道偏移 1000。
                    float rawT = noise.SampleFbm(x * 0.004f,        z * 0.004f, 2, 2f, 0.5f);
                    float rawH = noise.SampleFbm(x * 0.004f + 1000f, z * 0.004f, 2, 2f, 0.5f);
                    float t = rawT * 0.5f + 0.5f;
                    float h = rawH * 0.5f + 0.5f;
                    biomeCounts[(int)BiomeSelector.Select(t, h)]++;
                }
            }

            Assert.That(biomeCounts[(int)Biome.Plains], Is.GreaterThan(0),
                "应能看到 Plains biome");
            Assert.That(biomeCounts[(int)Biome.Desert], Is.GreaterThan(0),
                "应能看到 Desert biome");
            Assert.That(biomeCounts[(int)Biome.Forest], Is.GreaterThan(0),
                "应能看到 Forest biome");
            Assert.That(biomeCounts[(int)Biome.Mountains], Is.GreaterThan(0),
                "应能看到 Mountains biome");
        }

        /// <summary>
        /// 验证 BiomeSelector 在阈值边界处的过渡正确。WorldGenerator 在地表噪声逼近阈值时
        /// 应当自然出现 biome 边界——边界条件如果写错（&lt;/&gt; 反了、阈值偏移），biome 之间会
        /// 出现错误的方块 / 树木分布。
        /// </summary>
        [Test]
        public void BiomeSelector_RespectsThresholdBoundaries()
        {
            // Desert 要求 t > 0.7 且 h < 0.4
            Assert.That(BiomeSelector.Select(0.71f, 0.30f), Is.EqualTo(Biome.Desert),
                "刚过 Desert 阈值 (t=0.71, h=0.30) 应判为 Desert");
            Assert.That(BiomeSelector.Select(0.69f, 0.30f), Is.Not.EqualTo(Biome.Desert),
                "刚不及 Desert 阈值 (t=0.69, h=0.30) 不应判为 Desert");

            // Forest 要求 h > 0.6 且 t < 0.6
            Assert.That(BiomeSelector.Select(0.50f, 0.61f), Is.EqualTo(Biome.Forest),
                "刚过 Forest 阈值 (t=0.50, h=0.61) 应判为 Forest");
            Assert.That(BiomeSelector.Select(0.50f, 0.59f), Is.Not.EqualTo(Biome.Forest),
                "刚不及 Forest 阈值 (t=0.50, h=0.59) 不应判为 Forest");

            // Mountains 要求 t < 0.3 且 h < 0.3
            Assert.That(BiomeSelector.Select(0.29f, 0.20f), Is.EqualTo(Biome.Mountains),
                "刚过 Mountains 阈值 (t=0.29, h=0.20) 应判为 Mountains");
            Assert.That(BiomeSelector.Select(0.31f, 0.20f), Is.Not.EqualTo(Biome.Mountains),
                "刚不及 Mountains 阈值 (t=0.31, h=0.20) 不应判为 Mountains");

            // 其它组合落回 Plains
            Assert.That(BiomeSelector.Select(0.50f, 0.50f), Is.EqualTo(Biome.Plains),
                "中间值 (t=0.50, h=0.50) 应判为 Plains");
        }
    }
}
#endif