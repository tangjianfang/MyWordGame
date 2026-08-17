using MyWorld.Core.Voxel;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 花草散布判定（m11 W1-3）。与 <see cref="TreeFeature"/> 同款「世界坐标整数哈希」纯函数：
    /// 相同 (worldX, worldZ, entry, seed) 必得相同结果，不持有随机数对象，可并行调用。
    /// <para>
    /// 哈希通道与树不同：树的密度通道掺 <c>seed*2654435761</c>（0x9E3779B1），
    /// 花草换乘子 <c>2246822519</c>（0x85EBCA77），并把花草条目 id 的稳定哈希掺进通道——
    /// 两条通道与不同条目之间互不覆盖，同一格既能长树又能长草、poppy 与 daisy 各自独立。
    /// </para>
    /// <para>
    /// <see cref="FlowerEntry.DensityPerChunk"/> 的语义是「每区块期望株数」：
    /// 区块有 16×16=256 格，判定取 <c>hash % 256 &lt; density</c>，
    /// 每格命中概率即 density/256，一个区块的期望株数恰为 density。
    /// </para>
    /// </summary>
    public static class FlowerFeature
    {
        /// <summary>一个区块的列数（16×16），密度语义的分母。</summary>
        public const int ColumnsPerChunk = VoxelCoords.ChunkSize * VoxelCoords.ChunkSize;

        /// <summary>
        /// 花草通道乘子（与树的 2654435761 不同源的质数常量），保证花草判定独立于树判定。
        /// </summary>
        private const int FlowerChannelMultiplier = unchecked((int)2246822519);   // 0x85EBCA77

        /// <summary>
        /// 判定 (worldX, worldZ) 这一格是否放 <paramref name="entry"/> 对应的花草。
        /// 只做散布判定；「这格能不能长」（草方块地表、上方无遮挡）由调用方
        /// （<c>WorldGenerator</c>）在放置时检查。entry 为 null 或密度 ≤0 一律 false。
        /// </summary>
        public static bool ShouldPlaceFlower(int worldX, int worldZ, FlowerEntry entry, int seed)
        {
            if (entry == null || entry.DensityPerChunk <= 0) return false;

            uint h = unchecked((uint)((worldX * 73856093) ^ (worldZ * 19349663)
                                      ^ (seed * FlowerChannelMultiplier + EntryOrdinal(entry.Id))));
            return (int)(h % ColumnsPerChunk) < entry.DensityPerChunk;
        }

        /// <summary>
        /// 花草条目 id 的稳定「序号」：整数哈希（不含随机数对象，不含 seed）。
        /// 不同条目派生不同通道，供散布判定掺进花草通道——与 TreeFeature 的
        /// species 序号哈希算法一致但通道乘子不同，两族判定互不相干。
        /// </summary>
        private static int EntryOrdinal(string entryId)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in entryId)
                {
                    h = h * 31 + c;
                }
                return h;
            }
        }
    }
}
