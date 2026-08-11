using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    [TestFixture]
    public class TreeFeatureTests
    {
        /// <summary>在草方块地表上方放一棵完整树：log 树干 + 树叶圆盘。</summary>
        [Test]
        public void TryGenerate_PlacesLogTrunkAndLeaves_OnGrassSurface()
        {
            var world = new World();
            var col = new ChunkColumn();
            // 整个 chunk 表面铺草方块（y=68），上方全空气
            for (int lx = 0; lx < 16; lx++)
            for (int lz = 0; lz < 16; lz++)
            {
                for (int y = 0; y < 68; y++) col.SetBlock(lx, y, lz, BlockIds.Dirt);
                col.SetBlock(lx, 68, lz, BlockIds.Grass);
                col.SetBlock(lx, 69, lz, BlockIds.Air);
            }
            world.AddChunk(new ChunkPos(0, 0), col);

            // 找一个能让 (8,8) 生成树的 seed：TryGenerate 内部 hash 命中时才生成
            bool generated = false;
            for (int seed = 0; seed < 200 && !generated; seed++)
            {
                generated = TreeFeature.TryGenerate(world, seed, 8, 8);
            }
            Assert.That(generated, Is.True, "200 个 seed 都没在 (8,8) 生成树");

            // 验证 (8,8) 列：草方块之上应该是 log，直到某个高度后是 leaves
            int trunkStart = 69;
            int trunkEnd = trunkStart;
            while (trunkEnd < VoxelCoords.MaxY && world.GetBlock(8, trunkEnd, 8) == TreeFeature.LogId)
                trunkEnd++;
            Assert.That(trunkEnd - trunkStart, Is.GreaterThanOrEqualTo(TreeFeature.MinTrunk),
                $"树干至少有 {TreeFeature.MinTrunk} 高，实际 {trunkEnd - trunkStart}");
            Assert.That(trunkEnd - trunkStart, Is.LessThanOrEqualTo(TreeFeature.MaxTrunk),
                $"树干至多 {TreeFeature.MaxTrunk} 高，实际 {trunkEnd - trunkStart}");

            // 树干顶部紧邻上方应该有 leaves（叶冠第一层）
            Assert.That(world.GetBlock(8, trunkEnd, 8), Is.EqualTo(TreeFeature.LeavesId),
                "树干顶部之上应该是树叶");
        }

        /// <summary>TryGenerate 命中与否只由 hash 决定，且幂等：再次调用结果不变。</summary>
        [Test]
        public void TryGenerate_IsDeterministic_ForSameSeed()
        {
            World BuildWorld()
            {
                var w = new World();
                var c = new ChunkColumn();
                for (int lx = 0; lx < 16; lx++)
                for (int lz = 0; lz < 16; lz++)
                {
                    for (int y = 0; y < 68; y++) c.SetBlock(lx, y, lz, BlockIds.Dirt);
                    c.SetBlock(lx, 68, lz, BlockIds.Grass);
                }
                w.AddChunk(new ChunkPos(0, 0), c);
                return w;
            }

            var w1 = BuildWorld();
            var w2 = BuildWorld();
            TreeFeature.TryGenerate(w1, 1234, 8, 8);
            TreeFeature.TryGenerate(w2, 1234, 8, 8);

            for (int lx = 0; lx < 16; lx++)
            for (int lz = 0; lz < 16; lz++)
            for (int y = 0; y < 100; y++)
            {
                Assert.That(w1.GetBlock(lx, y, lz), Is.EqualTo(w2.GetBlock(lx, y, lz)),
                    $"位置 ({lx},{y},{lz}) 不一致");
            }
        }

        /// <summary>不放树的位置不产生任何 log/leaves 方块。</summary>
        [Test]
        public void TryGenerate_LeavesUntouched_WhenHashMisses()
        {
            var world = new World();
            var col = new ChunkColumn();
            for (int lx = 0; lx < 16; lx++)
            for (int lz = 0; lz < 16; lz++)
            {
                for (int y = 0; y < 68; y++) col.SetBlock(lx, y, lz, BlockIds.Dirt);
                col.SetBlock(lx, 68, lz, BlockIds.Grass);
            }
            world.AddChunk(new ChunkPos(0, 0), col);

            // 试不同 seed 直到找到一个不生成树的位置
            bool anyMiss = false;
            for (int wx = 0; wx < 16 && !anyMiss; wx++)
            for (int wz = 0; wz < 16 && !anyMiss; wz++)
            {
                bool g = TreeFeature.TryGenerate(world, 9999, wx, wz);
                anyMiss = !g;
            }
            Assert.That(anyMiss, Is.True, "16x16 内必有一些位置不命中");

            // 草方块之上的 100 层不应出现任何 log/leaves
            for (int lx = 0; lx < 16; lx++)
            for (int lz = 0; lz < 16; lz++)
            for (int y = 69; y < VoxelCoords.MaxY; y++)
            {
                var b = world.GetBlock(lx, y, lz);
                Assert.That(b == TreeFeature.LogId || b == TreeFeature.LeavesId, Is.False,
                    $"不命中位置仍出现了 log/leaves: ({lx},{y},{lz})={b}");
            }
        }
    }
}