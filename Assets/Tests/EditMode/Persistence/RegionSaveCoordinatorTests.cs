using System;
using System.IO;
using MyWorld.Core.Persistence;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Persistence
{
    /// <summary>
    /// milestone-4 A3：脏区块按 32×32 region 落盘 + 启动 overlay。
    /// </summary>
    [TestFixture]
    public class RegionSaveCoordinatorTests
    {
        private static string TempDir() =>
            Path.Combine(Path.GetTempPath(), $"region-{Guid.NewGuid():N}");

        private static World BuildWorldWithEdits(params (int x, int y, int z)[] edits)
        {
            var world = new World();
            foreach (var (x, y, z) in edits)
            {
                var pos = new ChunkPos(x >> 4, z >> 4);
                if (!world.TryGetChunk(pos, out _))
                {
                    var generator = new WorldGenerator(42);
                    world.AddChunk(pos, generator.Generate(pos));
                }
            }
            foreach (var (x, y, z) in edits)
            {
                world.SetBlock(x, y, z, BlockIds.Bedrock); // 用基岩做标记方块
            }
            return world;
        }

        [Test]
        public void SaveDirty_WritesRegion_AndClearsDirty()
        {
            string dir = TempDir();
            try
            {
                var world = BuildWorldWithEdits((3, 64, 5), (20, 64, 600)); // chunk (0,0) 与 (1,37)，跨 region (0,0) 和 (0,1)
                int saved = RegionSaveCoordinator.SaveDirty(world, dir);
                Assert.That(saved, Is.EqualTo(2));
                Assert.That(world.DirtyChunks, Is.Empty, "保存成功后脏标记应清空");
                Assert.That(Directory.GetFiles(dir, "r.*.mwr").Length, Is.EqualTo(2),
                    "两个 region 各落一个文件");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void TryLoadChunk_RestoresEditedBlock_LeavesOthersAsGenerated()
        {
            string dir = TempDir();
            try
            {
                var world = BuildWorldWithEdits((3, 64, 5));
                RegionSaveCoordinator.SaveDirty(world, dir);

                // 新 world 模拟重启：重新按 seed 生成同一区块，再 overlay
                var fresh = new World();
                var generator = new WorldGenerator(42);
                var pos = new ChunkPos(0, 0);
                fresh.AddChunk(pos, generator.Generate(pos));
                bool hit = RegionSaveCoordinator.TryLoadChunk(fresh, pos, dir);

                Assert.That(hit, Is.True);
                Assert.That(fresh.GetBlock(3, 64, 5), Is.EqualTo(BlockIds.Bedrock),
                    "改过的方块应被存档覆盖恢复");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void TryLoadChunk_MissingRegion_ReturnsFalse()
        {
            string dir = TempDir();
            Directory.CreateDirectory(dir);
            try
            {
                var fresh = new World();
                Assert.That(RegionSaveCoordinator.TryLoadChunk(fresh, new ChunkPos(99, 99), dir), Is.False,
                    "region 文件不存在应返回 false，调用方保留 seed 生成结果");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void SaveDirty_MergesWithExistingRegion()
        {
            string dir = TempDir();
            try
            {
                // 第一轮只改区块 (0,0)
                var world1 = BuildWorldWithEdits((3, 64, 5));
                RegionSaveCoordinator.SaveDirty(world1, dir);

                // 第二轮只改同 region 的区块 (1,0)——旧记录里的 (0,0) 不能丢
                var world2 = BuildWorldWithEdits((20, 64, 5));
                RegionSaveCoordinator.SaveDirty(world2, dir);

                var restored = new World();
                var generator = new WorldGenerator(42);
                restored.AddChunk(new ChunkPos(0, 0), generator.Generate(new ChunkPos(0, 0)));
                restored.AddChunk(new ChunkPos(1, 0), generator.Generate(new ChunkPos(1, 0)));
                Assert.That(RegionSaveCoordinator.TryLoadChunk(restored, new ChunkPos(0, 0), dir), Is.True);
                Assert.That(RegionSaveCoordinator.TryLoadChunk(restored, new ChunkPos(1, 0), dir), Is.True,
                    "第二轮保存不能覆盖丢掉第一轮的区块记录");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void TryLoadChunk_CorruptRegionFile_ReturnsFalse()
        {
            string dir = TempDir();
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "r.99.99.mwr"), "垃圾数据");
                Assert.That(RegionSaveCoordinator.TryLoadChunk(new World(), new ChunkPos(99 * 32, 99 * 32), dir),
                    Is.False, "region 损坏返回 false 而不是抛异常——读容忍");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void SaveDirty_RegionWriteFailure_KeepsDirty_AndDoesNotCount()
        {
            string dir = TempDir();
            Directory.CreateDirectory(dir);
            try
            {
                var world = BuildWorldWithEdits((3, 64, 5), (20, 64, 600)); // chunk (0,0) 与 (1,37)，两个 region

                // 用独占句柄锁住 region (0,0) 的 .tmp 文件，让 AtomicWrite 的 File.Create 抛 IOException
                string tmp = Path.Combine(dir, "r.0.0.mwr.tmp");
                using (new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    int saved = RegionSaveCoordinator.SaveDirty(world, dir);
                    Assert.That(saved, Is.EqualTo(1),
                        "只有写成功的 region 计入 saved；写失败的 region 不能虚报");
                    Assert.That(world.DirtyChunks, Is.EquivalentTo(new[] { new ChunkPos(0, 0) }),
                        "写失败的 region 保持脏，下轮保存重试");
                }

                // 解锁后重试：脏区块补写成功
                int savedRetry = RegionSaveCoordinator.SaveDirty(world, dir);
                Assert.That(savedRetry, Is.EqualTo(1), "重试成功后补计入 saved");
                Assert.That(world.DirtyChunks, Is.Empty, "补写成功后脏标记清空");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void SaveDirty_UnloadedChunk_ClearsStaleDirty_WithoutCounting()
        {
            string dir = TempDir();
            try
            {
                var world = BuildWorldWithEdits((3, 64, 5));
                world.RemoveChunk(new ChunkPos(0, 0)); // 模拟流式卸载：数据没了，脏标记残留

                int saved = RegionSaveCoordinator.SaveDirty(world, dir);
                Assert.That(saved, Is.EqualTo(0), "已卸载区块的数据随卸载丢失，不应计入保存数");
                Assert.That(world.DirtyChunks, Is.Empty,
                    "stale 脏标记应被清掉，否则卸载区块会永远卡在待保存列表里");
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
