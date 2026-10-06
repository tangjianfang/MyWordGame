using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// (区块, 段) → <see cref="ChunkSectionView"/> 的索引。
    /// <para>
    /// 视图的生命周期只有三件事：需要时建出来、脏了重建、走远了销毁。
    /// 空段（一个面都没有）不保留 GameObject——13×13 区块 × 24 段有 4000 多个，
    /// 其中绝大多数是纯空气或纯石头，留着白占场景层级。
    /// </para>
    /// </summary>
    public sealed class ChunkViewRegistry
    {
        private readonly Dictionary<SectionRef, ChunkSectionView> _views =
            new Dictionary<SectionRef, ChunkSectionView>();

        /// <summary>已有可见段的区块位置集合，去重。ChunkStreamer 用它做"已知区块"判别。</summary>
        private readonly HashSet<ChunkPos> _knownChunks = new HashSet<ChunkPos>();

        private readonly Transform _parent;
        private readonly World _world;
        private readonly BlockRegistry _registry;
        private readonly BlockMaterialLibrary _materials;

        private readonly List<SectionRef> _dirtyScratch = new List<SectionRef>();

        // 评审 02#3/03 B-3：编辑触发的段重建分帧——待建段集合（连续挖掘同段天然去重）
        private readonly HashSet<SectionRef> _pendingSections = new HashSet<SectionRef>();
        private static readonly List<SectionRef> _drainScratch = new List<SectionRef>();

        public ChunkViewRegistry(Transform parent, World world, BlockRegistry registry,
            BlockMaterialLibrary materials)
        {
            _parent = parent;
            _world = world;
            _registry = registry;
            _materials = materials;
        }

        public int ViewCount => _views.Count;

        /// <summary>待重建段数（评审 02#3 测试断言「没当场重建」用）。</summary>
        internal int PendingEditCount => _pendingSections.Count;

        /// <summary>
        /// 建好一根区块列上所有非空段的网格。返回真正出了面的段数。
        /// <para>
        /// m5 C2 起，流式加载路径（<see cref="MyWorld.Unity.Streaming.ChunkStreamer"/>）不再
        /// 调它——整列一次建 24 段是移动尖峰的最大头，改为逐段调 <see cref="Rebuild"/>、
        /// 按毫秒预算跨帧消费。本方法保留给需要整列同步构建的调用方（如测试的等价性比对）。
        /// </para>
        /// </summary>
        public int BuildColumn(ChunkPos chunk)
        {
            if (!_world.TryGetChunk(chunk, out ChunkColumn column))
            {
                return 0;
            }

            var built = 0;
            for (var section = 0; section < VoxelCoords.SectionCount; section++)
            {
                if (column.HasSection(section) && Rebuild(new SectionRef(chunk, section)))
                {
                    built++;
                }
            }

            return built;
        }

        /// <summary>改了一个方块之后调用，牵连到的段一并入待建集合。
        /// 评审 02#3/03 B-3：不再当场重建——挖一格牵连 1-8 段 × 5.5ms/段，常超 16.6ms
        /// 帧预算；现在由 <see cref="DrainPendingEdits"/> 在每帧预算内分批重建（首段延迟
        /// ≤1 帧，孩子无感），连续挖掘同段去重。</summary>
        public void MarkBlockChanged(int worldX, int worldY, int worldZ)
        {
            DirtySections.Collect(worldX, worldY, worldZ, _dirtyScratch);
            foreach (SectionRef section in _dirtyScratch)
            {
                _pendingSections.Add(section);
            }
        }

        /// <summary>在毫秒预算内排空待建段（评审 02#3/03 B-3）。返回剩余待建段数。
        /// 由 ChunkStreamer.Tick 每帧驱动；预算耗尽等下一帧。</summary>
        public int DrainPendingEdits(int budgetMillis)
        {
            if (_pendingSections.Count == 0) return 0;
            _drainScratch.Clear();
            foreach (SectionRef section in _pendingSections) _drainScratch.Add(section);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < _drainScratch.Count; i++)
            {
                if (watch.ElapsedMilliseconds >= budgetMillis) break; // 预算耗尽，剩余留待下帧
                SectionRef section = _drainScratch[i];
                _pendingSections.Remove(section);
                Rebuild(section);
            }
            return _pendingSections.Count;
        }

        /// <summary>重建一个段。返回它是否还有可见面——没有面的段会被销毁。</summary>
        public bool Rebuild(SectionRef section)
        {
            if (!_views.TryGetValue(section, out ChunkSectionView view))
            {
                view = ChunkSectionView.Create(_parent, section.Chunk, section.SectionIndex);
                _views[section] = view;
            }

            if (view.Rebuild(_world, _registry, _materials))
            {
                _knownChunks.Add(section.Chunk);
                return true;
            }

            // 段被挖空（或本来就被完全包裹）：留着只是白占一个 GameObject
            Destroy(section);
            return false;
        }

        /// <summary>卸载整根区块列的视图。</summary>
        public void UnloadColumn(ChunkPos chunk)
        {
            for (var section = 0; section < VoxelCoords.SectionCount; section++)
            {
                Destroy(new SectionRef(chunk, section));
            }

            _knownChunks.Remove(chunk);
        }

        /// <summary>已经至少建过一次（且没被卸载）的区块位置。ChunkStreamer 用它避免重复入队。</summary>
        public IEnumerable<ChunkPos> EnumerateKnownChunks() => _knownChunks;

        private void Destroy(SectionRef section)
        {
            if (!_views.TryGetValue(section, out ChunkSectionView view))
            {
                return;
            }

            _views.Remove(section);
            if (view != null)
            {
                DestroyObject(view.gameObject);
            }
        }

        /// <summary>编辑器非播放态下 Destroy 不生效，必须走 DestroyImmediate。</summary>
        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
