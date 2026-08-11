using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class CombatEventTests
    {
        [SetUp]
        public void SetUp() => CombatEvents.Reset();

        [TearDown]
        public void TearDown() => CombatEvents.Reset();

        [Test]
        public void RaiseDealt_FiresOnDamageDealt()
        {
            int count = 0;
            DamageEvent received = default;
            CombatEvents.OnDamageDealt += ev => { count++; received = ev; };

            var ev = new DamageEvent(DamageSource.Melee, 5, 1, 2, new Float3(0, 0, 0));
            CombatEvents.RaiseDealt(ev);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(received.Amount, Is.EqualTo(5));
        }

        [Test]
        public void MultipleSubscribers_AllFire()
        {
            int a = 0, b = 0;
            CombatEvents.OnDamageTaken += _ => a++;
            CombatEvents.OnDamageTaken += _ => b++;

            CombatEvents.RaiseTaken(new DamageEvent(DamageSource.Melee, 1, 0, 0, default));

            Assert.That(a, Is.EqualTo(1));
            Assert.That(b, Is.EqualTo(1));
        }

        [Test]
        public void Reset_ClearsAllSubscribers()
        {
            int count = 0;
            CombatEvents.OnDamageDealt += _ => count++;
            CombatEvents.RaiseDealt(new DamageEvent(DamageSource.Melee, 1, 0, 0, default));
            Assert.That(count, Is.EqualTo(1));

            CombatEvents.Reset();
            CombatEvents.RaiseDealt(new DamageEvent(DamageSource.Melee, 1, 0, 0, default));
            Assert.That(count, Is.EqualTo(1), "Reset 之后再 Raise 不该再触发");
        }
    }
}
