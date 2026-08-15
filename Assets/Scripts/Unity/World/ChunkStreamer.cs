using System;
using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Streaming
{
    /// <summary>
    /// 跟随玩家位置持续生成 / 卸载区块列。
    /// <para>
    /// 关键设计：生成与建网格**分两个队列**。一根区块列先由 <see cref="WorldGenerator"/> 灌进
    /// <see cref="World"/>，只有当它的 4 个水平邻居都已在 <see cref="World"/> 里时，才允许进
    /// 建网格队列。原因：贪心网格生成时会采样邻居来剔除接缝面，"边生成边建网格"会让先建
    /// 的那几根在接缝处凭空多出一整面——里程碑 1 已经踩过这个坑，<see cref="WorldBootstrap"/>
    /// 的注释里写过。
    /// </para>
    /// </summary>
    public sealed class ChunkStreamer
    {
        private readonly World _world;
        private readonly WorldGenerator _generator;
        private readonly ChunkViewRegistry _views;
        private readonly string _saveRegionsDir;

        private readonly Queue<ChunkPos> _generateQueue = new Queue<ChunkPos>();
        private readonly Queue<ChunkPos> _meshQueue = new Queue<ChunkPos>();
        private readonly HashSet<ChunkPos> _generating = new HashSet<ChunkPos>();
        private readonly HashSet<ChunkPos> _meshing = new HashSet<ChunkPos>();

        // m5 C1：Tick 每帧跑，原先 EnqueueMissing/UnloadDistant 各自 new List（LoadRadius=6
        // 时候补最多 13×13=169 项）+ 闭包 Sort 比较器、卸载重建队列也逐个 new——这些短命对象
        // 是周期性 GC 停顿的主要来源。改为成员级 scratch 容器，每帧 Clear 复用；
        // 排序比较器在构造时缓存成实例委托（不闭包），中心坐标经字段传入，行为与逐帧
        // new 完全等价（ChunkStreamerOverlayTests 的加载顺序 / 不重复入队断言守着）。
        private readonly List<ChunkPos> _enqueueScratch = new List<ChunkPos>();
        private readonly List<ChunkPos> _unloadScratch = new List<ChunkPos>();
        private readonly Queue<ChunkPos> _queueRebuildScratch = new Queue<ChunkPos>();
        private readonly HashSet<ChunkPos> _dirtyScratch = new HashSet<ChunkPos>();
        private readonly System.Comparison<ChunkPos> _enqueueComparison;
        private int _sortCenterCx;
        private int _sortCenterCz;

        public int LoadRadius { get; set; } = 6;
        public int UnloadRadius { get; set; } = 8;

        // m5 C2：ChunksPerFrame 从「每帧固定根数」变为「每帧工作件数硬上限」，默认取
        // MaxWorkUnitsPerFrame=16——预算制下每帧件数 = min(预算内能做完的件数, ChunksPerFrame)。
        // 默认 16 只是防极端快的机器一帧塞爆队列的宽裕上限，实际由时间预算约束；
        // 显式调小（如逐帧观察加载顺序的测试设 1）仍然逐字生效。
        public int ChunksPerFrame { get; set; } = MaxWorkUnitsPerFrame;

        /// <summary>
        /// 每帧区块工作的时间预算（毫秒，默认 8ms）。m5 C2：取代固定根数制——移动跨区块时
        /// 每帧 2 次全列生成 + 2 次整列建网格是规律性尖峰的最大头，改为「一帧做多少件
        /// 工作由耗时决定」。一件工作 = 生成一列 / 建一个 section 的网格 / 轮换一个邻居
        /// 未就绪的列。
        /// </summary>
        public float FrameBudgetMillis { get; set; } = 8f;

        /// <summary>
        /// ChunksPerFrame 的默认值，即每帧工作件数硬上限：时间预算再富余也不超过这个
        /// 件数，防止极端快的机器一帧内塞爆队列（一次性生成几百列 / 建几百个 section
        /// 会让内存和场景层级突变）。
        /// </summary>
        public const int MaxWorkUnitsPerFrame = 16;

        /// <summary>
        /// 毫秒时钟（绝对毫秒）。默认走 System.Diagnostics.Stopwatch；测试经
        /// InternalsVisibleTo 注入假计时器，构造「单件工作 5ms」这类确定性预算场景。
        /// </summary>
        internal Func<long> StopwatchMillis = DefaultStopwatchMillis;

        private static long DefaultStopwatchMillis()
        {
            return (long)(System.Diagnostics.Stopwatch.GetTimestamp() * 1000.0
                          / System.Diagnostics.Stopwatch.Frequency);
        }

        // m5 C2：正在逐 section 续建的列。单列 24 个 section，整列一次建正是移动尖峰的
        // 最大头，故按 section 拆分、跨帧续建；null 表示当前没有进行中的列。
        private ChunkPos? _activeMeshColumn;
        private int _activeMeshSection;

        // registry 与 seed 由 WorldGenerator 与 ChunkViewRegistry 各自持有，
        // 此处仅保留构造参数以维持 WorldBootstrap.Awake() 的调用契约。
        // saveRegionsDir（milestone-4 B1）：非 null 时启用存档 overlay——生成后用 region
        // 存档覆盖玩家改过的区块，卸载前先把脏区块落盘；null 时行为与之前完全一致。
        // views 允许为 null（无渲染依赖的测试 / 无头场景）：只跳过建网格，生成与卸载照常。
        public ChunkStreamer(World world, WorldGenerator generator, BlockRegistry registry,
            ChunkViewRegistry views, long seed, string saveRegionsDir = null)
        {
            _ = registry;
            _ = seed;
            _world = world;
            _generator = generator;
            _views = views;
            _saveRegionsDir = saveRegionsDir;
            // 缓存实例委托：List.Sort(Comparison<T>) 不再每帧分配闭包对象
            _enqueueComparison = CompareEnqueueOrder;
        }

        public void Tick(Float3 playerPosition)
        {
            // 必须用 Mathf.FloorToInt：玩家在 (-1, 0) 段时 (int) 向零取整会落到 0，
            // 整个 LoadRadius 窗口相对正确位置偏移一格。(VoxelCoords.WorldToChunk 本身
            // 用 >> 处理负坐标没问题——这是输入端的事。)
            int cx = VoxelCoords.WorldToChunk(Mathf.FloorToInt(playerPosition.X));
            int cz = VoxelCoords.WorldToChunk(Mathf.FloorToInt(playerPosition.Z));

            // 1. 卸载：超出 UnloadRadius 的列
            UnloadDistant(cx, cz);

            // 2. 入队 / 卸载后，把 LoadRadius 范围内还没加载的列加入 generate 队列
            EnqueueMissing(cx, cz);

            // 3. 毫秒预算制（m5 C2）：一帧做多少件工作由时间预算决定，取代原先固定
            //    ChunksPerFrame 根的根数制。循环条件在「做下一件」之前读钟：预算 8ms、
            //    单件 5ms 时第一次检查 5ms ≤ 8ms 做第 1 件、第二次检查累计 10ms > 8ms
            //    停——一帧恰 1 件（ChunkBudgetTests 用「每读一次前进 5ms」的假计时器
            //    确定性地守着这个语义）。另有 ChunksPerFrame 硬上限防极端快的机器一帧塞爆队列。
            long tickStartMillis = StopwatchMillis();
            // 取整后至少留 1ms，避免 FrameBudgetMillis < 1 时预算恒不满足、流式加载停摆
            long budgetMillis = System.Math.Max(1L, (long)FrameBudgetMillis);
            int frameCap = System.Math.Max(1, ChunksPerFrame);
            int unitsDone = 0;
            while (unitsDone < frameCap && StopwatchMillis() - tickStartMillis < budgetMillis)
            {
                if (!DoOneWorkUnit())
                {
                    break;
                }
                unitsDone++;
            }
        }

        /// <summary>
        /// 预算循环里的一件工作。生成队列优先（建网格依赖生成结果），生成队列空了才
        /// 做建网格侧的工作。返回 false 表示两边都没有可做的事，本帧提前收工。
        /// </summary>
        private bool DoOneWorkUnit()
        {
            if (_generateQueue.Count > 0)
            {
                GenerateOne();
                return true;
            }
            return MeshOneUnit();
        }

        /// <summary>生成侧的一件工作：从队列取一根列灌进 <see cref="World"/>（含存档 overlay）。</summary>
        private void GenerateOne()
        {
            ChunkPos next = _generateQueue.Dequeue();
            _generating.Remove(next);
            _world.AddChunk(next, _generator.Generate(next));
            // 存档 overlay（milestone-4 B1）：region 里有这根区块的改动记录时，
            // TryLoadChunk 内部 AddChunk 覆盖 seed 生成结果；未命中返回 false，
            // 上面刚 AddChunk 的生成结果原样保留——「有存档用存档、没存档用生成」。
            if (_saveRegionsDir != null)
            {
                RegionSaveCoordinator.TryLoadChunk(_world, next, _saveRegionsDir);
            }
            _meshQueue.Enqueue(next);
        }

        /// <summary>
        /// 建网格侧的一件工作（m5 C2）：优先续建进行中的列的下一个 section——一列 24 个
        /// section，整列一次建（旧 BuildColumn）正是移动尖峰的最大头；列建完再从队列取
        /// 下一根。section 的 GameObject 在建它的那一件工作里创建并立刻填好完整网格，
        /// 不存在「半截网格先挂出来」的中间态，一列自下而上逐段显现。
        /// </summary>
        private bool MeshOneUnit()
        {
            if (_views == null)
            {
                // 无渲染依赖（测试 / 无头场景）：建网格没有意义，丢弃队列里的列并消耗
                // 一件预算，与原先 BuildColumn 空调用的语义一致。
                if (_meshQueue.Count == 0)
                {
                    return false;
                }
                _meshing.Remove(_meshQueue.Dequeue());
                return true;
            }

            if (_activeMeshColumn.HasValue)
            {
                var column = _activeMeshColumn.Value;
                if (_world.TryGetChunk(column, out ChunkColumn chunk))
                {
                    if (chunk.HasSection(_activeMeshSection))
                    {
                        _views.Rebuild(new SectionRef(column, _activeMeshSection));
                    }
                    _activeMeshSection++;
                    if (_activeMeshSection >= VoxelCoords.SectionCount)
                    {
                        _activeMeshColumn = null;
                    }
                    return true;
                }

                // 列在续建途中被卸载：丢弃进度，落到下面的队列分支
                _activeMeshColumn = null;
            }

            if (_meshQueue.Count == 0)
            {
                return false;
            }

            ChunkPos next = _meshQueue.Dequeue();
            _meshing.Remove(next);

            // 邻居全到位才建网格，否则回到队列末尾（语义与根数制时代完全一致）
            if (!AllHorizontalNeighborsLoaded(next))
            {
                _meshQueue.Enqueue(next);
                _meshing.Add(next);
                // 轮换也消耗一件预算，避免外圈区块令当前帧无限自旋
                return true;
            }

            // 入位算一件工作：section 的实际构建从下一件开始（预算富余时同一帧紧跟着做）
            _activeMeshColumn = next;
            _activeMeshSection = 0;
            return true;
        }

        private void EnqueueMissing(int centerCx, int centerCz)
        {
            // 收集范围内的待加载列，再按“距中心由近到远”排序，避免单帧涌入外围区块
            // 而玩家所在的中心列迟迟得不到处理（玩家会落入未加载的“空气”，最后
            // 撞进石头里被夹住）。主键 Chebyshev 与卸载逻辑保持一致；
            // 同环内按平方距离再加 (dz, dx) 两级 tiebreaker，整套序完全确定性。
            var candidates = _enqueueScratch;
            candidates.Clear();
            for (var dx = -LoadRadius; dx <= LoadRadius; dx++)
            for (var dz = -LoadRadius; dz <= LoadRadius; dz++)
            {
                var pos = new ChunkPos(centerCx + dx, centerCz + dz);
                if (_world.TryGetChunk(pos, out _))
                {
                    continue;
                }

                if (_generating.Contains(pos) || _meshing.Contains(pos))
                {
                    continue;
                }

                candidates.Add(pos);
            }

            // 比较器经 _sortCenter* 字段取中心坐标（Tick 在主线程串行调用，
            // Sort 是同步的，字段不会跨帧残留）
            _sortCenterCx = centerCx;
            _sortCenterCz = centerCz;
            candidates.Sort(_enqueueComparison);

            foreach (var pos in candidates)
            {
                _generateQueue.Enqueue(pos);
                _generating.Add(pos);
            }
        }

        /// <summary>
        /// EnqueueMissing 的排序比较器（构造时缓存为 <see cref="_enqueueComparison"/>）。
        /// 语义与原先的闭包 Sort 逐行相同：Chebyshev 环距主键 → 平方距离 → (dz, dx) 字典序。
        /// </summary>
        private int CompareEnqueueOrder(ChunkPos a, ChunkPos b)
        {
            int da = System.Math.Max(System.Math.Abs(a.X - _sortCenterCx),
                                     System.Math.Abs(a.Z - _sortCenterCz));
            int db = System.Math.Max(System.Math.Abs(b.X - _sortCenterCx),
                                     System.Math.Abs(b.Z - _sortCenterCz));
            if (da != db) return da.CompareTo(db);

            int sa = (a.X - _sortCenterCx) * (a.X - _sortCenterCx)
                   + (a.Z - _sortCenterCz) * (a.Z - _sortCenterCz);
            int sb = (b.X - _sortCenterCx) * (b.X - _sortCenterCx)
                   + (b.Z - _sortCenterCz) * (b.Z - _sortCenterCz);
            if (sa != sb) return sa.CompareTo(sb);

            int dzA = a.Z - _sortCenterCz;
            int dzB = b.Z - _sortCenterCz;
            if (dzA != dzB) return dzA.CompareTo(dzB);
            return (a.X - _sortCenterCx).CompareTo(b.X - _sortCenterCx);
        }

        private void UnloadDistant(int centerCx, int centerCz)
        {
            // 遍历 _world 而非 _views：区块列可能在生成后、还没建网格之前就被玩家甩开，
            // 走 _views.EnumerateKnownChunks 看不到这种列，会在 _world 里持续累积。先物化
            // 成 List 再统一处理，避免迭代中修改集合本身（List 每帧复用，见 _unloadScratch）。
            var toUnload = _unloadScratch;
            toUnload.Clear();
            foreach (ChunkPos loaded in _world.ChunkPositions)
            {
                int d = System.Math.Max(System.Math.Abs(loaded.X - centerCx),
                                        System.Math.Abs(loaded.Z - centerCz));
                if (d > UnloadRadius)
                {
                    toUnload.Add(loaded);
                }
            }

            // 卸载前保存（milestone-4 B1）：脏区块一旦 RemoveChunk，玩家的方块改动就随内存
            // 丢掉，走远再回来会被 seed 重新生成覆盖。只要本次有脏区块要卸载就整批落盘一次
            // （SaveDirty 只写脏区块）；没有脏区块卸载时不做任何磁盘 IO。
            // 脏集合快照复用 _dirtyScratch（Clear + 拷入），语义与原先每帧 new HashSet 等价：
            // 快照在 SaveDirty 之前完成，保存过程中对 World.DirtyChunks 的清脏不影响判定。
            if (_saveRegionsDir != null && toUnload.Count > 0)
            {
                var dirty = _dirtyScratch;
                dirty.Clear();
                dirty.UnionWith(_world.DirtyChunks);
                foreach (ChunkPos chunk in toUnload)
                {
                    if (dirty.Contains(chunk))
                    {
                        RegionSaveCoordinator.SaveDirty(_world, _saveRegionsDir);
                        break;
                    }
                }
            }

            foreach (ChunkPos chunk in toUnload)
            {
                if (_meshing.Remove(chunk))
                {
                    // 仍在 _meshQueue 里；用共享 scratch 队列重建一个不含该列的队列，
                    // 避免 O(n^2) 全扫描。scratch 在每次用完后必然被排空，可安全复用。
                    var rebuilt = _queueRebuildScratch;
                    while (_meshQueue.Count > 0)
                    {
                        var queued = _meshQueue.Dequeue();
                        if (!queued.Equals(chunk))
                        {
                            rebuilt.Enqueue(queued);
                        }
                    }
                    while (rebuilt.Count > 0)
                    {
                        _meshQueue.Enqueue(rebuilt.Dequeue());
                    }
                }
                if (_generating.Remove(chunk))
                {
                    var rebuilt = _queueRebuildScratch;
                    while (_generateQueue.Count > 0)
                    {
                        var queued = _generateQueue.Dequeue();
                        if (!queued.Equals(chunk))
                        {
                            rebuilt.Enqueue(queued);
                        }
                    }
                    while (rebuilt.Count > 0)
                    {
                        _generateQueue.Enqueue(rebuilt.Dequeue());
                    }
                }
                if (_activeMeshColumn.HasValue && _activeMeshColumn.Value.Equals(chunk))
                {
                    // m5 C2：正在逐 section 续建的列被卸载：丢弃续建进度
                    //（已建的 section 由下面的 UnloadColumn 销毁，未建的不会再建）
                    _activeMeshColumn = null;
                }
                _world.RemoveChunk(chunk);
                _views?.UnloadColumn(chunk);
            }
        }

        private bool AllHorizontalNeighborsLoaded(ChunkPos pos)
        {
            return _world.TryGetChunk(new ChunkPos(pos.X - 1, pos.Z), out _)
                && _world.TryGetChunk(new ChunkPos(pos.X + 1, pos.Z), out _)
                && _world.TryGetChunk(new ChunkPos(pos.X, pos.Z - 1), out _)
                && _world.TryGetChunk(new ChunkPos(pos.X, pos.Z + 1), out _);
        }
    }
}
