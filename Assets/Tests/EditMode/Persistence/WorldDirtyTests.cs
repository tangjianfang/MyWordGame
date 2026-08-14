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
    }
}
