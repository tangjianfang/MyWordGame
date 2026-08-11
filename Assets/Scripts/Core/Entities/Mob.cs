using MyWorld.Core.Math;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 动物实体。MobTypeId 1=猪, 2=羊, 3=僵尸, 4=骷髅, 5=苦力怕。
    /// 渲染由 Unity 侧按 MobTypeId 找对应 GameObject 模板。
    /// </summary>
    public sealed class Mob
    {
        public int MobTypeId;
        public int EntityId;        // 运行时唯一 ID（用于 DamageEvent.AttackerEntityId）
        public MobKind Kind;
        public MobState State;
        public Health Health;
        public Float3 Position;
        public Float3 Velocity;
        public Float3 WanderTarget;
        public float WanderCooldown;
        public float AttackDamage;     // zombie=2, skeleton=3, creeper=0
        public float LastAttackTime;
        public Float3 LastAttackerPos;
        public float HitFlashTimer;    // 受伤红闪剩余时间
        public float DeathTimer;       // Dying 计时
        public float AttackCooldown;   // 下次可攻击剩余时间
        public float AttackRange = 1.6f;   // 不同 mob 攻击距离不同（skeleton=6）

        // creeper 专用：自爆倒计时（>0 表示正在引爆中）
        public float FuseTimer;

        public bool IsAlive => State != MobState.Dead && State != MobState.Dying;

        public static Mob Create(int mobTypeId, Float3 position)
        {
            return mobTypeId switch
            {
                1 => new Mob { MobTypeId = 1, Kind = MobKind.Passive, Health = new Health(10), Position = position, WanderCooldown = 2f },
                2 => new Mob { MobTypeId = 2, Kind = MobKind.Passive, Health = new Health(8), Position = position, WanderCooldown = 2f },
                3 => new Mob { MobTypeId = 3, Kind = MobKind.Hostile, Health = new Health(20), Position = position, AttackDamage = 2f, WanderCooldown = 2f },
                4 => new Mob { MobTypeId = 4, Kind = MobKind.Hostile, Health = new Health(16), Position = position, AttackDamage = 3f, AttackRange = 6f, WanderCooldown = 2f },
                5 => new Mob { MobTypeId = 5, Kind = MobKind.Hostile, Health = new Health(20), Position = position, WanderCooldown = 2f },
                _ => throw new System.ArgumentException($"未知 mobTypeId: {mobTypeId}"),
            };
        }

        public bool IsCreeper => MobTypeId == 5;
    }
}
