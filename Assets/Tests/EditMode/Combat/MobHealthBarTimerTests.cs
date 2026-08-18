using MyWorld.Core.Combat;
using MyWorld.Core.Entities;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Combat
{
    /// <summary>
    /// m13 W2：MobView 头顶血条纯逻辑状态机测试。
    /// <para>
    /// 只测 <see cref="MobHealthBarTimer"/>（timer 字段）——OnGUI 路径在 EditMode 下
    /// 不跑（IMGUI 需 PlayMode），所以绘制（IMGUI vs billboard quad 取舍注释见
    /// <see cref="MobHealthBarTimer"/>）不进单元测试；UI shot 管线（<c>--ui-shot</c>）跑
    /// 集成。
    /// </para>
    /// </summary>
    [TestFixture]
    public class MobHealthBarTimerTests
    {
        [Test]
        public void 普通mob_构造后_不可见_Alpha为0()
        {
            var t = new MobHealthBarTimer(MobKind.Zombie);
            Assert.That(t.VisibleKind, Is.False, "僵尸不是 Boss，VisibleKind 应为 false");
            Assert.That(t.Alpha, Is.EqualTo(0f), "未受击时血条不可见");
        }

        [Test]
        public void 普通mob_受击_可见_3秒内Alpha为1()
        {
            var t = new MobHealthBarTimer(MobKind.Zombie);
            t.OnHit();
            Assert.That(t.Alpha, Is.EqualTo(1f).Within(0.001f),
                "受击 0 时刻 alpha 应为 1（还在 3s 满显窗口内）");

            t.Tick(1.0f);
            Assert.That(t.Alpha, Is.EqualTo(1f).Within(0.001f),
                "1 秒后剩余 2s，仍在 FadeOut 窗口之上，alpha 应仍为 1");
        }

        [Test]
        public void 普通mob_3秒后_进入淡出_Alpha线性下降()
        {
            var t = new MobHealthBarTimer(MobKind.Zombie);
            t.OnHit();
            t.Tick(2.6f);
            Assert.That(t.Alpha, Is.EqualTo(0.8f).Within(0.05f),
                "剩 0.4s 时 alpha 应线性降到 0.8（FadeOut 区间内）");
        }

        [Test]
        public void 普通mob_3秒后_完全淡出_Alpha为0()
        {
            var t = new MobHealthBarTimer(MobKind.Zombie);
            t.OnHit();
            t.Tick(MobHealthBarTimer.VisibleDuration + 0.1f);
            Assert.That(t.Alpha, Is.EqualTo(0f),
                "过 3.1s 应完全淡出（剩余 ≤ 0 时 alpha = 0）");
            Assert.That(t.Tick(0f), Is.False, "Tick 返回是否可见：不可见 = false");
        }

        [Test]
        public void 普通mob_连续受击_重置3秒计时()
        {
            var t = new MobHealthBarTimer(MobKind.Zombie);
            t.OnHit();
            t.Tick(2.5f);
            Assert.That(t.Alpha, Is.EqualTo(1f).Within(0.001f),
                "前置：烧 2.5s 后仍在满显窗口");

            t.OnHit();
            Assert.That(t.Alpha, Is.EqualTo(1f).Within(0.001f),
                "再受击应立刻重置到 3s 满显窗口");

            t.Tick(1f);
            Assert.That(t.Alpha, Is.EqualTo(1f).Within(0.001f),
                "再受击后烧 1s 仍在满显窗口（剩余 2s）");
        }

        [Test]
        public void Boss_构造即可见_Alpha永远为1()
        {
            var t = new MobHealthBarTimer(MobKind.MachineGuardian);
            Assert.That(t.VisibleKind, Is.True, "Boss kind=27 应触发 VisibleKind=true");

            Assert.That(t.Alpha, Is.EqualTo(1f), "Boss 未受击也应可见（永远 alpha=1）");
            Assert.That(t.Tick(100f), Is.True, "Boss Tick 永远返回可见");
            Assert.That(t.Alpha, Is.EqualTo(1f), "Boss 烧 100s 仍 alpha=1（不受 3s 限制）");

            t.OnHit();
            Assert.That(t.Alpha, Is.EqualTo(1f));
        }

        [Test]
        public void FillRatio_当前血与最大血_返回比例()
        {
            Assert.That(MobHealthBarTimer.FillRatio(20f, 20f), Is.EqualTo(1f));
            Assert.That(MobHealthBarTimer.FillRatio(10f, 20f), Is.EqualTo(0.5f));
            Assert.That(MobHealthBarTimer.FillRatio(0f, 20f), Is.EqualTo(0f));
            Assert.That(MobHealthBarTimer.FillRatio(5f, 0f), Is.EqualTo(0f),
                "MaxHealth=0 时不应除零爆炸，返回 0");
        }

        [Test]
        public void 普通mob_OnHit_前_Tick仍递减_可见窗不延长()
        {
            var t = new MobHealthBarTimer(MobKind.Zombie);
            t.Tick(5f);
            Assert.That(t.Alpha, Is.EqualTo(0f),
                "从未受击的 timer 烧 5s 仍应不可见");
            Assert.That(t.Tick(0f), Is.False);
        }
    }
}
