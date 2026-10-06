using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MyWorld.Core.Blocks;
using MyWorld.Core.Meshing;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;

namespace MyWorld.Preview
{
    /// <summary>
    /// 在没有 Unity 的情况下，用文本预览世界生成结果。这是本项目"用文本表达视觉"的主要工具。
    /// </summary>
    internal static class Program
    {
        /// <summary>植被方块 numericId → 剖面字形（m11 W1-3）。新树种/花草的 numericId 是自动分配的，
        /// 首次用到时从真实注册表建映射。字母区分树种：原木与树叶各一档，花草统一 ','。</summary>
        private static Dictionary<ushort, char> _vegetationGlyphs;

        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            int seed = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 20260806;
            var generator = new WorldGenerator(seed);

            // m11 W1-3：剖面里要能看到树种/花草，先建植被字形表（缺文件时保持旧字形集）
            try
            {
                _vegetationGlyphs = BuildVegetationGlyphs(LoadBlockRegistry(), LoadVegetationTable());
            }
            catch
            {
                _vegetationGlyphs = null;
            }

            Console.WriteLine($"种子: {seed}");
            Console.WriteLine();

            PrintCrossSection(generator);
            Console.WriteLine();
            PrintHeightMap(generator);
            Console.WriteLine();
            PrintOreStats(generator);
            Console.WriteLine();
            PrintMeshStats(generator);
            Console.WriteLine();
            PrintVegetationStats(generator);
            Console.WriteLine();
            PrintVillageStats(generator, seed);
        }

        /// <summary>纵向剖面：直观展示地表起伏、土层厚度、水面与地下矿层（m10 起下探到 y=-24）。</summary>
        private static void PrintCrossSection(WorldGenerator generator)
        {
            const int width = 110;
            const int top = 100;
            const int bottom = -24;

            Console.WriteLine($"── 纵向剖面 (z=0, x=0..{width - 1}, y={bottom}..{top}) ──");

            var world = new World();
            LoadChunksForRow(generator, world, width);

            var line = new StringBuilder(width);
            for (int y = top; y >= bottom; y--)
            {
                line.Clear();
                for (var x = 0; x < width; x++)
                {
                    line.Append(Glyph(world.GetBlock(x, y, 0)));
                }

                Console.WriteLine($"{y,4} |{line}");
            }

            Console.WriteLine("     +" + new string('-', width));
            Console.WriteLine("     图例: '\"'草 '.'土 '#'石 ':'沙 '~'水 '_'基岩 '*'雪 ' '空气");
            Console.WriteLine("     矿石: '$'金 '%'粗铁 '&'合金 '@'机元 'D'钻石（地层：粗铁<48 金<32 合金<24 机元/钻石<16）");
            Console.WriteLine("     植被: 原木 i橡 I桦 j松 J雪松 K丛林 S红杉 c樱 | 树叶 o橡 O桦 p松 P雪松 Q丛林 Z红杉 C樱 U灌木 | ','花草");
        }

