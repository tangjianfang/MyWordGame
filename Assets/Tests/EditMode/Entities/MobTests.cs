using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class MobTests
    {
        [Test]
        public void Create_Pig_HasPassiveKind()
        {
            var pig = Mob.Create(1, new Float3(0, 64, 0));
            Assert.That(pig.Kind, Is.EqualTo(MobKind.Passive));
            Assert.That(pig.Health.Max, Is.EqualTo(10));
        }

        [Test]
        public void Create_Sheep_Has8Health()
        {
            var sheep = Mob.Create(2, default);
            Assert.That(sheep.Health.Max, Is.EqualTo(8));
        }

        [Test]
        public void Create_Zombie_HasHostileKind()
        {
            var z = Mob.Create(3, default);
            Assert.That(z.Kind, Is.EqualTo(MobKind.Hostile));
            Assert.That(z.AttackDamage, Is.GreaterThan(0));
        }

        [Test]
        public void Passive_PlayerClose_BecomesScared()
        {
            var pig = Mob.Create(1, new Float3(0, 64, 0));
            MobAI.Tick(pig, new Float3(2, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.EqualTo(MobState.Scared));
        }

        [Test]
        public void Passive_PlayerFar_StaysIdle()
        {
            var pig = Mob.Create(1, new Float3(0, 64, 0));
            MobAI.Tick(pig, new Float3(100, 64, 100), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.EqualTo(MobState.Idle));
        }

        [Test]
        public void Hostile_Daytime_ActsAsPassive()
        {
            var z = Mob.Create(3, new Float3(0, 64, 0));
            var day = new TimeOfDay { CurrentTick = 6000 };  // 正午
            MobAI.Tick(z, new Float3(2, 64, 0), null, day, 0.1f);
            Assert.That(z.State, Is.EqualTo(MobState.Scared),
                "白天僵尸看到玩家应该害怕而非追击");
        }

        [Test]
        public void Hostile_Night_ChasesPlayer()
        {
            var z = Mob.Create(3, new Float3(0, 64, 0));
            var night = new TimeOfDay { CurrentTick = 15000 };  // 夜晚
            MobAI.Tick(z, new Float3(5, 64, 0), null, night, 0.1f);
            Assert.That(z.State, Is.EqualTo(MobState.Chasing));
        }

        [Test]
        public void Hostile_InRange_RaisesDamageEvent()
        {
            var z = Mob.Create(3, new Float3(0, 64, 0));
            int hitCount = 0;
            CombatEvents.OnDamageDealt += _ => hitCount++;
            var night = new TimeOfDay { CurrentTick = 15000 };
            // 走 60 帧让 attack cooldown 转好
            for (var i = 0; i < 60; i++)
                MobAI.Tick(z, new Float3(0, 64, 0.5f), null, night, 0.05f);
            Assert.That(hitCount, Is.GreaterThan(0));
            CombatEvents.Reset();
        }
    }
}
