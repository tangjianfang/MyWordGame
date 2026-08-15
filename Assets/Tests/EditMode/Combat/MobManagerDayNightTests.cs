#if UNITY_EDITOR
// m5 A1：MobManager 昼夜判定修复。旧代码 `phase < 0.5` 方向反了——
// 正午(0.25)被当夜晚刷僵尸，孩子进游戏就挨打掉 4 血。
// 正确区间：tick∈[13000,23000) → phase∈[0.5417,0.9583)。
// 整个文件用 #if UNITY_EDITOR 包裹：dotnet 链跑纯 Core 测试时跳过，Unity EditMode 链跑。
using NUnit.Framework;

namespace MyWorld.Core.Tests.Combat
{
    /// <summary>
    /// m5 A1：MobManager 昼夜判定修复。旧代码 <c>phase &lt; 0.5</c> 方向反了——
    /// 正午(0.25)被当夜晚刷僵尸，孩子进游戏就挨打掉 4 血。
    /// 正确区间：tick∈[13000,23000) → phase∈[0.5417,0.9583)。
    /// </summary>
    [TestFixture]
    public class MobManagerDayNightTests
    {
        [TestCase(0.00f, false)] // 午夜 tick 0 —— 注意 0 是「深夜结束/清晨」边界，IsNight=false
        [TestCase(0.25f, false)] // 正午 6000 tick —— 旧 bug 在这里判夜晚
        [TestCase(0.50f, false)] // 傍晚前 12000 tick
        [TestCase(0.55f, true)]  // 13200 tick，刚入夜
        [TestCase(0.70f, true)]  // 深夜 16800 tick
        [TestCase(0.95f, true)]  // 黎明前 22800 tick
        [TestCase(0.99f, false)] // 23760 tick，已出夜（NightEnd=23000 → 0.9583）
        public void IsNightPhase_MatchesTimeOfDayNightWindow(float phase, bool expected)
        {
            Assert.That(MyWorld.Unity.Combat.MobManager.IsNightPhase(phase), Is.EqualTo(expected),
                $"phase={phase} 的昼夜判定错误——刷怪光照会跟着错");
        }

        [Test]
        public void IsNightPhase_ConsistentWithTimeOfDayIsNight()
        {
            // 与 Core 的 TimeOfDay.IsNight（tick 区间）逐点对齐，防止两处阈值漂移
            var time = new MyWorld.Core.Time.TimeOfDay();
            for (int tick = 0; tick < 24000; tick += 500)
            {
                time.CurrentTick = tick;
                Assert.That(MyWorld.Unity.Combat.MobManager.IsNightPhase(time.DayPhase01),
                    Is.EqualTo(time.IsNight),
                    $"tick={tick} 处 MobManager 与 TimeOfDay 的昼夜判定不一致");
            }
        }
    }
}
#endif
