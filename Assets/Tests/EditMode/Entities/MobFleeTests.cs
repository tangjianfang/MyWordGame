using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// m9 A2：动物受击逃跑 + 血量平衡（修打猎断环②「追不上动物」）。
    /// 语义变更：被动动物（Passive/Pig/Cow/Chicken）不再因玩家靠近而惊跑——
    /// m3 D1 的「ScareRadius=8 内进 Scared」退役，改为 <see cref="MobAI.TakeHit"/> 受击才逃：
    /// 逃 3 秒后停（可被再次攻击再逃）、速度 4.0m/s（&lt; 玩家走速 4.3，追得上但不轻松）。
    /// 旧惊跑 5m/s &gt; 玩家 4.3m/s 且半径 8m &gt; 攻击距离 4m，玩家永远贴不了身。
    /// 血量：猪 10 / 牛 15 / 鸡 4（木剑 4 伤，3 下杀猪）。
    /// </summary>
    [TestFixture]
    public class MobFleeTests
    {
        [SetUp]
        public void SetUp() => DifficultyMode.ResetCache();

        [TearDown]
        public void TearDown() => DifficultyMode.ResetCache();


        private static float HorizontalDistance(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }

        [Test]
        public void TakeHit_PassiveEntersFleeing()
        {
            // 猪在攻击者东侧 10 格；受击应进入 FleeingFromAttacker（m9 A2 接上从未被赋值的状态）
            var pig = Mob.Create(6, new Float3(10, 64, 0));
            MobAI.TakeHit(pig, new Float3(0, 64, 0), 4f);

            Assert.That(pig.State, Is.EqualTo(MobState.FleeingFromAttacker),
                "受击后应进入 FleeingFromAttacker 逃跑状态");

            // tick 一帧验证逃跑方向：远离攻击者（+X），玩家在哪无所谓
            MobAI.Tick(pig, new Float3(100, 64, 100), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.Position.X, Is.GreaterThan(10f),
                "逃跑方向应远离攻击者（猪在攻击者东侧，应朝 +X 逃）");
            Assert.That(pig.Position.Z, Is.EqualTo(0f).Within(0.0001f),
                "共线场景 Z 不应漂移");
        }

        [Test]
        public void TakeHit_HostileDoesNotFlee()
        {
            // 受击逃跑只属于被动动物（spec 非目标「AI 大改」）：僵尸挨打照旧追击
            var zombie = Mob.Create(9, new Float3(10, 64, 0));
            var night = new TimeOfDay { CurrentTick = 15000 };
            MobAI.TakeHit(zombie, new Float3(0, 64, 0), 4f);

            Assert.That(zombie.State, Is.Not.EqualTo(MobState.FleeingFromAttacker),
                "僵尸受击不应进入逃跑状态");

            MobAI.Tick(zombie, new Float3(0, 64, 0), null, night, 0.1f);
            Assert.That(zombie.State, Is.EqualTo(MobState.Chasing),
                "僵尸受击后夜里仍应追击玩家");
        }

        [Test]
        public void Flee_StopsAfter3Seconds()
        {
            var pig = Mob.Create(6, new Float3(10, 64, 0));
            var attacker = new Float3(0, 64, 0);
            MobAI.TakeHit(pig, attacker, 1f);

            // 步进 3.5 秒（35 × 0.1f，略过 3s 容掉浮点误差）：逃跑计时烧完，回落 wander 流
            for (int i = 0; i < 35; i++)
                MobAI.Tick(pig, attacker, null, new TimeOfDay(), 0.1f);

            Assert.That(pig.State, Is.Not.EqualTo(MobState.FleeingFromAttacker),
                "受击 3 秒后应退出逃跑状态");

            // 再 tick 1 秒（站定 Idle、WanderCooldown=2 未烧完）：位置不应继续远离攻击者
            float distAfterFlee = HorizontalDistance(pig.Position, attacker);
            for (int i = 0; i < 10; i++)
                MobAI.Tick(pig, attacker, null, new TimeOfDay(), 0.1f);

            Assert.That(HorizontalDistance(pig.Position, attacker),
                Is.EqualTo(distAfterFlee).Within(0.0001f),
                "逃跑 3 秒停止后，位置不应继续远离攻击者");
        }

        [Test]
        public void FleeSpeed_Is4PerSecond()
        {
            var pig = Mob.Create(6, new Float3(10, 64, 0));
            MobAI.TakeHit(pig, new Float3(0, 64, 0), 1f);

            // 单帧步进整 1 秒（FleeUntil 3→2 仍在逃）：位移应为 4.0m
            var before = pig.Position;
            MobAI.Tick(pig, new Float3(0, 64, 0), null, new TimeOfDay(), 1.0f);
            float moved = HorizontalDistance(pig.Position, before);

            Assert.That(moved, Is.EqualTo(4.0f).Within(0.1f),
                "逃跑速度应为 4.0m/s（< 玩家走速 4.3，追得上但不轻松）");
        }

        [Test]
        public void TakeHit_ReducesHealth()
        {
            var pig = Mob.Create(6, new Float3(10, 64, 0));
            MobAI.TakeHit(pig, new Float3(0, 64, 0), 4f);
            Assert.That(pig.Health.Current, Is.EqualTo(6f).Within(0.0001f),
                "10 血猪挨 4 伤应剩 6 血（木剑 3 下杀猪的节奏基础）");
        }

        [Test]
        public void TakeHit_SecondHit_RestartsFleeWindow()
        {
            // 「打一下→追→再打」：逃跑窗按最后一次受击重开 3 秒
            var pig = Mob.Create(6, new Float3(10, 64, 0));
            var attacker = new Float3(0, 64, 0);
            MobAI.TakeHit(pig, attacker, 1f);

            // 第一窗烧掉 2.9 秒（29 × 0.1f，只剩 0.1 秒）
            for (int i = 0; i < 29; i++)
                MobAI.Tick(pig, attacker, null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.EqualTo(MobState.FleeingFromAttacker),
                "前置：第一窗内仍在逃跑");

            // 再次受击重开完整 3 秒：再烧 2.9 秒仍应处于逃跑
            MobAI.TakeHit(pig, attacker, 1f);
            for (int i = 0; i < 29; i++)
                MobAI.Tick(pig, attacker, null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.EqualTo(MobState.FleeingFromAttacker),
                "再次受击应重开完整 3 秒逃跑窗（否则第一窗残值会让它提前停下挨打）");
        }

        [Test]
        public void PlayerClose_DoesNotTriggerFlee()
        {
            // m9 A2 语义：靠近不逃、受击才逃——否则 8m 惊跑半径 > 4m 攻击距离，
            // 玩家永远进不了攻击范围，打猎无从谈起
            var pig = Mob.Create(6, new Float3(0, 64, 0));
            MobAI.Tick(pig, new Float3(1, 64, 0), null, new TimeOfDay(), 0.1f);

            Assert.That(pig.State, Is.EqualTo(MobState.Idle),
                "玩家贴到 1 格也不应惊跑——受击才逃（m9 A2 修断环②）");
            Assert.That(pig.State, Is.Not.EqualTo(MobState.Scared),
                "Scared（靠近惊跑）已退役");
        }

        [TestCase(6, MobKind.Pig, 10f, "猪")]
        [TestCase(7, MobKind.Cow, 15f, "牛")]
        [TestCase(8, MobKind.Chicken, 4f, "鸡")]
        public void Create_AnimalHealthTable(int mobTypeId, MobKind kind, float expectedMax, string name)
        {
            var mob = Mob.Create(mobTypeId, new Float3(0, 64, 0));
            Assert.That(mob.Kind, Is.EqualTo(kind), $"mobTypeId={mobTypeId} 应是 {name}");
            Assert.That(mob.Health.Max, Is.EqualTo(expectedMax),
                $"{name}最大血量应为 {expectedMax}（m9 A2 血量平衡）");
            Assert.That(mob.Health.Current, Is.EqualTo(expectedMax),
                $"{name}出生应满血");
        }
    }
}
