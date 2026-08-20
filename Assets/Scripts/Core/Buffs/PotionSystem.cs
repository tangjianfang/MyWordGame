using System.Collections.Generic;

namespace MyWorld.Core.Buffs
{
    /// <summary>药水带来的增益种类（m12 W3）。治疗是即时效果不进 buff 池。</summary>
    public enum BuffKind
    {
        Swiftness = 1,
        Strength,
        Leaping,
        NightVision,
        WaterBreathing,
    }

    /// <summary>
    /// 药水 buff 状态机（m12 W3）——纯 Core、无 Unity 依赖，时间由调用方传入
    /// （运行时喂 <c>Time.time</c>，测试喂显式秒数，与 FlightState 同款做法）。
    /// <para>
    /// 数值（取舍记录，注释即文档）：速度 +30% / 力量 +2 攻 / 跳跃 ×1.3 /
    /// 夜视（视觉提亮，Unity 侧 PotionHost 消费）/ 水肺占位 = 生命上限 +4（水无
    /// 氧气系统，照 m12 计划的取舍注释改 +heart 上限）。持续一律 30s。
    /// buff 是短时状态，<b>存档不记</b>（重进世界 buff 清零，与飞行态同取舍）。
    /// </para>
    /// </summary>
    public sealed class PotionSystem
    {
        public const float DurationSeconds = 30f;
        public const float SwiftnessMoveBonus = 0.30f;
        public const int StrengthAttackBonus = 2;
        public const float LeapingJumpMultiplier = 1.3f;
        public const int WaterBreathingMaxHealthBonus = 4;

        private readonly Dictionary<BuffKind, float> _expiry = new Dictionary<BuffKind, float>();

        /// <summary>喝下一种 buff（续喝刷新到 now + 30s）。</summary>
        public void Drink(BuffKind kind, float now)
        {
            _expiry[kind] = now + DurationSeconds;
        }

        public bool Active(BuffKind kind, float now)
            => _expiry.TryGetValue(kind, out float until) && now < until;

        /// <summary>移速加成（比例，喂给 PlayerMotorSettings.MoveSpeedBonus）。</summary>
        public float MoveSpeedBonus(float now)
            => Active(BuffKind.Swiftness, now) ? SwiftnessMoveBonus : 0f;

        /// <summary>攻击加成（点数，叠进 CombatController.ResolveAttackDamage）。</summary>
        public int AttackBonus(float now)
            => Active(BuffKind.Strength, now) ? StrengthAttackBonus : 0;

        /// <summary>跳跃倍率（乘进 PlayerMotorSettings.JumpSpeed）。</summary>
        public float JumpMultiplier(float now)
            => Active(BuffKind.Leaping, now) ? LeapingJumpMultiplier : 1f;

        /// <summary>生命上限加成（点数，水肺占位效果）。</summary>
        public int MaxHealthBonus(float now)
            => Active(BuffKind.WaterBreathing, now) ? WaterBreathingMaxHealthBonus : 0;

        /// <summary>测试用：清空全部 buff（跨夹具防污染，与 DifficultyMode.ResetCache 同纪律）。</summary>
        public void ResetAll()
        {
            _expiry.Clear();
        }
    }
}
