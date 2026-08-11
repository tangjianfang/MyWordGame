using MyWorld.Core.Voxel;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 程序化树生成。在地表上方放 5-7 高 log + 3 层 leaves 圆盘。
    /// 纯函数：相同 (seed, worldX, worldZ) 必得相同结果，可并行调用。
    /// </summary>
    public static class TreeFeature
    {
        public const ushort LogId = 1001;   // 来自 StreamingAssets/blocks/log.json numericId
        public const ushort LeavesId = 1002;
        public const ushort SaplingId = 1003;

        public const int MinTrunk = 5;
        public const int MaxTrunk = 7;
        public const int LeafLayers = 3;
        public const int LeafRadius = 2;     // 顶层半径
        public const int SkipChanceDenominator = 80;  // 每 N 格才有 1 棵树

        /// <summary>
        /// 通过 <see cref="World"/> 寻址 chunk，尝试在世界坐标 (worldX, worldZ) 上方放一棵树。
        /// </summary>
        public static bool TryGenerate(World world, int seed, int worldX, int worldZ)
        {
            int chunkX = VoxelCoords.WorldToChunk(worldX);
            int chunkZ = VoxelCoords.WorldToChunk(worldZ);
            if (!world.TryGetChunk(new ChunkPos(chunkX, chunkZ), out var column)) return false;

            return TryGenerate(column, seed, worldX, worldZ);
        }

        /// <summary>
        /// 直接对单根 <see cref="ChunkColumn"/> 尝试放树。WorldGenerator 在生成地形后调用它，
        /// 不再需要 World 引用。
        /// </summary>
        public static bool TryGenerate(ChunkColumn column, int seed, int worldX, int worldZ)
        {
            // 用 hash 决定：这里要不要放树？
            int skipHash = Hash2D(seed ^ 0x511A, worldX, worldZ);
            if (skipHash % SkipChanceDenominator != 0) return false;

            int trunkHeight = MinTrunk + Hash2D(seed ^ 0x713A, worldX, worldZ) % (MaxTrunk - MinTrunk + 1);

            int localX = VoxelCoords.WorldToLocal(worldX);
            int localZ = VoxelCoords.WorldToLocal(worldZ);

            // 找地表
            int surfaceY = -1;
            for (int y = VoxelCoords.MaxY - 1; y >= VoxelCoords.MinY; y--)
            {
                var id = column.GetBlock(localX, y, localZ);
                if (id != BlockIds.Air && id != BlockIds.Water)
                {
                    surfaceY = y;
                    break;
                }
            }
            if (surfaceY < 0) return false;

            // 只在草方块上长（也可放树叶/log 上，简单起见限草方块）
            var groundId = column.GetBlock(localX, surfaceY, localZ);
            if (groundId != BlockIds.Grass) return false;

            // 树干：surface+1 到 surface+trunkHeight 都放 log
            for (int y = surfaceY + 1; y <= surfaceY + trunkHeight; y++)
            {
                column.SetBlock(localX, y, localZ, LogId);
            }

            // 叶冠：从顶层向下 3 层，半径 1-2 圆盘
            for (int layer = 0; layer < LeafLayers; layer++)
            {
                int y = surfaceY + trunkHeight - layer;
                int radius = (layer == 0) ? 1 : LeafRadius;

                for (int dx = -radius; dx <= radius; dx++)
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (dx == 0 && dz == 0) continue;     // 树干位置保留 log
                    // 圆形边界：到中心距离平方 ≤ 半径平方
                    if (dx * dx + dz * dz > radius * radius + 1) continue;
                    int tx = localX + dx;
                    int tz = localZ + dz;
                    if (tx < 0 || tx >= 16 || tz < 0 || tz >= 16) continue;
                    var cur = column.GetBlock(tx, y, tz);
                    if (cur == BlockIds.Air)
                    {
                        column.SetBlock(tx, y, tz, LeavesId);
                    }
                }
            }

            // 最顶上加一个小叶子（顶部冠）
            int topY = surfaceY + trunkHeight + 1;
            if (topY <= VoxelCoords.MaxY)
            {
                int tlx = localX;
                int tlz = localZ;
                if (tlx >= 0 && tlx < 16 && tlz >= 0 && tlz < 16)
                {
                    if (column.GetBlock(tlx, topY, tlz) == BlockIds.Air)
                    {
                        column.SetBlock(tlx, topY, tlz, LeavesId);
                    }
                }
            }

            return true;
        }

        private static int Hash2D(int seed, int x, int z)
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ x;
                h = (h * 397) ^ z;
                h ^= h >> 13;
                h *= 0x5BD1E995;
                h ^= h >> 15;
                return h & 0x7FFFFFFF;
            }
        }
    }
}