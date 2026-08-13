using System.Collections.Generic;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    /// <summary>
    /// Task C3：WorldGenerator 接入 CaveCarver + Biome。
    /// 验证生成器在地表下方能按群系权重挖出洞穴、基岩层与地表方块保持原状、确定性。
    /// </summary>
    [TestFixture]
    public class WorldGeneratorBiomeCaveTests
    {
        private const int Seed = 20260806;

        private static List<BiomeConfig> AllBiomesWithCaveMultiplier(float plainsMultiplier)
        {
            // 显式构造四套配置；其它 biome 不参与测试断言。
            return new List<BiomeConfig>
            {
                new BiomeConfig { Id = (int)Biome.Plains,    Name = "plains",    CaveMultiplier = plainsMultiplier },
                new BiomeConfig { Id = (int)Biome.Desert,    Name = "desert",    CaveMultiplier = 0.5f },
                new BiomeConfig { Id = (int)Biome.Forest,    Name = "forest",    CaveMultiplier = 1.5f },
                new BiomeConfig { Id = (int)Biome.Mountains, Name = "mountains", CaveMultiplier = 2.0f }
            };
        }

        private static int FindSurfaceHeight(ChunkColumn column, int localX, int localZ)
        {
            for (int y = VoxelCoords.MaxY - 1; y >= VoxelCoords.MinY; y--)
            {
                if (column.GetBlock(localX, y, localZ) != BlockIds.Air)
                {
                    return y;
                }
            }
            return VoxelCoords.MinY;
        }

        [Test]
        public void Generate_WithHighCaveMultiplier_CarvesAirPocketsBelowSurface()
        {
            // 把 CaveMultiplier 拉满，确保必然挖出可观察的空气。
            var configs = AllBiomesWithCaveMultiplier(plainsMultiplier: 2.0f);
            ChunkColumn column = new WorldGenerator(Seed, configs).Generate(new ChunkPos(0, 0));

            int surface = FindSurfaceHeight(column, 8, 8);
            int airBelow = 0;
            for (int y = VoxelCoords.MinY; y < surface - 7; y++)
            {
                if (column.GetBlock(8, y, 8) == BlockIds.Air) airBelow++;
            }

            Assert.That(airBelow, Is.GreaterThan(5),
                "高 CaveMultiplier 应当在地表下挖出大量空气");
        }

        [Test]
        public void Generate_WithZeroCaveMultiplier_LeavesStoneIntactBelowSurface()
        {
            // CaveMultiplier=0 时不应挖掉任何石头方块（地表-7 之下全是 Stone）。
            var configs = AllBiomesWithCaveMultiplier(plainsMultiplier: 0f);
            ChunkColumn column = new WorldGenerator(Seed, configs).Generate(new ChunkPos(0, 0));

            int surface = FindSurfaceHeight(column, 8, 8);
            for (int y = VoxelCoords.MinY + 1; y < surface - 7; y++)
            {
                Assert.That(column.GetBlock(8, y, 8), Is.Not.EqualTo(BlockIds.Air),
                    $"y={y} 不应被挖空");
            }
        }

        [Test]
        public void Generate_CavePass_NeverCarvesBedrock()
        {
            var configs = AllBiomesWithCaveMultiplier(plainsMultiplier: 2.0f);
            ChunkColumn column = new WorldGenerator(Seed, configs).Generate(new ChunkPos(0, 0));

            for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
            for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
            {
                Assert.That(column.GetBlock(lx, VoxelCoords.MinY, lz), Is.EqualTo(BlockIds.Bedrock),
                    $"({lx},{VoxelCoords.MinY},{lz}) 基岩层永不挖空");
            }
        }

        [Test]
        public void Generate_CavePass_DoesNotDisturbSurfaceAndTopsoil()
        {
            // 地表/表土层应当免受洞穴挖掘破坏，否则现有视觉/玩法都会断。
            var configs = AllBiomesWithCaveMultiplier(plainsMultiplier: 2.0f);
            ChunkColumn column = new WorldGenerator(Seed, configs).Generate(new ChunkPos(0, 0));

            int surface = FindSurfaceHeight(column, 4, 12);
            Assert.That(column.GetBlock(4, surface, 12), Is.EqualTo(BlockIds.Grass),
                "地表草方块不被挖空");
            Assert.That(column.GetBlock(4, surface - 1, 12), Is.EqualTo(BlockIds.Dirt),
                "地表下方第一格土方块不被挖空");
            Assert.That(column.GetBlock(4, surface - 6, 12), Is.EqualTo(BlockIds.Stone),
                "地表下方第 6 格仍是石头（不应当挖成空气）");
        }

        [Test]
        public void Generate_CavePass_IsDeterministic()
        {
            var configs = AllBiomesWithCaveMultiplier(plainsMultiplier: 1.5f);
            ChunkColumn first = new WorldGenerator(Seed, configs).Generate(new ChunkPos(3, -5));
            ChunkColumn second = new WorldGenerator(Seed, configs).Generate(new ChunkPos(3, -5));

            for (int y = VoxelCoords.MinY; y < VoxelCoords.MaxY; y++)
            for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
            for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
            {
                Assert.That(first.GetBlock(lx, y, lz), Is.EqualTo(second.GetBlock(lx, y, lz)),
                    $"({lx},{y},{lz}) 同 seed 应产生同样的方块");
            }
        }

        [Test]
        public void Generate_DifferentBiomeConfigs_ProduceDifferentCavePatterns()
        {
            // 两份配置：一个把 CaveMultiplier 全置 0，另一个置 2.0，列内 cave 必有差异。
            var allZero = AllBiomesWithCaveMultiplier(plainsMultiplier: 0f);
            var allMax = AllBiomesWithCaveMultiplier(plainsMultiplier: 2.0f);

            ChunkColumn withoutCaves = new WorldGenerator(Seed, allZero).Generate(new ChunkPos(0, 0));
            ChunkColumn withCaves = new WorldGenerator(Seed, allMax).Generate(new ChunkPos(0, 0));

            int diffs = 0;
            for (int y = VoxelCoords.MinY; y < VoxelCoords.MaxY; y++)
            for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
            for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
            {
                if (withoutCaves.GetBlock(lx, y, lz) != withCaves.GetBlock(lx, y, lz))
                {
                    diffs++;
                }
            }

            Assert.That(diffs, Is.GreaterThan(20),
                "不同 CaveMultiplier 应在地表下方产生可观察的差异");
        }

        [Test]
        public void BiomeSelector_IsInvokedPerColumn()
        {
            // 用极简配置：只配一个 Plains，CaveMultiplier=1.5；至少要看到地表下方有空气，证明 WorldGenerator
            // 真的走了 biome + cave pass 链路，而不是把 biomes.json 跳过走回旧逻辑。
            var plainsOnly = new List<BiomeConfig>
            {
                new BiomeConfig { Id = (int)Biome.Plains, Name = "plains", CaveMultiplier = 1.5f }
            };
            ChunkColumn column = new WorldGenerator(Seed, plainsOnly).Generate(new ChunkPos(0, 0));

            int surface = FindSurfaceHeight(column, 8, 8);
            int airBelow = 0;
            for (int y = VoxelCoords.MinY; y < surface - 7; y++)
            {
                if (column.GetBlock(8, y, 8) == BlockIds.Air) airBelow++;
            }

            Assert.That(airBelow, Is.GreaterThan(0),
                "即便只有一个 biome 配置，cave pass 也应参与生成");
        }

        [Test]
        public void Generate_NullBiomeConfigs_FallsBackToDefaultsAndStillCarves()
        {
            // 不传 biome 配置时使用内置默认值，仍应能挖出洞穴（默认值非零）。
            ChunkColumn column = new WorldGenerator(Seed, null).Generate(new ChunkPos(0, 0));

            int surface = FindSurfaceHeight(column, 8, 8);
            int airBelow = 0;
            for (int y = VoxelCoords.MinY; y < surface - 7; y++)
            {
                if (column.GetBlock(8, y, 8) == BlockIds.Air) airBelow++;
            }

            Assert.That(airBelow, Is.GreaterThan(0),
                "默认配置（含 CaveMultiplier）应仍能产生洞穴");
        }
    }
}
