using System.Collections.Generic;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Persistence
{
    /// <summary>
    /// milestone-4 A1：World 脏区块追踪。SetBlock 改过的区块必须可枚举，
    /// 保存成功后可单块清除；只读操作（GetBlock/AddChunk）不产生脏。
    /// </summary>
    [TestFixture]
    public class WorldDirtyTests
    {
        [Test]
        public void SetBlock_MarksChunkDirty()
        {
            var world = new World();
            world.AddChunk(new ChunkPos(0, 0), new ChunkColumn());
            Assert.That(world.DirtyChunks, Is.Empty, "新区块不算脏——没改过不用存");

            world.SetBlock(3, 64, 5, BlockIds.Stone);
            Assert.That(world.DirtyChunks, Is.EquivalentTo(new[] { new ChunkPos(0, 0) }),
                "SetBlock 后所在区块应标脏");
        }

        [Test]
        public void ClearDirty_RemovesSingleChunk()
        {
            var world = new World();
            world.AddChunk(new ChunkPos(0, 0), new ChunkColumn());
            world.AddChunk(new ChunkPos(1, 0), new ChunkColumn());
            world.SetBlock(3, 64, 5, BlockIds.Stone);
            world.SetBlock(20, 64, 5, BlockIds.Dirt);

            world.ClearDirty(new ChunkPos(0, 0));
            Assert.That(world.DirtyChunks, Is.EquivalentTo(new[] { new ChunkPos(1, 0) }),
                "ClearDirty 只清指定的区块，其它脏区块保留");
        }

        [Test]
        public void SameChunk_SetBlockTwice_DirtyOnlyOnce()
        {
            var world = new World();
            world.AddChunk(new ChunkPos(0, 0), new ChunkColumn());
            world.SetBlock(3, 64, 5, BlockIds.Stone);
            world.SetBlock(4, 64, 5, BlockIds.Dirt);
            Assert.That(world.DirtyChunks, Is.EquivalentTo(new[] { new ChunkPos(0, 0) }),
                "同一区块多次 SetBlock 只出现一次（HashSet 语义）");
        }

        [Test]
        public void GetBlock_NeverMarksDirty()
        {
            var world = new World();
            world.AddChunk(new ChunkPos(0, 0), new ChunkColumn());
            world.SetBlock(3, 64, 5, BlockIds.Stone);
            world.ClearDirty(new ChunkPos(0, 0));

            _ = world.GetBlock(3, 64, 5);
            _ = world.GetBlock(9999, 64, 9999); // 越界宽容读
            Assert.That(world.DirtyChunks, Is.Empty, "读取（含越界宽容读）不产生脏");
        }

        // ─── 编辑版本守卫（milestone-5 C3 异步存档）──────────────────────

        [Test]
        public void GetEditVersion_SetBlockBumpsPerChunk()
        {
            var world = new World();
            var pos = new ChunkPos(0, 0);
            world.AddChunk(pos, new ChunkColumn());
            Assert.That(world.GetEditVersion(pos), Is.EqualTo(0L), "从未改过的区块版本为 0");

            world.SetBlock(3, 64, 5, BlockIds.Stone);
            long v1 = world.GetEditVersion(pos);
            Assert.That(v1, Is.GreaterThan(0L), "SetBlock 后版本应为正");

            world.SetBlock(4, 64, 5, BlockIds.Dirt);
            Assert.That(world.GetEditVersion(pos), Is.GreaterThan(v1), "同一区块再次改动版本应递增");
        }

        [Test]
        public void ClearDirtyIfUnchanged_版本不符保留脏_一致才清()
        {
            var world = new World();
            var pos = new ChunkPos(0, 0);
            world.AddChunk(pos, new ChunkColumn());
            world.SetBlock(3, 64, 5, BlockIds.Stone);
            long versionAtSave = world.GetEditVersion(pos);

            // 模拟异步保存窗口：快照之后、清脏之前，该区块又被玩家改了一刀
            world.SetBlock(4, 64, 5, BlockIds.Dirt);
            world.ClearDirtyIfUnchanged(pos, versionAtSave);
            Assert.That(world.DirtyChunks, Is.EquivalentTo(new[] { pos }),
                "保存之后又有新改动的区块必须保持脏——否则清脏会丢掉新改动");

            // 版本一致（无新改动）时才真正清脏
            world.ClearDirtyIfUnchanged(pos, world.GetEditVersion(pos));
            Assert.That(world.DirtyChunks, Is.Empty, "无新改动的区块正常清脏");
        }
    }
}