        /// <summary>地下矿层统计（m10）：四矿在石层中的实测占比（含洞穴挖掉的部分），供调稀有度参数时对比。</summary>
        private static void PrintOreStats(WorldGenerator generator)
        {
            const int chunkRadius = 2;   // 5×5 区块，y ∈ (MinY, 48) 的石层

            long stone = 0;
            long gold = 0, iron = 0, alloy = 0, essence = 0, diamond = 0;
            for (var chunkX = -chunkRadius; chunkX <= chunkRadius; chunkX++)
            {
                for (var chunkZ = -chunkRadius; chunkZ <= chunkRadius; chunkZ++)
                {
                    ChunkColumn column = generator.Generate(new ChunkPos(chunkX, chunkZ));
                    for (int y = VoxelCoords.MinY + 1; y < OreFeature.RawIronMaxY; y++)
                    for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
                    for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
                    {
                        switch (column.GetBlock(lx, y, lz))
                        {
                            case BlockIds.Stone: stone++; break;
                            case BlockIds.GoldOre: gold++; break;
                            case BlockIds.RawIronOre: iron++; break;
                            case BlockIds.SummerAlloyOre: alloy++; break;
                            case BlockIds.MachineEssenceOre: essence++; break;
                            case BlockIds.DiamondOre: diamond++; break;
                        }
                    }
                }
            }

            long solid = stone + gold + iron + alloy + essence + diamond;
            Console.WriteLine($"── 地下矿层统计 ({(chunkRadius * 2 + 1) * (chunkRadius * 2 + 1)} 区块, y∈({VoxelCoords.MinY}, {OreFeature.RawIronMaxY}) 石层) ──");
            Console.WriteLine($"     石头: {stone}");
            Console.WriteLine($"     粗铁: {iron,7}  占石层 {Pct(iron, solid)}");
            Console.WriteLine($"     金:   {gold,7}  占石层 {Pct(gold, solid)}");
            Console.WriteLine($"     合金: {alloy,7}  占石层 {Pct(alloy, solid)}");
            Console.WriteLine($"     机元: {essence,7}  占石层 {Pct(essence, solid)}");
            Console.WriteLine($"     钻石: {diamond,7}  占石层 {Pct(diamond, solid)}");
        }

        private static string Pct(long part, long total) =>
            total > 0 ? $"{part * 100.0 / total:F2}%" : "n/a";


        /// <summary>俯视高度图：用字符深浅表达海拔，检查大尺度地形是否自然。</summary>
        private static void PrintHeightMap(WorldGenerator generator)
        {
            const int size = 60;
            const string ramp = " .:-=+*#%@";

            Console.WriteLine($"── 俯视高度图 ({size}×{size} 方块, 每字符 4 方块) ──");

            var line = new StringBuilder(size);
            for (var z = 0; z < size; z++)
            {
                line.Clear();
                for (var x = 0; x < size; x++)
                {
                    int height = generator.SurfaceHeightAt(x * 4, z * 4);
                    int level = Math.Clamp((height - 40) * ramp.Length / 60, 0, ramp.Length - 1);
                    line.Append(ramp[level]);
                }

                Console.WriteLine("     " + line);
            }
        }

        /// <summary>用真实区块数据验证贪心合并的压缩效果。</summary>
        private static void PrintMeshStats(WorldGenerator generator)
        {
            // 选取跨越地表的那一段，避免全实心或全空气的段落给出失真的压缩比
            int surface = generator.SurfaceHeightAt(8, 8);
            int sectionIndex = VoxelCoords.SectionIndexForY(surface);
            int sectionBaseY = VoxelCoords.MinY + sectionIndex * ChunkSection.Size;

            Console.WriteLine($"── 贪心网格压缩效果 (区块 0,0, 地表段 y={sectionBaseY}..{sectionBaseY + 15}) ──");

            // 连同相邻区块一起装入，使接缝处的面剔除与实际渲染一致
            var world = new World();
            for (var chunkX = -1; chunkX <= 1; chunkX++)
            {
                for (var chunkZ = -1; chunkZ <= 1; chunkZ++)
                {
                    LoadChunkInto(generator, world, new ChunkPos(chunkX, chunkZ));
                }
            }

            BlockRegistry registry = LoadBlockRegistry();
            var source = new ChunkMeshSource(world, registry, new ChunkPos(0, 0), sectionBaseY);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            int naiveQuads = CountNaiveQuads(source);
            Console.WriteLine($"     朴素逐面: {naiveQuads} 个四边形");
            Console.WriteLine($"     贪心合并: {mesh.QuadCount} 个四边形");

            if (mesh.QuadCount > 0)
            {
                Console.WriteLine($"     压缩比:   {naiveQuads / (float)mesh.QuadCount:F2}×");
            }
        }

