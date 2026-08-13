using System.Collections.Generic;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    [TestFixture]
    public class WorldGeneratorTests
    {
        private const int Seed = 20260806;

        /// <summary>
        /// 关掉所有 biome 的树生成，方便测「纯地形」分层。测试只关心 grass / dirt / stone 的纵向结构，
        /// 不关心树叶干扰。
        /// </summary>
        private static List<BiomeConfig> NoTreeBiomes()
        {
            return new List<BiomeConfig>
            {
                new BiomeConfig { Id = (int)Biome.Plains,    Name = "plains",    TreeDensity = 0, CaveMultiplier = 1.0f },
                new BiomeConfig { Id = (int)Biome.Desert,    Name = "desert",    TreeDensity = 0, CaveMultiplier = 0.5f },
                new BiomeConfig { Id = (int)Biome.Forest,    Name = "forest",    TreeDensity = 0, CaveMultiplier = 1.5f },
                new BiomeConfig { Id = (int)Biome.Mountains, Name = "mountains", TreeDensity = 0, CaveMultiplier = 2.0f }
            };
        }

        [Test]
        public void Generate_SameSeedAndChunk_ProducesIdenticalColumns()
        {
            var first = new WorldGenerator(Seed).Generate(new ChunkPos(3, -5));
            var second = new WorldGenerator(Seed).Generate(new ChunkPos(3, -5));

            AssertColumnsMatch(first, second);
        }

        [Test]
        public void Generate_DifferentSeeds_ProduceDifferentTerrain()
        {
            var a = new WorldGenerator(1).Generate(new ChunkPos(0, 0));
            var b = new WorldGenerator(2).Generate(new ChunkPos(0, 0));

            Assert.That(ColumnsMatch(a, b), Is.False);
        }

        [Test]
        public void Generate_IsIndependentOfChunkOrder()
        {
            var forward = new WorldGenerator(Seed);
            ChunkColumn a1 = forward.Generate(new ChunkPos(0, 0));
            ChunkColumn b1 = forward.Generate(new ChunkPos(1, 0));

            var reversed = new WorldGenerator(Seed);
            ChunkColumn b2 = reversed.Generate(new ChunkPos(1, 0));
            ChunkColumn a2 = reversed.Generate(new ChunkPos(0, 0));

            AssertColumnsMatch(a1, a2);
            AssertColumnsMatch(b1, b2);
        }

        [Test]
        public void Generate_PlacesBedrockAtWorldBottom()
        {
            ChunkColumn column = new WorldGenerator(Seed).Generate(new ChunkPos(0, 0));

            Assert.That(column.GetBlock(0, VoxelCoords.MinY, 0), Is.EqualTo(BlockIds.Bedrock));
        }

        [Test]
        public void Generate_LeavesAirAboveSurfaceAndSolidBelow()
        {
            ChunkColumn column = new WorldGenerator(Seed, NoTreeBiomes()).Generate(new ChunkPos(0, 0));

            int surface = FindSurfaceHeight(column, 8, 8);

            Assert.That(surface, Is.GreaterThan(VoxelCoords.MinY), "应当存在地表");
            Assert.That(column.GetBlock(8, surface, 8), Is.Not.EqualTo(BlockIds.Air));
            Assert.That(column.GetBlock(8, surface + 1, 8), Is.EqualTo(BlockIds.Air));
            Assert.That(column.GetBlock(8, surface - 1, 8), Is.Not.EqualTo(BlockIds.Air));
        }

        [Test]
        public void Generate_TopsSurfaceWithGrassOverDirtOverStone()
        {
            ChunkColumn column = new WorldGenerator(Seed, NoTreeBiomes()).Generate(new ChunkPos(0, 0));

            int surface = FindSurfaceHeight(column, 4, 12);

            Assert.That(column.GetBlock(4, surface, 12), Is.EqualTo(BlockIds.Grass));
            Assert.That(column.GetBlock(4, surface - 1, 12), Is.EqualTo(BlockIds.Dirt));
            Assert.That(column.GetBlock(4, surface - 6, 12), Is.EqualTo(BlockIds.Stone));
        }

        [Test]
        public void Generate_AdjacentChunksAgreeAcrossTheSeam()
        {
            var generator = new WorldGenerator(Seed);
            ChunkColumn left = generator.Generate(new ChunkPos(0, 0));
            ChunkColumn right = generator.Generate(new ChunkPos(1, 0));

            // 左区块的 x=16 与右区块的 x=0 在世界坐标上相邻，地表高度不应出现断崖
            int leftEdge = FindSurfaceHeight(left, 15, 8);
            int rightEdge = FindSurfaceHeight(right, 0, 8);

            Assert.That(System.Math.Abs(leftEdge - rightEdge), Is.LessThanOrEqualTo(3),
                "跨区块接缝处地表高度应连续");
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

        private static bool ColumnsMatch(ChunkColumn a, ChunkColumn b)
        {
            for (int y = VoxelCoords.MinY; y < VoxelCoords.MaxY; y++)
            for (var z = 0; z < VoxelCoords.ChunkSize; z++)
            for (var x = 0; x < VoxelCoords.ChunkSize; x++)
            {
                if (a.GetBlock(x, y, z) != b.GetBlock(x, y, z))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AssertColumnsMatch(ChunkColumn a, ChunkColumn b)
        {
            Assert.That(ColumnsMatch(a, b), Is.True, "两次生成结果应逐方块一致");
        }
    }
}
