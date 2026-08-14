using NUnit.Framework;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;

namespace MyWorld.Core.Tests.WorldGen
{
    /// <summary>
    /// F2 follow-up：biome-aware 地表方块（spec C4「plains=grass / desert=sand / snow=snow」）。
    /// <see cref="WorldGenerator.SurfaceBlockFor"/> 是 FillColumn 地表选择的纯函数抽取，
    /// 单测直接覆盖映射，不必依赖某个 seed 恰好在区块里生成雪原列。
    /// </summary>
    [TestFixture]
    public class SnowSurfaceTests
    {
        [Test]
        public void SurfaceBlock_PlainsAndForest_AreGrass()
        {
            Assert.That(WorldGenerator.SurfaceBlockFor(Biome.Plains, WorldGenerator.SeaLevel + 8),
                Is.EqualTo(BlockIds.Grass));
            Assert.That(WorldGenerator.SurfaceBlockFor(Biome.Forest, WorldGenerator.SeaLevel + 8),
                Is.EqualTo(BlockIds.Grass));
        }

        [Test]
        public void SurfaceBlock_Desert_IsSand()
        {
            Assert.That(WorldGenerator.SurfaceBlockFor(Biome.Desert, WorldGenerator.SeaLevel + 8),
                Is.EqualTo(BlockIds.Sand));
        }

        [Test]
        public void SurfaceBlock_Snow_IsSnowBlock()
        {
            Assert.That(WorldGenerator.SurfaceBlockFor(Biome.Snow, WorldGenerator.SeaLevel + 8),
                Is.EqualTo(BlockIds.Snow), "雪原地表必须是雪方块（spec C4）");
        }

        [Test]
        public void SurfaceBlock_AtOrBelowSeaLevel_IsSandRegardlessOfBiome()
        {
            // 海平面及以下统一沙子（岸线/水下），沿用既有行为，雪原临海也走沙岸
            Assert.That(WorldGenerator.SurfaceBlockFor(Biome.Snow, WorldGenerator.SeaLevel),
                Is.EqualTo(BlockIds.Sand));
            Assert.That(WorldGenerator.SurfaceBlockFor(Biome.Plains, WorldGenerator.SeaLevel - 3),
                Is.EqualTo(BlockIds.Sand));
        }

        [Test]
        public void WorldGeneration_SnowBiomeColumn_SurfaceIsSnowBlock()
        {
            // 生成级集成：在有界范围内扫一个海平面以上的雪原列，断言生成出的地表方块确实是雪。
            // seed 固定，结果确定性可复现。
            var generator = new WorldGenerator(20260806);
            for (int x = -512; x < 512; x += 16)
            {
                for (int z = -512; z < 512; z += 16)
                {
                    if (generator.BiomeAt(x, z) != Biome.Snow) continue;
                    int surfaceY = generator.SurfaceHeightAt(x, z);
                    if (surfaceY <= WorldGenerator.SeaLevel) continue; // 水下走沙岸，不覆盖此断言

                    var chunk = generator.Generate(new ChunkPos(x >> 4, z >> 4));
                    ushort surface = chunk.GetBlock(x & 15, surfaceY, z & 15);
                    Assert.That(surface, Is.EqualTo(BlockIds.Snow),
                        $"雪原列 ({x}, {z}) 地表应为雪方块，实际是 {surface}");
                    return;
                }
            }
            Assert.Fail("在 [-512, 512) 范围内没扫到海平面以上的雪原列——selector 或气候噪声可能有问题");
        }
    }
}