        private static int CountNaiveQuads(ChunkMeshSource source)
        {
            var count = 0;
            for (var y = 0; y < ChunkSection.Size; y++)
            for (var z = 0; z < ChunkSection.Size; z++)
            for (var x = 0; x < ChunkSection.Size; x++)
            {
                if (!source.IsSolid(source.GetBlock(x, y, z)))
                {
                    continue;
                }

                if (!source.IsSolid(source.GetBlock(x + 1, y, z))) count++;
                if (!source.IsSolid(source.GetBlock(x - 1, y, z))) count++;
                if (!source.IsSolid(source.GetBlock(x, y + 1, z))) count++;
                if (!source.IsSolid(source.GetBlock(x, y - 1, z))) count++;
                if (!source.IsSolid(source.GetBlock(x, y, z + 1))) count++;
                if (!source.IsSolid(source.GetBlock(x, y, z - 1))) count++;
            }

            return count;
        }

        private static void LoadChunksForRow(WorldGenerator generator, World world, int width)
        {
            int chunkCount = width / VoxelCoords.ChunkSize + 1;
            for (var chunkX = 0; chunkX < chunkCount; chunkX++)
            {
                LoadChunkInto(generator, world, new ChunkPos(chunkX, 0));
            }
        }

        private static void LoadChunkInto(WorldGenerator generator, World world, ChunkPos pos)
        {
            ChunkColumn column = generator.Generate(pos);
            int originX = pos.X * VoxelCoords.ChunkSize;
            int originZ = pos.Z * VoxelCoords.ChunkSize;

            for (int y = VoxelCoords.MinY; y < VoxelCoords.MaxY; y++)
            {
                for (var localZ = 0; localZ < VoxelCoords.ChunkSize; localZ++)
                {
                    for (var localX = 0; localX < VoxelCoords.ChunkSize; localX++)
                    {
                        ushort block = column.GetBlock(localX, y, localZ);
                        if (block != BlockIds.Air)
                        {
                            world.SetBlock(originX + localX, y, originZ + localZ, block);
                        }
                    }
                }
            }
        }

