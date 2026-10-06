#if UNITY_EDITOR
// milestone-4 B1：ChunkStreamer 生成路径接 region 存档 overlay + 卸载前保存脏区块。
// views 传 null（ChunkStreamer 对 views 做了空容忍），无需渲染依赖即可驱动整条 Tick 链。
// 整个文件用 #if UNITY_EDITOR 包裹：dotnet 链不引用 MyWorld.Unity，跳过；Unity EditMode 链跑。
using System;
using System.IO;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Streaming;
using NUnit.Framework;

// 命名空间不用 .World 段——那会让所有 MyWorld.Core.Tests.* 下的文件把
// MyWorld.Core.Voxel.World 类型解析成命名空间（CS0118），用 WorldStreaming 区分。
namespace MyWorld.Core.Tests.WorldStreaming
{
    /// <summary>
    /// milestone-4 B1：ChunkStreamer 的存档 overlay 与卸载保存。
    /// <para>
    /// 覆盖四件事：生成后用 region 存档覆盖改动、saveRegionsDir=null 时纯 seed 生成、
    /// 存档缺失时保留生成结果、脏区块在卸载前落盘（走远再回来改动还在）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ChunkStreamerOverlayTests
    {
        private const long Seed = 42;
        private const int MarkerX = 8;
        private const int MarkerY = 70; // 地表（约 y=98）以下、基岩层（y=-64）以上，生成结果必不是基岩
        private const int MarkerZ = 8;

        private static string TempDir() =>
            Path.Combine(Path.GetTempPath(), $"streamer-{Guid.NewGuid():N}");

        /// <summary>
        /// 小半径 streamer：LoadRadius=1 共 9 根列。m5 C2 改毫秒预算制后一帧能做几件
        /// 由真实耗时决定，TickUntilLoaded 的 maxTicks 取宽裕值（断言不变）。
        /// </summary>
        private static ChunkStreamer NewStreamer(World world, string saveRegionsDir)
        {
            var streamer = new ChunkStreamer(world, new WorldGenerator((int)Seed),
                registry: null, views: null, Seed, saveRegionsDir);
            streamer.LoadRadius = 1;
            streamer.UnloadRadius = 2;
            streamer.ChunksPerFrame = 9;
            return streamer;
        }

        private static void TickUntilLoaded(ChunkStreamer streamer, Float3 position, int maxTicks)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                streamer.Tick(position);
            }
        }

        [Test]
        public void GeneratedChunk_OverlaidBySavedRegion()
        {
            string dir = TempDir();
            try
            {
                // 1. 先存一份带改动的世界：区块 (0,0) 上放一块基岩做标记
                var saved = new World();
                var generator = new WorldGenerator((int)Seed);
                var pos = new ChunkPos(0, 0);
                saved.AddChunk(pos, generator.Generate(pos));
                saved.SetBlock(MarkerX, MarkerY, MarkerZ, BlockIds.Bedrock);
                RegionSaveCoordinator.SaveDirty(saved, dir);

                // 2. 新世界 + 同 seed + regionsDir：走 streamer 的生成路径，overlay 应恢复改动
                var fresh = new World();
                var streamer = NewStreamer(fresh, dir);
                TickUntilLoaded(streamer, new Float3(0.5f, 100f, 0.5f), maxTicks: 30);

                Assert.That(fresh.LoadedChunkCount, Is.EqualTo(9), "LoadRadius=1 应加载 3×3 共 9 根列");
                Assert.That(fresh.GetBlock(MarkerX, MarkerY, MarkerZ), Is.EqualTo(BlockIds.Bedrock),
                    "生成后 overlay 应把存档里的方块改动恢复回来");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void NullRegionsDir_GeneratesFromSeedOnly()
        {
            // 不传 saveRegionsDir：纯 seed 生成，行为与改动前完全一致
            var world = new World();
            var streamer = NewStreamer(world, saveRegionsDir: null);
            TickUntilLoaded(streamer, new Float3(0.5f, 100f, 0.5f), maxTicks: 30);

            Assert.That(world.LoadedChunkCount, Is.EqualTo(9));
            Assert.That(world.GetBlock(MarkerX, MarkerY, MarkerZ), Is.Not.EqualTo(BlockIds.Bedrock),
                "没有存档目录时不应有任何 overlay");
        }

        [Test]
        public void EmptyRegionsDir_KeepsGeneratedResult()
        {
            string dir = TempDir();
            Directory.CreateDirectory(dir); // 目录存在但一个 region 文件都没有
            try
            {
                var world = new World();
                var streamer = NewStreamer(world, dir);
                TickUntilLoaded(streamer, new Float3(0.5f, 100f, 0.5f), maxTicks: 30);

                Assert.That(world.TryGetChunk(new ChunkPos(0, 0), out _), Is.True,
                    "TryLoadChunk 未命中时应保留 seed 生成结果，区块不能丢");
                Assert.That(world.GetBlock(MarkerX, MarkerY, MarkerZ), Is.Not.EqualTo(BlockIds.Bedrock));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void UnloadDistant_SavesDirtyChunk_BeforeRemoval()
        {
            string dir = TempDir();
            try
            {
                // 1. 加载、改一个方块（区块 (0,0) 变脏）
                var world = new World();
                var streamer = NewStreamer(world, dir);
                Float3 origin = new Float3(0.5f, 100f, 0.5f);
                TickUntilLoaded(streamer, origin, maxTicks: 30);
                world.SetBlock(MarkerX, MarkerY, MarkerZ, BlockIds.Bedrock);
                Assert.That(world.DirtyChunks, Is.Not.Empty);

                // 2. 玩家走到 20 个区块外（> UnloadRadius=2）：UnloadDistant 应先保存再卸载
                Float3 farAway = new Float3(20 * 16 + 0.5f, 100f, 0.5f);
                streamer.Tick(farAway);

                Assert.That(world.TryGetChunk(new ChunkPos(0, 0), out _), Is.False,
                    "走远的区块应被卸载");

                // 3. 新世界 + 新 streamer 回到原点：改动应从存档恢复（没有卸载前保存就会丢）
                var reloaded = new World();
                var streamer2 = NewStreamer(reloaded, dir);
                TickUntilLoaded(streamer2, origin, maxTicks: 30);

                Assert.That(reloaded.GetBlock(MarkerX, MarkerY, MarkerZ), Is.EqualTo(BlockIds.Bedrock),
                    "卸载前保存的脏区块，重新加载后玩家的改动应还在");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void RepeatedTick_AtSamePosition_DoesNotDuplicateEnqueue()
        {
            // m5 C1：EnqueueMissing 改为复用成员容器后，同一位置反复 Tick 不得重复入队
            // （scratch 未 Clear / 排序状态残留都会让重复区块进 _generateQueue，
            // 表现为 LoadedChunkCount 涨过 9 或 Tick 卡死）。
            var world = new World();
            var streamer = NewStreamer(world, saveRegionsDir: null);
            var origin = new Float3(0.5f, 100f, 0.5f);

            // 比加载完成所需（m5 C2 预算制下最多 9+9=18 件、每帧至少 1 件）更多次 Tick，
            // 观察是否有重复入队
            TickUntilLoaded(streamer, origin, maxTicks: 30);
            TickUntilLoaded(streamer, origin, maxTicks: 9);

            Assert.That(world.LoadedChunkCount, Is.EqualTo(9),
                "同一位置反复 Tick 不应产生重复区块，恰好 LoadRadius=1 的 3×3 共 9 根");
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                Assert.That(world.TryGetChunk(new ChunkPos(dx, dz), out _), Is.True,
                    $"3×3 内的列 ({dx},{dz}) 都应被加载");
            }
        }

        [Test]
        public void EnqueueOrder_NearToFar_ChebyshevThenDistanceThenTiebreak()
        {
            // m5 C1：排序语义等价断言。ChunksPerFrame=1 逐帧生成，前 k 帧 LoadedChunkCount
            // 恰为 k，且已加载集合必须等于期望序的前 k 项——Chebyshev 主键 + 平方距离 +
            // (dz, dx) 两级 tiebreaker，与复用容器前的闭包 Sort 完全一致。
            var world = new World();
            var streamer = NewStreamer(world, saveRegionsDir: null);
            streamer.ChunksPerFrame = 1;

            // 手工推导的中心 (0,0)、LoadRadius=1 的完整加载序（见 EnqueueMissing 注释）：
            // 中心列 → 同环按平方距离 → (dz,dx) 字典序
            ChunkPos[] expectedOrder =
            {
                new ChunkPos(0, 0),
                new ChunkPos(0, -1), new ChunkPos(-1, 0), new ChunkPos(1, 0), new ChunkPos(0, 1),
                new ChunkPos(-1, -1), new ChunkPos(1, -1), new ChunkPos(-1, 1), new ChunkPos(1, 1),
            };

            var loaded = new System.Collections.Generic.HashSet<ChunkPos>();
            var origin = new Float3(0.5f, 100f, 0.5f);
            for (int k = 0; k < expectedOrder.Length; k++)
            {
                streamer.Tick(origin);
                loaded.Clear();
                loaded.UnionWith(world.ChunkPositions);
                Assert.That(loaded.Count, Is.EqualTo(k + 1),
                    $"第 {k + 1} 次 Tick 后应恰好加载 {k + 1} 根列（每帧预算 1 且不重复入队）");
                for (int j = 0; j <= k; j++)
                {
                    Assert.That(loaded.Contains(expectedOrder[j]), Is.True,
                        $"前 {k + 1} 根必须按距中心由近到远加载，缺第 {j + 1} 项 {expectedOrder[j]}");
                }
            }
        }

        [Test]
        public void UnloadDistant_NoDirtyChunks_DoesNotWriteFiles()
        {
            string dir = TempDir();
            try
            {
                // 没有任何方块改动：卸载时不应触发落盘（目录都不该被创建）
                var world = new World();
                var streamer = NewStreamer(world, dir);
                TickUntilLoaded(streamer, new Float3(0.5f, 100f, 0.5f), maxTicks: 30);

                streamer.Tick(new Float3(20 * 16 + 0.5f, 100f, 0.5f));

                Assert.That(world.TryGetChunk(new ChunkPos(0, 0), out _), Is.False,
                    "原点 3×3 全部在卸载半径外，应被卸载（新中心另算）");
                Assert.That(Directory.Exists(dir), Is.False,
                    "没有脏区块时卸载不应产生任何 region 文件");
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test]
        public void UnloadDistant_注入冲刷回调_卸载脏块走后台路径()
        {
            // 评审 02#2/03 B-4：注入 FlushDirtyBeforeUnload 后，卸载脏区块的落盘必须走
            // 回调（SaveLoadService 后台快照，主线程只冻结快照 <1ms），不再主线程同步
            // 整批 SaveDirty（32 脏列实测 679ms 单帧冻结）；未注入时保持旧行为（上面的
            // 走远卸载用例仍走同步路径，两者互为守卫）
            string dir = TempDir();
            try
            {
                var world = new World();
                var streamer = NewStreamer(world, dir);
                TickUntilLoaded(streamer, new Float3(MarkerX + 0.5f, 100f, MarkerZ + 0.5f), 200);
                world.SetBlock(MarkerX, MarkerY, MarkerZ, BlockIds.Bedrock); // 制脏

                bool flushed = false;
                streamer.FlushDirtyBeforeUnload = () => flushed = true;

                streamer.Tick(new Float3(20 * 16 + 0.5f, 100f, 0.5f)); // 走远 → 卸载触发

                Assert.That(flushed, Is.True, "卸载脏块必须经注入的冲刷回调（后台快照路径）");
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }
    }
}
#endif
