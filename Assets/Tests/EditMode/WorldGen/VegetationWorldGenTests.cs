using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    /// <summary>
    /// m11 W1-3（植被）：8 树种 + 12 花草方块注册与投放的守卫测试。
    /// 覆盖四件事：
    ///   1. vegetation/*.json 引用的方块名全部在真实 blocks 注册表可解析（I2 留下的闭环口）；
    ///   2. FlowerFeature 散布纯函数：确定性、每区块期望株数语义、与树通道独立；
    ///   3. WorldGenerator 端到端：花草只长在草方块上方、植被确定、
    ///      新树种只在 trees.json 声明的群系出现（五群系树种差异）；
    ///   4. oak 走旧路径：橡树树干位置仍由旧密度通道判定（行为逐格不变铁律）。
    /// </summary>
    [TestFixture]
    public class VegetationWorldGenTests
    {
        private const int Seed = 20260806;

        private static string LocateStreamingAssets()
        {
#if UNITY_EDITOR
            return UnityEngine.Application.streamingAssetsPath;
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("未能从测试输出目录向上找到 Assets/StreamingAssets。");
#endif
        }

        private static BlockRegistry LoadRealRegistry()
        {
            string blocksDir = Path.Combine(LocateStreamingAssets(), "blocks");
            return BlockRegistry.FromJson(Directory.GetFiles(blocksDir, "*.json").Select(File.ReadAllText));
        }

        private static VegetationTable LoadRealTable()
        {
            string vegetationDir = Path.Combine(LocateStreamingAssets(), "vegetation");
            return VegetationTable.Load(
                File.ReadAllText(Path.Combine(vegetationDir, "trees.json")),
                File.ReadAllText(Path.Combine(vegetationDir, "flowers.json")));
        }

        /// <summary>vegetation 两份 JSON 引用的全部方块名（log/leaves/花草块）都必须在真实注册表里。</summary>
        [Test]
        public void 植被表引用的方块_全部在真实注册表可解析()
        {
            var registry = LoadRealRegistry();
            var table = LoadRealTable();

            foreach (TreeSpecies species in table.Trees)
            {
                Assert.That(() => registry.GetById(species.LogBlock), Throws.Nothing,
                    $"树种 {species.Id} 的 logBlock {species.LogBlock} 未在 blocks/*.json 注册");
                Assert.That(() => registry.GetById(species.LeavesBlock), Throws.Nothing,
                    $"树种 {species.Id} 的 leavesBlock {species.LeavesBlock} 未在 blocks/*.json 注册");
            }

            foreach (FlowerEntry flower in table.Flowers)
            {
                Assert.That(() => registry.GetById(flower.Block), Throws.Nothing,
                    $"花草 {flower.Id} 的 block {flower.Block} 未在 blocks/*.json 注册");
            }
        }

        /// <summary>花草散布是纯函数：同 (worldX, worldZ, entry, seed) 反复调用结果一致。</summary>
        [Test]
        public void 花草散布_同seed同位判定确定()
        {
            var table = LoadRealTable();
            Assert.That(table.TryGetFlower("poppy", out var poppy), Is.True);

            for (int x = -50; x < 50; x++)
            for (int z = -50; z < 50; z++)
            {
                bool first = FlowerFeature.ShouldPlaceFlower(x, z, poppy, Seed);
                bool second = FlowerFeature.ShouldPlaceFlower(x, z, poppy, Seed);
                Assert.That(first, Is.EqualTo(second), $"花草判定不确定：({x},{z}) 两次结果不同");
            }
        }

        /// <summary>densityPerChunk 语义 = 每区块（16×16 格）期望株数：大样本下命中率 ≈ density/256。</summary>
        [Test]
        public void 花草散布_密度语义为每区块期望株数()
        {
            var table = LoadRealTable();
            Assert.That(table.TryGetFlower("poppy", out var poppy), Is.True);
            Assert.That(table.TryGetFlower("dandelion", out var dandelion), Is.True);

            // 256 个区块的格数（65536 列）作为样本
            const int columns = 256 * VoxelCoords.ChunkSize * VoxelCoords.ChunkSize;

            foreach ((FlowerEntry entry, int density) in new[] { (poppy, 4), (dandelion, 6) })
            {
                long hits = 0;
                for (int i = 0; i < columns; i++)
                {
                    if (FlowerFeature.ShouldPlaceFlower(i & 1023, i >> 10, entry, Seed)) hits++;
                }

                double perChunk = hits / 256.0;
                Assert.That(perChunk, Is.InRange(density * 0.6, density * 1.4),
                    $"花草 {entry.Id} 的 densityPerChunk={density}，实测每区块 {perChunk:F1} 株（允许 ±40%）");
            }
        }

        /// <summary>密度 0 的条目任何位置都不放；entry 为 null 也不放。</summary>
        [Test]
        public void 花草散布_密度0或空条目不放置()
        {
            var zero = new FlowerEntry { Id = "zero", Block = "flower_poppy", Biomes = new[] { "Plains" }, DensityPerChunk = 0 };
            for (int x = 0; x < 64; x++)
            for (int z = 0; z < 64; z++)
            {
                Assert.That(FlowerFeature.ShouldPlaceFlower(x, z, zero, Seed), Is.False,
                    $"densityPerChunk=0 的条目在 ({x},{z}) 不应放置");
            }
            Assert.That(FlowerFeature.ShouldPlaceFlower(3, 5, null, Seed), Is.False, "null 条目不应放置");
        }

        /// <summary>花草哈希通道独立于树：既有花草命中而树不命中的格子，也有反例（两条通道互不覆盖）。</summary>
        [Test]
        public void 花草散布_通道独立于树判定通道()
        {
            var table = LoadRealTable();
            Assert.That(table.TryGetFlower("poppy", out var poppy), Is.True);
            Assert.That(table.TryGetFlower("daisy", out var daisy), Is.True);

            var forest = new BiomeConfig { Id = (int)Biome.Forest, Name = "Forest", TreeDensity = 30 };

            int flowerOnly = 0, treeOnly = 0, both = 0;
            for (int x = 0; x < 120; x++)
            for (int z = 0; z < 120; z++)
            {
                bool flower = FlowerFeature.ShouldPlaceFlower(x, z, poppy, Seed);
                bool tree = TreeFeature.ShouldPlaceTree(x, z, "oak", forest, Seed);
                if (flower && !tree) flowerOnly++;
                if (!flower && tree) treeOnly++;
                if (flower && tree) both++;
            }
            Assert.That(flowerOnly, Is.GreaterThan(0), "应存在花草命中而树不命中的格子（通道被树覆盖了）");
            Assert.That(treeOnly, Is.GreaterThan(0), "应存在树命中而花草不命中的格子");
            Assert.That(both, Is.GreaterThan(0), "两条独立通道在 120×120 内应有共同命中");

            // 不同花草条目之间也互相独立（不同条目不会永远挤在同一批格子上）
            int differ = 0;
            for (int x = 0; x < 200; x++)
            for (int z = 0; z < 200; z++)
            {
                if (FlowerFeature.ShouldPlaceFlower(x, z, poppy, Seed)
                    != FlowerFeature.ShouldPlaceFlower(x, z, daisy, Seed)) differ++;
            }
            Assert.That(differ, Is.GreaterThan(0), "poppy 与 daisy 的散布通道必须独立");
        }

        /// <summary>用真实注册表 + 植被表构造生成器（双链一致：EditMode 下 Core 自动加载走不到
        /// 工程目录，测试显式注入，与集成点②的 Unity 接线同款入口）。</summary>
        private static WorldGenerator BuildGenerator(int seed, IList<BiomeConfig> biomeConfigs = null)
        {
            return new WorldGenerator(seed, biomeConfigs, LoadRealTable(), LoadRealRegistry());
        }

        /// <summary>密度统一拉到 200（每列 4%）的群系配置——测试验接线与群系门槛，不是平衡数值。</summary>
        private static List<BiomeConfig> BoostedConfigs()
        {
            return new List<BiomeConfig>
            {
                new BiomeConfig { Id = (int)Biome.Plains,    Name = "Plains",    TreeDensity = 200, CaveMultiplier = 0f },
                new BiomeConfig { Id = (int)Biome.Desert,    Name = "Desert",    TreeDensity = 0,   CaveMultiplier = 0f },
                new BiomeConfig { Id = (int)Biome.Forest,    Name = "Forest",    TreeDensity = 200, CaveMultiplier = 0f },
                new BiomeConfig { Id = (int)Biome.Mountains, Name = "Mountains", TreeDensity = 200, CaveMultiplier = 0f },
                new BiomeConfig { Id = (int)Biome.Snow,      Name = "Snow",      TreeDensity = 200, CaveMultiplier = 0f },
            };
        }

        /// <summary>
        /// 用纯函数 BiomeAt/SurfaceHeightAt 做环带扩张扫描，确定性找到指定群系中
        /// 「地表高出海平面 ≥2 格」的采样列（草地/雪地确定可长树）。同 seed 恒定，不依赖运气。
        /// </summary>
        private static List<(int x, int z)> FindBiomeSamples(WorldGenerator generator, Biome biome, int target)
        {
            var samples = new List<(int x, int z)>();
            for (int ring = 0; ring < 60 && samples.Count < target; ring++)
            {
                int lo = -8 - ring * 32, hi = 8 + ring * 32;
                for (int x = lo; x <= hi && samples.Count < target; x += 4)
                for (int z = lo; z <= hi && samples.Count < target; z += 4)
                {
                    // 只扫环带边框，避免重复扫描内圈（注意别用 Math.Abs——会撞上 Core 的 Math 命名空间）
                    if (x != lo && x != hi && z != lo && z != hi) continue;
                    if (generator.BiomeAt(x, z) != biome) continue;
                    if (generator.SurfaceHeightAt(x, z) < WorldGenerator.SeaLevel + 2) continue;
                    samples.Add((x, z));
                }
            }
            return samples;
        }

        /// <summary>采样列 → 去重后的区块集合（对它们做真实生成）。</summary>
        private static HashSet<ChunkPos> ChunksOf(IEnumerable<(int x, int z)> samples)
        {
            var chunks = new HashSet<ChunkPos>();
            foreach ((int x, int z) in samples)
            {
                chunks.Add(new ChunkPos(VoxelCoords.WorldToChunk(x), VoxelCoords.WorldToChunk(z)));
            }
            return chunks;
        }

        /// <summary>端到端：生成的世界里花草只出现在草方块正上方（沙/雪/水下不长，树干/树叶占位处不长）。</summary>
        [Test]
        public void 生成_花草只长在草方块上方()
        {
            var registry = LoadRealRegistry();
            var table = LoadRealTable();
            var flowerIds = new HashSet<ushort>(table.Flowers.Select(f => registry.GetById(f.Block).NumericId));

            var generator = BuildGenerator(Seed);
            int totalFlowers = 0;

            for (var chunkX = 0; chunkX < 8; chunkX++)
            for (var chunkZ = 0; chunkZ < 8; chunkZ++)
            {
                ChunkColumn column = generator.Generate(new ChunkPos(chunkX, chunkZ));
                for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
                for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
                for (int y = 60; y < 140; y++)   // 植被只可能出现在海平面以上的地表带
                {
                    if (!flowerIds.Contains(column.GetBlock(lx, y, lz))) continue;
                    totalFlowers++;
                    Assert.That(column.GetBlock(lx, y - 1, lz), Is.EqualTo(BlockIds.Grass),
                        $"({chunkX * 16 + lx},{y},{chunkZ * 16 + lz}) 的花草下方不是草方块——花草只能长在草方块上方");
                }
            }
            Assert.That(totalFlowers, Is.GreaterThan(0),
                "8×8 区块内应至少长出一株花草（Plains/Forest 草地上 densityPerChunk 2-8）");
        }

        /// <summary>端到端：同 seed 两次生成的花草位置逐格一致（植被确定性）。</summary>
        [Test]
        public void 生成_同seed两次生成的花草逐格一致()
        {
            var registry = LoadRealRegistry();
            var table = LoadRealTable();
            var flowerIds = new HashSet<ushort>(table.Flowers.Select(f => registry.GetById(f.Block).NumericId));

            List<(int x, int y, int z, ushort id)> Collect(WorldGenerator generator)
            {
                var found = new List<(int, int, int, ushort)>();
                for (var chunkX = 0; chunkX < 6; chunkX++)
                for (var chunkZ = 0; chunkZ < 6; chunkZ++)
                {
                    ChunkColumn column = generator.Generate(new ChunkPos(chunkX, chunkZ));
                    for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
                    for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
                    for (int y = 60; y < 140; y++)   // 植被只可能出现在海平面以上的地表带
                    {
                        if (flowerIds.Contains(column.GetBlock(lx, y, lz)))
                        {
                            found.Add((chunkX * 16 + lx, y, chunkZ * 16 + lz, column.GetBlock(lx, y, lz)));
                        }
                    }
                }
                return found;
            }

            var first = Collect(BuildGenerator(Seed));
            var second = Collect(BuildGenerator(Seed));
            Assert.That(first, Is.EqualTo(second), "两次生成的花草集合应完全一致");
            Assert.That(first.Count, Is.GreaterThan(0), "6×6 区块内应能采到花草样本");
        }

        /// <summary>
        /// 五群系树种差异：新树种（非 oak）的原木只出现在 trees.json 声明的群系；
        /// jungle 是占位树种，任何位置都不投放。oak 走旧路径（不限声明群系），单独守护。
        /// 密度统一拉到 200（每列 4%）保证各群系必有命中——这里验的是接线与群系门槛，不是平衡数值。
        /// </summary>
        [Test]
        public void 生成_五群系树种差异_新树种只在声明群系出现()
        {
            var registry = LoadRealRegistry();
            var table = LoadRealTable();

            // 新树种 → (logId, 允许群系)。注意 bush 与 oak 共用 log，log 只认 oak/bush 之外的新树种。
            var speciesByLog = new Dictionary<ushort, (TreeSpecies species, HashSet<string> biomes)>();
            foreach (TreeSpecies species in table.Trees)
            {
                if (species.Id == "oak" || species.Id == "bush") continue;   // 共用 log，另行守护
                speciesByLog[registry.GetById(species.LogBlock).NumericId] =
                    (species, new HashSet<string>(species.Biomes));
            }

            var boosted = BoostedConfigs();
            var generator = BuildGenerator(Seed, boosted);

            var speciesColumns = new Dictionary<string, HashSet<Biome>>();
            foreach (TreeSpecies species in table.Trees) speciesColumns[species.Id] = new HashSet<Biome>();

            const int radius = 12;   // 25×25 = 625 区块的窗口，覆盖多种群系
            for (var chunkX = -radius; chunkX <= radius; chunkX++)
            for (var chunkZ = -radius; chunkZ <= radius; chunkZ++)
            {
                ChunkColumn column = generator.Generate(new ChunkPos(chunkX, chunkZ));
                for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
                for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
                {
                    int worldX = chunkX * VoxelCoords.ChunkSize + lx;
                    int worldZ = chunkZ * VoxelCoords.ChunkSize + lz;
                    Biome biome = generator.BiomeAt(worldX, worldZ);
                    string biomeName = biome.ToString();

                    // 植被只可能出现在海平面以上的地表带（地表≤海平面是沙/水，长不了树）
                    for (int y = 60; y < 140; y++)
                    {
                        ushort block = column.GetBlock(lx, y, lz);
                        if (speciesByLog.TryGetValue(block, out var pair))
                        {
                            Assert.That(pair.biomes.Contains(biomeName), Is.True,
                                $"{pair.species.Id} 的原木出现在了未声明的群系 {biomeName} ({worldX},{y},{worldZ})——" +
                                "trees.json 的 biomes 字段与实际投放不一致");
                            speciesColumns[pair.species.Id].Add(biome);
                        }
                    }
                }
            }

            // 关键差异断言：Mountains 有 sequoia/pine、Plains 有 cherry。
            // Snow 的 pine/cedar 与 Forest 的 birch/bush 由「生成_雪原…/生成_森林…」两个
            // 环带定位测试覆盖——原点窗口内的雪原/森林区多在海平面以下，不适合做存在性断言。
            Assert.That(speciesColumns["sequoia"], Does.Contain(Biome.Mountains), "山地应出现红杉 sequoia");
            Assert.That(speciesColumns["pine"], Does.Contain(Biome.Mountains), "山地应出现松树 pine");
            Assert.That(speciesColumns["cherry"], Does.Contain(Biome.Plains), "平原应出现樱花 cherry");

            // 雪原绝不该有白桦/樱花/红杉/丛林（biomes 声明不含 Snow 的树种在 Snow 列不出现已由上面循环守住，
            // 这里再显式点名雪原的差异形状，防将来把 biomes 改糊）
            foreach (string forbidden in new[] { "birch", "cherry", "sequoia", "jungle" })
            {
                Assert.That(speciesColumns[forbidden], Does.Not.Contain(Biome.Snow),
                    $"雪原不应出现 {forbidden}");
            }

            // jungle 是占位树种（biomes 为空）：任何位置都不能投放
            Assert.That(speciesColumns["jungle"], Is.Empty,
                "jungle 的 biomes 为空数组（占位），不应在任何群系投放");
        }

        /// <summary>
        /// 森林树种：birch/bush 投放在 Forest 群系。默认种子原点附近的 Forest 区是深海（无草地），
        /// 所以用 <see cref="FindBiomeSamples"/> 在大范围内确定性定位有草地的 Forest 采样列再断言。
        /// </summary>
        [Test]
        public void 生成_森林草地上有birch与bush()
        {
            var registry = LoadRealRegistry();
            var table = LoadRealTable();
            Assert.That(table.TryGetTree("birch", out var birch), Is.True);
            Assert.That(table.TryGetTree("bush", out var bush), Is.True);
            ushort birchLogId = registry.GetById(birch.LogBlock).NumericId;
            ushort bushLeavesId = registry.GetById(bush.LeavesBlock).NumericId;

            var generator = BuildGenerator(Seed, BoostedConfigs());

            var sampleColumns = FindBiomeSamples(generator, Biome.Forest, target: 300);
            Assert.That(sampleColumns.Count, Is.GreaterThanOrEqualTo(30),
                "大范围环带扫描应能找到一批有草地的 Forest 采样列");

            int birchTrunks = 0, bushCrowns = 0;
            foreach (ChunkPos pos in ChunksOf(sampleColumns))
            {
                ChunkColumn column = generator.Generate(pos);
                for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
                for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
                for (int y = 60; y < 140; y++)
                {
                    if (column.GetBlock(lx, y, lz) == birchLogId) birchTrunks++;
                    if (column.GetBlock(lx, y, lz) == bushLeavesId) bushCrowns++;
                }
            }
            Assert.That(birchTrunks, Is.GreaterThan(0),
                "Forest 草地采样列附近应出现白桦原木（密度已拉满 200）");
            Assert.That(bushCrowns, Is.GreaterThan(0),
                "Forest 草地采样列附近应出现灌木叶冠（bush 树干借用内置 log，以叶冠辨认）");
        }

        /// <summary>
        /// 雪原树种：pine/cedar 长在雪方块上（Snow 群系地表是雪不是草——不放开雪地基
        /// 这两个树种在声明群系永远出不来）。默认种子雪原露出海面的部分离原点较远，环带定位。
        /// </summary>
        [Test]
        public void 生成_雪原雪地上有pine与cedar()
        {
            var registry = LoadRealRegistry();
            var table = LoadRealTable();
            Assert.That(table.TryGetTree("pine", out var pine), Is.True);
            Assert.That(table.TryGetTree("cedar", out var cedar), Is.True);
            ushort pineLogId = registry.GetById(pine.LogBlock).NumericId;
            ushort cedarLogId = registry.GetById(cedar.LogBlock).NumericId;

            var generator = BuildGenerator(Seed, BoostedConfigs());

            var sampleColumns = FindBiomeSamples(generator, Biome.Snow, target: 300);
            Assert.That(sampleColumns.Count, Is.GreaterThanOrEqualTo(30),
                "大范围环带扫描应能找到一批露出海面的 Snow 采样列（地表为雪方块）");

            int pineTrunks = 0, cedarTrunks = 0, snowGroundColumns = 0;
            foreach (ChunkPos pos in ChunksOf(sampleColumns))
            {
                ChunkColumn column = generator.Generate(pos);
                for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
                for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
                for (int y = 60; y < 140; y++)
                {
                    ushort block = column.GetBlock(lx, y, lz);
                    if (block == pineLogId) pineTrunks++;
                    if (block == cedarLogId) cedarTrunks++;
                    if (block == BlockIds.Snow && column.GetBlock(lx, y + 1, lz) == BlockIds.Air
                        && generator.BiomeAt(pos.X * 16 + lx, pos.Z * 16 + lz) == Biome.Snow)
                    {
                        snowGroundColumns++;
                    }
                }
            }
            Assert.That(snowGroundColumns, Is.GreaterThan(0), "采样区块内应存在雪面敞开的雪原列（可长树的地面）");
            Assert.That(pineTrunks, Is.GreaterThan(0), "雪原雪地上应出现松树原木 pine_log（密度已拉满 200）");
            Assert.That(cedarTrunks, Is.GreaterThan(0), "雪原雪地上应出现雪松原木 cedar_log（密度已拉满 200）");
        }

        /// <summary>
        /// oak 行为不变铁律：世界里每根橡树原木所在列，旧密度通道必须曾经放行——
        /// 即 oak 的放置判定仍是 <see cref="TreeFeature.ShouldPlaceTree(int, int, BiomeConfig, int)"/> 旧通道，
        /// 不因接入植被表而漂移。
        /// </summary>
        [Test]
        public void 生成_oak树干位置仍由旧密度通道判定()
        {
            var registry = LoadRealRegistry();
            var table = LoadRealTable();
            Assert.That(table.TryGetTree("bush", out var bush), Is.True);
            ushort bushLeavesId = registry.GetById(bush.LeavesBlock).NumericId;

            var generator = new WorldGenerator(Seed);
            int checkedTrunks = 0;

            for (var chunkX = -4; chunkX <= 4; chunkX++)
            for (var chunkZ = -4; chunkZ <= 4; chunkZ++)
            {
                ChunkColumn column = generator.Generate(new ChunkPos(chunkX, chunkZ));
                for (var lx = 0; lx < VoxelCoords.ChunkSize; lx++)
                for (var lz = 0; lz < VoxelCoords.ChunkSize; lz++)
                {
                    int worldX = chunkX * VoxelCoords.ChunkSize + lx;
                    int worldZ = chunkZ * VoxelCoords.ChunkSize + lz;
                    Biome biome = generator.BiomeAt(worldX, worldZ);

                    // 从上往下找「草方块之上紧挨 log」的树干起点（植被带在海平面以上）。
                    // bush 的树干也是 log（trees.json 借用内置原木），靠树干顶上的叶冠区分：
                    // 灌木树干 1-2 高、顶上是 bush_leaves，不计入 oak 守护。
                    for (int y = 60; y < 139; y++)
                    {
                        if (column.GetBlock(lx, y, lz) == BlockIds.Grass
                            && column.GetBlock(lx, y + 1, lz) == TreeFeature.LogId)
                        {
                            int top = y + 1;
                            while (top + 1 < VoxelCoords.MaxY && column.GetBlock(lx, top + 1, lz) == TreeFeature.LogId)
                            {
                                top++;
                            }
                            if (column.GetBlock(lx, top + 1, lz) == bushLeavesId) continue;   // 灌木不是橡树

                            checkedTrunks++;
                            var config = new BiomeConfig { TreeDensity = TreeDensityOf(biome) };
                            Assert.That(TreeFeature.ShouldPlaceTree(worldX, worldZ, config, Seed), Is.True,
                                $"({worldX},{worldZ}) 长了橡树但旧密度通道未放行——oak 生成路径被植被表改动了");
                        }
                    }
                }
            }
            Assert.That(checkedTrunks, Is.GreaterThan(0), "9×9 区块内应至少有一棵橡树用于守护");
        }

        private static int TreeDensityOf(Biome biome)
        {
            switch (biome)
            {
                case Biome.Plains: return 8;
                case Biome.Forest: return 30;
                case Biome.Mountains: return 2;
                case Biome.Snow: return 1;
                default: return 0;
            }
        }
    }
}
