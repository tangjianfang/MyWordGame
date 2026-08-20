// m12 W4：水生/飞行生物 Core 契约（dotnet / EditMode 双链同源）。
// 鱼不出水（折返）/ 飞行 Y 钉死与受击下落 / 建档 28-36 / spawn_rules 名字白名单。
using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class AquaticFlyerMobTests
    {
        [Test]
        public void Create_28To36_AllBuildWithPassiveStats()
        {
            int[] ids = { 28, 29, 30, 31, 32, 33, 34, 35, 36 };
            var expect = new[]
            {
                MobKind.Cod, MobKind.Salmon, MobKind.TropicalFish, MobKind.Pufferfish, MobKind.Turtle,
                MobKind.Sparrow, MobKind.Parrot, MobKind.Owl, MobKind.Butterfly,
            };
            for (int i = 0; i < ids.Length; i++)
            {
                var mob = Mob.Create(ids[i], new Float3(0, 64, 0));
                Assert.That(mob.Kind, Is.EqualTo(expect[i]), $"mobTypeId {ids[i]}");
                Assert.That(mob.AttackDamage, Is.EqualTo(0f), $"{expect[i]} 全被动无攻击");
                Assert.That(mob.MoveSpeed, Is.GreaterThan(0f), $"{expect[i]} 会动");
            }
        }

        [Test]
        public void TickAquatic_BlocksAtWaterBoundary_DoesNotLeaveWater()
        {
            // 3×3 水柱（y 60-62 是水，其它是石头边界），鱼从中间出发向 +X 游
            var world = new World();
            for (int x = 0; x <= 2; x++)
            {
                for (int y = 60; y <= 62; y++)
                {
                    for (int z = 0; z <= 2; z++)
                    {
                        world.SetBlock(x, y, z, BlockIds.Water);
                    }
                }
            }

            var mob = Mob.Create(28, new Float3(1.5f, 61.5f, 1.5f)); // Cod
            mob.State = MobState.Wander;
            mob.WanderTarget = new Float3(50f, 61.5f, 1.5f); // 直指水域外
            Float3 before = mob.Position;

            for (int i = 0; i < 60; i++)
            {
                MobAI.Tick(mob, new Float3(1.5f, 70f, 1.5f), world, null, 0.05f, false);
            }

            Assert.That(mob.Position.X, Is.LessThan(3.0f), "鱼不能游出水柱（边界折返）");
            Assert.That(mob.Position.X, Is.GreaterThan(-1f), "也没游丢");
        }

        [Test]
        public void TickFlyer_HoldsHeight_AndDropsWhenFleeing()
        {
            var mob = Mob.Create(33, new Float3(0, 70, 0)); // Sparrow
            mob.State = MobState.Wander;
            mob.WanderTarget = new Float3(5f, 70f, 0);
            Float3 start = mob.Position;

            for (int i = 0; i < 20; i++)
            {
                MobAI.Tick(mob, new Float3(0, 70, 30), null, null, 0.1f, false);
            }

            Assert.That(mob.Position.Y, Is.EqualTo(start.Y).Within(1e-4),
                "盘旋时 Y 钉死在出生高度（无重力悬停）");
            Assert.That(mob.Position.X, Is.GreaterThan(0f), "水平照常朝目标游走");

            // 受击 → 逃跑窗内下落
            MobAI.TakeHit(mob, new Float3(-5f, 70f, 0), 1f);
            Assert.That(mob.State, Is.EqualTo(MobState.FleeingFromAttacker), "受击开逃跑窗");
            float yBefore = mob.Position.Y;
            MobAI.Tick(mob, new Float3(-5f, 70f, 0), null, null, 1f, false);
            Assert.That(mob.Position.Y, Is.LessThan(yBefore), "逃跑窗内掉高度（受击下落）");
        }

        [Test]
        public void TakeHit_NewKinds_TriggerFlee()
        {
            foreach (int id in new[] { 28, 29, 30, 31, 32, 33, 34, 35, 36 })
            {
                var mob = Mob.Create(id, new Float3(0, 64, 0));
                bool killed = MobAI.TakeHit(mob, new Float3(5, 64, 0), 0.5f);
                Assert.That(killed, Is.False, $"mobTypeId {id} 0.5 伤不该死");
                Assert.That(mob.State, Is.EqualTo(MobState.FleeingFromAttacker),
                    $"mobTypeId {id}（水生/飞行）受击要逃");
            }
        }

        [Test]
        public void EnumValues_28To36_FixedForSaveCompatibility()
        {
            Assert.That((int)MobKind.Cod, Is.EqualTo(28));
            Assert.That((int)MobKind.Turtle, Is.EqualTo(32));
            Assert.That((int)MobKind.Butterfly, Is.EqualTo(36),
                "枚举值固定——存档 spawn 数据按值序列化（m11 约定延续）");
        }
    }
}
