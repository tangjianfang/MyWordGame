using MyWorld.Core.Entities;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// m11 W2-3：血条心形布局纯数学（Core/Entities/HeartMath）。心数随有效上限
    /// （Health.Max + MaxHealthBonus）走、半心 = 余 1 点血、&gt;10 心按行排列——
    /// 机元装备 +2/件此前生效但 UI 不多画心，这套契约就是那条欠账的守卫。
    /// 纯 Core，dotnet 与 EditMode 双链同跑。
    /// </summary>
    [TestFixture]
    public class HeartMathTests
    {
        // ─── 心数：随有效上限走 ─────────────────────────────────────────

        [TestCase(20f, 10)]
        [TestCase(24f, 12)]
        [TestCase(26f, 13)]
        [TestCase(21f, 11, Description = "奇数上限向上取整：多出的 1 点血画半心可见")]
        [TestCase(2f, 1)]
        [TestCase(0f, 0)]
        [TestCase(-5f, 0)]
        public void TotalHearts_FollowsEffectiveMax(float effectiveMax, int expected)
        {
            Assert.That(HeartMath.TotalHearts(effectiveMax), Is.EqualTo(expected),
                $"有效上限 {effectiveMax} 应画 {expected} 颗心（每心 2HP，向上取整）");
        }

        // ─── 半心：余 1 点血 ────────────────────────────────────────────

        [Test]
        public void FillAt_FullHeart_AtTwoHealth()
        {
            Assert.That(HeartMath.FillAt(0, 2f), Is.EqualTo(HeartMath.Fill.Full), "第 1 颗心在血量 2 时满");
            Assert.That(HeartMath.FillAt(1, 4f), Is.EqualTo(HeartMath.Fill.Full), "第 2 颗心在血量 4 时满");
        }

        [Test]
        public void FillAt_HalfHeart_AtOneHealth()
        {
            Assert.That(HeartMath.FillAt(0, 1f), Is.EqualTo(HeartMath.Fill.Half), "血量 1 = 半心（旧 UI 已有语义，不许漂移）");
            Assert.That(HeartMath.FillAt(1, 3f), Is.EqualTo(HeartMath.Fill.Half), "血量 3：第 1 心满 + 第 2 心半");
        }

        [Test]
        public void FillAt_Empty_BelowOneHealth()
        {
            Assert.That(HeartMath.FillAt(0, 0.5f), Is.EqualTo(HeartMath.Fill.Empty), "血量 0.5：第 1 颗心空（半心只在 ≥1 点时出现）");
            Assert.That(HeartMath.FillAt(1, 1.9f), Is.EqualTo(HeartMath.Fill.Empty), "血量 1.9：第 2 颗心空");
        }

        [Test]
        public void FillAt_TwelveHearts_OddMax_LastHeartCapsAtHalf()
        {
            // 上限 24（12 心）+ 血量 24 → 全满；血量 23 → 第 12 颗只可能半
            Assert.That(HeartMath.FillAt(11, 24f), Is.EqualTo(HeartMath.Fill.Full), "满血 24：第 12 颗心满");
            Assert.That(HeartMath.FillAt(11, 23f), Is.EqualTo(HeartMath.Fill.Half), "血量 23：第 12 颗心半（上限 24 的最后一颗永远到不了 26）");
        }

        // ─── 行排布：>10 心两行，多出的行向上叠 ──────────────────────────

        [TestCase(1, 1)]
        [TestCase(10, 1)]
        [TestCase(11, 2)]
        [TestCase(12, 2)]
        [TestCase(20, 2)]
        [TestCase(21, 3)]
        [TestCase(0, 0)]
        public void RowCount_SplitsAtTenPerRow(int totalHearts, int expectedRows)
        {
            Assert.That(HeartMath.RowCount(totalHearts), Is.EqualTo(expectedRows),
                $"{totalHearts} 颗心应排 {expectedRows} 行（每行 10 颗）");
        }

        [Test]
        public void HeartsInRow_BottomRowFull_TopRowHoldsRemainder()
        {
            Assert.That(HeartMath.HeartsInRow(0, 12), Is.EqualTo(10), "12 心：底行排满 10");
            Assert.That(HeartMath.HeartsInRow(1, 12), Is.EqualTo(2), "12 心：顶行摆余数 2（向上叠）");
            Assert.That(HeartMath.HeartsInRow(2, 12), Is.EqualTo(0), "12 心：没有第三行");
            Assert.That(HeartMath.HeartsInRow(0, 8), Is.EqualTo(8), "8 心单行照旧");
        }

        [Test]
        public void PositionOf_RowZeroIsFirstTen_SecondRowStartsAtTen()
        {
            Assert.That(HeartMath.PositionOf(0), Is.EqualTo((0, 0)), "第 1 颗心在底行最左");
            Assert.That(HeartMath.PositionOf(9), Is.EqualTo((0, 9)), "第 10 颗心在底行最右");
            Assert.That(HeartMath.PositionOf(10), Is.EqualTo((1, 0)), "第 11 颗心起第二行（向上叠）");
            Assert.That(HeartMath.PositionOf(11), Is.EqualTo((1, 1)), "第 12 颗心在第二行第 2 列");
        }

        // ─── 组合：机元装备 +2/件场景 ──────────────────────────────────

        [Test]
        public void MaxHealthBonus_AboveTenHearts_ProducesTwoRows()
        {
            // m10 机元装备 maxHealth +2/件：基础 20（10 心）+ 2 件 +4 = 24（12 心、2 行）
            float effectiveMax = 20f + 4f;
            int hearts = HeartMath.TotalHearts(effectiveMax);
            Assert.That(hearts, Is.EqualTo(12), "20+4 上限应画 12 颗心");
            Assert.That(HeartMath.RowCount(hearts), Is.EqualTo(2), "12 心排两行（>10 心两行排列）");
            Assert.That(HeartMath.FillAt(10, 23f), Is.EqualTo(HeartMath.Fill.Full), "血量 23：前 11 颗全满（覆盖 22 点）");
            Assert.That(HeartMath.FillAt(11, 23f), Is.EqualTo(HeartMath.Fill.Half), "第 12 颗心半（余 1 点）");
        }
    }
}
