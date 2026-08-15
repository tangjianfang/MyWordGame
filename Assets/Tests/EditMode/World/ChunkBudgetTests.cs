#if UNITY_EDITOR
// m5 C2：区块工作毫秒预算分帧。ChunkStreamer 从固定根数制（ChunksPerFrame）改为
// 时间预算制（FrameBudgetMillis，默认 8ms），建网格按 section 逐段消费。
// 整个文件用 #if UNITY_EDITOR 包裹：dotnet 链不引用 MyWorld.Unity，跳过；
// Unity EditMode 链跑。假计时器经 InternalsVisibleTo 注入 internal StopwatchMillis。
using System;
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Rendering;
using MyWorld.Unity.Streaming;
using NUnit.Framework;
using UnityEngine;

// 命名空间不用 .World 段——那会让 MyWorld.Core.Tests.* 下的文件把
// MyWorld.Core.Voxel.World 解析成命名空间（CS0118），沿用 WorldStreaming。
namespace MyWorld.Core.Tests.WorldStreaming
{
    /// <summary>
    /// m5 C2：预算制语义的确定性测试。
    /// <para>
    /// 真实 Stopwatch 在 EditMode 下单件工作往往远小于 8ms，预算几乎不起约束作用，
    /// 因此全部测试注入假计时器：<c>StopwatchMillis</c> 返回绝对毫秒，预算循环每个
    /// 工作件恰读一次钟（外加 Tick 开头的一次起点读数），把时钟建模为「每读一次前进
    /// 5ms」就等价于「单件工作耗时 5ms」。
    /// </para>
    /// </summary>
    [TestFixture]
    public class ChunkBudgetTests
    {
        private const int Seed = 42;

        private static ChunkStreamer NewStreamer(World world, ChunkViewRegistry views)
        {
            var streamer = new ChunkStreamer(world, new WorldGenerator(Seed),
                registry: null, views: views, Seed, saveRegionsDir: null);
            streamer.LoadRadius = 1;
            streamer.UnloadRadius = 2;
            return streamer;
        }

        [Test]
        public void BudgetEightMillis_FiveMillisPerUnit_DoesOneUnitPerFrame()
        {
            var world = new World();
            var streamer = NewStreamer(world, views: null);
            streamer.FrameBudgetMillis = 8f;

            // 假时钟：每读一次前进 5ms。预算循环里第一次工作检查 elapsed = 5ms ≤ 8ms
            // 做第 1 件，第二次检查 elapsed = 10ms > 8ms 停——一帧恰好 1 件。
            long now = 0;
            streamer.StopwatchMillis = () => now += 5;

            var origin = new Float3(0.5f, 100f, 0.5f);
            streamer.Tick(origin);
            Assert.That(world.LoadedChunkCount, Is.EqualTo(1),
                "预算 8ms、单件工作 5ms：一帧只应完成 1 件（第 2 件累计 10ms 已超预算）");

            streamer.Tick(origin);
            Assert.That(world.LoadedChunkCount, Is.EqualTo(2),
                "第二帧同样只做 1 件（时间预算逐帧重置）");

            for (int i = 0; i < 20; i++)
            {
                streamer.Tick(origin);
            }
            Assert.That(world.LoadedChunkCount, Is.EqualTo(9),
                "预算内持续推进，3×3 全部加载完成——「预算充足时全部完成」语义不变");
        }

        [Test]
        public void FrozenClock_HardCapLimitsUnitsPerFrame()
        {
            // 时钟冻结（恒返回 0）：预算永不超支，每帧件数只受硬上限约束。
            // LoadRadius=2 → 5×5=25 列 > 硬上限 16，恰好能观察到截断。
            var world = new World();
            var streamer = NewStreamer(world, views: null);
            streamer.LoadRadius = 2;
            streamer.UnloadRadius = 3;
            streamer.FrameBudgetMillis = 8f;
            streamer.StopwatchMillis = () => 0;

            var origin = new Float3(0.5f, 100f, 0.5f);
            streamer.Tick(origin);
            Assert.That(world.LoadedChunkCount, Is.EqualTo(16),
                "预算富余时每帧最多 16 件（硬上限，防极端快的机器一帧塞爆队列）");

            streamer.Tick(origin);
            Assert.That(world.LoadedChunkCount, Is.EqualTo(25),
                "剩余 9 件第二帧完成，总量语义不变");
        }

