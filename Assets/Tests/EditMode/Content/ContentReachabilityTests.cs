// 评审 08 F0 内容断链修复的可达性守卫——防止「物品存在、配方存在、获取途径不存在」
// 的死胡同再次合入（钻石断链让 16 成就里的钻石系成就永远不可达，用户存档画像实证
// 成就 2/16、任务停在第一章第 2 步）。纯 System.IO 定位数据目录，dotnet / EditMode 双链同跑。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Content
{
    [TestFixture]
    public class ContentReachabilityTests
    {
        private ItemDatabase _items;
        private RecipeDatabase _recipes;
        private BlockRegistry _blocks;

        [OneTimeSetUp]
        public void SetUp()
        {
            _items = ItemDatabase.FromJson(ReadDir("items"));
            _recipes = RecipeDatabase.FromJson(ReadDir("recipes"), _items);
            _blocks = BlockRegistry.FromJson(ReadDir("blocks"));
        }

        [Test]
        public void 盔甲12件_金合金机元配方全部可合成()
        {
            // 评审 08 F0：金/夏合金/机元盔甲 12 件物品早已注册但零配方——「看得见够不着」
            string[] prefixes = { "gold", "summer_alloy", "machine_essence" };
            string[] ingots = { "gold_ingot", "summer_alloy", "machine_essence" };
            string[] parts = { "helmet", "chest", "legs", "boots" };
            for (int m = 0; m < prefixes.Length; m++)
            {
                for (int p = 0; p < parts.Length; p++)
                {
                    ItemStack[] grid = ArmorGrid(ingots[m], parts[p]);
                    Recipe match = _recipes.FindMatch(grid, 3, 3);
                    Assert.That(match, Is.Not.Null,
                        $"{prefixes[m]}_{parts[p]} 配方缺失或不可匹配（F0 断链回归）");
                    Assert.That(match.Output.ItemId,
                        Is.EqualTo(_items.GetById($"{prefixes[m]}_{parts[p]}").NumericId),
                        $"{prefixes[m]}_{parts[p]} 配方产物不对");
                }
            }
        }

        [Test]
        public void 钻石链路_矿方块嵌矿掉落全通()
        {
            // 评审 08 F0：钻镐/钻剑配方一直消耗 diamond，但此前全世界没有任何获取途径
            Assert.That(_blocks.TryGetById("diamond_ore", out BlockDefinition ore), Is.True,
                "diamond_ore 方块未注册（断链）");
            Assert.That(ore.NumericId, Is.EqualTo(BlockIds.DiamondOre),
                "diamond_ore 的 numericId 必须与 BlockIds.DiamondOre 一致");

            // 嵌矿：y<16 扫描必命中（1/250 密度，13.5 万格内全空概率 ≈ e^-540）
            bool found = false;
            for (int x = 0; x < 300 && !found; x++)
            {
                for (int z = 0; z < 300 && !found; z++)
                {
                    for (int y = 0; y < 16 && !found; y++)
                    {
                        if (OreFeature.OreAt(42, x, y, z) == BlockIds.DiamondOre) found = true;
                    }
                }
            }
            Assert.That(found, Is.True, "OreFeature 应在 y<16 石层嵌出钻石矿");

            // 掉落表：diamond_ore → diamond 1:1
            string dropsJson = File.ReadAllText(
                Path.Combine(SaDir("blocks"), "drops", "block_drops.json"));
            StringAssert.Contains("\"blockId\": \"diamond_ore\"", dropsJson, "掉落表缺 diamond_ore 条目");

            // 消费端闭环：diamond 物品与钻镐配方本来就在
            Assert.That(_items.GetById("diamond").NumericId, Is.GreaterThan(0), "diamond 物品应存在");
        }

        [Test]
        public void 面包与红石粉配方_可达()
        {
            // 面包：交易池 VillagerOffers 引用 bread（此前悬空），物品+配方落地后链路自愈
            Assert.That(_items.GetById("bread").HealAmount, Is.EqualTo(5), "面包应为食物 heal 5");
            ItemStack wheat = Stack(_items.GetById("wheat"));
            ItemStack air = new ItemStack(0, 0);
            var breadMatch = _recipes.FindMatch(
                new[] { wheat, wheat, wheat, air, air, air, air, air, air }, 3, 3);
            Assert.That(breadMatch, Is.Not.Null, "3 麦子应合成面包（workbench 3×1 子区匹配）");
            Assert.That(breadMatch.Output.ItemId, Is.EqualTo(_items.GetById("bread").NumericId));

            // 红石粉：redstone → redstone_dust 口袋 1×1
            var redstone = Stack(_items.GetById("redstone"));
            var dustMatch = _recipes.FindMatch(new[] { redstone }, 1, 1);
            Assert.That(dustMatch, Is.Not.Null, "redstone → redstone_dust 口袋配方缺失");
            Assert.That(dustMatch.Output.ItemId, Is.EqualTo(_items.GetById("redstone_dust").NumericId));
        }

        [Test]
        public void 工作台物品_可放置对应方块()
        {
            // 评审 08 F0：crafting_table 物品此前无 blockId——右键放不出工作台（放置路由
            // 只走 ItemDefinition.BlockId，评审 07#9 起占位路径已退役）
            Assert.That(_items.GetById("crafting_table").BlockId, Is.EqualTo("crafting_table"),
                "crafting_table 物品必须带 blockId");
            Assert.That(_blocks.TryGetById("crafting_table", out _), Is.True,
                "crafting_table 方块应已注册（跨表引用闭环）");
        }

        // ─── helpers ─────────────────────────────────────────────────────

        private ItemStack Stack(ItemDefinition d) => new ItemStack(d.NumericId, 1);

        /// <summary>盔甲配方网格（3×3，3×2 配方摆顶部两行，FindMatch 子区匹配）。</summary>
        private ItemStack[] ArmorGrid(string ingot, string part)
        {
            ItemStack m = Stack(_items.GetById(ingot));
            ItemStack air = new ItemStack(0, 0);
            switch (part)
            {
                case "helmet": return new[] { m, m, m, m, air, m, air, air, air };
                case "chest": return new[] { m, air, m, m, m, m, m, m, m };
                case "legs": return new[] { m, m, m, m, air, m, m, air, m };
                default: return new[] { m, air, m, m, air, m, air, air, air }; // boots
            }
        }

        private static IEnumerable<string> ReadDir(string sub) =>
            Directory.GetFiles(SaDir(sub), "*.json").Select(File.ReadAllText);

        /// <summary>双链定位 StreamingAssets 子目录：Unity 下走引擎路径，
        /// dotnet 链从测试输出目录向上找仓库根（照 BlockDefinitionFilesTests 同款）。</summary>
        private static string SaDir(string sub)
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, sub);
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", sub);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException($"未能从测试输出目录向上找到 Assets/StreamingAssets/{sub}。");
#endif
        }
    }
}
