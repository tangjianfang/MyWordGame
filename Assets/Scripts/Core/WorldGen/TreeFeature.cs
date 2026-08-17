using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 程序化树生成。在地表上方放 5-7 高 log + 3 层 leaves 圆盘。
    /// 纯函数：相同 (seed, worldX, worldZ) 必得相同结果，可并行调用。
    /// m11 I2 起：数值进 <see cref="TreeSpecies"/>（vegetation/trees.json 数据驱动），
    /// 旧 API 全部保留并内部走 <see cref="OakSpecies"/>——oak 行为逐格不变。
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
        /// 内置 oak species：数值取上面的编译期常量。旧 API 与它等价（守卫测试逐格比对），
        /// 也是 <c>vegetation/trees.json</c> 里 oak 条目必须照抄的基准。
        /// </summary>
        public static readonly TreeSpecies OakSpecies = new TreeSpecies
        {
            Id = "oak",
            LogBlock = "log",
            LeavesBlock = "leaves",
            TrunkMin = MinTrunk,
            TrunkMax = MaxTrunk,
            LeafLayers = LeafLayers,
            LeafRadius = LeafRadius,
            Biomes = new[] { "Plains", "Forest" },
        };

        /// <summary>
        /// 方块注册表（可选）。species 按方块名（如 "birch_log"）查 numericId 时用；
        /// 未绑定时内置 log/leaves 走编译期常量兜底。第 1 波注册新树种方块后由启动器绑定。
        /// </summary>
        private static BlockRegistry _blockRegistry;

        /// <summary>把方块注册表接进树生成（species 的方块名 → numericId）。传 null 解绑。</summary>
        public static void BindBlockRegistry(BlockRegistry registry)
        {
            _blockRegistry = registry;
        }

        /// <summary>
        /// 注册表是否已绑定。WorldGenerator 的 Core 侧兜底绑定用它在首次生成时
        /// 「未绑定才绑」，避免覆盖启动器（集成点②）显式绑定的自定义注册表。
        /// </summary>
        public static bool BlockRegistryBound => _blockRegistry != null;

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
        /// 按 species 密度通道判定 (worldX, worldZ) 这一格是否放该树种（m11 I2）。
        /// 通道 = 旧通道第三项换成 <c>seed*2654435761 + species 序号</c>——不同树种在同一格
        /// 派生出互不覆盖的哈希，判定彼此独立；仍是世界坐标整数哈希，纯函数、可并行。
        /// speciesId 的「序号」用稳定字符串哈希充当（表不在手边也能纯函数派生）。
        /// </summary>
        public static bool ShouldPlaceTree(int worldX, int worldZ, string speciesId, BiomeConfig config, int seed)
        {
            if (config == null || config.TreeDensity <= 0) return false;
            if (string.IsNullOrEmpty(speciesId)) return false;

            uint h = unchecked((uint)((worldX * 73856093) ^ (worldZ * 19349663)
                                      ^ (seed * 2654435761 + SpeciesOrdinal(speciesId))));
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

            return GenerateTree(column, seed, worldX, worldZ, OakSpecies);
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

            return GenerateTree(column, seed, worldX, worldZ, OakSpecies);
        }

        /// <summary>
        /// 按 <see cref="TreeSpecies"/> 生成一棵树（m11 I2）。不做密度判定——放置与否由调用方先用
        /// <see cref="ShouldPlaceTree(int, int, string, BiomeConfig, int)"/> 的 species 通道问过；
        /// 本重载只负责「这格能不能长（找地表/限草方块）+ 按参数落块」。species 为 null 返回 false。
        /// </summary>
        public static bool TryGenerate(World world, int seed, int worldX, int worldZ, TreeSpecies species)
        {
            if (species == null) return false;

            int chunkX = VoxelCoords.WorldToChunk(worldX);
            int chunkZ = VoxelCoords.WorldToChunk(worldZ);
            if (!world.TryGetChunk(new ChunkPos(chunkX, chunkZ), out var column)) return false;

            return GenerateTree(column, seed, worldX, worldZ, species);
        }

        /// <summary>
        /// 直接对单根 <see cref="ChunkColumn"/> 按 species 放树（m11 W1-3）。
        /// 与 <see cref="WorldGenerator"/> 的生成循环配合：那里手里是刚填完地形的列，
        /// 不必再经 <see cref="World"/> 寻址。放置与否仍由调用方先用
        /// <see cref="ShouldPlaceTree(int, int, string, BiomeConfig, int)"/> 问过；species 为 null 返回 false。
        /// </summary>
        public static bool TryGenerate(ChunkColumn column, int seed, int worldX, int worldZ, TreeSpecies species)
        {
            if (species == null) return false;
            return GenerateTree(column, seed, worldX, worldZ, species);
        }

        /// <summary>
        /// 真正的放置逻辑：找到地表 → 树干 → 叶冠 → 顶部叶。前提是放置判定已经放行。
        /// 按 species 参数落块；树干高度哈希沿用旧通道（不掺 species 序号）——
        /// oak 走这里必须与历史逐格一致，这是「oak 行为不变」的铁律。
        /// </summary>
        private static bool GenerateTree(ChunkColumn column, int seed, int worldX, int worldZ, TreeSpecies species)
        {
            // 手工构造的 species 可能绕过 VegetationTable.Load 的校验，这里挡一下非法区间
            if (species.TrunkMax < species.TrunkMin || species.LeafLayers < 1 || species.LeafRadius < 1) return false;

            int localX = VoxelCoords.WorldToLocal(worldX);
            int localZ = VoxelCoords.WorldToLocal(worldZ);

            int trunkHeight = species.TrunkMin
                + Hash2D(seed ^ 0x713A, worldX, worldZ) % (species.TrunkMax - species.TrunkMin + 1);

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

            // 只在草方块上长（也可放树叶/log 上，简单起见限草方块）。
            // m11 W1-3 例外：声明投放 Snow 群系的树种（pine/cedar）也能长在雪方块上——
            // 雪原地表是雪不是草，不放开的话这些树种在声明群系里永远出不来。
            // oak 的 biomes 只有 Plains/Forest，不享此例外——旧路径行为逐格不变仍成立。
            var groundId = column.GetBlock(localX, surfaceY, localZ);
            bool growsOnSnow = GrowsOnSnowGround(species);
            if (groundId != BlockIds.Grass && !(growsOnSnow && groundId == BlockIds.Snow)) return false;

            ushort logId = ResolveBlockId(species.LogBlock);
            ushort leavesId = ResolveBlockId(species.LeavesBlock);

            // 树干：surface+1 到 surface+trunkHeight 都放 log
            for (int y = surfaceY + 1; y <= surfaceY + trunkHeight; y++)
            {
                column.SetBlock(localX, y, localZ, logId);
            }

            // 叶冠：从顶层向下 LeafLayers 层，半径 1..LeafRadius 圆盘
            for (int layer = 0; layer < species.LeafLayers; layer++)
            {
                int y = surfaceY + trunkHeight - layer;
                int radius = (layer == 0) ? 1 : species.LeafRadius;

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
                        column.SetBlock(tx, y, tz, leavesId);
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
                        column.SetBlock(tlx, topY, tlz, leavesId);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// species 方块名 → numericId。优先走绑定的 <see cref="BlockRegistry"/>（未注册抛
        /// KeyNotFoundException，消息自带方块 id）；未绑定时内置 log/leaves 用编译期常量兜底，
        /// 保证纯 Core 路径（测试/旧世界生成）不需要注册表也能得到与历史一致的 oak。
        /// </summary>
        private static ushort ResolveBlockId(string blockId)
        {
            var registry = _blockRegistry;
            if (registry != null)
            {
                return registry.GetById(blockId).NumericId;
            }

            switch (blockId)
            {
                case "log": return LogId;
                case "leaves": return LeavesId;
                default:
                    throw new KeyNotFoundException(
                        $"树种方块 {blockId} 尚未注册：注册新方块后需先 TreeFeature.BindBlockRegistry 绑定注册表");
            }
        }

        /// <summary>
        /// 该树种是否允许长在雪方块上：biomes 声明含 "Snow" 即耐雪（数据驱动，无代码硬编码树种表）。
        /// 未声明 Snow 的树种（含 oak——Plains/Forest）仍只在草方块上长，旧路径行为不变。
        /// </summary>
        private static bool GrowsOnSnowGround(TreeSpecies species)
        {
            if (species?.Biomes == null) return false;
            foreach (string biome in species.Biomes)
            {
                if (biome == "Snow") return true;
            }
            return false;
        }

        /// <summary>
        /// speciesId 的稳定「序号」：整数哈希（不含随机数对象，不含 seed）。
        /// 不同 id 派生不同通道，供放置判定掺进 seed*2654435761 通道。
        /// </summary>
        private static int SpeciesOrdinal(string speciesId)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in speciesId)
                {
                    h = h * 31 + c;
                }
                return h;
            }
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