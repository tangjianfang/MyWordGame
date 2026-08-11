using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
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

        private readonly Queue<ChunkPos> _generateQueue = new Queue<ChunkPos>();
        private readonly Queue<ChunkPos> _meshQueue = new Queue<ChunkPos>();
        private readonly HashSet<ChunkPos> _generating = new HashSet<ChunkPos>();
        private readonly HashSet<ChunkPos> _meshing = new HashSet<ChunkPos>();

        public int LoadRadius { get; set; } = 6;
        public int UnloadRadius { get; set; } = 8;
        public int ChunksPerFrame { get; set; } = 2;

        // registry 与 seed 由 WorldGenerator 与 ChunkViewRegistry 各自持有，
        // 此处仅保留构造参数以维持 WorldBootstrap.Awake() 的调用契约。
        public ChunkStreamer(World world, WorldGenerator generator, BlockRegistry registry,
            ChunkViewRegistry views, long seed)
        {
            _ = registry;
            _ = seed;
            _world = world;
            _generator = generator;
            _views = views;
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
                ChunkColumn column = _generator.Generate(next);
                _world.AddChunk(next, column);
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

                _views.BuildColumn(next);
                budget--;
            }
        }

        private void EnqueueMissing(int centerCx, int centerCz)
        {
            // 收集范围内的待加载列，再按“距中心由近到远”排序，避免单帧涌入外围区块
            // 而玩家所在的中心列迟迟得不到处理（玩家会落入未加载的“空气”，最后
            // 撞进石头里被夹住）。主键 Chebyshev 与卸载逻辑保持一致；
            // 同环内按平方距离再加 (dz, dx) 两级 tiebreaker，整套序完全确定性。
            var candidates = new List<ChunkPos>();
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

            candidates.Sort((a, b) =>
            {
                int da = System.Math.Max(System.Math.Abs(a.X - centerCx),
                                         System.Math.Abs(a.Z - centerCz));
                int db = System.Math.Max(System.Math.Abs(b.X - centerCx),
                                         System.Math.Abs(b.Z - centerCz));
                if (da != db) return da.CompareTo(db);

                int sa = (a.X - centerCx) * (a.X - centerCx)
                       + (a.Z - centerCz) * (a.Z - centerCz);
                int sb = (b.X - centerCx) * (b.X - centerCx)
                       + (b.Z - centerCz) * (b.Z - centerCz);
                if (sa != sb) return sa.CompareTo(sb);

                int dzA = a.Z - centerCz;
                int dzB = b.Z - centerCz;
                if (dzA != dzB) return dzA.CompareTo(dzB);
                return (a.X - centerCx).CompareTo(b.X - centerCx);
            });

            foreach (var pos in candidates)
            {
                _generateQueue.Enqueue(pos);
                _generating.Add(pos);
            }
        }

        private void UnloadDistant(int centerCx, int centerCz)
        {
            // 遍历 _world 而非 _views：区块列可能在生成后、还没建网格之前就被玩家甩开，
            // 走 _views.EnumerateKnownChunks 看不到这种列，会在 _world 里持续累积。先物化
            // 成 List 再统一处理，避免迭代中修改集合本身。
            var toUnload = new List<ChunkPos>();
            foreach (ChunkPos loaded in _world.ChunkPositions)
            {
                int d = System.Math.Max(System.Math.Abs(loaded.X - centerCx),
                                        System.Math.Abs(loaded.Z - centerCz));
                if (d > UnloadRadius)
                {
                    toUnload.Add(loaded);
                }
            }

            foreach (ChunkPos chunk in toUnload)
            {
                if (_meshing.Remove(chunk))
                {
                    // 仍在 _meshQueue 里；重建一个不含该列的临时队列避免 O(n^2) 全扫描
                    var rebuilt = new Queue<ChunkPos>(_meshQueue.Count);
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
                    var rebuilt = new Queue<ChunkPos>(_generateQueue.Count);
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
                _views.UnloadColumn(chunk);
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
