using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Lighting;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Lighting
{
    /// <summary>
    /// m11 W1-4 集成点②：真实世界 → 采样光照体积的构建契约。
    /// 构建顺序由 <see cref="LightPropagator.PropagateBlockLight"/> 注释锁定：
    /// 先 <see cref="LightPropagator.PropagateSkyLight"/>（柱状直射无条件覆写所在格，
    /// 必须先跑）、再填 lightEmission 发射通道、最后方块光洪泛（只增不减）。
    /// 顺序接反 = 火把光被天光直射清零，这里逐格断言守住。
    /// </summary>
    [TestFixture]
    public class WorldLightVolumeBuilderTests
    {
        private const int Size = 16;

        private const ushort TorchId = 9001;
        private const ushort StoneId = 9002;

        /// <summary>stub 注册表：火把（发光 14、不挡光）+ 石头（不透明）。
        /// numericId 挑 9000 段与真实表无关，测试只做相对引用（照 FarmSystemTests 模式）。</summary>
        private static BlockRegistry StubRegistry()
        {
            return BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""torch"", ""numericId"": 9001, ""textures"": { ""all"": ""torch"" }, ""opaque"": false, ""lightEmission"": 14 }",
                @"{ ""id"": ""stone"", ""numericId"": 9002, ""textures"": { ""all"": ""stone"" }, ""opaque"": true }",
            });
        }

        [Test]
        public void 合成体积_露天白天天光满_火把圈取更亮()
        {
            var world = new World();
            world.SetBlock(8, 4, 8, TorchId);
            var registry = StubRegistry();

            var region = WorldLightVolumeBuilder.Build(world, registry, 0, 0, 0, Size, Size, Size);

            // 露天格天光满级（火把所在格也是 15——方块光只增不减，不覆盖更亮的天光）
            Assert.That(region.GetLight(1, 4, 1), Is.EqualTo(15), "露天白天格应天光满级 15");
            Assert.That(region.GetLight(8, 4, 8), Is.EqualTo(15), "天光 15 不被火把 14 覆盖（同通道取更亮）");
        }

        [Test]
        public void 合成体积_屋顶阴影里天光归零_火把照样亮()
        {
            var registry = StubRegistry();

            // 无火把：屋顶下全黑
            var world = new World();
            for (var x = 0; x < Size; x++)
            {
                for (var z = 0; z < Size; z++)
                {
                    world.SetBlock(x, 12, z, StoneId); // 整层不透明屋顶
                }
            }

            var dark = WorldLightVolumeBuilder.Build(world, registry, 0, 0, 0, Size, Size, Size);
            Assert.That(dark.GetLight(8, 8, 8), Is.EqualTo(0), "屋顶挡住柱状直射，室内无光源应全黑");

            // 有火把：同一位置阴影里靠方块光照亮（14 本体、邻格 13）
            world.SetBlock(8, 8, 8, TorchId);
            var lit = WorldLightVolumeBuilder.Build(world, registry, 0, 0, 0, Size, Size, Size);
            Assert.That(lit.GetLight(8, 8, 8), Is.EqualTo(14), "阴影里火把本体 = 发光强度 14");
            Assert.That(lit.GetLight(9, 8, 8), Is.EqualTo(13), "距火把 1 格 = 13（每格衰减 1）");
            Assert.That(lit.GetLight(8, 12, 8), Is.EqualTo(0), "屋顶是不透明方块，本体格不透光——光从它下方向外扩散，不在它身上");
        }

        /// <summary>夜间采样用的纯方块光体积：露天格 0（天光不进这份体积）、火把圈 14 递减。</summary>
        [Test]
        public void 方块光体积_露天读0_火把圈按距离衰减()
        {
            var world = new World();
            world.SetBlock(8, 8, 8, TorchId);

            var region = WorldLightVolumeBuilder.BuildBlockLight(
                world, StubRegistry(), 0, 0, 0, Size, Size, Size);

            Assert.That(region.GetLight(8, 8, 8), Is.EqualTo(14), "火把本体 = 14");
            Assert.That(region.GetLight(8, 8, 11), Is.EqualTo(11), "3 格外 = 14-3");
            Assert.That(region.GetLight(1, 1, 1), Is.EqualTo(0), "露天远处无方块光 = 0（夜间天光不计入）");
        }

        [Test]
        public void 采样_区域外读容忍返0_Contains越界判false()
        {
            var world = new World();
            var region = WorldLightVolumeBuilder.Build(world, StubRegistry(), 0, 0, 0, 4, 4, 4);

            Assert.That(region.Contains(0, 0, 0), Is.True, "左下角（含）在区域内");
            Assert.That(region.Contains(3, 3, 3), Is.True, "右上角（含）在区域内");
            Assert.That(region.Contains(4, 0, 0), Is.False, "边界外（不含）");
            Assert.That(region.Contains(-1, 0, 0), Is.False, "负方向越界");
            Assert.That(region.GetLight(100, 100, 100), Is.EqualTo(0), "区域外读 0 不抛（调用方自行降级）");
        }

        [Test]
        public void 构建_同一世界两次结果一字不差()
        {
            var world = new World();
            world.SetBlock(8, 8, 8, TorchId);
            world.SetBlock(2, 3, 2, StoneId);
            var registry = StubRegistry();

            var first = WorldLightVolumeBuilder.Build(world, registry, 0, 0, 0, Size, Size, Size);
            var second = WorldLightVolumeBuilder.Build(world, registry, 0, 0, 0, Size, Size, Size);

            var samples = new List<(int X, int Y, int Z)>();
            for (var i = 0; i < Size; i += 3)
            {
                samples.Add((i, 8, 8));
                samples.Add((8, i, 8));
                samples.Add((8, 8, i));
            }

            foreach (var (x, y, z) in samples)
            {
                Assert.That(second.GetLight(x, y, z), Is.EqualTo(first.GetLight(x, y, z)),
                    $"({x},{y},{z}) 两次构建的光值必须一致（确定性铁律）");
            }
        }
    }
}
