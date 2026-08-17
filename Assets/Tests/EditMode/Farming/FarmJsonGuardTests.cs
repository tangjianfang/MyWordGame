using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Farming;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Farming
{
    /// <summary>
    /// m11 W1-6 守卫：仓库里真实的农业 JSON（11 方块 + 9 物品 + 6 配方）与
    /// <see cref="FarmSystem"/> 的常量/物品表严格一致。照
    /// <c>BlockDefinitionFilesTests</c> 的真实文件端到端模式——写坏 JSON 这里立刻红。
    /// </summary>
    [TestFixture]
    public class FarmJsonGuardTests
    {
        private BlockRegistry _blocks;
        private ItemDatabase _items;
        private RecipeDatabase _recipes;

        [OneTimeSetUp]
        public void LoadRealDefinitions()
        {
            string blocksDir = LocateDirectory("blocks");
            string itemsDir = LocateDirectory("items");
            string recipesDir = LocateDirectory("recipes");

            _blocks = BlockRegistry.FromJson(Directory.GetFiles(blocksDir, "*.json").Select(File.ReadAllText));
            _items = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));
            _recipes = RecipeDatabase.FromJson(
                Directory.GetFiles(recipesDir, "*.json").Select(File.ReadAllText), _items);
        }

        // ─── 方块：耕地 ×2 + 作物 ×9 ─────────────────────────────────

        [Test]
        public void 耕地方块_numericId与FarmSystem常量一致()
        {
            Assert.That(_blocks.GetById("farmland").NumericId, Is.EqualTo(FarmSystem.FarmlandId),
                "farmland.json 的 numericId 必须与 FarmSystem.FarmlandId 一致（锄地写的就是这个数）");
            Assert.That(_blocks.GetById("farmland_wet").NumericId, Is.EqualTo(FarmSystem.FarmlandWetId),
                "farmland_wet.json 的 numericId 必须与 FarmSystem.FarmlandWetId 一致（湿地加速判定读它）");
        }

        [Test]
        public void 耕地_实心且不透明_Solid同泥土()
        {
            foreach (string id in new[] { "farmland", "farmland_wet" })
            {
                Assert.That(_blocks.GetById(id).Solid, Is.True, $"{id} 必须实心（Solid 同泥土，玩家踩得住）");
                Assert.That(_blocks.GetById(id).Opaque, Is.True, $"{id} 必须不透明（整方块渲染，别漏面）");
                Assert.That(_blocks.GetById(id).MinToolTier, Is.EqualTo(0), $"{id} 徒手可挖（收回泥土的门槛）");
                Assert.That(_blocks.GetById(id).Hardness, Is.GreaterThan(0f).And.LessThan(2f),
                    $"{id} 硬度照 MC 耕地 0.6 一档");
            }
        }

        [Test]
        public void 作物九方块_全部注册且numericId连续对齐()
        {
            (CropKind crop, int stage)[] all =
            {
                (CropKind.Wheat, 0), (CropKind.Wheat, 1), (CropKind.Wheat, 2),
                (CropKind.Beet, 0), (CropKind.Beet, 1), (CropKind.Beet, 2),
                (CropKind.Mung, 0), (CropKind.Mung, 1), (CropKind.Mung, 2),
            };

            foreach ((CropKind crop, int stage) in all)
            {
                ushort expected = FarmSystem.StageBlockId(crop, stage);
                Assert.That(FarmSystem.TryParseStageBlock(expected, out CropKind parsed, out int parsedStage),
                    Is.True, "StageBlockId/TryParseStageBlock 必须自洽（往返能解回作物与阶段）");
                Assert.That((parsed, parsedStage), Is.EqualTo((crop, stage)));
            }
        }

        [Test]
        public void 作物_不挡移动不挡视线_硬度0秒挖()
        {
            foreach (string id in new[]
                     {
                         "wheat_stage0", "wheat_stage1", "wheat_stage2",
                         "beet_stage0", "beet_stage1", "beet_stage2",
                         "mung_stage0", "mung_stage1", "mung_stage2",
                     })
            {
                var def = _blocks.GetById(id);
                Assert.That(_blocks.TryGetByNumericId(def.NumericId, out _), Is.True,
                    $"{id} 必须已注册（缺文件或 numericId 对不上）");
                Assert.That(def.Solid, Is.False, $"{id} 是十字类作物，不挡玩家移动");
                Assert.That(def.Opaque, Is.False, $"{id} 不挡视线（否则脚下作物把地形面剔没了）");
                Assert.That(def.Hardness, Is.EqualTo(0f), $"{id} 一碰就掉（作物硬度 0）");
                Assert.That(def.MinToolTier, Is.EqualTo(0), $"{id} 徒手可收");
            }
        }

        // ─── 物品：锄 ×4 + 小麦/种子 ×4 + 骨粉 ───────────────────────

        [Test]
        public void 四把锄_属性阶梯与镐同制()
        {
            (string id, int tier, int durability, float attack)[] expected =
            {
                ("hoe_wooden", 1, 59, 2f),
                ("hoe_stone", 2, 131, 3f),
                ("hoe_iron", 3, 250, 4f),
                ("hoe_diamond", 4, 255, 5f),
            };

            foreach ((string id, int tier, int durability, float attack) in expected)
            {
                Assert.That(_items.TryGetById(id, out var def), Is.True, $"{id} 应在真实物品表中");
                Assert.That(def.ToolTier, Is.EqualTo(tier), $"{id} 的 toolTier 应为 {tier}（守卫测试要求显式声明）");
                Assert.That(def.MaxDurability, Is.EqualTo(durability),
                    $"{id} 的 maxDurability 应为 {durability}（照镐的 MC 值阶梯，钻封顶 255）");
                Assert.That(def.AttackDamage, Is.EqualTo(attack), $"{id} 攻击力照同档镐");
                Assert.That(def.MaxStack, Is.EqualTo(1), $"{id} 是耐久工具，不可堆叠");
                Assert.That(def.IsTool, Is.True, $"{id} 必须标 isTool（耐久条/加速判定走它）");
            }
        }

        [Test]
        public void 作物物品_小麦与三种种子已注册_种子不可食用()
        {
            // 种子右键 = 播种而不是吃——IsEdible 必须为 false（右键路由食物优先， edible 会抢走播种）
            foreach (string id in new[] { "seeds_wheat", "seeds_beet", "seeds_mung" })
            {
                Assert.That(_items.TryGetById(id, out var def), Is.True, $"{id} 应在真实物品表中");
                Assert.That(def.IsEdible, Is.False, $"{id} 不是食物——右键要留给播种");
            }

            Assert.That(_items.TryGetById("wheat", out var wheat), Is.True, "小麦应注册（收获产物）");
            Assert.That(wheat.IsEdible, Is.False, "小麦本体不可直接吃（面包线后续接）");

            Assert.That(_items.TryGetById("bone_meal", out var meal), Is.True, "骨粉应注册");
            Assert.That(meal.IsEdible, Is.False, "骨粉不是食物——右键要留给催熟");
        }

        [Test]
        public void 骨粉_喂食表引用的饲料全部已注册()
        {
            // BreedingSystem.FeedItemFor 返回的 itemId 必须真实存在，否则右键喂食永远无效
            MobKind[] breedable =
            {
                MobKind.Pig, MobKind.Cow, MobKind.Chicken, MobKind.Sheep, MobKind.Rabbit,
                MobKind.Fox, MobKind.Deer, MobKind.Panda, MobKind.Penguin, MobKind.Goat,
                MobKind.Raccoon, MobKind.Hamster,
            };
            foreach (MobKind kind in breedable)
            {
                string feed = BreedingSystem.FeedItemFor(kind);
                Assert.That(feed, Is.Not.Null, $"{kind} 必须有饲料");
                Assert.That(_items.TryGetById(feed, out _), Is.True,
                    $"{kind} 的饲料 {feed} 未注册——跨表引用悬空");
            }
        }

        // ─── 配方：锄 ×4 + 小麦→种子 + 骨头→骨粉 ─────────────────────

        [Test]
        public void 四把锄配方_工作台三宽两行_材料行加手柄()
        {
            (string recipeId, string outputId, string materialId)[] expected =
            {
                ("hoe_wooden_recipe", "hoe_wooden", "plank"),
                ("hoe_stone_recipe", "hoe_stone", "cobblestone"),
                ("hoe_iron_recipe", "hoe_iron", "iron_ingot"),
                ("hoe_diamond_recipe", "hoe_diamond", "diamond"),
            };

            foreach ((string recipeId, string outputId, string materialId) in expected)
            {
                Recipe recipe = _recipes.All.FirstOrDefault(r => r.Id == recipeId);
                Assert.That(recipe, Is.Not.Null, $"{recipeId} 缺失");
                Assert.That(recipe.Tier, Is.EqualTo(CraftingTier.Workbench3x3), "锄照镐走工作台档");
                Assert.That(recipe.Width, Is.EqualTo(3));
                Assert.That(recipe.Height, Is.EqualTo(2), "锄是两行三材换形（材料×2 + 手柄）");
                Assert.That(recipe.Shaped, Is.True);
                Assert.That(recipe.Output.ItemId, Is.EqualTo(_items.GetById(outputId).NumericId),
                    $"{recipeId} 的输出应是 {outputId}");

                // 形状：上排 材料 材料 空，下排 空 木棍 空
                int materialNumeric = _items.GetById(materialId).NumericId;
                int stickNumeric = _items.GetById("stick").NumericId;
                Assert.That(recipe.Pattern[0].ItemId, Is.EqualTo(materialNumeric), "左上角是材料");
                Assert.That(recipe.Pattern[1].ItemId, Is.EqualTo(materialNumeric), "中上是材料");
                Assert.That(recipe.Pattern[2].IsEmpty, Is.True, "右上角留空");
                Assert.That(recipe.Pattern[3].IsEmpty, Is.True, "下排左留空");
                Assert.That(recipe.Pattern[4].ItemId, Is.EqualTo(stickNumeric), "下排中间是木棍手柄");
                Assert.That(recipe.Pattern[5].IsEmpty, Is.True, "下排右留空");
            }
        }

        [Test]
        public void 小麦转种子配方_口袋档_一换二()
        {
            Recipe recipe = _recipes.All.FirstOrDefault(r => r.Id == "wheat_to_seeds_recipe");
            Assert.That(recipe, Is.Not.Null, "wheat_to_seeds_recipe 缺失（小麦种子的手工合成入口）");
            Assert.That(recipe.Tier, Is.EqualTo(CraftingTier.Pocket1x1), "随身口袋即可搓种子（照 log_to_planks 档位）");
            Assert.That(recipe.Output.ItemId, Is.EqualTo(_items.GetById("seeds_wheat").NumericId));
            Assert.That(recipe.Output.Count, Is.EqualTo(2), "一株小麦搓两把种子");
            Assert.That(recipe.Pattern[0].ItemId, Is.EqualTo(_items.GetById("wheat").NumericId));
        }

        [Test]
        public void 骨头磨骨粉配方_一口袋骨头出三份()
        {
            Recipe recipe = _recipes.All.FirstOrDefault(r => r.Id == "bone_to_bone_meal_recipe");
            Assert.That(recipe, Is.Not.Null, "bone_to_bone_meal_recipe 缺失（骨粉获取入口）");
            Assert.That(recipe.Output.ItemId, Is.EqualTo(_items.GetById("bone_meal").NumericId));
            Assert.That(recipe.Output.Count, Is.EqualTo(3), "一根骨头磨三份骨粉");
            Assert.That(recipe.Pattern[0].ItemId, Is.EqualTo(_items.GetById("bone").NumericId));
        }

        // ─── 定位 StreamingAssets（照 BlockDefinitionFilesTests 的走法）──

        private static string LocateDirectory(string sub)
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
