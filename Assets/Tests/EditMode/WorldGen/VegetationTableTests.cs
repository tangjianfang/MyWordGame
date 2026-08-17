using System;
using System.IO;
using System.Linq;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.WorldGen
{
    /// <summary>
    /// m11 I2（INFRA-B）：植被特征表外置。
    /// 数据从 <c>Assets/StreamingAssets/vegetation/trees.json</c> 与 <c>flowers.json</c> 加载；
    /// 花草方块名对应 A1 花草资源名（连字符转下划线），第 1 波注册方块后由守卫测试闭环。
    /// </summary>
    [TestFixture]
    public class VegetationTableTests
    {
        private static string LocateVegetationFile(string fileName)
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "vegetation", fileName);
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "vegetation", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException($"未能找到 Assets/StreamingAssets/vegetation/{fileName}。");
#endif
        }

        private static VegetationTable LoadRealTable()
        {
            return VegetationTable.Load(
                File.ReadAllText(LocateVegetationFile("trees.json")),
                File.ReadAllText(LocateVegetationFile("flowers.json")));
        }

        /// <summary>真实 trees.json 必须含 8 个树种，且 oak 的数值与 TreeFeature 旧常量一字不差。</summary>
        [Test]
        public void Load_真实JSON_八树种齐_橡树数值与旧常量一致()
        {
            var table = LoadRealTable();

            string[] expectedIds = { "oak", "birch", "pine", "cedar", "jungle", "bush", "sequoia", "cherry" };
            foreach (string id in expectedIds)
            {
                Assert.That(table.TryGetTree(id, out _), Is.True, $"trees.json 必须包含树种 {id}");
            }
            Assert.That(table.Trees.Select(t => t.Id).ToArray(), Is.EquivalentTo(expectedIds),
                "trees.json 有且仅有 8 个已知树种（多出来的 id 一并报错）");

            // oak 数值=现常量：保证旧世界生成的橡树逐格不变（行为铁律）
            Assert.That(table.TryGetTree("oak", out var oak), Is.True);
            Assert.That(oak.LogBlock, Is.EqualTo("log"), "oak 必须继续引用内置原木 log");
            Assert.That(oak.LeavesBlock, Is.EqualTo("leaves"), "oak 必须继续引用内置树叶 leaves");
            Assert.That(oak.TrunkMin, Is.EqualTo(TreeFeature.MinTrunk), "oak.trunkMin 应等于 TreeFeature.MinTrunk");
            Assert.That(oak.TrunkMax, Is.EqualTo(TreeFeature.MaxTrunk), "oak.trunkMax 应等于 TreeFeature.MaxTrunk");
            Assert.That(oak.LeafLayers, Is.EqualTo(TreeFeature.LeafLayers), "oak.leafLayers 应等于 TreeFeature.LeafLayers");
            Assert.That(oak.LeafRadius, Is.EqualTo(TreeFeature.LeafRadius), "oak.leafRadius 应等于 TreeFeature.LeafRadius");
            Assert.That(oak.Biomes, Is.EquivalentTo(new[] { "Plains", "Forest" }), "oak 应投放于 Plains/Forest");

            // 卡片指定的其余树种关键参数
            Assert.That(table.TryGetTree("bush", out var bush), Is.True);
            Assert.That(bush.TrunkMin, Is.EqualTo(1), "bush 树干 1-2");
            Assert.That(bush.TrunkMax, Is.EqualTo(2), "bush 树干 1-2");
            Assert.That(bush.LeafLayers, Is.EqualTo(1), "bush 叶层 1");
            Assert.That(table.TryGetTree("sequoia", out var sequoia), Is.True);
            Assert.That(sequoia.TrunkMin, Is.EqualTo(8), "红杉树干 8-11");
            Assert.That(sequoia.TrunkMax, Is.EqualTo(11), "红杉树干 8-11");
            Assert.That(table.TryGetTree("cherry", out var cherry), Is.True);
            Assert.That(cherry.TrunkMin, Is.EqualTo(4), "樱花树干 4-5");
            Assert.That(cherry.TrunkMax, Is.EqualTo(5), "樱花树干 4-5");
            Assert.That(cherry.LeavesBlock, Is.EqualTo("cherry_leaves"), "樱花用粉叶 cherry_leaves");
            // jungle 是温度>0.7 群系的占位：当前任何群系都不投放（biomes 为空）
            Assert.That(table.TryGetTree("jungle", out var jungle), Is.True);
            Assert.That(jungle.Biomes, Is.Empty, "jungle 占位：暂不投放于任何现有群系");
            Assert.That(table.TryGetTree("bogus", out _), Is.False, "未知树种 id 应返回 false 而非抛异常");
        }

        /// <summary>真实 flowers.json 必须含 12 条花草，块名与 A1 花草资源名（下划线形式）一一对应。</summary>
        [Test]
        public void Load_真实JSON_十二条花草_块名与密度合法()
        {
            var table = LoadRealTable();

            Assert.That(table.Flowers, Has.Count.EqualTo(12), "flowers.json 首版应恰好 12 条");
            foreach (var flower in table.Flowers)
            {
                Assert.That(flower.Id, Is.Not.Empty, "花草条目必须有 id");
                Assert.That(flower.Block, Is.Not.Empty, $"花草 {flower.Id} 必须引用块名");
                Assert.That(flower.Biomes, Is.Not.Null, $"花草 {flower.Id} 必须声明 biomes");
                Assert.That(flower.Biomes.Length, Is.GreaterThan(0), $"花草 {flower.Id} 至少投放一个群系");
                Assert.That(flower.DensityPerChunk, Is.InRange(2, 8),
                    $"花草 {flower.Id} 的 densityPerChunk 应在 2-8/区块（实际 {flower.DensityPerChunk}）");
            }

            // 12 条的块名全集（连字符资源名转下划线块 id，与 gold_ore 等既有命名一致）
            string[] expectedBlocks =
            {
                "flower_poppy", "flower_dandelion", "flower_orchid", "flower_cornflower",
                "flower_rose", "flower_sunflower", "flower_lilac", "flower_daisy",
                "tall_grass", "fern", "mushroom_red", "mushroom_brown"
            };
            Assert.That(table.Flowers.Select(f => f.Block).ToArray(), Is.EquivalentTo(expectedBlocks),
                "花草块名必须与 A1 花草资源清单一一对应");
            Assert.That(table.TryGetFlower("poppy", out var poppy), Is.True);
            Assert.That(poppy.Block, Is.EqualTo("flower_poppy"));
            Assert.That(table.TryGetFlower("no_such_flower", out _), Is.False, "未知花草 id 应返回 false 而非抛异常");
        }

        /// <summary>参数非法的树/花草条目解析失败必须抛异常，且消息携带条目 id。</summary>
        [Test]
        public void Load_参数非法_抛异常且消息带id()
        {
            // trunkMax < trunkMin
            string badRange = "[{\"id\":\"birch\",\"logBlock\":\"birch_log\",\"leavesBlock\":\"birch_leaves\"," +
                              "\"trunkMin\":8,\"trunkMax\":6,\"leafLayers\":3,\"leafRadius\":2,\"biomes\":[\"Forest\"]}]";
            var ex1 = Assert.Throws<InvalidDataException>(() => VegetationTable.Load(badRange, "[]"));
            Assert.That(ex1.Message, Does.Contain("birch"), "异常消息必须携带出错的树种 id");

            // 缺 logBlock
            string noLog = "[{\"id\":\"pine\",\"leavesBlock\":\"pine_leaves\"," +
                           "\"trunkMin\":7,\"trunkMax\":9,\"leafLayers\":4,\"leafRadius\":1,\"biomes\":[\"Snow\"]}]";
            var ex2 = Assert.Throws<InvalidDataException>(() => VegetationTable.Load(noLog, "[]"));
            Assert.That(ex2.Message, Does.Contain("pine"), "异常消息必须携带出错的树种 id");

            // 树种 id 重复
            string oak = "{\"id\":\"oak\",\"logBlock\":\"log\",\"leavesBlock\":\"leaves\"," +
                         "\"trunkMin\":5,\"trunkMax\":7,\"leafLayers\":3,\"leafRadius\":2,\"biomes\":[\"Plains\"]}";
            var ex3 = Assert.Throws<InvalidDataException>(() => VegetationTable.Load($"[{oak},{oak}]", "[]"));
            Assert.That(ex3.Message, Does.Contain("oak"), "重复 id 的异常消息必须携带该 id");

            // 花草 densityPerChunk 为负
            string badFlower = "[{\"id\":\"poppy\",\"block\":\"flower_poppy\",\"biomes\":[\"Plains\"],\"densityPerChunk\":-1}]";
            var ex4 = Assert.Throws<InvalidDataException>(() => VegetationTable.Load("[]", badFlower));
            Assert.That(ex4.Message, Does.Contain("poppy"), "异常消息必须携带出错的花草 id");
        }

        /// <summary>JSON 本身不是合法格式时抛异常（不把 Newtonsoft 的原始异常裸抛出去）。</summary>
        [Test]
        public void Load_JSON格式非法_抛InvalidDataException()
        {
            Assert.Throws<InvalidDataException>(() => VegetationTable.Load("[{\"id\":\"oak\"", "[]"),
                "trees.json 非法应抛 InvalidDataException");
            Assert.Throws<InvalidDataException>(() => VegetationTable.Load("[]", "不是 json"),
                "flowers.json 非法应抛 InvalidDataException");
        }

        /// <summary>oak species 重载与旧 API 在同一 seed 下生成逐格一致（旧 API 内部走 oak species 的等价性证明）。</summary>
        [Test]
        public void TryGenerate_oakSpecies_与旧API生成完全一致()
        {
            World BuildWorld()
            {
                var world = new World();
                var col = new ChunkColumn();
                for (int lx = 0; lx < 16; lx++)
                for (int lz = 0; lz < 16; lz++)
                {
                    for (int y = 0; y < 68; y++) col.SetBlock(lx, y, lz, BlockIds.Dirt);
                    col.SetBlock(lx, 68, lz, BlockIds.Grass);
                }
                world.AddChunk(new ChunkPos(0, 0), col);
                return world;
            }

            var table = LoadRealTable();
            Assert.That(table.TryGetTree("oak", out var oak), Is.True);

            // 找一个旧 API 命中的 seed
            int hitSeed = -1;
            var oldWorld = BuildWorld();
            for (int seed = 0; seed < 200; seed++)
            {
                if (TreeFeature.TryGenerate(oldWorld, seed, 8, 8))
                {
                    hitSeed = seed;
                    break;
                }
            }
            Assert.That(hitSeed, Is.GreaterThanOrEqualTo(0), "200 个 seed 内旧 API 必有命中");

            // 同一 seed 用 species 重载在另一份相同世界上生成
            var newWorld = BuildWorld();
            Assert.That(TreeFeature.TryGenerate(newWorld, hitSeed, 8, 8, oak), Is.True,
                "species 重载应能生成（不再受旧 skip-hash 门限）");

            // 逐格一致：oak 行为不变
            for (int lx = 0; lx < 16; lx++)
            for (int lz = 0; lz < 16; lz++)
            for (int y = 60; y < 100; y++)
            {
                Assert.That(newWorld.GetBlock(lx, y, lz), Is.EqualTo(oldWorld.GetBlock(lx, y, lz)),
                    $"oak species 与旧 API 在 ({lx},{y},{lz}) 不一致");
            }
        }

        /// <summary>species 重载按 species 参数决定树干高度/叶层（用 cherry 参数但借用 log/leaves 方块以便落块）。</summary>
        [Test]
        public void TryGenerate_species重载_按species参数生成且确定()
        {
            World BuildWorld()
            {
                var world = new World();
                var col = new ChunkColumn();
                for (int lx = 0; lx < 16; lx++)
                for (int lz = 0; lz < 16; lz++)
                {
                    for (int y = 0; y < 68; y++) col.SetBlock(lx, y, lz, BlockIds.Dirt);
                    col.SetBlock(lx, 68, lz, BlockIds.Grass);
                }
                world.AddChunk(new ChunkPos(0, 0), col);
                return world;
            }

            // cherry 的尺寸参数，方块借用内置 log/leaves（cherry_log 等第 1 波才注册）
            var species = new TreeSpecies
            {
                Id = "cherry", LogBlock = "log", LeavesBlock = "leaves",
                TrunkMin = 4, TrunkMax = 5, LeafLayers = 2, LeafRadius = 1,
                Biomes = new[] { "Plains" }
            };

            // 多个 seed：树干高度必须始终落在 species 区间 [4,5]
            for (int seed = 0; seed < 50; seed++)
            {
                var world = BuildWorld();
                Assert.That(TreeFeature.TryGenerate(world, seed, 8, 8, species), Is.True,
                    $"species 重载在 seed={seed} 应生成（草面上的生成不受概率门限）");

                int trunkStart = 69;
                int trunkEnd = trunkStart;
                while (trunkEnd < VoxelCoords.MaxY && world.GetBlock(8, trunkEnd, 8) == TreeFeature.LogId)
                    trunkEnd++;
                Assert.That(trunkEnd - trunkStart, Is.InRange(species.TrunkMin, species.TrunkMax),
                    $"seed={seed} 树干高度 {trunkEnd - trunkStart} 不在 species 区间 [{species.TrunkMin},{species.TrunkMax}]");
            }

            // 确定性：同 seed 两份世界逐格一致
            var w1 = BuildWorld();
            var w2 = BuildWorld();
            TreeFeature.TryGenerate(w1, 777, 8, 8, species);
            TreeFeature.TryGenerate(w2, 777, 8, 8, species);
            for (int lx = 0; lx < 16; lx++)
            for (int lz = 0; lz < 16; lz++)
            for (int y = 60; y < 100; y++)
            {
                Assert.That(w1.GetBlock(lx, y, lz), Is.EqualTo(w2.GetBlock(lx, y, lz)),
                    $"species 生成不确定：({lx},{y},{lz})");
            }
        }

        /// <summary>species 密度通道：不同树种在同一格的判定互相独立，同参数重复调用结果确定。</summary>
        [Test]
        public void ShouldPlaceTree_species通道_不同树种判定独立且确定()
        {
            var config = new BiomeConfig { Id = (int)Biome.Forest, Name = "Forest", TreeDensity = 30 };

            int oakHits = 0, birchHits = 0, differ = 0;
            for (int x = 0; x < 60; x++)
            for (int z = 0; z < 60; z++)
            {
                bool oak = TreeFeature.ShouldPlaceTree(x, z, "oak", config, seed: 42);
                bool birch = TreeFeature.ShouldPlaceTree(x, z, "birch", config, seed: 42);
                if (oak) oakHits++;
                if (birch) birchHits++;
                if (oak != birch) differ++;
            }
            Assert.That(oakHits, Is.GreaterThan(0), "Forest 密度 30 下 oak 通道应有命中");
            Assert.That(birchHits, Is.GreaterThan(0), "Forest 密度 30 下 birch 通道应有命中");
            Assert.That(differ, Is.GreaterThan(0),
                $"不同树种的判定通道必须独立（60x60 内无差异，通道被互相覆盖了）");

            // 确定性：同参数重复调用结果不变
            for (int x = 0; x < 30; x++)
            for (int z = 0; z < 30; z++)
            {
                Assert.That(TreeFeature.ShouldPlaceTree(x, z, "oak", config, seed: 42),
                    Is.EqualTo(TreeFeature.ShouldPlaceTree(x, z, "oak", config, seed: 42)),
                    $"oak 通道判定不确定：({x},{z})");
            }

            // 密度 0（沙漠）任何树种都不放
            var desert = new BiomeConfig { Id = (int)Biome.Desert, Name = "Desert", TreeDensity = 0 };
            Assert.That(TreeFeature.ShouldPlaceTree(3, 7, "oak", desert, seed: 42), Is.False,
                "TreeDensity=0 时任何树种都不应放置");
            Assert.That(TreeFeature.ShouldPlaceTree(3, 7, "oak", null, seed: 42), Is.False,
                "config 为 null 时不放置");
        }
    }
}
