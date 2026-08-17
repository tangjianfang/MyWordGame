using System;
using System.IO;
using System.Linq;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    /// <summary>
    /// m10 C2：进阶合成链的真数据端到端——升级配方 + 六件新装备的基底配方 + 金锭注册。
    ///
    /// 升级规则（数值决策，见 task-C2-report.md）：**同材料 2 件 → 1 件、加成翻倍**——
    /// 铁防 +1→+2 / 金攻 attackDamage +1→+2 / 合金移速 +5%→+10% / 机元血上限 +2→+4，
    /// 与 spec §3「+1→+2」示例一致。产物是独立物品（<c>*_plus</c>，displayName「XX+1」），
    /// miningLevel / toolTier / maxDurability 与基底一致——升级只升加成，不升挖掘档位。
    ///
    /// 读仓库真实 JSON（与 RecipeFilesTests / BlockGatingTests 同模式），dotnet 与
    /// EditMode 双链跑；改坏任何一份 JSON 这里先红，轮不到进游戏炸 WorldBootstrap。
    /// </summary>
    [TestFixture]
    public class GearUpgradeRecipeTests
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

        /// <summary>四系剑 + 四系镐的（基底，升级件）配对——升级配方的全量清单。</summary>
        private static readonly (string Base, string Plus)[] UpgradePairs =
        {
            ("iron_sword", "iron_sword_plus"),
            ("gold_sword", "gold_sword_plus"),
            ("summer_alloy_sword", "summer_alloy_sword_plus"),
            ("machine_essence_sword", "machine_essence_sword_plus"),
            ("iron_pickaxe", "iron_pickaxe_plus"),
            ("gold_pickaxe", "gold_pickaxe_plus"),
            ("summer_alloy_pickaxe", "summer_alloy_pickaxe_plus"),
            ("machine_essence_pickaxe", "machine_essence_pickaxe_plus"),
        };

        [Test]
        public void RealRecipes_EightUpgradeRecipes_TwoSameItemsToOnePlus()
        {
            foreach ((string baseId, string plusId) in UpgradePairs)
            {
                string recipeId = baseId + "_upgrade";
                Recipe r = _recipes.All.FirstOrDefault(x => x.Id == recipeId);
                Assert.That(r, Is.Not.Null, $"应有升级配方 {recipeId}（recipes/{recipeId}.json）");
                Assert.That(r.Tier, Is.EqualTo(CraftingTier.Workbench3x3),
                    $"{recipeId} 沿用工作台 3x3 档位");
                Assert.That(r.Shaped, Is.False,
                    $"{recipeId} 应为无序配方——两件同物品任意摆放都能合，孩子不用记位置");

                var nonEmpty = r.Pattern.Where(p => !p.IsEmpty).ToArray();
                Assert.That(nonEmpty.Length, Is.EqualTo(2), $"{recipeId} 的 pattern 应恰含两件物品");
                int baseNumeric = _items.GetById(baseId).NumericId;
                Assert.That(nonEmpty[0].ItemId, Is.EqualTo(baseNumeric), $"{recipeId} 第一件是 {baseId}");
                Assert.That(nonEmpty[1].ItemId, Is.EqualTo(baseNumeric), $"{recipeId} 第二件也是 {baseId}（同材料）");

                Assert.That(r.Output.ItemId, Is.EqualTo(_items.GetById(plusId).NumericId),
                    $"{recipeId} 的产物应是升级件 {plusId}");
                Assert.That(r.Output.Count, Is.EqualTo(1), "两件合一件");
            }
        }

        [Test]
        public void RealItems_UpgradedGear_DoublesBaseBonus()
        {
            foreach ((string baseId, string plusId) in UpgradePairs)
            {
                ItemDefinition b = _items.GetById(baseId);
                ItemDefinition p = _items.GetById(plusId);
                Assert.That(p, Is.Not.Null, $"{plusId} 应已注册（items/{plusId}.json）");

                if (b.GearStat == GearStat.None)
                {
                    // 金系：攻击走 attackDamage 通道（m10 C1 的分工），升级 = +1→+2
                    Assert.That(b.AttackDamage, Is.Not.Null, $"{baseId} 应有 attackDamage（金系攻击通道）");
                    Assert.That(p.AttackDamage, Is.EqualTo(b.AttackDamage + 1f).Within(1e-6f),
                        $"{plusId} 攻击应比基底 +1（两件合一件、加成翻倍）");
                    Assert.That(p.GearStat, Is.EqualTo(GearStat.None), $"{plusId} 金系不走 gearBonus 通道");
                }
                else
                {
                    // 铁防 / 合金移速 / 机元血上限：amount 恰为基底两倍
                    Assert.That(p.GearStat, Is.EqualTo(b.GearStat), $"{plusId} 加成类别与基底一致");
                    Assert.That(p.GearAmount, Is.EqualTo(b.GearAmount * 2f).Within(1e-6f),
                        $"{plusId} 加成应为基底两倍（两件合一件）");
                }

                // 档位不漂移：升级只升加成，挖掘档位与耐久照旧
                Assert.That(p.MiningLevel, Is.EqualTo(b.MiningLevel), $"{plusId} miningLevel 与基底一致");
                Assert.That(p.ToolTier, Is.EqualTo(b.ToolTier), $"{plusId} toolTier 与基底一致");
                Assert.That(p.MaxDurability, Is.EqualTo(b.MaxDurability), $"{plusId} maxDurability 与基底一致");
                Assert.That(p.MaxStack, Is.EqualTo(1), $"{plusId} 是装备，不可堆叠");
                Assert.That(p.IsTool, Is.True, $"{plusId} 仍是工具");
            }
        }

        [Test]
        public void FindMatch_TwoSwordsAnywhere_CraftUpgrade_ConsumesTwo()
        {
            int ironSword = _items.GetById("iron_sword").NumericId;

            // 3x3 工作台网格，两把铁剑放在对角（无序配方不挑位置）
            var grid = new ItemStack[9];
            grid[0] = new ItemStack(ironSword, 1);
            grid[8] = new ItemStack(ironSword, 1);

            Recipe match = _recipes.FindMatch(grid, 3, 3);
            Assert.That(match, Is.Not.Null, "两把铁剑应合出升级配方");
            Assert.That(match.Id, Is.EqualTo("iron_sword_upgrade"));

            int[] consumed = CraftingMatrix.Consume(match, grid, 3);
            Assert.That(consumed.Sum(), Is.EqualTo(2), "升级恰好消耗两把铁剑");
        }

        [Test]
        public void FindMatch_SingleSword_DoesNotMatchAnyRecipe()
        {
            int ironSword = _items.GetById("iron_sword").NumericId;
            var grid = new ItemStack[9];
            grid[4] = new ItemStack(ironSword, 1);

            Assert.That(_recipes.FindMatch(grid, 3, 3), Is.Null,
                "单把铁剑不该触发任何配方——升级必须两件同物品");
        }

        [Test]
        public void FindMatch_DamagedGear_DoesNotCraftUpgrade_AntiDurabilityLaundering()
        {
            // m10 C2 fix1（I3）：残血装备 ×2 合成出满耐久 *_plus = 无限洗耐久漏洞。
            // 规则：带耐久编码的材料必须满耐久；未启用编码（旧档/刚合成，Metadata=0）
            // 按满耐久放行（ItemStack 既有语义）
            int ironPick = _items.GetById("iron_pickaxe").NumericId;
            const int max = 250;

            var damaged = new ItemStack[9];
            damaged[0] = new ItemStack(ironPick, 1).WithMaxDurability(max);
            damaged[8] = new ItemStack(ironPick, 1).WithMaxDurability(max).WithDurabilityUsed(max);  // 挖过一下：249/250
            Assert.That(_recipes.FindMatch(damaged, 3, 3), Is.Null,
                "残血镐 ×2 不该合出升级件——升级不是免费维修机");
            Assert.That(CraftingMatrix.HasDamagedMaterial(damaged), Is.True,
                "网格里有残血材料，UI 提示的判定源应为真");

            var full = new ItemStack[9];
            full[0] = new ItemStack(ironPick, 1).WithMaxDurability(max);
            full[8] = new ItemStack(ironPick, 1).WithMaxDurability(max);
            Recipe fullMatch = _recipes.FindMatch(full, 3, 3);
            Assert.That(fullMatch?.Id, Is.EqualTo("iron_pickaxe_upgrade"), "两把满耐久镐照常升级");
            Assert.That(CraftingMatrix.HasDamagedMaterial(full), Is.False);

            // 未启用耐久编码的镐（刚合成 / 旧档）视同满耐久，不因缺编码被拒
            var unencoded = new ItemStack[9];
            unencoded[0] = new ItemStack(ironPick, 1);
            unencoded[8] = new ItemStack(ironPick, 1);
            Assert.That(_recipes.FindMatch(unencoded, 3, 3)?.Id, Is.EqualTo("iron_pickaxe_upgrade"),
                "Metadata=0（未启用耐久）按满耐久放行——与 m10 B1 的存量兼容语义一致");
        }

        [Test]
        public void RealRecipes_SixBaseGearRecipes_CraftNewGearFromMaterials()
        {
            // (配方 id, 产物, 材料, 材料数, 柄数)：剑 1x3 竖排两材一柄 / 镐 3x2 三材两柄，
            // 与既有 iron_sword_recipe / iron_pickaxe_recipe 同构
            (string recipeId, string output, string material, int materialCount, int stickCount)[] expected =
            {
                ("gold_sword_recipe", "gold_sword", "gold_ingot", 2, 1),
                ("summer_alloy_sword_recipe", "summer_alloy_sword", "summer_alloy", 2, 1),
                ("machine_essence_sword_recipe", "machine_essence_sword", "machine_essence", 2, 1),
                ("gold_pickaxe_recipe", "gold_pickaxe", "gold_ingot", 3, 2),
                ("summer_alloy_pickaxe_recipe", "summer_alloy_pickaxe", "summer_alloy", 3, 2),
                ("machine_essence_pickaxe_recipe", "machine_essence_pickaxe", "machine_essence", 3, 2),
            };

            int stick = _items.GetById("stick").NumericId;
            foreach ((string recipeId, string output, string material, int materialCount, int stickCount) in expected)
            {
                Recipe r = _recipes.All.FirstOrDefault(x => x.Id == recipeId);
                Assert.That(r, Is.Not.Null, $"应有基底配方 {recipeId}——没有它，金锭只进不出");
                Assert.That(r.Tier, Is.EqualTo(CraftingTier.Workbench3x3), $"{recipeId} 工作台配方");
                Assert.That(r.Output.ItemId, Is.EqualTo(_items.GetById(output).NumericId),
                    $"{recipeId} 产物应是 {output}");

                int matId = _items.GetById(material).NumericId;
                Assert.That(r.Pattern.Count(p => p.ItemId == matId), Is.EqualTo(materialCount),
                    $"{recipeId} 应含 {materialCount} 个 {material}");
                Assert.That(r.Pattern.Count(p => p.ItemId == stick), Is.EqualTo(stickCount),
                    $"{recipeId} 应含 {stickCount} 根木棍");
            }
        }

        [Test]
        public void RealItems_GoldIngot_RegisteredAsFurnaceOutput()
        {
            // 熔炉粗金→金锭的输出契约：金锭必须已注册，且 numericId 与 FurnaceSystem 常量一致
            var def = _items.GetById("gold_ingot");
            Assert.That(def.MaxStack, Is.EqualTo(64), "锭类可堆叠");
            Assert.That(def.NumericId, Is.EqualTo(1027),
                "金锭 numericId=1027（m10 材料段顺延；1005 已被 diamond 占用，brief 的「1005」是笔误）");
            Assert.That(FurnaceSystem.GoldIngotItemId, Is.EqualTo(def.NumericId),
                "FurnaceSystem.GoldIngotItemId 应与物品表一致——漂移则烧出来的是未知物品");
            Assert.That(FurnaceSystem.RawGoldItemId, Is.EqualTo(_items.GetById("raw_gold").NumericId),
                "FurnaceSystem.RawGoldItemId 应与物品表 raw_gold 一致");
            Assert.That(FurnaceSystem.RawIronItemId, Is.EqualTo(_items.GetById("raw_iron").NumericId),
                "FurnaceSystem.RawIronItemId 应与物品表 raw_iron 一致");
        }

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
