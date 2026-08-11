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
    }
}
