using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class ExperienceTests
    {
        [Test]
        public void New_Experience_StartsAtZero()
        {
            var xp = new Experience();
            Assert.That(xp.Level, Is.EqualTo(0));
            Assert.That(xp.Current, Is.EqualTo(0));
            Assert.That(xp.Fraction, Is.EqualTo(0f));
        }

        [Test]
        public void Add_BelowThreshold_StaysAtCurrentLevel()
        {
            var xp = new Experience();
            bool leveled = xp.Add(50);
            Assert.That(leveled, Is.False);
            Assert.That(xp.Level, Is.EqualTo(0));
            Assert.That(xp.Current, Is.EqualTo(50));
            Assert.That(xp.Fraction, Is.EqualTo(0.5f));
        }

        [Test]
        public void Add_AboveThreshold_LevelsUp()
        {
            var xp = new Experience();
            bool leveled = xp.Add(120);
            Assert.That(leveled, Is.True);
            Assert.That(xp.Level, Is.EqualTo(1));
            Assert.That(xp.Current, Is.EqualTo(20));
        }

        [Test]
        public void Add_MultipleLevelsInOneHit()
        {
            var xp = new Experience();
            xp.Add(250);    // 100 (lvl1) + 100 (lvl2) + 50
            Assert.That(xp.Level, Is.EqualTo(2));
            Assert.That(xp.Current, Is.EqualTo(50));
        }

        [Test]
        public void Add_NegativeAmount_NoOp()
        {
            var xp = new Experience(50, 3);
            bool leveled = xp.Add(-10);
            Assert.That(leveled, Is.False);
            Assert.That(xp.Current, Is.EqualTo(50));
            Assert.That(xp.Level, Is.EqualTo(3));
        }
    }

    [TestFixture]
    public class DeathSystemTests
    {
        [Test]
        public void Start_Phase_IsAlive()
        {
            var d = new DeathSystem();
            Assert.That(d.IsAlive, Is.True);
        }

        [Test]
        public void OnDeath_SetsPhaseDying_AndRecordsPosition()
        {
            var d = new DeathSystem();
            d.OnDeath(new Float3(5, 64, 5));
            Assert.That(d.Phase, Is.EqualTo(DeathPhase.Dying));
            Assert.That(d.LastDeathPosition, Is.EqualTo(new Float3(5, 64, 5)));
            Assert.That(d.PhaseTimer, Is.EqualTo(DeathSystem.DeathScreenDelay));
        }

        [Test]
        public void Tick_WhileAlive_ReturnsFalse()
        {
            var d = new DeathSystem();
            bool inDeath = d.Tick(1f);
            Assert.That(inDeath, Is.False);
        }

        [Test]
        public void Tick_AfterRespawnDelay_ReturnsToAlive()
        {
            var d = new DeathSystem();
            d.OnDeath(default);
            // 走 5 秒（远大于 1+3 = 4）
            bool inDeath = true;
            for (int i = 0; i < 50 && inDeath; i++) inDeath = d.Tick(0.1f);
            Assert.That(inDeath, Is.False);
            Assert.That(d.Phase, Is.EqualTo(DeathPhase.Alive));
        }

        [Test]
        public void RequestRespawn_SkipsDyingPhase()
        {
            var d = new DeathSystem();
            d.OnDeath(default);
            d.RequestRespawn();
            Assert.That(d.PhaseTimer, Is.EqualTo(0f));
        }
    }
}