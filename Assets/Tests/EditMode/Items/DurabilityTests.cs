using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class DurabilityTests
    {
        [Test]
        public void NewStack_HasNoDurability()
        {
            var s = new ItemStack(100, 1);
            Assert.That(s.HasDurability, Is.False);
            Assert.That(s.CurrentDurability, Is.EqualTo(0));
        }

        [Test]
        public void WithMaxDurability_EncodesCurEqualsMax()
        {
            var s = new ItemStack(100, 1).WithMaxDurability(100);
            Assert.That(s.HasDurability, Is.True);
            Assert.That(s.CurrentDurability, Is.EqualTo(100));
            Assert.That(s.MaxDurability, Is.EqualTo(100));
        }

        [Test]
        public void DamageOnce_DecrementsCurrent()
        {
            var s = new ItemStack(100, 1).WithMaxDurability(50);
            var damaged = s.DamageOnce();
            Assert.That(damaged.CurrentDurability, Is.EqualTo(49));
            Assert.That(damaged.MaxDurability, Is.EqualTo(50));
            Assert.That(damaged.Count, Is.EqualTo(1));
        }

        [Test]
        public void DamageOnce_BreaksAtZero()
        {
            var s = new ItemStack(100, 1).WithMaxDurability(1);
            var broken = s.DamageOnce();
            Assert.That(broken.IsEmpty, Is.True);
        }

        [Test]
        public void DamageOnce_NonTool_ReturnsSameStack()
        {
            var s = new ItemStack(100, 1);    // 没有 max durability
            var damaged = s.DamageOnce();
            Assert.That(damaged, Is.EqualTo(s));
        }

        [Test]
        public void WithMaxDurability_ClampsAbove255()
        {
            var s = new ItemStack(100, 1).WithMaxDurability(999);
            Assert.That(s.MaxDurability, Is.EqualTo(255));
        }
    }
}