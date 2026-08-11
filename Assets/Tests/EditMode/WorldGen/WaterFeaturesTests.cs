using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    [TestFixture]
    public class WaterFeaturesTests
    {
        [Test]
        public void Generate_DoesNotThrow_OnEmptyWorld()
        {
            var world = new World();
            Assert.DoesNotThrow(() => WaterFeatures.Generate(world, 42, 0, 0));
        }

        [Test]
        public void Generate_IsDeterministic_SameSeed()
        {
            var w1 = new World();
            var w2 = new World();
            // 预填一个 chunk column
            w1.AddChunk(new ChunkPos(0, 0), new ChunkColumn());
            w2.AddChunk(new ChunkPos(0, 0), new ChunkColumn());

            WaterFeatures.Generate(w1, 42, 0, 0);
            WaterFeatures.Generate(w2, 42, 0, 0);

            for (int y = 0; y < 100; y++)
                Assert.That(w1.GetBlock(0, y, 0), Is.EqualTo(w2.GetBlock(0, y, 0)),
                    "同 seed 同 chunk 应该产生同样的方块布局");
        }

        [Test]
        public void Generate_FillsAirAboveSurfaceWithWater_WhenInBody()
        {
            var world = new World();
            var col = new ChunkColumn();
            // 模拟一个低洼地表：y=60 草方块，y=61 空气
            for (int lx = 0; lx < 16; lx++)
            for (int lz = 0; lz < 16; lz++)
            {
                col.SetBlock(lx, 60, lz, BlockIds.Grass);
                col.SetBlock(lx, 61, lz, (ushort)0);
            }
            world.AddChunk(new ChunkPos(0, 0), col);

            // 用多个 seed 直到命中水域（4 次以内几乎一定命中）
            for (int seed = 0; seed < 10; seed++)
            {
                var w = new World();
                var c = new ChunkColumn();
                for (int lx = 0; lx < 16; lx++)
                for (int lz = 0; lz < 16; lz++)
                {
                    c.SetBlock(lx, 60, lz, BlockIds.Grass);
                    c.SetBlock(lx, 61, lz, (ushort)0);
                }
                w.AddChunk(new ChunkPos(0, 0), c);
                WaterFeatures.Generate(w, seed, 0, 0);
                // 至少有一个 y > 60 的位置变成了水
                bool anyWater = false;
                for (int lx = 0; lx < 16 && !anyWater; lx++)
                for (int lz = 0; lz < 16 && !anyWater; lz++)
                for (int y = 61; y <= 62; y++)
                {
                    if (w.GetBlock(lx, y, lz) == BlockIds.Water) anyWater = true;
                }
                if (anyWater) return;  // 命中即可
            }
            Assert.Fail("10 个 seed 都没在 chunk (0,0) 产生水");
        }
    }
}
