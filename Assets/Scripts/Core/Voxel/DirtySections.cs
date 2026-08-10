using System.Collections.Generic;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 改一个方块之后，哪些区块段需要重建网格。
    /// <para>
    /// 不是只有它自己那一段：贪心网格生成时会采样**邻居**来剔除接缝面，所以贴着段边界的
    /// 方块会改变相邻段的面可见性。最多牵连 8 个段（水平 4 个 × 竖直 2 个），
    /// 绝大多数情况只有 1 个——比无脑重建 3×3×3 = 27 个段省得多，连续挖掘时差别明显。
    /// </para>
    /// </summary>
    public static class DirtySections
    {
        /// <summary>结果写进 <paramref name="output"/>（会先清空），避免每次挖掘都分配一个新列表。</summary>
        public static void Collect(int worldX, int worldY, int worldZ, List<SectionRef> output)
        {
            output.Clear();

            if (worldY < VoxelCoords.MinY || worldY >= VoxelCoords.MaxY)
            {
                return;
            }

            int chunkX = VoxelCoords.WorldToChunk(worldX);
            int chunkZ = VoxelCoords.WorldToChunk(worldZ);
            int localX = VoxelCoords.WorldToLocal(worldX);
            int localZ = VoxelCoords.WorldToLocal(worldZ);
            int section = VoxelCoords.SectionIndexForY(worldY);
            int localY = VoxelCoords.WorldToLocal(worldY);

            // 水平：自己这根，加上贴边时的邻居（最多 3 根，角上是西/北两根 + 自己）
            AddColumn(output, chunkX, chunkZ, section, localY);

            if (localX == 0) AddColumn(output, chunkX - 1, chunkZ, section, localY);
            if (localX == VoxelCoords.ChunkSize - 1) AddColumn(output, chunkX + 1, chunkZ, section, localY);
            if (localZ == 0) AddColumn(output, chunkX, chunkZ - 1, section, localY);
            if (localZ == VoxelCoords.ChunkSize - 1) AddColumn(output, chunkX, chunkZ + 1, section, localY);
        }

        /// <summary>某根区块列上，本段以及（贴着段上下边界时）相邻的那一段。</summary>
        private static void AddColumn(List<SectionRef> output, int chunkX, int chunkZ, int section, int localY)
        {
            var chunk = new ChunkPos(chunkX, chunkZ);
            output.Add(new SectionRef(chunk, section));

            if (localY == 0 && section > 0)
            {
                output.Add(new SectionRef(chunk, section - 1));
            }
            else if (localY == VoxelCoords.ChunkSize - 1 && section < VoxelCoords.SectionCount - 1)
            {
                output.Add(new SectionRef(chunk, section + 1));
            }
        }
    }
}
