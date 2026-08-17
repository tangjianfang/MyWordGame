using System.Linq;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class ItemDatabaseTests
    {
        private const string SwordJson = @"{
            ""id"": ""wooden_sword"",
            ""displayName"": ""木剑"",
            ""numericId"": 100,
            ""maxStack"": 1,
            ""texture"": ""wooden_sword"",
            ""attackDamage"": 4,
            ""isTool"": true,
            ""miningLevel"": 1
        }";

        private const string AppleJson = @"{
            ""id"": ""apple"",
            ""displayName"": ""苹果"",
            ""maxStack"": 64,
            ""texture"": ""apple"",
            ""healAmount"": 4
        }";

        [Test]
        public void Loads_ExplicitId_AsGiven()
        {
            var db = ItemDatabase.FromJson(new[] { SwordJson });
            Assert.That(db.Count, Is.EqualTo(1));
            Assert.That(db.ById["wooden_sword"].NumericId, Is.EqualTo(100));
        }

        [Test]
        public void Loads_AutoId_StartsAt1000()
        {
            var db = ItemDatabase.FromJson(new[] { AppleJson });
            Assert.That(db.ById["apple"].NumericId, Is.EqualTo(1000));
        }

        [Test]
        public void TryGetById_Unknown_ReturnsFalse()
        {
            var db = ItemDatabase.FromJson(new[] { SwordJson });
            Assert.That(db.TryGetById("nonexistent", out _), Is.False);
        }

        [Test]
        public void TryGetByNumericId_Found_ReturnsDef()
        {
            var db = ItemDatabase.FromJson(new[] { SwordJson, AppleJson });
            Assert.That(db.TryGetByNumericId(100, out var def), Is.True);
            Assert.That(def.Id, Is.EqualTo("wooden_sword"));
            Assert.That(def.AttackDamage, Is.EqualTo(4f));
        }

        [Test]
        public void Heal_And_Damage_Populated()
        {
            var db = ItemDatabase.FromJson(new[] { SwordJson, AppleJson });
            Assert.That(db.ById["apple"].HealAmount, Is.EqualTo(4f));
            Assert.That(db.ById["wooden_sword"].IsTool, Is.True);
        }

        [Test]
        public void AutoAssign_IsOrderIndependent()
        {
            var a = ItemDatabase.FromJson(new[] { AppleJson, SwordJson });
            var b = ItemDatabase.FromJson(new[] { SwordJson, AppleJson });
            Assert.That(a.ById["apple"].NumericId, Is.EqualTo(b.ById["apple"].NumericId));
        }

        [Test]
        public void DuplicateId_Throws()
        {
            Assert.That(() => ItemDatabase.FromJson(new[] { SwordJson, SwordJson }),
                Throws.InstanceOf<System.IO.InvalidDataException>());
        }

        // ─── m10 B1：maxDurability 解析（镐耐久上限，1..255） ────────────────

        private const string PickaxeJson = @"{
            ""id"": ""wooden_pickaxe"",
            ""numericId"": 1400,
            ""isTool"": true,
            ""miningLevel"": 1,
            ""toolTier"": 1,
            ""maxDurability"": 59
        }";

        [Test]
        public void MaxDurability_ParsedFromJson()
        {
            var def = ItemDatabase.FromJson(new[] { PickaxeJson }).GetById("wooden_pickaxe");
            Assert.That(def.MaxDurability, Is.EqualTo(59), "maxDurability 应原样进 ItemDefinition");
        }

        [Test]
        public void MaxDurability_Missing_DefaultsToZero()
        {
            var def = ItemDatabase.FromJson(new[] { SwordJson }).GetById("wooden_sword");
            Assert.That(def.MaxDurability, Is.EqualTo(0),
                "未声明 = 无耐久概念（剑/斧/锹 m10 B1 不启用，视同永不磨损）");
        }

        [Test]
        public void MaxDurability_Negative_Throws()
        {
            Assert.That(() => ItemDatabase.FromJson(new[]
                {
                    @"{ ""id"": ""bad_pickaxe"", ""numericId"": 1499, ""maxDurability"": -1 }",
                }),
                Throws.InstanceOf<System.IO.InvalidDataException>(),
                "负数没有意义，写错立刻报（与 toolTier / minToolTier 同态度）");
        }

        [Test]
        public void MaxDurability_Above255_Throws()
        {
            // Metadata 只有 8 位存上限（m3 预留位段 + WithMaxDurability 的 255 clamp），
            // MC 原值 1561 放不下——照抄会在运行时被静默截断，必须在加载层就拦下
            Assert.That(() => ItemDatabase.FromJson(new[]
                {
                    @"{ ""id"": ""greedy_pickaxe"", ""numericId"": 1498, ""maxDurability"": 1561 }",
                }),
                Throws.InstanceOf<System.IO.InvalidDataException>(),
                "maxDurability 超过 8 位编码上限 255 应在加载时抛，不能静默截断");
        }

        // ─── m10 C1：gearBonus 解析（手持装备三属性） ─────────────────────────

        [Test]
        public void GearBonus_ParsedFromJson_AllThreeStats()
        {
            var db = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""iron_sword"", ""numericId"": 1102,
                    ""gearBonus"": { ""stat"": ""defense"", ""amount"": 1 } }",
                @"{ ""id"": ""summer_alloy_sword"", ""numericId"": 1107,
                    ""gearBonus"": { ""stat"": ""moveSpeed"", ""amount"": 0.05 } }",
                @"{ ""id"": ""machine_essence_sword"", ""numericId"": 1108,
                    ""gearBonus"": { ""stat"": ""maxHealth"", ""amount"": 2 } }",
            });

            var iron = db.GetById("iron_sword");
            Assert.That(iron.GearStat, Is.EqualTo(GearStat.Defense), "stat=defense 应解析成 Defense");
            Assert.That(iron.GearAmount, Is.EqualTo(1f), "amount 原样进 GearAmount");

            Assert.That(db.GetById("summer_alloy_sword").GearStat, Is.EqualTo(GearStat.MoveSpeed));
            Assert.That(db.GetById("summer_alloy_sword").GearAmount, Is.EqualTo(0.05f).Within(1e-6f));

            Assert.That(db.GetById("machine_essence_sword").GearStat, Is.EqualTo(GearStat.MaxHealth));
            Assert.That(db.GetById("machine_essence_sword").GearAmount, Is.EqualTo(2f));
        }

        [Test]
        public void GearBonus_Missing_DefaultsToNone()
        {
            var def = ItemDatabase.FromJson(new[] { SwordJson }).GetById("wooden_sword");
            Assert.That(def.GearStat, Is.EqualTo(GearStat.None),
                "绝大多数物品没有 gearBonus——缺省 = 无加成");
            Assert.That(def.GearAmount, Is.EqualTo(0f));
        }

        [Test]
        public void GearBonus_UnknownStat_Throws()
        {
            Assert.That(() => ItemDatabase.FromJson(new[]
                {
                    @"{ ""id"": ""bad_gear"", ""numericId"": 1199,
                        ""gearBonus"": { ""stat"": ""luck"", ""amount"": 1 } }",
                }),
                Throws.InstanceOf<System.IO.InvalidDataException>(),
                "stat 是受控词表（defense/moveSpeed/maxHealth），写错立刻报——与 toolTier 同态度");
        }

        [Test]
        public void GearBonus_NonPositiveAmount_Throws()
        {
            Assert.That(() => ItemDatabase.FromJson(new[]
                {
                    @"{ ""id"": ""zero_gear"", ""numericId"": 1198,
                        ""gearBonus"": { ""stat"": ""defense"", ""amount"": 0 } }",
                }),
                Throws.InstanceOf<System.IO.InvalidDataException>(),
                "amount <= 0 没有意义（写了 gearBonus 却不给加成），加载层就拦下");
        }
    }
}
