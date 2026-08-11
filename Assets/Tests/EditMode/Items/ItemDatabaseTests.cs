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
    }
}
