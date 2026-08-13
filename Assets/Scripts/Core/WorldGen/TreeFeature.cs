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
        public const int SkipChanceDenominator = 80;  // 每 N 格才有 1 棵树（旧 API 回退值）

        // 新 biome 感知放置概率：modulus=10000 时 density*2 表示「每 10000 格期望的树数」。
        // 例：density=8 → 16/10000=0.16%，10000 格约 16 棵；density=30 → 60/10000=0.6%，约 60 棵。
        private const int BiomeChanceModulus = 10000;
        private const int BiomeChanceMultiplier = 2;

        /// <summary>
        /// 按 <see cref="BiomeConfig.TreeDensity"/> 决定 (worldX, worldZ) 这一格是否要尝试放树。
        /// 纯函数：仅依赖 (worldX, worldZ, config.TreeDensity, seed)，与区块生成顺序无关。
        /// config 为 null 或 TreeDensity ≤ 0 时直接 false（沙漠不放树）。
        /// </summary>
        public static bool ShouldPlaceTree(int worldX, int worldZ, BiomeConfig config, int seed)
        {
            if (config == null || config.TreeDensity <= 0) return false;

            // 与 TryGenerate 内部 hash 不同：新通道 (seed*2654435761) 互不干扰旧测试结果。
            uint h = unchecked((uint)((worldX * 73856093) ^ (worldZ * 19349663) ^ (seed * 2654435761)));
            int chance = (int)(h % BiomeChanceModulus);
            int threshold = config.TreeDensity * BiomeChanceMultiplier;
            return chance < threshold;
        }

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
        /// 直接对单根 <see cref="ChunkColumn"/> 尝试放树（旧 API，没有 biome 配置时回退到 <see cref="SkipChanceDenominator"/>）。
        /// </summary>
        public static bool TryGenerate(ChunkColumn column, int seed, int worldX, int worldZ)
        {
            // 用 hash 决定：这里要不要放树？
            int skipHash = Hash2D(seed ^ 0x511A, worldX, worldZ);
            if (skipHash % SkipChanceDenominator != 0) return false;

            return GenerateTree(column, seed, worldX, worldZ);
        }

        /// <summary>
        /// 按 <see cref="BiomeConfig"/> 的密度放置一棵树。WorldGenerator 在生成地形 + 洞穴后调用它。
        /// config 为 null 时退化为 <see cref="TryGenerate(ChunkColumn, int, int, int)"/> 的旧行为。
        /// </summary>
        public static bool TryGenerate(ChunkColumn column, BiomeConfig config, int seed, int worldX, int worldZ)
        {
            if (config == null)
            {
                return TryGenerate(column, seed, worldX, worldZ);
            }

            if (!ShouldPlaceTree(worldX, worldZ, config, seed)) return false;

            return GenerateTree(column, seed, worldX, worldZ);
        }

        /// <summary>
        /// 真正的放置逻辑：找到地表 → 树干 → 叶冠 → 顶部叶。前提是 ShouldPlaceTree 已经放行。
        /// </summary>
        private static bool GenerateTree(ChunkColumn column, int seed, int worldX, int worldZ)
        {
            int localX = VoxelCoords.WorldToLocal(worldX);
            int localZ = VoxelCoords.WorldToLocal(worldZ);

            int trunkHeight = MinTrunk + Hash2D(seed ^ 0x713A, worldX, worldZ) % (MaxTrunk - MinTrunk + 1);

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