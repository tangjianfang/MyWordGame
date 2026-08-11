using MyWorld.Core.Math;

namespace MyWorld.Core.Entities
{
    /// <summary>伤害来源分类。</summary>
    public enum DamageSource
    {
        Melee,         // 近战
        Projectile,    // 远程（计划中）
        Fall,          // 摔落
        Suffocation,   // 窒息（在水里）
        Starvation,    // 饥饿
        Environmental, // 岩浆/仙人掌
    }

    /// <summary>
    /// 不可变伤害事件。Core 侧只搬运数据，Unity 侧订阅 <see cref="CombatEvents"/> 后呈现。
    /// </summary>
    public readonly struct DamageEvent
    {
        public readonly DamageSource Source;
        public readonly float Amount;
        public readonly int AttackerEntityId;   // 0 = 环境
        public readonly int VictimEntityId;
        public readonly Float3 HitPosition;
        public readonly bool IsCritical;        // 连击 / 暴击

        public DamageEvent(DamageSource source, float amount, int attacker, int victim, Float3 hit, bool crit = false)
        {
            Source = source;
            Amount = amount;
            AttackerEntityId = attacker;
            VictimEntityId = victim;
            HitPosition = hit;
            IsCritical = crit;
        }
    }
}
