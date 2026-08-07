using System.Collections.Generic;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 已加载区块的容器，负责把世界坐标寻址到具体区块列。
    /// </summary>
    public sealed class World
    {
        private readonly Dictionary<ChunkPos, ChunkColumn> _chunks = new Dictionary<ChunkPos, ChunkColumn>();

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
    }
}
