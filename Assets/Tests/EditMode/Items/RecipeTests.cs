using System.Collections.Generic;
using System.Linq;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class RecipeTests
    {
        private const string SwordJson = @"{""id"":""wooden_sword"",""maxStack"":1,""texture"":""wooden_sword""}";
        private const string StickJson = @"{""id"":""stick"",""maxStack"":64,""texture"":""stick""}";
        private const string PlankJson = @"{""id"":""plank"",""maxStack"":64,""texture"":""plank""}";

        private const string LogJson = @"{""id"":""log"",""maxStack"":64,""texture"":""log""}";

        private const string PlankRecipe = @"{
            ""id"":""log_to_planks"",
            ""tier"":""pocket"",
            ""width"":1, ""height"":1,
            ""pattern"":[""log""],
            ""output"":{""item"":""plank"",""count"":4}
        }";

        private const string SwordRecipe = @"{
            ""id"":""wooden_sword_recipe"",
            ""tier"":""workbench"",
            ""width"":1, ""height"":3,
            ""pattern"":[""plank"",""plank"",""stick""],
            ""output"":{""item"":""wooden_sword"",""count"":1}
        }";

        private ItemDatabase _db;
        private RecipeDatabase _recipes;

        [SetUp]
        public void SetUp()
        {
            _db = ItemDatabase.FromJson(new[] { SwordJson, StickJson, PlankJson, LogJson });
            _recipes = RecipeDatabase.FromJson(new[] { PlankRecipe, SwordRecipe }, _db);
        }

        [Test]
        public void Pocket_Recipe_Matches_1x1()
        {
            var logDef = _db.ById["log"];
            var input = new[] { new ItemStack(logDef.NumericId, 1) };
            var r = _recipes.FindMatch(input, 1, 1);
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Tier, Is.EqualTo(CraftingTier.Pocket1x1));
        }

        [Test]
        public void Pocket_Recipe_DoesNotMatch_WrongItem()
        {
            var sword = _db.ById["wooden_sword"];
            var input = new[] { new ItemStack(sword.NumericId, 1) };
            var r = _recipes.FindMatch(input, 1, 1);
            Assert.That(r, Is.Null);
        }

        [Test]
        public void Workbench_Recipe_Matches_3x3SubArea()
        {
            // 配方是 1×3 垂直：[plank, plank, stick]，放在 3×3 网格的左列
            var plank = _db.ById["plank"];
            var stick = _db.ById["stick"];
            var air = new ItemStack(0, 0);
            var input = new[] {
                new ItemStack(plank.NumericId, 1), air, air,
                new ItemStack(plank.NumericId, 1), air, air,
                new ItemStack(stick.NumericId, 1),  air, air,
            };
            var r = _recipes.FindMatch(input, 3, 3);
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Id, Is.EqualTo("wooden_sword_recipe"));
        }

        [Test]
        public void Workbench_Recipe_DoesNotMatch_On2x2()
        {
            var plank = _db.ById["plank"];
            var stick = _db.ById["stick"];
            // 2x2 没有 3 高的 pattern，匹配不到 workbench
            var input = new[] {
                new ItemStack(plank.NumericId, 1), new ItemStack(plank.NumericId, 1),
                new ItemStack(stick.NumericId, 1), new ItemStack(stick.NumericId, 1),
            };
            var r = _recipes.FindMatch(input, 2, 2);
            Assert.That(r, Is.Null);
        }

        [Test]
        public void Consume_Workbench_DeductsPattern()
        {
            var plank = _db.ById["plank"];
            var stick = _db.ById["stick"];
            var air = new ItemStack(0, 0);
            var input = new[] {
                new ItemStack(plank.NumericId, 1), air, air,
                new ItemStack(plank.NumericId, 1), air, air,
                new ItemStack(stick.NumericId, 1),  air, air,
            };
            var r = _recipes.FindMatch(input, 3, 3);
            Assert.That(r, Is.Not.Null);
            var consumed = CraftingMatrix.Consume(r, input, 3);
            int sum = consumed.Sum();
            Assert.That(sum, Is.EqualTo(3), "1+1+1=3 个输入被消耗");
        }

        // ---- m11 ②（B 批守卫修复）：FindMatch 同档位稳定排序 ----

        private const string TableOutJson = @"{""id"":""crafting_table"",""maxStack"":64,""texture"":""table""}";
        private const string DoorOutJson = @"{""id"":""wooden_door"",""maxStack"":64,""texture"":""door""}";
        private const string PlankPileOutJson = @"{""id"":""plank_pile"",""maxStack"":64,""texture"":""pile""}";

        /// <summary>
        /// 复刻真实事故「6 板门被 4 板工作台 shapeless 抢匹配」的最小冲突对：
        /// shapeless 只数材料总数，6 ≥ 4 恒真。这里<b>刻意</b>把 shapeless 的插入顺序与
        /// id 字母序都排在 shaped 前面——修复靠的是「shaped 优先 + 材料多者优先」的
        /// 稳定排序，不再依赖配方文件的枚举顺序（文件系统实现细节，跨机器不可靠）。
        /// </summary>
        [Test]
        public void FindMatch_ShapedWinsOverShapeless_RegardlessOfInsertionOrder()
        {
            var db = ItemDatabase.FromJson(new[] { PlankJson, TableOutJson, DoorOutJson });
            string shapelessTable = @"{
                ""id"":""a_table"", ""tier"":""workbench"", ""width"":3, ""height"":3,
                ""pattern"":[""plank"",""plank"",""_"",""plank"",""plank"",""_"",""_"",""_"",""_""],
                ""shaped"":false, ""output"":{""item"":""crafting_table"",""count"":1} }";
            string shapedDoor = @"{
                ""id"":""z_door"", ""tier"":""workbench"", ""width"":3, ""height"":3,
                ""pattern"":[""plank"",""plank"",""plank"",""plank"",""plank"",""plank"",""_"",""_"",""_""],
                ""shaped"":true, ""output"":{""item"":""wooden_door"",""count"":3} }";
            var recipes = RecipeDatabase.FromJson(new[] { shapelessTable, shapedDoor }, db);

            int plank = db.ById["plank"].NumericId;
            var doorInput = new List<ItemStack>
            {
                new ItemStack(plank, 1), new ItemStack(plank, 1), new ItemStack(plank, 1),
                new ItemStack(plank, 1), new ItemStack(plank, 1), new ItemStack(plank, 1),
                ItemStack.Empty, ItemStack.Empty, ItemStack.Empty,
            };

            Assert.That(recipes.FindMatch(doorInput, 3, 3)?.Id, Is.EqualTo("z_door"),
                "6 板摆两行应匹配 shaped 门配方——即使 shapeless 工作台插入在前/id 序在前");

            var tableInput = new List<ItemStack>
            {
                new ItemStack(plank, 1), new ItemStack(plank, 1), ItemStack.Empty,
                new ItemStack(plank, 1), new ItemStack(plank, 1), ItemStack.Empty,
                ItemStack.Empty, ItemStack.Empty, ItemStack.Empty,
            };
            Assert.That(recipes.FindMatch(tableInput, 3, 3)?.Id, Is.EqualTo("a_table"),
                "恰好 4 块木板仍合工作台——门的 shape 不匹配 4 板摆法，不能反向误伤");
        }

        /// <summary>同档位两条 shapeless：材料格多者优先——否则宽匹配（需 4 板）永远抢在
        /// 窄匹配（需 6 板）前面，材料更多的配方形同虚设。</summary>
        [Test]
        public void FindMatch_Shapeless_MoreMaterialsWin()
        {
            var db = ItemDatabase.FromJson(new[] { PlankJson, TableOutJson, PlankPileOutJson });
            string need4 = @"{
                ""id"":""a_table"", ""tier"":""workbench"", ""width"":3, ""height"":3,
                ""pattern"":[""plank"",""plank"",""_"",""plank"",""plank"",""_"",""_"",""_"",""_""],
                ""shaped"":false, ""output"":{""item"":""crafting_table"",""count"":1} }";
            string need6 = @"{
                ""id"":""z_pile"", ""tier"":""workbench"", ""width"":3, ""height"":3,
                ""pattern"":[""plank"",""plank"",""plank"",""plank"",""plank"",""plank"",""_"",""_"",""_""],
                ""shaped"":false, ""output"":{""item"":""plank_pile"",""count"":1} }";
            var recipes = RecipeDatabase.FromJson(new[] { need4, need6 }, db);

            int plank = db.ById["plank"].NumericId;
            var sixPlanks = new List<ItemStack>
            {
                new ItemStack(plank, 1), new ItemStack(plank, 1), new ItemStack(plank, 1),
                new ItemStack(plank, 1), new ItemStack(plank, 1), new ItemStack(plank, 1),
                ItemStack.Empty, ItemStack.Empty, ItemStack.Empty,
            };
            Assert.That(recipes.FindMatch(sixPlanks, 3, 3)?.Id, Is.EqualTo("z_pile"),
                "6 板输入应匹配材料更多的 6 板配方（MaterialCount 降序），而不是插入在前的 4 板宽匹配");
        }

        // ---- m10 C2 fix1（I3）：带耐久的材料必须满耐久才能参与合成 ----

        [Test]
        public void MaterialsWithDurability_MustBeFullToCraft()
        {
            var plank = _db.ById["plank"];
            var stick = _db.ById["stick"];
            var sword = _recipes.All.First(r => r.Id == "wooden_sword_recipe");

            // 满耐久（cur=max 编码）照常匹配
            var full = new[] {
                new ItemStack(plank.NumericId, 1).WithMaxDurability(5),
                new ItemStack(plank.NumericId, 1),
                new ItemStack(stick.NumericId, 1),
            };
            Assert.That(CraftingMatrix.Matches(sword, full, 1), Is.True,
                "满耐久材料（含 Metadata=0 未启用编码的普通栈）不受影响");

            // 残血材料（cur<max）拒绝匹配——shaped 与 shapeless 两条路都要拦
            var damaged = new[] {
                new ItemStack(plank.NumericId, 1).WithMaxDurability(5).WithDurabilityUsed(5),  // 4/5
                new ItemStack(plank.NumericId, 1),
                new ItemStack(stick.NumericId, 1),
            };
            Assert.That(CraftingMatrix.Matches(sword, damaged, 1), Is.False,
                "shaped：残血材料所在格不匹配");
            Assert.That(CraftingMatrix.HasDamagedMaterial(damaged), Is.True,
                "HasDamagedMaterial 是 UI「装备耐久不满不能合成」提示的判定源");
            Assert.That(CraftingMatrix.HasDamagedMaterial(full), Is.False);
        }
    }
}
