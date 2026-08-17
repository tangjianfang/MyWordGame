using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Enchanting
{
    /// <summary>
    /// m11 W2-2：附魔获取链的真文件守卫——附魔台方块 / 配套物品 / 两条配方 / 掉落 / 贴图需求。
    /// 照 FunctionalBlockFilesTests 的模式直读仓库里的真实 JSON（FunctionalBlockFilesTests 同款
    /// 目录定位），改坏任何一环（JSON 打错、贴图没立需求、方块与物品 id 悬空）在这里立刻红。
    /// </summary>
    [TestFixture]
    public class EnchantingFilesTests
    {
        private BlockRegistry _blocks;
        private ItemDatabase _items;
        private RecipeDatabase _recipes;
        private BlockDrops _drops;

        [OneTimeSetUp]
        public void LoadRealDefinitions()
        {
            string blocksDir = LocateStreamingAssetsSubdirectory("blocks");
            string itemsDir = LocateStreamingAssetsSubdirectory("items");
            string recipesDir = LocateStreamingAssetsSubdirectory("recipes");

            _blocks = BlockRegistry.FromJson(Directory.GetFiles(blocksDir, "*.json").Select(File.ReadAllText));
            _items = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));
            _recipes = RecipeDatabase.FromJson(
                Directory.GetFiles(recipesDir, "*.json").Select(File.ReadAllText), _items);
            _drops = BlockDrops.FromJson(
                new[] { File.ReadAllText(Path.Combine(blocksDir, "drops", "block_drops.json")) }, _items);
        }

        // ─── 附魔台方块：注册 + numericId 段位 + 物理属性 ───────────────────

        /// <summary>附魔台方块 numericId：1062（显式段，避开农业 1050-1060 与自动分配位）。</summary>
        private const ushort EnchantingTableBlockId = 1062;

        /// <summary>附魔台物品 numericId：1604（紧跟附魔家族 book=1601 / enchanted_book=1602 / lapis=1603）。</summary>
        private const int EnchantingTableItemId = 1604;

        [Test]
        public void 附魔台方块_已注册且numericId钉在1062()
        {
            var table = _blocks.GetById("enchanting_table");

            Assert.That(table, Is.Not.Null, "blocks/enchanting_table.json 必须已注册");
            Assert.That(table.NumericId, Is.EqualTo(EnchantingTableBlockId),
                "numericId 显式钉在 1062：1050-1060 是农业段、自动分配已占到 1061，改号会漂移存档");
        }

        [Test]
        public void 附魔台方块_常规实体方块属性_走BreakTime默认分支()
        {
            var table = _blocks.GetById("enchanting_table");

            Assert.That(table.Solid, Is.True, "附魔台挡移动（工作台同款整格实心简化）");
            Assert.That(table.Opaque, Is.True, "附魔台不透明（整格实心，贴墙不剔除邻居面）");
            Assert.That(table.Hardness, Is.EqualTo(2.0f).Within(1e-5f),
                "hardness=2 照工作台：不进 BreakTime switch，default 分支采信 JSON hardness 对表");
            Assert.That(table.MinToolTier, Is.EqualTo(0), "徒手可挖——功能方块不该被门槛吞掉落");
        }

        [Test]
        public void 附魔台方块_贴图已入库且有美术需求()
        {
            string blocksDir = LocateStreamingAssetsSubdirectory("blocks");
            string texturesDir = Path.Combine(blocksDir, "textures");
            string repoRoot = new DirectoryInfo(blocksDir).Parent.Parent.Parent.FullName;

            var table = _blocks.GetById("enchanting_table");
            var referenced = new SortedSet<string>(table.Textures, StringComparer.Ordinal);
            Assert.That(referenced, Is.EquivalentTo(new[] { "enchanting-table-top", "enchanting-table-side" }),
                "六面引用 top + side 两张贴图（照 crafting_table 的分面结构）");

            foreach (string texture in referenced)
            {
                Assert.That(File.Exists(Path.Combine(texturesDir, texture + ".png")), Is.True,
                    $"{texture}.png 必须已入库 blocks/textures/——实机会显示 missing 棕块");
                Assert.That(
                    Directory.GetFiles(Path.Combine(repoRoot, "art", "requests"),
                        texture + ".md", SearchOption.AllDirectories).Length,
                    Is.GreaterThan(0),
                    $"{texture} 在 art/requests 下没有需求文件，美术链路会断档");
            }
        }

        // ─── 配套物品：注册 + 段位 + blockId 关联 ──────────────────────────

        [Test]
        public void 附魔台物品_已注册且numericId钉在1604_blockId指向方块()
        {
            var item = _items.GetById("enchanting_table");

            Assert.That(item, Is.Not.Null, "items/enchanting_table.json 必须已注册");
            Assert.That(item.NumericId, Is.EqualTo(EnchantingTableItemId),
                "物品 numericId 显式钉在 1604（附魔家族段），跳过自动分配防漂移");

            string itemJson = File.ReadAllText(
                Path.Combine(LocateStreamingAssetsSubdirectory("items"), "enchanting_table.json"));
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(
                    itemJson, "\"blockId\"\\s*:\\s*\"enchanting_table\""), Is.True,
                "物品必须声明 blockId=enchanting_table——摆放路由按它把物品解算成方块");
        }

        [Test]
        public void 附魔书物品_maxStack为1()
        {
            // maxStack=1 保证「手持一本」语义稳定（一本一格），融合整格清空才有确定性
            Assert.That(_items.GetById("enchanted_book").MaxStack, Is.EqualTo(1),
                "enchanted_book 不可堆叠——两本叠一格会让融合消耗语义含糊");
        }

        [Test]
        public void 附魔台物品图标贴图_已入库()
        {
            string texturesDir = Path.Combine(LocateStreamingAssetsSubdirectory("items"), "textures");

            Assert.That(File.Exists(Path.Combine(texturesDir, "enchanting_table.png")), Is.True,
                "items/textures/enchanting_table.png 必须已入库——缺了背包里显示品红占位块");
        }

        // ─── 掉落：挖掉附魔台回自身物品 ────────────────────────────────────

        [Test]
        public void 掉落_附魔台挖掉回自身物品()
        {
            var drops = _drops.DropsFor(EnchantingTableBlockId);

            Assert.That(drops.Length, Is.EqualTo(1), "附魔台应恰有一条掉落");
            Assert.That(drops[0].ItemId, Is.EqualTo(EnchantingTableItemId),
                "挖掉附魔台应掉回附魔台物品，孩子搭错拆掉不该血本无归");
        }

        // ─── 两条配方：真表加载可匹配 ──────────────────────────────────────

        [Test]
        public void 配方_书加青金石_2x2合成附魔书()
        {
            int book = _items.GetById("book").NumericId;
            int lapis = _items.GetById("lapis").NumericId;
            var input = new List<ItemStack>
            {
                new ItemStack(book, 1), new ItemStack(lapis, 1),
                ItemStack.Empty, ItemStack.Empty,
            };

            var recipe = _recipes.FindMatch(input, width: 2, height: 2);

            Assert.That(recipe, Is.Not.Null, "书 + 青金石应能合成附魔书");
            Assert.That(recipe.Id, Is.EqualTo("enchanted_book_recipe"));
            Assert.That(recipe.Output.ItemId, Is.EqualTo(_items.GetById("enchanted_book").NumericId),
                "产物是附魔书（未鉴定：Metadata=0，融合时确定性掷类型）");
            Assert.That(recipe.Output.Count, Is.EqualTo(1), "一次合 1 本");
        }

        [Test]
        public void 配方_书青金石双木板_2x2合成附魔台()
        {
            int book = _items.GetById("book").NumericId;
            int lapis = _items.GetById("lapis").NumericId;
            int plank = _items.GetById("plank").NumericId;
            var input = new List<ItemStack>
            {
                new ItemStack(book, 1), new ItemStack(lapis, 1),
                new ItemStack(plank, 1), new ItemStack(plank, 1),
            };

            var recipe = _recipes.FindMatch(input, width: 2, height: 2);

            Assert.That(recipe, Is.Not.Null, "书 + 青金石 + 木板×2 应能合成附魔台");
            Assert.That(recipe.Id, Is.EqualTo("enchanting_table_recipe"));
            Assert.That(recipe.Output.ItemId, Is.EqualTo(EnchantingTableItemId));
            Assert.That(recipe.Output.Count, Is.EqualTo(1));
        }

        // ─── 目录定位（dotnet 从输出目录向上爬；Unity 走 streamingAssetsPath） ──

        private static string LocateStreamingAssetsSubdirectory(string name)
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, name);
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", name);
                if (Directory.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException($"未能从测试输出目录向上找到 Assets/StreamingAssets/{name}。");
#endif
        }
    }
}
