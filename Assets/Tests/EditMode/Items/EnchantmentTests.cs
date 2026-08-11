using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class EnchantmentTests
    {
        [Test]
        public void Sharpness_AttackBonus_EqualsLevel()
        {
            var e = new Enchantment(EnchantmentType.Sharpness, 3);
            Assert.That(e.AttackBonus, Is.EqualTo(3f));
        }

        [Test]
        public void OtherEnchants_HaveZeroAttackBonus()
        {
            Assert.That(new Enchantment(EnchantmentType.Efficiency, 5).AttackBonus, Is.EqualTo(0f));
            Assert.That(new Enchantment(EnchantmentType.None, 0).AttackBonus, Is.EqualTo(0f));
        }

        [Test]
        public void CostForLevel_ClampsAndScales()
        {
            Assert.That(EnchantingTable.CostForLevel(1), Is.EqualTo((5, 1)));
            Assert.That(EnchantingTable.CostForLevel(5), Is.EqualTo((25, 1)));
            Assert.That(EnchantingTable.CostForLevel(0).ExpCost, Is.EqualTo(5));   // clamp to 1
            Assert.That(EnchantingTable.CostForLevel(99).ExpCost, Is.EqualTo(25));  // clamp to 5
        }

        [Test]
        public void Roll_Level0_ReturnsNone()
        {
            var e = EnchantingTable.Roll(0);
            Assert.That(e.Type, Is.EqualTo(EnchantmentType.None));
        }

        [Test]
        public void Roll_PositiveLevel_ReturnsSharpnessAtThatLevel()
        {
            for (int lv = 1; lv <= 5; lv++)
            {
                var e = EnchantingTable.Roll(lv);
                Assert.That(e.Type, Is.EqualTo(EnchantmentType.Sharpness));
                Assert.That(e.Level, Is.EqualTo(lv));
            }
        }
    }
}