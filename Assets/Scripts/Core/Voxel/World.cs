using System.Collections.Generic;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 已加载区块的容器，负责把世界坐标寻址到具体区块列。
    /// </summary>
    public sealed class World
    {
        private readonly Dictionary<ChunkPos, ChunkColumn> _chunks = new Dictionary<ChunkPos, ChunkColumn>();

        private readonly HashSet<ChunkPos> _dirtyChunks = new HashSet<ChunkPos>();

        /// <summary>每区块的编辑版本号（全局计数器发号）。异步保存用它做版本守卫（m5 C3）。</summary>
        private readonly Dictionary<ChunkPos, long> _editVersions = new Dictionary<ChunkPos, long>();

        private long _editCounter;

        /// <summary>全局编辑计数（每次 <see cref="SetBlock"/> 自增）。光照等周界系统用它
        /// 廉价感知「世界变过没」而无需逐调用方接线（评审 02#1/03 B-1：爆炸/挖掘全覆盖；
        /// 直接走 ChunkColumn.SetBlock 的旁路写入不计数——树苗长成等已知豁免，见
        /// ChunkLightSystem 注释）。</summary>
        public long EditCounter => _editCounter;

        public int LoadedChunkCount => _chunks.Count;

        /// <summary>读取不产生副作用：未加载区块一律视为空气，避免采样邻居时意外撑大内存。</summary>
        public ushort GetBlock(int worldX, int worldY, int worldZ)
        {
            var pos = new ChunkPos(VoxelCoords.WorldToChunk(worldX), VoxelCoords.WorldToChunk(worldZ));
            return _chunks.TryGetValue(pos, out ChunkColumn column)
                ? column.GetBlock(VoxelCoords.WorldToLocal(worldX), worldY, VoxelCoords.WorldToLocal(worldZ))
                : ChunkSection.AirId;
        }

        public void SetBlock(int worldX, int worldY, int worldZ, ushort blockId)
        {
            var pos = new ChunkPos(VoxelCoords.WorldToChunk(worldX), VoxelCoords.WorldToChunk(worldZ));
            if (!_chunks.TryGetValue(pos, out ChunkColumn column))
            {
                column = new ChunkColumn();
                _chunks[pos] = column;
            }

            column.SetBlock(VoxelCoords.WorldToLocal(worldX), worldY, VoxelCoords.WorldToLocal(worldZ), blockId);
            _dirtyChunks.Add(pos);
            _editCounter++;
            _editVersions[pos] = _editCounter;
        }

        /// <summary>自上次保存以来被 <see cref="SetBlock"/> 改过的区块（milestone-4 存档用）。</summary>
        public IEnumerable<ChunkPos> DirtyChunks => _dirtyChunks;

        /// <summary>单区块保存成功后清除其脏标记。</summary>
        public void ClearDirty(ChunkPos pos) => _dirtyChunks.Remove(pos);

        /// <summary>区块的编辑版本号：每次 <see cref="SetBlock"/> 自增，从未改过为 0。</summary>
        public long GetEditVersion(ChunkPos pos) => _editVersions.TryGetValue(pos, out long version) ? version : 0;

        /// <summary>版本守卫清脏（m5 C3 异步存档用）：只有保存快照之后<b>没有</b>再被改过的区块
        /// 才清脏标记；保存窗口内又被改过的区块保留脏，下轮保存重写覆盖——否则清脏会丢掉新改动。</summary>
        public void ClearDirtyIfUnchanged(ChunkPos pos, long versionAtSave)
        {
            if (GetEditVersion(pos) == versionAtSave) _dirtyChunks.Remove(pos);
        }

        public bool TryGetChunk(ChunkPos pos, out ChunkColumn column) => _chunks.TryGetValue(pos, out column);

        /// <summary>
        /// 直接挂入一根已经生成好（或从存档载入）的区块列。同一位置已有区块时覆盖，
        /// 使"重新生成"与"用存档覆盖"走同一条路径。
        /// </summary>
        public void AddChunk(ChunkPos pos, ChunkColumn column)
        {
            if (column == null)
            {
                throw new System.ArgumentNullException(nameof(column));
            }

            _chunks[pos] = column;
        }

        /// <summary>流式加载器在玩家走远时调用：移除该区块列，返回是否真的存在并被移除。</summary>
        public bool RemoveChunk(ChunkPos pos) => _chunks.Remove(pos);

        /// <summary>当前已加载区块位置集合。ChunkStreamer 用来判断哪些列还在视野内。</summary>
        public IEnumerable<ChunkPos> ChunkPositions => _chunks.Keys;
    }
}
