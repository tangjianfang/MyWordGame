// m12 P0-a：挖掘蓄力状态机纯数学契约（dotnet / EditMode 双链同源）。
// 换目标容差 / 进度累计 / 满格判定 / 裂纹档位换算都在这里钉死。
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class DigProgressTests
    {
        private static Float3 P(float x, float y, float z) => new Float3(x, y, z);

        [Test]
        public void Aim_SameBlock_KeepsProgress()
        {
            var dig = new DigProgress();
            dig.Aim(8, 70, 8, P(8.5f, 71f, 8.5f));
            dig.Tick(0.5f, 1f);

            bool changed = dig.Aim(8, 70, 8, P(8.9f, 71f, 8.9f));

            Assert.That(changed, Is.False, "同格对准不应报告目标切换");
            Assert.That(dig.Fraction, Is.EqualTo(0.5f).Within(1e-4), "同格续挖进度不清");
        }

        [Test]
        public void Aim_EdgeJitter_WithinTolerance_KeepsTarget()
        {
            var dig = new DigProgress();
            dig.Aim(8, 70, 8, P(9f, 71f, 8.5f)); // 贴右棱的命中点
            dig.Tick(0.5f, 1f);

            // 抖到相邻格 (9,70,8)，但命中点只挪了 0.05 格 → 视作仍瞄原方块
            bool changed = dig.Aim(9, 70, 8, P(9.02f, 71f, 8.5f));

            Assert.That(changed, Is.False, "容差内抖动不应切换目标");
            Assert.That(dig.TargetX, Is.EqualTo(8), "保留原目标格");
            Assert.That(dig.Fraction, Is.EqualTo(0.5f).Within(1e-4), "抖动不清进度");
        }

        [Test]
        public void Aim_EdgeJitter_BeyondTolerance_SwitchesAndResets()
        {
            var dig = new DigProgress();
            dig.Aim(8, 70, 8, P(8.5f, 71f, 8.5f));
            dig.Tick(0.8f, 1f);

            // 命中点挪了 1 格（真换目标）→ 清零重蓄
            bool changed = dig.Aim(12, 70, 8, P(9.5f, 71f, 8.5f));

            Assert.That(changed, Is.True, "真换目标要报告切换");
            Assert.That(dig.TargetX, Is.EqualTo(12), "目标切到新格");
            Assert.That(dig.Fraction, Is.EqualTo(0f).Within(1e-4), "换目标清零重蓄");
        }

        [Test]
        public void Tick_Accumulates_ByBreakSeconds()
        {
            var dig = new DigProgress();
            dig.Aim(8, 70, 8, P(8.5f, 71f, 8.5f));

            dig.Tick(0.25f, 4f); // 石头徒手 4s：0.25s → 1/16
            Assert.That(dig.Fraction, Is.EqualTo(0.0625f).Within(1e-4));

            dig.Tick(3.75f, 4f); // 补满 4s
            Assert.That(dig.ShouldBreak, Is.True, "满 4s 应可破坏");
        }

        [Test]
        public void Tick_IllegalSeconds_FallsBackTo1s()
        {
            var dig = new DigProgress();
            dig.Aim(8, 70, 8, P(8.5f, 71f, 8.5f));

            dig.Tick(0.5f, 0f); // 非法耗时兜底 1s，不除零
            Assert.That(dig.Fraction, Is.EqualTo(0.5f).Within(1e-4));
        }

        [Test]
        public void ShouldBreak_FalseWithoutTarget()
        {
            var dig = new DigProgress();
            dig.Tick(10f, 0.1f);
            Assert.That(dig.ShouldBreak, Is.False, "没有目标攒不了进度");
            Assert.That(dig.HasTarget, Is.False);
        }

        [Test]
        public void Reset_ClearsTargetAndProgress()
        {
            var dig = new DigProgress();
            dig.Aim(8, 70, 8, P(8.5f, 71f, 8.5f));
            dig.Tick(0.9f, 1f);

            dig.Reset();

            Assert.That(dig.HasTarget, Is.False);
            Assert.That(dig.Fraction, Is.EqualTo(0f));
            Assert.That(dig.ShouldBreak, Is.False);
        }

        [Test]
        public void CrackStage_HiddenWhenNoTargetOrNoProgress()
        {
            var dig = new DigProgress();
            Assert.That(dig.CrackStage(5), Is.EqualTo(-1), "无目标不显示裂纹");

            dig.Aim(8, 70, 8, P(8.5f, 71f, 8.5f));
            Assert.That(dig.CrackStage(5), Is.EqualTo(-1), "零进度不显示裂纹（防刚按下就闪第 0 档）");
        }

        [Test]
        public void CrackStage_ProgressionAcrossFiveStages()
        {
            var dig = new DigProgress();
            dig.Aim(8, 70, 8, P(8.5f, 71f, 8.5f));

            dig.Tick(0.1f, 1f);
            Assert.That(dig.CrackStage(5), Is.EqualTo(0), "10% → 第 0 档");

            dig.Tick(0.2f, 1f); // 30%
            Assert.That(dig.CrackStage(5), Is.EqualTo(1), "30% → 第 1 档");

            dig.Tick(0.45f, 1f); // 75%
            Assert.That(dig.CrackStage(5), Is.EqualTo(3), "75% → 第 3 档");

            dig.Tick(0.2f, 1f); // 95%
            Assert.That(dig.CrackStage(5), Is.EqualTo(4), "95% → 第 4 档");

            dig.Tick(1f, 1f); // 195% 越顶
            Assert.That(dig.CrackStage(5), Is.EqualTo(4), "越顶钳到最后一档");
        }

        [Test]
        public void Aim_ToleranceExactlyAtBoundary_Switches()
        {
            var dig = new DigProgress();
            dig.Aim(8, 70, 8, P(8.5f, 71f, 8.5f));

            // 恰好 0.1 格（= 容差值，非"小于"）→ 视作真换目标
            bool changed = dig.Aim(9, 70, 8, P(8.6f, 71f, 8.5f));

            Assert.That(changed, Is.True, "距离 == 容差不属于『小于容差』，应切换");
        }
    }
}
