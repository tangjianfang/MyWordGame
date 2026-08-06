using System.Runtime.CompilerServices;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 世界坐标与区块坐标之间的换算。全项目的坐标转换只允许经过这里，避免负坐标处理散落各处。
    /// </summary>
    public static class VoxelCoords
    {
        public const int ChunkSize = 16;

        public const int MinY = -64;
        public const int MaxY = 320;
        public const int WorldHeight = MaxY - MinY;
        public const int SectionCount = WorldHeight / ChunkSize;

        private const int ChunkShift = 4;
        private const int ChunkMask = ChunkSize - 1;

        /// <summary>算术右移天然向负无穷取整，避免 <c>/</c> 在负数上向零取整的经典错误。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WorldToChunk(int world) => world >> ChunkShift;

        /// <summary>位掩码保证结果恒在 [0, ChunkSize)，无需修正 <c>%</c> 的负余数。</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WorldToLocal(int world) => world & ChunkMask;

        public static int SectionIndexForY(int worldY) => (worldY - MinY) >> ChunkShift;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidY(int worldY) => worldY >= MinY && worldY < MaxY;
    }
}
