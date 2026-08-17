// m11 W3-3：下界合金盔甲升级链——4 件装备 + 4 张升级配方的真数据端到端。
// 升级语义与 m10 C2 的「同材料 2 件合 *_plus」不同：netherite 件是**新物品**
//（netherite_ingot + 铁件 → netherite 件，MC 锻造模板思路的简化），
// gearBonus 照铁系翻倍（铁防 1/2/2/1 → netherite 防 2/4/4/2）。
// 读仓库真实 JSON（GearUpgradeRecipeTests 同模式），dotnet 与 EditMode 双链跑。
using System;
using System.IO;
using System.Linq;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class NetheriteArmorTests
    {
        private ItemDatabase _items;
        private RecipeDatabase _recipes;

        [OneTimeSetUp]
        public void LoadRealDefinitions()
        {
            string itemsDir = LocateDirectory("items");
            _items = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));

            string recipesDir = LocateDirectory("recipes");
            _recipes = RecipeDatabase.FromJson(
                Directory.GetFiles(recipesDir, "*.json").Select(File.ReadAllText), _items);
        }

        /// <summary>四件配对：铁基底 → netherite 产物（部位一一对应）。</summary>
        private static readonly (string Iron, string Netherite, ArmorPart Part, float IronAmount)[] Pairs =
        {
            ("iron_helmet", "netherite_helmet", ArmorPart.Helmet, 1f),
            ("iron_chest", "netherite_chest", ArmorPart.Chest, 2f),
            ("iron_legs", "netherite_legs", ArmorPart.Legs, 2f),
            ("iron_boots", "netherite_boots", ArmorPart.Boots, 1f),
        };

        [Test]
        public void 真物品表_Netherite四件注册_部位正确_防御照铁系翻倍()
        {
            foreach (var (ironId, netheriteId, part, ironAmount) in Pairs)
            {
                ItemDefinition def = _items.GetById(netheriteId);
                Assert.That(def, Is.Not.Null, $"{netheriteId} 应已注册（items/{netheriteId}.json）");
                Assert.That(def.ArmorPart, Is.EqualTo(part), $"{netheriteId} 部位 = {part}");
                Assert.That(def.GearStat, Is.EqualTo(GearStat.Defense), $"{netheriteId} 走 defense 通道（照铁系）");
                Assert.That(def.GearAmount, Is.EqualTo(ironAmount * 2f),
                    $"{netheriteId} 防御 = 铁件（{ironAmount}）翻倍");
                Assert.That(def.MaxStack, Is.EqualTo(1), $"{netheriteId} 盔甲不可堆叠");
            }
        }

        [Test]
        public void 真配方表_四张升级配方_一锭一铁件合一件Netherite()
        {
            foreach (var (ironId, netheriteId, _, _) in Pairs)
            {
                string recipeId = "armor_netherite_" + PartWord(ironId) + "_upgrade";
                Recipe r = _recipes.All.FirstOrDefault(x => x.Id == recipeId);
                Assert.That(r, Is.Not.Null, $"应有升级配方 {recipeId}（recipes/{recipeId}.json）");
                Assert.That(r.Tier, Is.EqualTo(CraftingTier.Workbench3x3), $"{recipeId} 工作台档位");
                Assert.That(r.Shaped, Is.False, $"{recipeId} 无序配方——孩子不用记摆放位置");

                var nonEmpty = r.Pattern.Where(p => !p.IsEmpty).ToArray();
                Assert.That(nonEmpty.Length, Is.EqualTo(2), $"{recipeId} 恰含两件材料");
                int ingot = _items.GetById("netherite_ingot").NumericId;
                int iron = _items.GetById(ironId).NumericId;
                Assert.That(nonEmpty.Any(p => p.ItemId == ingot), Is.True, $"{recipeId} 含 1 个 netherite_ingot");
                Assert.That(nonEmpty.Any(p => p.ItemId == iron), Is.True, $"{recipeId} 含 1 个 {ironId}");
                Assert.That(r.Output.ItemId, Is.EqualTo(_items.GetById(netheriteId).NumericId),
                    $"{recipeId} 产物 = {netheriteId}");
                Assert.That(r.Output.Count, Is.EqualTo(1), "两件合一件");
            }
        }

        [Test]
        public void FindMatch_一锭一头盔任意摆_合成Netherite头盔_消耗两件()
        {
            int ingot = _items.GetById("netherite_ingot").NumericId;
            int ironHelmet = _items.GetById("iron_helmet").NumericId;
            int netheriteHelmet = _items.GetById("netherite_helmet").NumericId;

            // 3x3 工作台网格，两件材料放对角（无序配方不挑位置）
            var grid = new ItemStack[9];
            grid[0] = new ItemStack(ingot, 1);
            grid[8] = new ItemStack(ironHelmet, 1);

            Recipe match = _recipes.FindMatch(grid, 3, 3);
            Assert.That(match, Is.Not.Null, "锭 + 铁头盔应合出 netherite 头盔");
            Assert.That(match.Id, Is.EqualTo("armor_netherite_helmet_upgrade"));
            Assert.That(match.Output.ItemId, Is.EqualTo(netheriteHelmet));

            int[] consumed = CraftingMatrix.Consume(match, grid, 3);
            Assert.That(consumed.Sum(), Is.EqualTo(2), "恰好消耗两件材料");
        }

        [Test]
        public void FindMatch_只放一锭_不合任何配方()
        {
            int ingot = _items.GetById("netherite_ingot").NumericId;
            var grid = new ItemStack[9];
            grid[4] = new ItemStack(ingot, 1);

            Assert.That(_recipes.FindMatch(grid, 3, 3), Is.Null,
                "单锭不该触发任何配方——升级必须锭 + 铁件成对");
        }

        /// <summary>iron_helmet → helmet（配方 id 的部位词）。</summary>
        private static string PartWord(string ironId)
            => ironId.Substring("iron_".Length);

        /// <summary>与 RecipeFilesTests 同款的双链目录定位（dotnet 从输出目录向上爬）。</summary>
        private static string LocateDirectory(string subdir)
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, subdir);
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", subdir);
                if (Directory.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException($"未能从测试输出目录向上找到 Assets/StreamingAssets/{subdir}。");
#endif
        }
    }
}