        private static BlockRegistry LoadBlockRegistry()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "blocks");
                if (Directory.Exists(candidate))
                {
                    return BlockRegistry.FromJson(Directory.GetFiles(candidate, "*.json").Select(File.ReadAllText));
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("未找到 Assets/StreamingAssets/blocks 目录。");
        }

        /// <summary>
        /// 地表植被统计（m11 W1-3）：实测各树种树干数、花草株数与五群系的树种构成，
        /// 是调 trees.json/flowers.json 密度后的目检依据（对照 trees.json 的 biomes 声明）。
        /// </summary>
        private static void PrintVegetationStats(WorldGenerator generator)
        {
            const int chunkRadius = 16;   // 33×33 区块——覆盖多种群系（雪原等低温带离原点较远）
            const int scanBottom = 60;    // 植被只可能出现在海平面以上的地表带
            const int scanTop = 140;

            Console.WriteLine($"── 地表植被统计 ({chunkRadius * 2 + 1}×{chunkRadius * 2 + 1} 区块, y∈[{scanBottom},{scanTop})) ──");

            BlockRegistry registry = LoadBlockRegistry();
            VegetationTable table = LoadVegetationTable();

            // 树种 → 树干/树叶 numericId；花草条目 → numericId。
            // 注意 oak 与 bush 共用 log 方块——树干计数靠「树干顶上的叶冠」区分两者。
            var trunkIds = new Dictionary<ushort, List<string>>();
            var speciesLeaves = new Dictionary<string, ushort>();
            var leafIds = new HashSet<ushort>();
            foreach (TreeSpecies species in table.Trees)
            {
                ushort logId = registry.GetById(species.LogBlock).NumericId;
                if (!trunkIds.TryGetValue(logId, out var sharers))
                {
                    sharers = new List<string>();
                    trunkIds[logId] = sharers;
                }
                sharers.Add(species.Id);
                speciesLeaves[species.Id] = registry.GetById(species.LeavesBlock).NumericId;
                leafIds.Add(registry.GetById(species.LeavesBlock).NumericId);
            }
            var flowerIds = new Dictionary<ushort, string>();
            foreach (FlowerEntry flower in table.Flowers)
            {
                flowerIds[registry.GetById(flower.Block).NumericId] = flower.Id;
            }

            var trunks = new Dictionary<string, long>();
            long leafBlocks = 0;
            var flowers = new Dictionary<string, long>();
            var biomeTrees = new Dictionary<string, HashSet<string>>();
            var biomeFlowerCount = new Dictionary<string, long>();
            var biomeColumnCount = new Dictionary<string, long>();
            var biomeGrassColumns = new Dictionary<string, long>();
            var biomeSnowColumns = new Dictionary<string, long>();
            var biomeSandColumns = new Dictionary<string, long>();

            void TouchBiome(string biome)
            {
                if (!biomeTrees.ContainsKey(biome)) biomeTrees[biome] = new HashSet<string>();
                if (!biomeFlowerCount.ContainsKey(biome)) biomeFlowerCount[biome] = 0;
                if (!biomeColumnCount.ContainsKey(biome)) biomeColumnCount[biome] = 0;
                if (!biomeGrassColumns.ContainsKey(biome)) biomeGrassColumns[biome] = 0;
                if (!biomeSnowColumns.ContainsKey(biome)) biomeSnowColumns[biome] = 0;
                if (!biomeSandColumns.ContainsKey(biome)) biomeSandColumns[biome] = 0;
            }

            for (var chunkX = -chunkRadius; chunkX <= chunkRadius; chunkX++)
            {
                for (var chunkZ = -chunkRadius; chunkZ <= chunkRadius; chunkZ++)
                {
                    ChunkColumn column = generator.Generate(new ChunkPos(chunkX, chunkZ));
                    for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
                    for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
                    {
                        string biome = generator.BiomeAt(chunkX * VoxelCoords.ChunkSize + lx,
                            chunkZ * VoxelCoords.ChunkSize + lz).ToString();
                        TouchBiome(biome);
                        biomeColumnCount[biome]++;

                        // 该列地表方块类型（树/花草都只长在草上——雪原树种例外可长雪上）
                        for (int y = scanTop - 1; y >= scanBottom; y--)
                        {
                            ushort surface = column.GetBlock(lx, y, lz);
                            if (surface == BlockIds.Air || surface == BlockIds.Water) continue;
                            if (surface == BlockIds.Grass) biomeGrassColumns[biome]++;
                            else if (surface == BlockIds.Snow) biomeSnowColumns[biome]++;
                            else if (surface == BlockIds.Sand) biomeSandColumns[biome]++;
                            break;
                        }

                        for (int y = scanBottom; y < scanTop; y++)
                        {
                            ushort block = column.GetBlock(lx, y, lz);
                            if (!trunkIds.TryGetValue(block, out List<string> sharers2))
                            {
                                if (leafIds.Contains(block))
                                {
                                    leafBlocks++;
                                }
                                else if (flowerIds.TryGetValue(block, out string flowerId))
                                {
                                    flowers[flowerId] = flowers.TryGetValue(flowerId, out long m) ? m + 1 : 1;
                                    biomeFlowerCount[biome]++;
                                }
                                continue;
                            }

                            // 树干：顺着干往上找叶冠，用叶方块定树种（oak/bush 共用 log）
                            int top = y;
                            while (top + 1 < scanTop && column.GetBlock(lx, top + 1, lz) == block) top++;
                            ushort crown = top + 1 < scanTop ? column.GetBlock(lx, top + 1, lz) : BlockIds.Air;
                            string speciesId = null;
                            foreach (string candidate in sharers2)
                            {
                                if (speciesLeaves[candidate] == crown)
                                {
                                    speciesId = candidate;
                                    break;
                                }
                            }
                            speciesId = speciesId ?? sharers2[0];

                            trunks[speciesId] = trunks.TryGetValue(speciesId, out long n) ? n + 1 : 1;
                            biomeTrees[biome].Add(speciesId);
                            y = top;   // 干的其余段落不再重复计数
                        }
                    }
                }
            }

            Console.WriteLine("     树干: " + (trunks.Count == 0
                ? "无"
                : string.Join("  ", trunks.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}"))));
            Console.WriteLine($"     树叶: 共 {leafBlocks} 块");
            Console.WriteLine("     花草: " + (flowers.Count == 0
                ? "无"
                : $"共 {flowers.Values.Sum()} 株（" + string.Join(" / ",
                      flowers.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")) + "）"));
            foreach (string biome in new[] { "Plains", "Forest", "Mountains", "Snow", "Desert" })
            {
                if (!biomeTrees.TryGetValue(biome, out var speciesSet))
                {
                    continue;   // 该窗口内没有这个群系
                }
                string trees = speciesSet.Count == 0
                    ? "无树"
                    : string.Join("+", table.Trees.Where(s => speciesSet.Contains(s.Id)).Select(s => s.Id));
                Console.WriteLine(
                    $"     {biome,-10}: {trees}；花草 {biomeFlowerCount[biome]} 株" +
                    $"（列 {biomeColumnCount[biome]}，地表 草{biomeGrassColumns[biome]}/雪{biomeSnowColumns[biome]}/沙{biomeSandColumns[biome]}）");
            }
        }

        /// <summary>
        /// 村庄普查（m11 W3-2）：自适应扩张村格窗口直到命中村庄，列出各村的
        /// 中心/群系/楼数/井口高度与实测密度；再对首村做真实区块生成，
        /// 按楼足印与井 3×3 统计村庄材料块数——是村庄接线与密度的目检依据。
        /// </summary>
        private static void PrintVillageStats(WorldGenerator generator, int seed)
        {
            Console.WriteLine($"── 村庄统计 (村格 {VillageFeature.CellSize}×{VillageFeature.CellSize} 格, 出村率 {VillageFeature.PresencePercent}%, 窗口自适应 ±1..±4 村格) ──");

            var found = new List<VillagePlan>();
            int radius = 1;
            for (; radius <= 4; radius++)
            {
                found.Clear();
                for (int cellX = -radius; cellX <= radius; cellX++)
                {
                    for (int cellZ = -radius; cellZ <= radius; cellZ++)
                    {
                        VillagePlan plan = generator.TryPlanVillageInCell(cellX, cellZ);
                        if (plan != null) found.Add(plan);
                    }
                }
                if (found.Count > 0) break;
            }

            if (found.Count == 0)
            {
                Console.WriteLine("     命中 0 村（±4 村格 = 3600×3600 格内无村）——平原/森林占比与水/悬崖守卫叠加后属小概率，请人工复核");
                return;
            }

            double windowMillionBlocks = (2 * radius + 1) * (double)VillageFeature.CellSize
                                         * (2 * radius + 1) * VillageFeature.CellSize / 1e6;
            Console.WriteLine($"     命中 {found.Count} 村（窗口 {2 * radius + 1}×{2 * radius + 1} 村格 = {windowMillionBlocks:F1}M 格），"
                              + $"实测密度 ≈ {found.Count / windowMillionBlocks:F2} 村/百万格");
            foreach (VillagePlan plan in found)
            {
                Console.WriteLine($"     村 @ ({plan.CenterX,5}, {plan.CenterZ,5}) {generator.BiomeAt(plan.CenterX, plan.CenterZ),-7} "
                                  + $"{plan.Buildings.Length} 栋 井口 y={plan.WellY} 包围盒 [{plan.MinX}..{plan.MaxX}]×[{plan.MinZ}..{plan.MaxZ}]");
            }

            // 首村实检：真实生成覆盖区块，按楼足印与井 3×3 数材料块
            VillagePlan first = found[0];
            var world = new World();
            for (int chunkX = VoxelCoords.WorldToChunk(first.MinX); chunkX <= VoxelCoords.WorldToChunk(first.MaxX); chunkX++)
            {
                for (int chunkZ = VoxelCoords.WorldToChunk(first.MinZ); chunkZ <= VoxelCoords.WorldToChunk(first.MaxZ); chunkZ++)
                {
                    LoadChunkInto(generator, world, new ChunkPos(chunkX, chunkZ));
                }
            }

            long planks = 0, glass = 0, logs = 0;
            foreach (VillageBuilding building in first.Buildings)
            {
                for (int dx = 0; dx < 5; dx++)
                for (int dz = 0; dz < 5; dz++)
                for (int y = building.BaseY; y <= building.BaseY + 4; y++)
                {
                    ushort id = world.GetBlock(building.OriginX + dx, y, building.OriginZ + dz);
                    if (id == VillageFeature.PlanksId) planks++;
                    else if (id == VillageFeature.GlassId) glass++;
                    else if (id == TreeFeature.LogId) logs++;
                }
            }

            long wellStone = 0, wellWater = 0;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            for (int y = first.WellY - 3; y <= first.WellY + 1; y++)
            {
                ushort id = world.GetBlock(first.CenterX + dx, y, first.CenterZ + dz);
                if (id == BlockIds.Stone) wellStone++;
                else if (id == BlockIds.Water) wellWater++;
            }

            Console.WriteLine($"     首村实检 @ ({first.CenterX}, {first.CenterZ})："
                              + $"木板 {planks} / 玻璃 {glass} / 原木 {logs} / 井圈石 {wellStone} / 井水 {wellWater}");
        }

        /// <summary>从真实注册表 + 植被表建「numericId → 剖面字形」映射（见 Main 里的字形说明）。</summary>
        private static Dictionary<ushort, char> BuildVegetationGlyphs(BlockRegistry registry, VegetationTable table)
        {
            var logGlyphs = new Dictionary<string, char>
            {
                ["oak"] = 'i', ["birch"] = 'I', ["pine"] = 'j', ["cedar"] = 'J',
                ["jungle"] = 'K', ["sequoia"] = 'S', ["cherry"] = 'c',
            };
            var leafGlyphs = new Dictionary<string, char>
            {
                ["oak"] = 'o', ["birch"] = 'O', ["pine"] = 'p', ["cedar"] = 'P',
                ["jungle"] = 'Q', ["sequoia"] = 'Z', ["cherry"] = 'C', ["bush"] = 'U',
            };
            var map = new Dictionary<ushort, char>();
            foreach (TreeSpecies species in table.Trees)
            {
                if (logGlyphs.TryGetValue(species.Id, out char logGlyph))
                {
                    map[registry.GetById(species.LogBlock).NumericId] = logGlyph;
                }
                if (leafGlyphs.TryGetValue(species.Id, out char leafGlyph))
                {
                    map[registry.GetById(species.LeavesBlock).NumericId] = leafGlyph;
                }
            }
            foreach (FlowerEntry flower in table.Flowers)
            {
                map[registry.GetById(flower.Block).NumericId] = ',';
            }
            return map;
        }

        private static VegetationTable LoadVegetationTable()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "vegetation");
                if (Directory.Exists(candidate))
                {
                    return VegetationTable.Load(
                        File.ReadAllText(Path.Combine(candidate, "trees.json")),
                        File.ReadAllText(Path.Combine(candidate, "flowers.json")));
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("未找到 Assets/StreamingAssets/vegetation 目录。");
        }

        private static char Glyph(ushort blockId)
        {
            switch (blockId)
            {
                case BlockIds.Grass: return '"';
                case BlockIds.Dirt: return '.';
                case BlockIds.Stone: return '#';
                case BlockIds.Sand: return ':';
                case BlockIds.Water: return '~';
                case BlockIds.Bedrock: return '_';
                case BlockIds.Snow: return '*';
                case BlockIds.GoldOre: return '$';
                case BlockIds.RawIronOre: return '%';
                case BlockIds.SummerAlloyOre: return '&';
                case BlockIds.MachineEssenceOre: return '@';
                case BlockIds.DiamondOre: return 'D';
                default:
                    var vegetation = _vegetationGlyphs;
                    return vegetation != null && vegetation.TryGetValue(blockId, out char glyph) ? glyph : ' ';
            }
        }
    }
}
