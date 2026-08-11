using MyWorld.Core.Entities;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class HealthTests
    {
        [Test]
        public void New_Health_IsFull()
        {
            var h = new Health(20);
            Assert.That(h.Current, Is.EqualTo(20));
            Assert.That(h.IsDead, Is.False);
        }

        [Test]
        public void Damage_DecreasesCurrent()
        {
            var h = new Health(20);
            h.Damage(5);
            Assert.That(h.Current, Is.EqualTo(15));
        }

        [Test]
        public void Damage_NeverGoesBelowZero()
        {
            var h = new Health(20);
            h.Damage(100);
            Assert.That(h.Current, Is.EqualTo(0));
            Assert.That(h.IsDead, Is.True);
        }

        [Test]
        public void Heal_IncreasesCurrent()
        {
            var h = new Health(20);
            h.Damage(10);
            h.Heal(5);
            Assert.That(h.Current, Is.EqualTo(15));
        }

        [Test]
        public void Heal_NeverExceedsMax()
        {
            var h = new Health(20);
            h.Damage(5);
            h.Heal(100);
            Assert.That(h.Current, Is.EqualTo(20));
        }

        [Test]
        public void NegativeDamage_IsIgnored()
        {
            var h = new Health(20);
            h.Damage(-5);
            Assert.That(h.Current, Is.EqualTo(20));
        }
    }
}
