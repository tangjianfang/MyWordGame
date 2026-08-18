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
        [SetUp]
        public void SetUp() => DifficultyMode.ResetCache();

        [TearDown]
        public void TearDown() => DifficultyMode.ResetCache();


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
        public void Passive_PlayerClose_NoLongerScared()
        {
            // m9 A2 修正（原 m3 断言「靠近进 Scared」）：靠近惊跑退役、受击才逃
            // （MobAI.TakeHit）——否则 8m 惊跑半径 > 4m 攻击距离，玩家永远贴不了身。
            var pig = Mob.Create(1, new Float3(0, 64, 0));
            MobAI.Tick(pig, new Float3(2, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.EqualTo(MobState.Idle),
                "玩家靠近应保持 Idle 站定（受击才逃，m9 A2）");
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
            // m9 A2：白天走被动流且不再靠近惊跑，应保持 Idle（不追也不逃）
            Assert.That(z.State, Is.EqualTo(MobState.Idle),
                "白天僵尸看到玩家不追击（被动流，m9 A2 起靠近也不再惊跑）");
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

        [Test]
        public void Skeleton_HasRangedAttackRange()
        {
            var sk = Mob.Create(4, default);
            Assert.That(sk.Kind, Is.EqualTo(MobKind.Hostile));
            Assert.That(sk.AttackRange, Is.GreaterThan(MobAI.AttackRange),
                "骷髅应在更远距离开火");
            Assert.That(sk.AttackDamage, Is.GreaterThan(0));
        }

        [Test]
        public void Creeper_HasNoAttackDamage_ButStartsFuseOnPlayerClose()
        {
            var c = Mob.Create(5, new Float3(0, 64, 0));
            Assert.That(c.IsCreeper, Is.True);
            Assert.That(c.AttackDamage, Is.EqualTo(0f));
            Assert.That(c.FuseTimer, Is.EqualTo(0f));

            var night = new TimeOfDay { CurrentTick = 15000 };
            // 玩家在 2 格内：应进入 fuse
            MobAI.Tick(c, new Float3(2, 64, 0), null, night, 0.1f);
            Assert.That(c.FuseTimer, Is.GreaterThan(0f), "苦力怕进入 3 格内应起 fuse");
        }

        [Test]
        public void Creeper_FuseComplete_ExplodesAndDies()
        {
            var c = Mob.Create(5, new Float3(0, 64, 0));
            var night = new TimeOfDay { CurrentTick = 15000 };
            int hits = 0;
            CombatEvents.OnDamageTaken += _ => hits++;

            // 30 帧 × 0.1f = 3 秒，应足够让 fuse 烧完
            for (int i = 0; i < 30; i++)
                MobAI.Tick(c, new Float3(2, 64, 0), null, night, 0.1f);

            Assert.That(hits, Is.GreaterThan(0), "自爆应触发伤害事件");
            Assert.That(c.State, Is.EqualTo(MobState.Dying));
            Assert.That(c.Health.Current, Is.EqualTo(0));
            CombatEvents.Reset();
        }

        [Test]
        public void Creeper_FuseAborts_WhenPlayerRunsFar()
        {
            var c = Mob.Create(5, new Float3(0, 64, 0));
            var night = new TimeOfDay { CurrentTick = 15000 };
            MobAI.Tick(c, new Float3(2, 64, 0), null, night, 0.1f);
            Assert.That(c.FuseTimer, Is.GreaterThan(0f));

            // 玩家跑出 5 格
            MobAI.Tick(c, new Float3(20, 64, 0), null, night, 0.1f);
            Assert.That(c.FuseTimer, Is.EqualTo(0f), "玩家跑远应取消 fuse");
        }
    }
}
