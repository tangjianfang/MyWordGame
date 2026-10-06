// 评审 08 F2：剪羊毛核心契约（dotnet / EditMode 双链同跑）。
// 任务链 ch2_01 文案已承诺「或剪羊」——冷却语义照 BreedingSystem 绝对时间阈值。
using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Farming;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Farming
{
    [TestFixture]
    public class ShearSystemTests
    {
        [Test]
        public void TryShear_活羊首剪成功_冷却内拒_到期可再剪()
        {
            // Mob.Create(15) 即 Sheep（MobKind.Sheep=15 与 Create 的 mobTypeId 同值域）
            var sheep = Mob.Create(15, new Float3(0f, 1f, 0f));
            Assert.That(sheep.Kind, Is.EqualTo(MobKind.Sheep), "前置：Create(15) 是羊");
            var sheared = new Dictionary<long, double>();

            Assert.That(ShearSystem.TryShear(sheep, now: 100.0, sheared), Is.True, "首剪成功");
            Assert.That(ShearSystem.TryShear(sheep, now: 100.0 + ShearSystem.CooldownSeconds - 1, sheared),
                Is.False, "冷却内再剪 no-op（羊毛还没长回来）");
            Assert.That(ShearSystem.TryShear(sheep, now: 100.0 + ShearSystem.CooldownSeconds, sheared),
                Is.True, "冷却到期可再剪（边界含等号）");
            Assert.That(ShearSystem.TryShear(sheep, now: 1e9, null), Is.True,
                "null 冷却表（极端装配）不炸——每次都允许剪");
        }

        [Test]
        public void TryShear_非羊与尸体一律拒绝()
        {
            var pig = Mob.Create(6, new Float3(0f, 1f, 0f)); // Pig
            var dead = Mob.Create(15, new Float3(0f, 1f, 0f));
            dead.State = MobState.Dead;
            var sheared = new Dictionary<long, double>();

            Assert.That(ShearSystem.TryShear(pig, 100.0, sheared), Is.False, "猪不可剪");
            Assert.That(ShearSystem.TryShear(dead, 100.0, sheared), Is.False, "尸体不可剪");
            Assert.That(ShearSystem.TryShear(null, 100.0, sheared), Is.False, "null 安全");
            Assert.That(sheared.Count, Is.EqualTo(0), "拒绝路径零副作用");
        }

        [Test]
        public void RollWoolCount_区间内确定性()
        {
            for (int hash = 0; hash < 200; hash++)
            {
                int count = ShearSystem.RollWoolCount(hash);
                Assert.That(count, Is.InRange(1, 2), "羊毛恒 1-2 个");
                Assert.That(count, Is.EqualTo(ShearSystem.RollWoolCount(hash)), "同 hash 同结果（确定性）");
            }
        }
    }
}
