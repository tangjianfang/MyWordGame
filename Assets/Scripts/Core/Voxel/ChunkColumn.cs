using System;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 一根区块列，纵向由 <see cref="VoxelCoords.SectionCount"/> 个 section 组成，按需分配。
    /// </summary>
    public sealed class ChunkColumn
    {
        private readonly ChunkSection[] _sections = new ChunkSection[VoxelCoords.SectionCount];
        private int _allocatedSectionCount;

        public int AllocatedSectionCount => _allocatedSectionCount;

        public bool HasSection(int sectionIndex) => _sections[sectionIndex] != null;

        public ChunkSection GetSection(int sectionIndex) => _sections[sectionIndex];

        public ChunkSection GetOrCreateSection(int sectionIndex)
        {
            ChunkSection section = _sections[sectionIndex];
            if (section == null)
            {
                section = new ChunkSection();
                _sections[sectionIndex] = section;
                _allocatedSectionCount++;
            }

            return section;
        }

        /// <summary>
        /// 越界时返回空气而非抛异常：网格生成需要采样世界顶底部的“邻居”，宽容读取可免掉大量边界判断。
        /// </summary>
        public ushort GetBlock(int localX, int worldY, int localZ)
        {
            if (!VoxelCoords.IsValidY(worldY))
            {
                return ChunkSection.AirId;
            }

            ChunkSection section = _sections[VoxelCoords.SectionIndexForY(worldY)];
            return section == null
                ? ChunkSection.AirId
                : section.Get(localX, VoxelCoords.WorldToLocal(worldY), localZ);
        }

        /// <summary>写入则严格校验，越界写入总是调用方的 bug。</summary>
        public void SetBlock(int localX, int worldY, int localZ, ushort blockId)
        {
            if (!VoxelCoords.IsValidY(worldY))
            {
                throw new ArgumentOutOfRangeException(nameof(worldY), worldY,
                    $"高度必须落在 [{VoxelCoords.MinY}, {VoxelCoords.MaxY}) 内。");
            }

            int sectionIndex = VoxelCoords.SectionIndexForY(worldY);
            ChunkSection section = _sections[sectionIndex];

            if (section == null)
            {
                if (blockId == ChunkSection.AirId)
                {
                    return;
                }

                section = new ChunkSection();
                _sections[sectionIndex] = section;
                _allocatedSectionCount++;
            }

            section.Set(localX, VoxelCoords.WorldToLocal(worldY), localZ, blockId);
        }
    }
}
