using System;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    /// <summary>
    /// Fix-up X2：Core 层 <see cref="BlockDrops"/> 把 block numericId 映射到应掉落的
    /// <see cref="ItemStack"/> 列表。覆盖 review-final.md critical #2：BlockInteraction.Break()
    /// 之前只 SetBlock 不 spawn ItemDropEntity，spec B7（挖方块掉 ItemDropEntity）未满足。
    /// <para>
    /// 本测试只验证 Core 数据契约（FromJson 解析 + 查表），运行时拼接（PlayerContext.Add、
    /// 位置计算）由 <c>BlockBreakDropTests</c> 走 EditMode + Unity 侧覆盖。
    /// </para>
    /// </summary>
    [TestFixture]
    public class BlockDropsTests
    {
        private const string StoneDropsCobble = @"{
            ""blockId"": ""stone"",
            ""blockNumericId"": 1,
            ""drops"": [
                { ""itemId"": ""cobblestone"", ""countMin"": 1, ""countMax"": 1 }
            ]
        }";

        private const string GrassDropsDirt = @"{
            ""blockId"": ""grass"",
            ""blockNumericId"": 3,
            ""drops"": [
                { ""itemId"": ""dirt"", ""countMin"": 1, ""countMax"": 1 }
            ]
        }";

        private const string LogDropsLog = @"{
            ""blockId"": ""log"",
            ""blockNumericId"": 1001,
            ""drops"": [
                { ""itemId"": ""log"", ""countMin"": 1, ""countMax"": 1 }
            ]
        }";

        private const string NoDrops = @"{
            ""blockId"": ""bedrock"",
            ""blockNumericId"": 6,
            ""drops"": []
        }";

        private const string MultipleDrops = @"{
            ""blockId"": ""fancy"",
            ""blockNumericId"": 100,
            ""drops"": [
                { ""itemId"": ""cobblestone"", ""countMin"": 1, ""countMax"": 3 },
                { ""itemId"": ""stick"", ""countMin"": 0, ""countMax"": 2 }
            ]
        }";

        private static ItemDatabase StandardItemDatabase()
        {
            // 用 stub 风格的最小 JSON 让 ItemDatabase 分配确定 numericId，
            // 测试只引相对引用而非写死数字。
            string[] docs =
            {
                @"{ ""id"": ""dirt"", ""numericId"": 1000, ""texture"": ""dirt"" }",
                @"{ ""id"": ""cobblestone"", ""numericId"": 1003, ""texture"": ""cobblestone"" }",
                @"{ ""id"": ""log"", ""numericId"": 1001, ""texture"": ""log"" }",
                @"{ ""id"": ""stick"", ""numericId"": 1002, ""texture"": ""stick"" }",
            };
            return ItemDatabase.FromJson(docs);
        }

        [Test]
        public void FromJson_ParsesSingleDropEntry()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(new[] { StoneDropsCobble }, items);

            ItemStack[] result = drops.DropsFor(1);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(1));
            Assert.That(result[0].ItemId, Is.EqualTo(1003), "cobblestone numericId 应为 1003");
            Assert.That(result[0].Count, Is.InRange(1, 1), "count 区间为 [1,1]");
        }

        [Test]
        public void FromJson_ParsesMultipleBlocks()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(
                new[] { StoneDropsCobble, GrassDropsDirt, LogDropsLog }, items);

            ItemStack[] stone = drops.DropsFor(1);
            ItemStack[] grass = drops.DropsFor(3);
            ItemStack[] log = drops.DropsFor(1001);

            Assert.That(stone[0].ItemId, Is.EqualTo(1003), "石头掉 cobblestone");
            Assert.That(grass[0].ItemId, Is.EqualTo(1000), "草掉 dirt");
            Assert.That(log[0].ItemId, Is.EqualTo(1001), "原木掉 log");
        }

        [Test]
        public void FromJson_EmptyDropsList_Allowed()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(new[] { NoDrops }, items);

            ItemStack[] result = drops.DropsFor(6);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0), "bedrock 不应掉任何东西");
        }

        [Test]
        public void DropsFor_UnknownBlock_ReturnsEmpty()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(new[] { StoneDropsCobble }, items);

            ItemStack[] result = drops.DropsFor(999); // 未注册的方块
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0), "未注册方块应返回空数组（与 World.GetBlock 的宽容读取保持一致）");
        }

        [Test]
        public void DropsFor_Air_ReturnsEmpty()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(
                new[] { StoneDropsCobble, GrassDropsDirt }, items);

            ItemStack[] result = drops.DropsFor(0); // Air
            Assert.That(result.Length, Is.EqualTo(0), "挖空气不应产生掉落");
        }

        [Test]
        public void FromJson_MultipleDropsForOneBlock_BothRetained()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(new[] { MultipleDrops }, items);

            ItemStack[] result = drops.DropsFor(100);
            Assert.That(result.Length, Is.EqualTo(2));
            Assert.That(result[0].ItemId, Is.EqualTo(1003), "条目 0 应是 cobblestone");
            Assert.That(result[1].ItemId, Is.EqualTo(1002), "条目 1 应是 stick");
        }

        [Test]
        public void ResolveRolls_PicksCountWithinRange()
        {
            // 多组 (min, max) 随机种子验证 100 次至少产生一个非空范围值
            int hits = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                int count = BlockDrops.RollCount(seed, min: 1, max: 3);
                Assert.That(count, Is.InRange(1, 3), $"seed={seed} 算出的 count 应在 [1,3]");
                if (count != 1)
                {
                    hits++;
                }
            }
            // 1-3 区间里非 1 的概率是 50%，100 次种子起码应出现若干次
            Assert.That(hits, Is.GreaterThan(0),
                "100 个种子下 1-3 区间应至少有一次 ≠ 1（即随机性证明）");
        }

        [Test]
        public void ResolveRolls_DeterministicForSameSeed()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                int a = BlockDrops.RollCount(seed, min: 0, max: 5);
                int b = BlockDrops.RollCount(seed, min: 0, max: 5);
                Assert.That(a, Is.EqualTo(b),
                    $"seed={seed} 两次 RollCount 必须同值（确定性）");
            }
        }

        [Test]
        public void ResolveRolls_DifferentSeedsGiveDifferentCounts()
        {
            var counts = new System.Collections.Generic.HashSet<int>();
            for (int seed = 0; seed < 50; seed++)
            {
                counts.Add(BlockDrops.RollCount(seed, min: 0, max: 10));
            }
            Assert.That(counts.Count, Is.GreaterThan(1),
                "50 个不同种子在 [0,10] 区间应产出至少 2 个不同值");
        }

        [Test]
        public void FromJson_MalformedDropEntry_Throws()
        {
            string bad = @"{
                ""blockId"": ""bad"",
                ""blockNumericId"": 50,
                ""drops"": [
                    { ""itemId"": ""not_a_real_item"" }
                ]
            }";
            var items = StandardItemDatabase();

            Assert.Throws<System.IO.InvalidDataException>(
                () => BlockDrops.FromJson(new[] { bad }, items),
                "未注册的 itemId 应抛 InvalidDataException，与其它注册表行为一致");
        }

        [Test]
        public void FromJson_BlockNumericIdUnregistered_Allowed_EmptyDrops()
        {
            // 旧命名 misleading："Mismatch_Throws" 实际断言不抛。
            // 真实语义：blockNumericId=99 在 BlockRegistry 里没注册，但 BlockDrops
            // 本身不依赖 BlockRegistry（数据驱动，只看 items + drops JSON），
            // 因此允许——drops 为空时只把条目存进表，DropsFor 查到返回空数组。
            string doc = @"{
                ""blockId"": ""stone"",
                ""blockNumericId"": 99,
                ""drops"": []
            }";
            var items = StandardItemDatabase();

            BlockDrops drops = BlockDrops.FromJson(new[] { doc }, items);
            Assert.That(drops.DropsFor(99).Length, Is.EqualTo(0));
        }

        [Test]
        public void FromJson_MalformedRoot_Throws()
        {
            // 真正的不抛测试：缺 blockNumericId 字段——BlockDrops 必须报错（与同仓其它
            // 注册表一致：少字段 = 解析失败，不静默忽略）。
            string bad = @"{
                ""blockId"": ""stone"",
                ""drops"": []
            }";
            var items = StandardItemDatabase();

            Assert.Throws<System.IO.InvalidDataException>(
                () => BlockDrops.FromJson(new[] { bad }, items),
                "缺 blockNumericId 字段时应抛 InvalidDataException，不应静默通过");
        }

        /// <summary>
        /// 集成测试（X2 review fix-up #important）：跑真实
        /// <c>Assets/StreamingAssets/items/*.json</c> + <c>Assets/StreamingAssets/blocks/drops/block_drops.json</c>
        /// 的端到端组合，验证数据契约在游戏启动时不抛、且 stone 至少掉 cobblestone。
        /// <para>
        /// 这是 X2 review 找到的 critical bug 的回归门：之前 mock ItemDatabase 用
        /// 假 id 注册 "dirt"，从未跑到真实 Loader + 真实 JSON 这条路径，因此
        /// 「dirt 不在物品库 → InvalidDataException → 静默吞掉 → 掉落全废」
        /// 整条断链漏到了运行时。本测试守住它。
        /// </para>
        /// </summary>
        [Test]
        public void BlockDrops_RealLoaders_NoThrow_StoneDropsCobble()
        {
            string itemsDir = LocateItemsDirectory();
            Assert.That(Directory.Exists(itemsDir), Is.True,
                $"找不到物品 JSON 目录：{itemsDir}");

            var itemDocs = Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText);
            var items = ItemDatabase.FromJson(itemDocs);

            // 必须含 dirt——之前 review 的 critical bug 就是因为 block_drops.json 引用 dirt，
            // 但 dirt 不在 items 里。补完 dirt.json 后这一条必须为真。
            Assert.That(items.TryGetById("dirt", out _), Is.True,
                "review X2 critical fix：StreamingAssets/items 必须含 dirt.json，否则" +
                "BlockDropsLoader.Load 抛 InvalidDataException、WorldBootstrap 静默吞掉、" +
                "运行时挖方块全不掉");

            string dropsPath = LocateBlockDropsPath();
            Assert.That(File.Exists(dropsPath), Is.True,
                $"找不到 block_drops.json：{dropsPath}");

            // 端到端：解析 real JSON，断言不抛、stone 掉 cobblestone。
            BlockDrops drops = BlockDrops.FromJson(new[] { File.ReadAllText(dropsPath) }, items);

            ItemStack[] stoneDrops = drops.DropsFor(BlockIds.Stone);
            Assert.That(stoneDrops, Is.Not.Null);
            Assert.That(stoneDrops.Length, Is.GreaterThanOrEqualTo(1),
                "石头应至少有一个掉落条目（数据驱动：stone → cobblestone×1）");

            Assert.That(items.TryGetById("cobblestone", out var cobbleDef), Is.True,
                "cobblestone 应在真实物品库中");
            Assert.That(stoneDrops[0].ItemId, Is.EqualTo(cobbleDef.NumericId),
                "石头应掉 cobblestone，且 ItemId 等于 cobblestone.NumericId");
            Assert.That(stoneDrops[0].Count, Is.InRange(1, cobbleDef.MaxStack),
                "count 应在 [countMin, countMax ∩ def.MaxStack] 区间内");
        }

        /// <summary>
        /// m10 A1 跨表引用守卫：真实 items/*.json + 真实 block_drops.json 端到端，
        /// 四矿石必须各自掉对应材料物品 ×1（spec §1 矿物表）。任何一环 JSON 打错字
        /// （itemId 悬空、blockNumericId 与 BlockIds 常量不一致）在这里立刻失败。
        /// </summary>
        [Test]
        public void BlockDrops_RealFile_FourOresDropTheirMaterial()
        {
            var itemDocs = Directory.GetFiles(LocateItemsDirectory(), "*.json").Select(File.ReadAllText);
            var items = ItemDatabase.FromJson(itemDocs);

            BlockDrops drops = BlockDrops.FromJson(
                new[] { File.ReadAllText(LocateBlockDropsPath()) }, items);

            (ushort blockNumericId, string itemId)[] expected =
            {
                (BlockIds.GoldOre, "raw_gold"),
                (BlockIds.RawIronOre, "raw_iron"),
                (BlockIds.SummerAlloyOre, "summer_alloy"),
                (BlockIds.MachineEssenceOre, "machine_essence"),
            };

            foreach ((ushort blockNumericId, string itemId) in expected)
            {
                Assert.That(items.TryGetById(itemId, out var def), Is.True,
                    $"矿石掉落引用的物品 {itemId} 必须先在 items/*.json 注册");
                Assert.That(def.MaxStack, Is.GreaterThanOrEqualTo(1),
                    $"{itemId} 的 maxStack 至少为 1，否则永远掉不出来");

                ItemStack[] result = drops.DropsFor(blockNumericId);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Length, Is.EqualTo(1),
                    $"方块 numericId={blockNumericId} 应恰好掉 1 种物品（spec §7：矿石掉 1 个）");
                Assert.That(result[0].ItemId, Is.EqualTo(def.NumericId),
                    $"方块 numericId={blockNumericId} 应掉 {itemId}（ItemId 应等于其 numericId）");
                Assert.That(result[0].Count, Is.EqualTo(1),
                    "矿石掉落数量固定 ×1（countMin=countMax=1，确定性掷骰必得 1）");
            }
        }

        /// <summary>
        /// m11 W1-5 九件家具的 block↔item 配对表（numericId 经真实注册表解析，不在此重复写死——
        /// 与 BlockDefinitionFilesTests.M11FurnitureBlocks 的 numericId 守卫各管一半，改号那边先红）。
        /// </summary>
        private static readonly (string BlockId, string ItemId)[] M11FurniturePairs =
        {
            ("chair_block", "chair"),
            ("globe_block", "globe"),
            ("hacker_pc_block", "hacker_pc"),
            ("keyboard_block", "keyboard"),
            ("laptop_block", "laptop"),
            ("mouse_block", "mouse"),
            ("notebook_block", "notebook"),
            ("office_desk_block", "office_desk"),
            ("table_block", "table"),
        };

        /// <summary>
        /// m11 W1-5 家具掉落闭环（照 FourOres 真实加载器模式）：真实 items + 真实 blocks +
        /// 真实 block_drops.json 端到端——九个家具方块破坏后必须各掉回对应物品 ×1。
        /// 任何一环打错字（itemId 悬空、blockNumericId 与注册表不一致）在这里立刻失败。
        /// </summary>
        [Test]
        public void BlockDrops_RealFile_FurnitureBlocks_DropBackTheirItems()
        {
            var items = ItemDatabase.FromJson(
                Directory.GetFiles(LocateItemsDirectory(), "*.json").Select(File.ReadAllText));
            var blocks = BlockRegistry.FromJson(
                Directory.GetFiles(LocateBlocksDirectory(), "*.json").Select(File.ReadAllText));
            BlockDrops drops = BlockDrops.FromJson(
                new[] { File.ReadAllText(LocateBlockDropsPath()) }, items);

            foreach ((string blockId, string itemId) in M11FurniturePairs)
            {
                BlockDefinition block = blocks.GetById(blockId);
                Assert.That(items.TryGetById(itemId, out var def), Is.True,
                    $"家具掉落引用的物品 {itemId} 必须先在 items/*.json 注册");

                ItemStack[] result = drops.DropsFor(block.NumericId);
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Length, Is.EqualTo(1),
                    $"{blockId} 破坏应恰好掉 1 种物品（对应 {itemId}），破坏回收是家具摆放链路的另一半");
                Assert.That(result[0].ItemId, Is.EqualTo(def.NumericId),
                    $"{blockId} 应掉回 {itemId}（ItemId 应等于其 numericId）");
                Assert.That(result[0].Count, Is.EqualTo(1),
                    "家具破坏掉回原物品固定 ×1（countMin=countMax=1，确定性掷骰必得 1）");
            }
        }

        /// <summary>
        /// m11 W1-5 物品↔方块关联守卫：九件家具物品的 <c>blockId</c> 字段必须指向真实注册的
        /// 家具方块。物品侧加载器（ItemDatabase）暂不解析该字段（Unity 摆放路由在集成点②接线），
        /// 所以这里直接读原始 JSON——字段写错（悬空 blockId）在此拦下，而不是等接线后才发现摆不出来。
        /// </summary>
        [Test]
        public void FurnitureItems_DeclaredBlockId_PointsAtRegisteredFurnitureBlocks()
        {
            var blocks = BlockRegistry.FromJson(
                Directory.GetFiles(LocateBlocksDirectory(), "*.json").Select(File.ReadAllText));

            foreach ((string blockId, string itemId) in M11FurniturePairs)
            {
                string itemPath = Path.Combine(LocateItemsDirectory(), itemId + ".json");
                Assert.That(File.Exists(itemPath), Is.True,
                    $"家具物品 {itemId} 应有定义文件 {itemPath}");

                // 轻量 JSON 扫描（不依赖 Newtonsoft——EditMode 测试链惯例，见 RecipeFilesTests）
                var match = System.Text.RegularExpressions.Regex.Match(
                    File.ReadAllText(itemPath), "\"blockId\"\\s*:\\s*\"([^\"]+)\"");
                Assert.That(match.Success, Is.True,
                    $"items/{itemId}.json 应声明 blockId 字段");
                string declared = match.Groups[1].Value;
                Assert.That(declared, Is.EqualTo(blockId),
                    $"items/{itemId}.json 的 blockId 字段应指向 {blockId}——摆放路由将按它把物品解算成方块");
                Assert.That(blocks.GetById(declared).Id, Is.EqualTo(blockId),
                    $"blockId 指向的方块必须真实注册（{declared}）");
            }
        }

        private static string LocateItemsDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "items");
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "items");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("未能从测试输出目录向上找到 Assets/StreamingAssets/items。");
#endif
        }

        private static string LocateBlockDropsPath()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "blocks", "drops", "block_drops.json");
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "blocks", "drops", "block_drops.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException("未能从测试输出目录向上找到 block_drops.json。");
#endif
        }

        /// <summary>m11 W1-5：家具掉落测试要按真实注册表解析 block numericId。</summary>
        private static string LocateBlocksDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "blocks");
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "blocks");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("未能从测试输出目录向上找到 Assets/StreamingAssets/blocks。");
#endif
        }
    }
}
