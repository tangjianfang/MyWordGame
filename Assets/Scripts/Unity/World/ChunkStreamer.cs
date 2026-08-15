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
        public int ChunksPerFrame { get; set; } = 2;

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

            // 3. 一帧最多处理 ChunksPerFrame 根（生成 + 建网格合计）
            int budget = ChunksPerFrame;
            while (budget > 0 && _generateQueue.Count > 0)
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
                budget--;
            }

            while (budget > 0 && _meshQueue.Count > 0)
            {
                ChunkPos next = _meshQueue.Dequeue();
                _meshing.Remove(next);

                // 邻居全到位才建网格，否则回到队列末尾
                if (!AllHorizontalNeighborsLoaded(next))
                {
                    _meshQueue.Enqueue(next);
                    _meshing.Add(next);
                    // 消耗本次预算，避免外圈区块令当前帧无限自旋
                    budget--;
                    continue;
                }

                _views?.BuildColumn(next);
                budget--;
            }
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
