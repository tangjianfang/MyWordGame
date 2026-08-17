using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 世界生成入口。所有阶段只依赖 seed 与世界坐标，不依赖生成顺序，保证跨区块地形连续且可复现。
    ///
    /// C3 起在原生成流程后追加两个阶段：
    ///   1. 每根 (worldX, worldZ) 列根据温度/湿度噪声选 <see cref="Biome"/>，
    ///      再用 <see cref="BiomeConfig.CaveMultiplier"/> 决定洞穴密度；
    ///   2. <see cref="CaveCarver"/> 在地表下若干层起开始挖空，但绝不挖基岩。
    ///
    /// m11 W1-3 起地表植被按 <c>StreamingAssets/vegetation/</c> 的植被表投放：
    /// oak 走旧路径（行为逐格不变），其余 7 树种按 species 密度通道投放，
    /// 12 种花草按 <see cref="FlowerFeature"/> 散布在草方块上方。
    /// </summary>
    public sealed class WorldGenerator
    {
        private const float TerrainScale = 0.008f;
        private const float ClimateScale = 0.004f;
        /// <summary>海平面高度。公开给地表方块映射的单测与运行时查询。</summary>
        public const int SeaLevel = 62;
        private const int BaseHeight = 68;
        private const int HeightAmplitude = 40;
        private const int DirtDepth = 4;

        /// <summary>洞穴挖掘始终停留在地表下方 N 格以内，避免把地表/表土也挖穿。</summary>
        private const int CaveStartBelowSurface = 7;

        private readonly ValueNoise2D _terrainNoise;
        private readonly ValueNoise2D _climateNoise;
        private readonly CaveCarver _carver;
        private readonly Dictionary<int, BiomeConfig> _biomeConfigs;
        private readonly int _seed;

        // m11 W1-3：植被表与方块注册表，首次生成时从 StreamingAssets 惰性加载（见 EnsureVegetationLoaded）
        private VegetationTable _vegetationTable;
        private BlockRegistry _vegetationBlockRegistry;
        private bool _vegetationLoadAttempted;

        public WorldGenerator(int seed)
            : this(seed, LoadBiomeConfigsOrNull(), null, null)
        {
        }

        public WorldGenerator(int seed, IList<BiomeConfig> biomeConfigs)
            : this(seed, biomeConfigs, null, null)
        {
        }

        /// <summary>
        /// 显式注入植被表与方块注册表的构造（m11 W1-3）。Unity 侧启动器（集成点②）与
        /// 双链测试用它在引擎进程里传入已加载的表——Core 的自动加载依赖
        /// 「从进程目录向上找 Assets/StreamingAssets」，在 Unity 编辑器里
        /// AppContext.BaseDirectory 指向编辑器安装目录，走不到工程目录，
        /// 显式注入是引擎链路唯一的可靠入口（与 BlockDefinitionFilesTests 的
        /// #if UNITY_EDITOR 定位同因）。注入的注册表同时绑给
        /// <see cref="TreeFeature.BindBlockRegistry"/>（仅当尚未绑定时）。
        /// 传 null 则退回自动加载行为。
        /// </summary>
        public WorldGenerator(int seed, IList<BiomeConfig> biomeConfigs,
            VegetationTable vegetation, BlockRegistry blockRegistry)
        {
            _seed = seed;
            _terrainNoise = new ValueNoise2D(seed);
            _climateNoise = new ValueNoise2D(seed ^ unchecked((int)0x4F1A2C3B));
            _carver = new CaveCarver(seed);
            _biomeConfigs = BuildBiomeLookup(biomeConfigs);

            if (vegetation != null)
            {
                _vegetationTable = vegetation;
                _vegetationBlockRegistry = blockRegistry;
                _vegetationLoadAttempted = true;   // 已显注入，不再自动加载
                if (blockRegistry != null && !TreeFeature.BlockRegistryBound)
                {
                    TreeFeature.BindBlockRegistry(blockRegistry);
                }
            }
        }

        public ChunkColumn Generate(ChunkPos pos)
        {
            var column = new ChunkColumn();

            int originX = pos.X * VoxelCoords.ChunkSize;
            int originZ = pos.Z * VoxelCoords.ChunkSize;

            // 阶段 1：实心地形（沿用旧逻辑）。先把每根列的地表高度记下来，cave pass 直接复用。
            var surfaceYs = new int[VoxelCoords.ChunkSize, VoxelCoords.ChunkSize];
            for (var localZ = 0; localZ < VoxelCoords.ChunkSize; localZ++)
            {
                for (var localX = 0; localX < VoxelCoords.ChunkSize; localX++)
                {
                    // 以世界坐标而非区块内坐标采样，接缝两侧自然对齐
                    int surfaceY = SurfaceHeightAt(originX + localX, originZ + localZ);
                    surfaceYs[localX, localZ] = surfaceY;
                    FillColumn(column, originX + localX, originZ + localZ, surfaceY,
                        BiomeAt(originX + localX, originZ + localZ), _seed);
                }
            }

            // 阶段 2：cave pass。每根列按所在 biome 的 CaveMultiplier 决定洞穴密度。
            for (var localZ = 0; localZ < VoxelCoords.ChunkSize; localZ++)
            {
                for (var localX = 0; localX < VoxelCoords.ChunkSize; localX++)
                {
                    int worldX = originX + localX;
                    int worldZ = originZ + localZ;
                    Biome biome = BiomeAt(worldX, worldZ);
                    _biomeConfigs.TryGetValue((int)biome, out var config);
                    float caveDensity = config != null ? config.CaveMultiplier * 0.5f : 0.5f;
                    CarveCavesInColumn(column, localX, localZ, surfaceYs[localX, localZ],
                        worldX, worldZ, caveDensity);
                }
            }

            // 阶段 3：地表铺好后再长植被（m11 W1-3 起按植被表投放）。
            //   · oak 走旧路径：不查植被表、不限声明群系（Mountains/Snow 等密度>0 的群系照旧长橡树），
            //     密度判定仍用旧通道——oak 行为逐格不变是铁律，I2/W1-3 守卫测试锁定；
            //   · 其余树种按 VegetationTable 的 species 密度通道投放，只在 trees.json 声明的群系出现；
            //   · 花草在树之后散布（树先占地表判定，花草让位），只落在草方块上方的空格。
            // TreeDensity=0 一直是「无植被」的开关（沙漠 + 测试纯地形断言），新树种与花草同样遵守。
            EnsureVegetationLoaded();
            var vegetation = _vegetationTable;
            for (var localZ = 0; localZ < VoxelCoords.ChunkSize; localZ++)
            {
                for (var localX = 0; localX < VoxelCoords.ChunkSize; localX++)
                {
                    int worldX = originX + localX;
                    int worldZ = originZ + localZ;
                    Biome biome = BiomeAt(worldX, worldZ);
                    _biomeConfigs.TryGetValue((int)biome, out var config);

                    // oak 旧路径：保持与接入植被表之前完全一致
                    TreeFeature.TryGenerate(column, config, _seed, worldX, worldZ);

                    if (vegetation == null || config == null || config.TreeDensity <= 0) continue;

                    foreach (var species in vegetation.Trees)
                    {
                        if (species.Id == TreeFeature.OakSpecies.Id) continue;   // oak 已走旧路径
                        if (!BiomeAllows(species.Biomes, config.Name)) continue;
                        if (!TreeFeature.ShouldPlaceTree(worldX, worldZ, species.Id, config, _seed)) continue;
                        TreeFeature.TryGenerate(column, _seed, worldX, worldZ, species);
                    }

                    foreach (var flower in vegetation.Flowers)
                    {
                        if (!BiomeAllows(flower.Biomes, config.Name)) continue;
                        if (!FlowerFeature.ShouldPlaceFlower(worldX, worldZ, flower, _seed)) continue;
                        TryPlaceFlower(column, localX, localZ, flower);
                    }
                }
            }

            return column;
        }

        /// <summary>
        /// 惰性加载植被表与方块注册表（m11 W1-3）。
        /// <para>
        /// 绑定说明：本波并行纪律禁改 WorldBootstrap，Unity 侧显式绑定推迟到集成点②；
        /// Core 侧在生成器首次生成时自行从 StreamingAssets 加载默认注册表并绑给
        /// <see cref="TreeFeature"/>（仅当尚未绑定时才绑——不覆盖启动器将来显式
        /// 绑定的自定义注册表），保证 dotnet 测试 / MyWorld.Preview / 实机三条链路
        /// 在无人显式绑定前行为一致。
        /// </para>
        /// <para>
        /// 任一环节缺失或解析失败都退回「只有旧路径 oak」的既有行为，不中断地形生成；
        /// 真实文件的错误由 BlockDefinitionFilesTests / VegetationTableTests 守卫。
        /// </para>
        /// </summary>
        private void EnsureVegetationLoaded()
        {
            if (_vegetationLoadAttempted) return;
            _vegetationLoadAttempted = true;

            string streamingAssets = LocateStreamingAssetsOrNull();
            if (streamingAssets == null) return;

            try
            {
                string vegetationDir = Path.Combine(streamingAssets, "vegetation");
                var table = VegetationTable.Load(
                    File.ReadAllText(Path.Combine(vegetationDir, "trees.json")),
                    File.ReadAllText(Path.Combine(vegetationDir, "flowers.json")));

                string blocksDir = Path.Combine(streamingAssets, "blocks");
                var registry = BlockRegistry.FromJson(
                    Directory.GetFiles(blocksDir, "*.json").Select(File.ReadAllText));

                _vegetationTable = table;
                _vegetationBlockRegistry = registry;
                if (!TreeFeature.BlockRegistryBound)
                {
                    TreeFeature.BindBlockRegistry(registry);
                }
            }
            catch (Exception)
            {
                // 植被表/注册表有问题（文件缺失、JSON 坏）时退回旧 oak-only 行为
                _vegetationTable = null;
                _vegetationBlockRegistry = null;
            }
        }

        /// <summary>从当前进程目录向上最多 8 层找 Assets/StreamingAssets（与 LoadBiomeConfigsOrNull 同款约定）。
        /// 除 AppContext.BaseDirectory 外也尝试当前工作目录——Unity 编辑器进程的 BaseDirectory
        /// 指向编辑器安装目录，但以 -projectPath 启动时 CWD 是工程根，多一个候选多一条活路。</summary>
        private static string LocateStreamingAssetsOrNull()
        {
            foreach (string root in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                DirectoryInfo directory;
                try
                {
                    directory = new DirectoryInfo(root);
                }
                catch
                {
                    continue;
                }

                for (int depth = 0; depth < 8 && directory != null; depth++, directory = directory.Parent)
                {
                    string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 在 (localX, localZ) 列自上而下找地表：顶格是草方块且其上一格是空气时放花草方块。
        /// 沙地/雪原/水下（地表非草）不放；树干/树叶占据的格子（地表之上非空气）也不放。
        /// 花草方块未注册时防御性跳过（表↔注册表闭环由守卫测试守）。
        /// </summary>
        private void TryPlaceFlower(ChunkColumn column, int localX, int localZ, FlowerEntry flower)
        {
            ushort flowerId;
            try
            {
                flowerId = _vegetationBlockRegistry.GetById(flower.Block).NumericId;
            }
            catch (KeyNotFoundException)
            {
                return;
            }

            for (int y = VoxelCoords.MaxY - 1; y > VoxelCoords.MinY; y--)
            {
                ushort id = column.GetBlock(localX, y, localZ);
                if (id == BlockIds.Air || id == BlockIds.Water) continue;   // 空中/水面直接跳过

                if (id != BlockIds.Grass) return;                            // 只有草地长花草
                if (y + 1 >= VoxelCoords.MaxY) return;                       // 顶到世界上限就不放
                if (column.GetBlock(localX, y + 1, localZ) != BlockIds.Air) return;   // 已被树干/树叶占据
                column.SetBlock(localX, y + 1, localZ, flowerId);
                return;
            }
        }

        /// <summary>species/花草的 biomes 数组是否包含当前群系名（Ordinal 精确匹配——
        /// 群系名是跨表标识符，必须与 biomes.json 的 name 字段一字不差）。</summary>
        private static bool BiomeAllows(string[] biomes, string biomeName)
        {
            if (biomes == null || string.IsNullOrEmpty(biomeName)) return false;
            foreach (string name in biomes)
            {
                if (string.Equals(name, biomeName, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        public int SurfaceHeightAt(int worldX, int worldZ)
        {
            float noise = _terrainNoise.SampleFbm(worldX * TerrainScale, worldZ * TerrainScale,
                octaves: 4, lacunarity: 2f, gain: 0.5f);
            return BaseHeight + (int)(noise * HeightAmplitude);
        }

        /// <summary>取 (worldX, worldZ) 列对应的 biome。仅依赖世界坐标与 seed，与生成顺序无关。
        /// 公开给 MobManager 等运行时刷怪系统查询当前刷怪点的 biome。</summary>
        public Biome BiomeAt(int worldX, int worldZ)
        {
            float temperature = SampleClimate(worldX, worldZ, channelOffset: 0f);
            float humidity = SampleClimate(worldX, worldZ, channelOffset: 1000f);
            return BiomeSelector.Select(temperature, humidity);
        }

        /// <summary>温度/湿度共用一套噪声，靠通道偏移分开两路；输出归一化到 [0, 1]。</summary>
        private float SampleClimate(int worldX, int worldZ, float channelOffset)
        {
            float raw = _climateNoise.SampleFbm(
                worldX * ClimateScale + channelOffset,
                worldZ * ClimateScale,
                octaves: 2, lacunarity: 2f, gain: 0.5f);
            // SampleFbm 输出 [-1, 1]，平移到 [0, 1] 再交给 BiomeSelector。
            return raw * 0.5f + 0.5f;
        }

        /// <summary>沿一根列从基岩顶到地表下方 <see cref="CaveStartBelowSurface"/> 处逐格询问 carver。</summary>
        private void CarveCavesInColumn(ChunkColumn column, int localX, int localZ, int surfaceY,
            int worldX, int worldZ, float caveDensity)
        {
            // CaveMultiplier=0 表示「这个群系不要挖洞」，直接跳过整根列；carver 自身仍有 35% 基础概率，
            // 不在这里短路的话测试期望（无洞）与实际（仍然有洞）对不上。
            if (caveDensity <= 0f)
            {
                return;
            }

            int carveTop = surfaceY - CaveStartBelowSurface;
            if (carveTop >= VoxelCoords.MaxY - 1)
            {
                // 地表接近世界顶，整根列都被 cave pass 跳过（地表就在天上，少见）。
                return;
            }

            if (carveTop < VoxelCoords.MinY + 1)
            {
                carveTop = VoxelCoords.MinY + 1;
            }

            for (int y = VoxelCoords.MinY + 1; y <= carveTop; y++)
            {
                var pos = new Float3(worldX, y, worldZ);
                if (!_carver.ShouldCarve(pos, caveDensity))
                {
                    continue;
                }

                ushort current = column.GetBlock(localX, y, localZ);
                if (current == BlockIds.Bedrock || current == BlockIds.Air)
                {
                    continue;
                }

                column.SetBlock(localX, y, localZ, BlockIds.Air);
            }
        }

        /// <summary>
        /// biome-aware 地表方块选择（spec C4：plains/forest=grass、desert=sand、snow=snow）。
        /// 海平面及以下不分群系统一沙子——岸线/水下行为沿用旧规则。
        /// 纯函数、无状态，单测直接覆盖映射表。
        /// </summary>
        public static ushort SurfaceBlockFor(Biome biome, int surfaceY)
        {
            if (surfaceY <= SeaLevel) return BlockIds.Sand;
            switch (biome)
            {
                case Biome.Desert: return BlockIds.Sand;
                case Biome.Snow: return BlockIds.Snow;
                default: return BlockIds.Grass;
            }
        }

        /// <summary>
        /// 沿一根列从基岩填到地表：基岩 → 石层（嵌矿）→ 表土 → 地表。
        /// 嵌矿只发生在石头分支——地表/表土/基岩的方块选择不受 <see cref="OreFeature"/> 影响。
        /// 参数用世界坐标（哈希按世界坐标散列，与区块生成顺序无关）。
        /// </summary>
        private static void FillColumn(ChunkColumn column, int worldX, int worldZ, int surfaceY, Biome biome, int seed)
        {
            int localX = VoxelCoords.WorldToLocal(worldX);
            int localZ = VoxelCoords.WorldToLocal(worldZ);

            column.SetBlock(localX, VoxelCoords.MinY, localZ, BlockIds.Bedrock);

            for (int y = VoxelCoords.MinY + 1; y <= surfaceY; y++)
            {
                ushort block;
                if (y == surfaceY)
                {
                    block = SurfaceBlockFor(biome, surfaceY);
                }
                else if (y > surfaceY - DirtDepth)
                {
                    block = BlockIds.Dirt;
                }
                else
                {
                    // m10 A2：石层按世界坐标哈希嵌矿，无矿时 OreAt 返回 Stone
                    block = OreFeature.OreAt(seed, worldX, y, worldZ);
                }

                column.SetBlock(localX, y, localZ, block);
            }

            for (int y = surfaceY + 1; y <= SeaLevel; y++)
            {
                column.SetBlock(localX, y, localZ, BlockIds.Water);
            }
        }

        /// <summary>
        /// 构造群系查找表。先写一份保守的内置默认值，保证即使没读到 biomes.json
        /// 也能跑；调用方传入的 JSON 配置覆盖同 ID 条目。
        /// </summary>
        private static Dictionary<int, BiomeConfig> BuildBiomeLookup(IList<BiomeConfig> configs)
        {
            var lookup = new Dictionary<int, BiomeConfig>
            {
                [(int)Biome.Plains]    = new BiomeConfig { Id = (int)Biome.Plains,    Name = "Plains",    TreeDensity = 8,  CaveMultiplier = 1.0f },
                [(int)Biome.Desert]    = new BiomeConfig { Id = (int)Biome.Desert,    Name = "Desert",    TreeDensity = 0,  CaveMultiplier = 0.5f },
                [(int)Biome.Forest]    = new BiomeConfig { Id = (int)Biome.Forest,    Name = "Forest",    TreeDensity = 30, CaveMultiplier = 1.5f },
                [(int)Biome.Mountains] = new BiomeConfig { Id = (int)Biome.Mountains, Name = "Mountains", TreeDensity = 2,  CaveMultiplier = 2.0f },
                [(int)Biome.Snow]      = new BiomeConfig { Id = (int)Biome.Snow,      Name = "Snow",      TreeDensity = 1,  CaveMultiplier = 1.0f }
            };

            if (configs != null)
            {
                foreach (var c in configs)
                {
                    if (c == null) continue;
                    lookup[c.Id] = c;
                }
            }

            return lookup;
        }

        /// <summary>
        /// 从 <c>Assets/StreamingAssets/biomes.json</c> 加载群系配置。从当前进程工作目录向上最多 8 层查找，
        /// 这样无论测试进程、Unity 编辑器、还是打包后的桌面播放器，CWD 不同也能命中同一份 JSON。
        /// </summary>
        private static IList<BiomeConfig> LoadBiomeConfigsOrNull()
        {
            DirectoryInfo directory;
            try
            {
                directory = new DirectoryInfo(AppContext.BaseDirectory);
            }
            catch
            {
                return null;
            }

            for (int depth = 0; depth < 8 && directory != null; depth++, directory = directory.Parent)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "biomes.json");
                if (!File.Exists(candidate))
                {
                    continue;
                }

                try
                {
                    return BiomeConfigLoader.Load(candidate);
                }
                catch
                {
                    // 单个候选解析失败不要中断整个生成流程，下个候选再试。
                }
            }

            return null;
        }
    }
}
