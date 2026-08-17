// m11 W1-1（战斗）：骷髅 / 蜘蛛 / 苦力怕（新 kind 24-26）专属 AI + Mob.Create 12 新生物建档。
// Mob.Create 对 typeId 15-26 一并建档（被动 9 个照猪组参数 + 卡片血量，敌对 3 个照卡片数值），
// 守卫测试照 MobKindWiringTests 模式：先全量建档不抛，再逐项断参数。
using System.Collections.Generic;
using MyWorld.Core.Combat;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class HostileAiTests
    {
        private static readonly TimeOfDay Night = new TimeOfDay { CurrentTick = 15000 };

        private List<ProjectileEntity> _fired;

        [SetUp]
        public void SetUp()
        {
            _fired = new List<ProjectileEntity>();
            MobAI.OnProjectileFired += _fired.Add;
        }

        [TearDown]
        public void TearDown()
        {
            MobAI.OnProjectileFired -= _fired.Add;
            CombatEvents.Reset();
            Explosion.BoundRegistry = null;
            Explosion.BoundDrops = null;
        }

        // ─── Mob.Create 15-26 建档 ──────────────────────────────────────────

        /// <summary>12 个新 kindId（与 MobKindWiringTests 的契约表一致）。</summary>
        private static readonly MobKind[] AllNewKinds =
        {
            MobKind.Sheep, MobKind.Rabbit, MobKind.Fox, MobKind.Deer, MobKind.Panda,
            MobKind.Penguin, MobKind.Goat, MobKind.Raccoon, MobKind.Hamster,
            MobKind.Skeleton, MobKind.Spider, MobKind.Creeper,
        };

        [Test]
        public void Mob_Create_十五至二十六_全部建档不抛且Kind正确()
        {
            // P0 的关键缺口：Mob.Create 对 15-26 曾一律抛「未知 mobTypeId」。
            // spawn_rules 已有条目，创建失败 = 夜间刷怪当场炸——全量建档是第一道闸。
            for (int typeId = 15; typeId <= 26; typeId++)
            {
                var mob = Mob.Create(typeId, new Float3(0f, 64f, 0f));
                Assert.That(mob.Kind, Is.EqualTo((MobKind)typeId),
                    $"typeId {typeId} 应建档为同名 kind（P0 约定 typeId = 枚举数值）");
                Assert.That(mob.Health.Max, Is.GreaterThan(0f), $"{mob.Kind} 血量必须为正");
                Assert.That(mob.Health.IsDead, Is.False, $"{mob.Kind} 出生应满血存活");
            }
        }

        // 被动 9 个：血量按卡片（猪10/牛15/鸡4 量级）
        [TestCase(MobKind.Sheep, 8f)]
        [TestCase(MobKind.Rabbit, 3f)]
        [TestCase(MobKind.Fox, 5f)]
        [TestCase(MobKind.Deer, 8f)]
        [TestCase(MobKind.Panda, 15f)]
        [TestCase(MobKind.Penguin, 4f)]
        [TestCase(MobKind.Goat, 8f)]
        [TestCase(MobKind.Raccoon, 4f)]
        [TestCase(MobKind.Hamster, 2f)]
        public void Mob_Create_九被动_血量照卡片(MobKind kind, float expectedMax)
        {
            var mob = Mob.Create((int)kind, new Float3(0f, 64f, 0f));
            Assert.That(mob.Health.Max, Is.EqualTo(expectedMax), $"{kind} 血量应照 W1-1 卡片建档");
            Assert.That(mob.AttackDamage, Is.EqualTo(0f), $"{kind} 被动生物无攻击力");
        }

        // 敌对 3 个：照卡片数值（骷髅远程 8-12m 每 2s 一箭 / 蜘蛛夜间追 4.5 / 苦力怕 <3m 引信 1.5s）
        [TestCase(MobKind.Skeleton, 16f)]
        [TestCase(MobKind.Spider, 16f)]
        [TestCase(MobKind.Creeper, 20f)]
        public void Mob_Create_三敌对_血量照卡片(MobKind kind, float expectedMax)
        {
            var mob = Mob.Create((int)kind, new Float3(0f, 64f, 0f));
            Assert.That(mob.Health.Max, Is.EqualTo(expectedMax), $"{kind} 血量应照卡片建档");
            Assert.That(mob.ChaseRadius, Is.EqualTo(20f), $"{kind} 追击半径对齐僵尸（20 格）");
        }

        // ─── 骷髅：远程风筝 ─────────────────────────────────────────────────

        [Test]
        public void Skeleton_太近后撤_太远靠近_窗口内站定并射箭()
        {
            // 5m（< 8m）：后撤远离玩家
            var skeleton = Mob.Create((int)MobKind.Skeleton, new Float3(0f, 64f, 0f));
            MobAI.Tick(skeleton, new Float3(5f, 64f, 0f), null, Night, 0.1f, isNight: true);
            Assert.That(skeleton.Velocity.X, Is.LessThan(0f), "5m 太近：骷髅应向远离玩家方向后撤");

            // 16m（> 12m，< 追击 20）：靠近玩家
            skeleton = Mob.Create((int)MobKind.Skeleton, new Float3(0f, 64f, 0f));
            MobAI.Tick(skeleton, new Float3(16f, 64f, 0f), null, Night, 0.1f, isNight: true);
            Assert.That(skeleton.State, Is.EqualTo(MobState.Chasing), "16m 在追击半径内应进入交战");
            Assert.That(skeleton.Velocity.X, Is.GreaterThan(0f), "16m 太远：骷髅应朝玩家靠近");

            // 10m（窗口 8-12m 内）：站定 + 首箭即刻出膛
            skeleton = Mob.Create((int)MobKind.Skeleton, new Float3(0f, 64f, 0f));
            MobAI.Tick(skeleton, new Float3(10f, 64f, 0f), null, Night, 0.1f, isNight: true);
            Assert.That(skeleton.Velocity.X, Is.EqualTo(0f), "射击窗口内应站定不动");
            Assert.That(_fired.Count, Is.EqualTo(1), "进窗口且冷却就绪应立刻射一箭");
            Assert.That(skeleton.AttackCooldown, Is.EqualTo(MobAI.SkeletonShootInterval).Within(1e-4f),
                "射箭后冷却重置为 2s");
            var arrow = _fired[0];
            Assert.That(arrow.Position.Y, Is.EqualTo(65.4f).Within(1e-3f),
                "箭应从持弓高度（脚底 +1.4）出膛");
        }

        [Test]
        public void Skeleton_每两秒一箭_白天不射()
        {
            var skeleton = Mob.Create((int)MobKind.Skeleton, new Float3(0f, 64f, 0f));

            // 0.25s 一步：第 1 步出首箭，冷却 2s = 8 步；第 9 步（2.25s 累计）出第二箭
            for (int i = 0; i < 8; i++)
            {
                MobAI.Tick(skeleton, new Float3(10f, 64f, 0f), null, Night, 0.25f, isNight: true);
            }
            Assert.That(_fired.Count, Is.EqualTo(1), "2s 冷却未到（累计 2.0s）只出 1 箭");
            MobAI.Tick(skeleton, new Float3(10f, 64f, 0f), null, Night, 0.25f, isNight: true);
            Assert.That(_fired.Count, Is.EqualTo(2), "冷却一过（2.25s）立刻补第二箭");

            // 白天：不追不射（与僵尸同款昼夜语义）
            var daySkeleton = Mob.Create((int)MobKind.Skeleton, new Float3(0f, 64f, 0f));
            for (int i = 0; i < 10; i++)
            {
                MobAI.Tick(daySkeleton, new Float3(10f, 64f, 0f), null, Night, 0.1f, isNight: false);
            }
            Assert.That(daySkeleton.State, Is.Not.EqualTo(MobState.Chasing), "白天骷髅不交战");
            Assert.That(_fired.Count, Is.EqualTo(2), "白天一支箭都不该多射");
        }

        // ─── 蜘蛛：夜间追击速度 4.5 ─────────────────────────────────────────

        [Test]
        public void Spider_夜间追击速度四点五_白天不追()
        {
            var spider = Mob.Create((int)MobKind.Spider, new Float3(0f, 64f, 0f));
            MobAI.Tick(spider, new Float3(15f, 64f, 0f), null, Night, 0.1f, isNight: true);
            Assert.That(spider.State, Is.EqualTo(MobState.Chasing), "夜间 20 格内蜘蛛应追击");
            float speed = (float)System.Math.Sqrt(
                spider.Velocity.X * spider.Velocity.X + spider.Velocity.Z * spider.Velocity.Z);
            Assert.That(speed, Is.EqualTo(MobAI.SpiderChaseSpeed).Within(1e-3f),
                "蜘蛛夜间追击速度 = 4.5（卡片数值）");

            var daySpider = Mob.Create((int)MobKind.Spider, new Float3(0f, 64f, 0f));
            MobAI.Tick(daySpider, new Float3(15f, 64f, 0f), null, Night, 0.1f, isNight: false);
            Assert.That(daySpider.State, Is.Not.EqualTo(MobState.Chasing), "白天蜘蛛不追（回落被动流）");
        }

        [Test]
        public void Spider_贴脸近战两点()
        {
            var spider = Mob.Create((int)MobKind.Spider, new Float3(0f, 64f, 0f));
            int dealt = 0;
            DamageEvent got = default;
            CombatEvents.OnDamageDealt += ev => { got = ev; dealt++; };

            MobAI.Tick(spider, new Float3(2f, 64f, 0f), null, Night, 0.1f, isNight: true);

            Assert.That(dealt, Is.EqualTo(1), "射程内应近战攻击一次");
            Assert.That(got.Amount, Is.EqualTo(2f), "蜘蛛近战 2 点（照卡片量级）");
            Assert.That(spider.Velocity.X, Is.EqualTo(0f), "贴脸站定输出");
        }

        // ─── 苦力怕（新 kind）：<3m 引信 1.5s → 爆炸 ─────────────────────────

        [Test]
        public void NewCreeper_三米内起引信一点五秒_期间站定()
        {
            var creeper = Mob.Create((int)MobKind.Creeper, new Float3(0f, 64f, 0f));
            MobAI.Tick(creeper, new Float3(2f, 64f, 0f), null, Night, 0.1f, isNight: true);
            Assert.That(creeper.FuseTimer, Is.EqualTo(1.4f).Within(1e-4f),
                "2m 触发引信（<3m），首帧扣 0.1s 后剩 1.4s");
            Assert.That(creeper.Velocity.X, Is.EqualTo(0f), "引信期间站定不再移动");

            // 5m：不触发（边界外）
            var farCreeper = Mob.Create((int)MobKind.Creeper, new Float3(0f, 64f, 0f));
            MobAI.Tick(farCreeper, new Float3(5f, 64f, 0f), null, Night, 0.1f, isNight: true);
            Assert.That(farCreeper.FuseTimer, Is.EqualTo(0f), "5m（≥触发半径 3m）不起引信，继续逼近");
            Assert.That(farCreeper.Velocity.X, Is.GreaterThan(0f), "引信未起时应朝玩家走");
        }

        [Test]
        public void NewCreeper_玩家跑出五米取消引信_白天清引信()
        {
            var creeper = Mob.Create((int)MobKind.Creeper, new Float3(0f, 64f, 0f));
            MobAI.Tick(creeper, new Float3(2f, 64f, 0f), null, Night, 0.1f, isNight: true);
            Assert.That(creeper.FuseTimer, Is.GreaterThan(0f), "前置：引信已起");

            MobAI.Tick(creeper, new Float3(6f, 64f, 0f), null, Night, 0.1f, isNight: true);
            Assert.That(creeper.FuseTimer, Is.EqualTo(0f), "玩家跑出 5m 应取消引信");

            // 引信中入昼：立即熄引信回落被动流（不带着半截引信进白天闪白）
            var dayCreeper = Mob.Create((int)MobKind.Creeper, new Float3(0f, 64f, 0f));
            MobAI.Tick(dayCreeper, new Float3(2f, 64f, 0f), null, Night, 0.1f, isNight: true);
            MobAI.Tick(dayCreeper, new Float3(2f, 64f, 0f), null, Night, 0.1f, isNight: false);
            Assert.That(dayCreeper.FuseTimer, Is.EqualTo(0f), "入昼应清引信");
            Assert.That(dayCreeper.State, Is.Not.EqualTo(MobState.Chasing), "白天苦力怕不交战");
        }

        [Test]
        public void NewCreeper_引信烧完自爆_死亡且伤害距离衰减最高六点()
        {
            var creeper = Mob.Create((int)MobKind.Creeper, new Float3(0.5f, 64f, 0.5f));
            int taken = 0;
            DamageEvent got = default;
            CombatEvents.OnDamageTaken += ev => { got = ev; taken++; };

            // 引信 1.5s：0.1s 一步 16 步（1.6s）必烧完
            for (int i = 0; i < 16; i++)
            {
                MobAI.Tick(creeper, new Float3(2.5f, 64f, 0.5f), null, Night, 0.1f, isNight: true);
            }

            Assert.That(taken, Is.EqualTo(1), "自爆应发一次玩家伤害事件");
            Assert.That(got.Source, Is.EqualTo(DamageSource.Environmental), "爆炸伤害走环境源");
            // 玩家距爆心（0.5,64.9,0.5）→(2.5,64,0.5)：dist=√(4+0.81)≈2.194 → 6×(1-2.194/3)≈1.61
            Assert.That(got.Amount, Is.EqualTo(1.6114f).Within(0.01f), "距离衰减伤害（最高 6 点封顶）");
            Assert.That(got.Amount, Is.LessThanOrEqualTo(6f), "伤害不许超过卡片上限 6");
            Assert.That(creeper.State, Is.EqualTo(MobState.Dying), "自爆后转 Dying");
            Assert.That(creeper.Health.Current, Is.EqualTo(0f), "自爆即死");
        }

        [Test]
        public void NewCreeper_自爆破坏半径三方块_基岩确定性幸存()
        {
            var world = new World();
            for (int x = -3; x <= 4; x++)
            for (int z = -3; z <= 4; z++)
            {
                world.SetBlock(x, 63, z, BlockIds.Stone); // 石板地面
            }
            world.SetBlock(0, 63, 1, BlockIds.Bedrock); // 半径内一格基岩

            var creeper = Mob.Create((int)MobKind.Creeper, new Float3(0.5f, 64f, 0.5f));
            for (int i = 0; i < 16; i++)
            {
                MobAI.Tick(creeper, new Float3(2.5f, 64f, 0.5f), world, Night, 0.1f, isNight: true);
            }

            Assert.That(creeper.State, Is.EqualTo(MobState.Dying), "前置：已自爆");
            Assert.That(world.GetBlock(0, 63, 0), Is.EqualTo(BlockIds.Air), "爆心正下方石板应被炸空");
            // (1,63,2) 方块中心 (1.5,63.5,2.5) 距爆心 (0.5,64.9,0.5) ≈ 2.65 < 3：应破坏
            Assert.That(world.GetBlock(1, 63, 2), Is.EqualTo(BlockIds.Air), "半径内斜角石板应被炸空");
            Assert.That(world.GetBlock(0, 63, 1), Is.EqualTo(BlockIds.Bedrock), "基岩确定性幸存");
        }

        // ─── 三敌对通用：受击不逃（与僵尸同语义） ──────────────────────────

        [Test]
        public void 三敌对_受击不进逃跑态()
        {
            foreach (var kind in new[] { MobKind.Skeleton, MobKind.Spider, MobKind.Creeper })
            {
                var mob = Mob.Create((int)kind, new Float3(0f, 64f, 0f));
                MobAI.TakeHit(mob, new Float3(0f, 64f, 0f), 1f);
                Assert.That(mob.State, Is.Not.EqualTo(MobState.FleeingFromAttacker),
                    $"{kind} 敌对受击不逃（与僵尸同语义）");
            }
        }
    }
}
