// 评审 08 F3：垂钓状态机与鱼获掷骰的核心契约（dotnet / EditMode 双链同跑）。
using System.Collections.Generic;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class FishingSystemTests
    {
        [Test]
        public void 抛竿_等待区间_咬钩窗口_脱钩全链()
        {
            var s = new FishingSession();

            // 抛竿：hash=0 → 等待恰 3.0s（MinWait）；hash 拉满 → 10.0s（MaxWait）
            Assert.That(s.Cast(now: 100.0, hash: 0), Is.True, "Idle 抛竿成功");
            Assert.That(s.Phase, Is.EqualTo(FishingPhase.Waiting));
            Assert.That(s.Cast(100.5, 1), Is.False, "垂钓中重复抛竿 no-op");

            Assert.That(s.Tick(102.9), Is.False, "3s 前不咬钩");
            Assert.That(s.Tick(103.0), Is.True, "恰 3.0s 咬钩（边界含等号）");
            Assert.That(s.Phase, Is.EqualTo(FishingPhase.Biting));

            // 收杆窗口 1.5s：窗口内上鱼
            Assert.That(s.TryReel(103.5), Is.True, "窗口内收杆上鱼");
            Assert.That(s.Phase, Is.EqualTo(FishingPhase.Idle));

            // 脱钩路径：抛竿后不收，窗口过了回 Idle
            s.Cast(200.0, 0);
            s.Tick(203.0); // 咬钩
            Assert.That(s.Tick(204.6), Is.False, "窗口过后的 Tick 不再报边沿");
            Assert.That(s.Phase, Is.EqualTo(FishingPhase.Idle), "超窗脱钩回 Idle");
            Assert.That(s.TryReel(204.7), Is.False, "脱钩后收杆=空竿");
        }

        [Test]
        public void 提前收杆_空竿不炸()
        {
            var s = new FishingSession();
            s.Cast(0.0, 7);
            Assert.That(s.TryReel(0.5), Is.False, "等咬期提前收 = 空竿");
            Assert.That(s.Phase, Is.EqualTo(FishingPhase.Idle), "收杆总是回 Idle（可放弃）");
        }

        [Test]
        public void RollItemId_分布确定性与幸运效果()
        {
            int fish = 0, junk = 0, treasure = 0;
            for (int hash = 0; hash < 10000; hash++)
            {
                int id = FishingRoll.RollItemId(hash, 0);
                Assert.That(id, Is.EqualTo(FishingRoll.RollItemId(hash, 0)), "同 hash 同结果");
                if (id == FishingRoll.RawFishItemId) fish++;
                else if (id == 1002 || id == 1501) junk++;
                else if (id == 1600) treasure++;
                else Assert.Fail($"鱼获池外的物品 id：{id}");
            }

            // 名义 鱼 80% / 垃圾 15% / 宝藏 5%——万次采样容差 ±2%
            Assert.That(fish / 10000.0, Is.InRange(0.78, 0.82), $"鱼占比（实测 {fish}）");
            Assert.That(junk / 10000.0, Is.InRange(0.13, 0.17), $"垃圾占比（实测 {junk}）");
            Assert.That(treasure / 10000.0, Is.InRange(0.03, 0.07), $"宝藏占比（实测 {treasure}）");

            // 幸运 3 级：宝藏 5% → 20%，且封顶不过半
            int luckyTreasure = 0;
            for (int hash = 0; hash < 10000; hash++)
            {
                if (FishingRoll.RollItemId(hash, 3) == 1600) luckyTreasure++;
            }
            Assert.That(luckyTreasure / 10000.0, Is.InRange(0.18, 0.22),
                $"Luck 3 宝藏占比（实测 {luckyTreasure}）");
        }
    }
}
