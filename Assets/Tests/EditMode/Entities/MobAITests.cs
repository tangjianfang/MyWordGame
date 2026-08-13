using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// Phase D 第一批测试：在现有 <see cref="Mob.Create"/> + <see cref="MobAI.Tick"/> API 上
    /// 验证 type-specific 行为（spec line 173/181：PIG/COW/CHICKEN 巡逻+怕玩家、ZOMBIE 32 格追击、
    /// 死亡触发 Drop）。Phase D 扩展既有 state machine，5 个新测试。
    /// </summary>
    [TestFixture]
    public class MobAITests
    {
        [Test]
        public void ZombieChasesOnlyWithinAggroRange()
        {
            // 新僵尸 (MobTypeId 9)：32 格 chase 半径
            var z = Mob.Create(9, new Float3(0, 64, 0));
            Assert.That(z.Kind, Is.EqualTo(MobKind.Zombie));
            Assert.That(z.ChaseRadius, Is.EqualTo(32f), "新僵尸 chase 半径应为 32");

            // 玩家 16 格外、32 格内，应转入 Chasing
            MobAI.Tick(z, new Float3(20, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(z.State, Is.EqualTo(MobState.Chasing),
                "新僵尸 32 格内应追击玩家");

            // 玩家跑到 50 格外（远超 32 格），下次 tick 应脱离 Chasing
            MobAI.Tick(z, new Float3(50, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(z.State, Is.Not.EqualTo(MobState.Chasing),
                "新僵尸超 32 格不应追击");
        }

        [Test]
        public void PigFleesWhenPlayerClose()
        {
            // 新猪 (MobTypeId 6)：MobKind.Pig，路由到 TickPassive
            var pig = Mob.Create(6, new Float3(0, 64, 0));
            Assert.That(pig.Kind, Is.EqualTo(MobKind.Pig));

            // 玩家 3 格内（< ScareRadius=8），应进入 Scared
            MobAI.Tick(pig, new Float3(3, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.EqualTo(MobState.Scared),
                "新猪玩家靠近应害怕逃跑");
        }

        [Test]
        public void PigNeverEntersChaseState()
        {
            // 新猪在各种距离 tick 一遍，确认永远不会进 Chasing（spec line 181 "pig 永不变 Chase"）
            var pig = Mob.Create(6, new Float3(0, 64, 0));
            MobAI.Tick(pig, new Float3(2, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.Not.EqualTo(MobState.Chasing));
            MobAI.Tick(pig, new Float3(5, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.Not.EqualTo(MobState.Chasing));
            MobAI.Tick(pig, new Float3(0.5f, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.Not.EqualTo(MobState.Chasing));
        }

        [Test]
        public void ChickenMovesLessThanPigOverSameTime()
        {
            // 新鸡/新猪的 MoveSpeed 直接比较——比驱动 wander 状态更确定
            var chicken = Mob.Create(8, default);
            var pig = Mob.Create(6, default);
            Assert.That(chicken.Kind, Is.EqualTo(MobKind.Chicken));
            Assert.That(chicken.MoveSpeed, Is.LessThan(pig.MoveSpeed),
                $"鸡 ({chicken.MoveSpeed}) 应比猪 ({pig.MoveSpeed}) 移动慢（spec line 181 'chicken 随机跳'）");
        }

        [Test]
        public void DeathTriggersItemDrop()
        {
            // 新猪 (MobTypeId 6) Health=10，扣 100 必死
            var pig = Mob.Create(6, new Float3(0, 64, 0));
            pig.Health.Damage(100f);
            Assert.That(pig.Health.IsDead, Is.True);

            MobAI.Tick(pig, new Float3(100, 64, 100), null, new TimeOfDay(), 0.1f);

            Assert.That(pig.State, Is.EqualTo(MobState.Dying),
                "死亡应自动转 Dying");
            Assert.That(pig.LastDrops, Is.Not.Null, "MobAI 应调用 ItemDropTable.Drop");
            Assert.That(pig.LastDrops, Is.Not.Empty, "猪死亡应掉落物品");
        }
    }
}