        [Test]
        public void SmallChunksPerFrame_ClampsHardCapDown()
        {
            // ChunksPerFrame 就是每帧工作件数硬上限（m5 C2 起默认 16，预算制下由时间
            // 预算实际约束）。显式调小时逐字生效，保住了「逐帧观察加载顺序」类测试
            // （如 EnqueueOrder_NearToFar）的前提。
            var world = new World();
            var streamer = NewStreamer(world, views: null);
            streamer.ChunksPerFrame = 1;
            streamer.StopwatchMillis = () => 0;

            var origin = new Float3(0.5f, 100f, 0.5f);
            for (int i = 1; i <= 3; i++)
            {
                streamer.Tick(origin);
                Assert.That(world.LoadedChunkCount, Is.EqualTo(i),
                    $"ChunksPerFrame=1 时每帧恰好生成 {i} 根列");
            }
        }

        [Test]
        public void Meshing_SplitPerSection_AtMostOneViewPerUnitFrame()
        {
            // 建网格按 section 拆分（m5 C2 的核心）：每帧 1 件工作时，视图数每帧最多
            // 涨 1。若仍是旧 BuildColumn 整列一次建 24 段，单帧增量可达两位数。
            // 最后与「整列同步构建」的 BuildColumn 结果比对视图数，证明逐段路径
            // 构建出的视图集合与整列路径完全一致。
            BlockRegistry registry = BlockRegistryLoader.Load();
            BlockMaterialLibrary materials =
                BlockMaterialLibrary.Load(registry, BlockRegistryLoader.TextureDirectory);
            var parent = new GameObject("预算分帧测试视图根");
            try
            {
                var world = new World();
                var views = new ChunkViewRegistry(parent.transform, world, registry, materials);
                var streamer = NewStreamer(world, views);
                streamer.ChunksPerFrame = 1;          // 每帧恰 1 件工作
                streamer.StopwatchMillis = () => 0;   // 预算不设限

                var origin = new Float3(0.5f, 100f, 0.5f);
                int previousViews = 0;
                int maxDelta = 0;
                for (int i = 0; i < 800; i++)
                {
                    streamer.Tick(origin);
                    int delta = views.ViewCount - previousViews;
                    previousViews = views.ViewCount;
                    // 不能写 Math.Max——Math 会解析成 MyWorld.Core.Math 命名空间
                    if (delta > maxDelta) maxDelta = delta;
                }

                Assert.That(maxDelta, Is.AtMost(1),
                    "每帧只建 1 个 section（视图增量 ≤ 1）；若整列一次建网格会远超 1");
                Assert.That(views.ViewCount, Is.GreaterThan(0),
                    "流式加载完成后应有可见视图");

                // 收敛检查：继续 Tick 不再产生新视图（队列里只剩永远等不到邻居的外圈列）
                for (int i = 0; i < 100; i++)
                {
                    streamer.Tick(origin);
                }
                Assert.That(views.ViewCount, Is.EqualTo(previousViews),
                    "该建的 section 都建完后视图数应稳定");

                // 等价性：同样 9 根列、同 seed，用整列同步的 BuildColumn 建中心列，
                // 视图数必须与逐段路径一致（LoadRadius=1 时只有中心列的 4 个水平
                // 邻居都在 3×3 内，故两条路径都只会构建中心列）。
                var referenceWorld = new World();
                var referenceGenerator = new WorldGenerator(Seed);
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    var pos = new ChunkPos(dx, dz);
                    referenceWorld.AddChunk(pos, referenceGenerator.Generate(pos));
                }
                var referenceParent = new GameObject("预算分帧参照视图根");
                try
                {
                    var referenceViews = new ChunkViewRegistry(
                        referenceParent.transform, referenceWorld, registry, materials);
                    referenceViews.BuildColumn(new ChunkPos(0, 0));
                    Assert.That(views.ViewCount, Is.EqualTo(referenceViews.ViewCount),
                        "逐 section 分帧构建的视图数应与整列同步构建完全一致");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(referenceParent);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                materials.Dispose();
            }
        }
    }
}
#endif
