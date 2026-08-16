using MyWorld.Core.Items;
using MyWorld.Core.Math;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 动物实体。
    /// 旧 mobTypeId：1=猪, 2=羊, 3=僵尸, 4=骷髅, 5=苦力怕（既有逻辑，按 MobKind.Passive/Hostile 处理）。
    /// Phase D 新增 mobTypeId：6=猪(type-specific), 7=牛, 8=鸡, 9=僵尸(type-specific)。
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

        // m9 A2：受击逃跑剩余时间（秒）。MobAI.TakeHit 置为 MobAI.FleeDuration（3s），
        // TickPassive 逐帧递减，归零即退出 FleeingFromAttacker 回落 wander 流。
        public float FleeUntil;
        public float DeathTimer;       // Dying 计时
        public float AttackCooldown;   // 下次可攻击剩余时间
        public float AttackRange = 1.6f;   // 不同 mob 攻击距离不同（skeleton=6, 新僵尸=4——m7 A2 由 8 收窄）

        // creeper 专用：自爆倒计时（>0 表示正在引爆中）
        public float FuseTimer;

        // Phase D 新增：type-specific 速度（默认 1.5=既有 WanderSpeed，旧 mobTypeId 沿用）
        public float MoveSpeed = 1.5f;

        // Phase D 新增：type-specific 追击半径（默认 16=既有 HostileChaseRadius，旧 mobTypeId 沿用）
        public float ChaseRadius = 16f;

        // Phase D 新增：死亡时由 MobAI.Tick 写入的掉落表结果，Unity 侧可在 Dying 时取用
        public ItemStack[] LastDrops;

        public bool IsAlive => State != MobState.Dead && State != MobState.Dying;

        public static Mob Create(int mobTypeId, Float3 position)
        {
            return mobTypeId switch
            {
                // 旧 mobTypeId（既有 Passive/Hostile 走法）
                1 => new Mob { MobTypeId = 1, Kind = MobKind.Passive, Health = new Health(10), Position = position, WanderCooldown = 2f },
                2 => new Mob { MobTypeId = 2, Kind = MobKind.Passive, Health = new Health(8), Position = position, WanderCooldown = 2f },
                3 => new Mob { MobTypeId = 3, Kind = MobKind.Hostile, Health = new Health(20), Position = position, AttackDamage = 2f, WanderCooldown = 2f },
                4 => new Mob { MobTypeId = 4, Kind = MobKind.Hostile, Health = new Health(16), Position = position, AttackDamage = 3f, AttackRange = 6f, WanderCooldown = 2f },
                5 => new Mob { MobTypeId = 5, Kind = MobKind.Hostile, Health = new Health(20), Position = position, WanderCooldown = 2f },

                // Phase D 新增 mobTypeId（spec line 173 type-specific）。
                // m9 A2 血量平衡：猪 10 / 牛 15 / 鸡 4——木剑 4 伤 3 下杀猪、5 下杀牛、1 下杀鸡，
                // 与「打一下→追→再打」的逃跑节奏（见 MobAI.TakeHit）配套。
                6 => new Mob { MobTypeId = 6, Kind = MobKind.Pig, Health = new Health(10), Position = position, WanderCooldown = 2f, MoveSpeed = 1.5f },
                7 => new Mob { MobTypeId = 7, Kind = MobKind.Cow, Health = new Health(15), Position = position, WanderCooldown = 2f, MoveSpeed = 1.0f },
                8 => new Mob { MobTypeId = 8, Kind = MobKind.Chicken, Health = new Health(4), Position = position, WanderCooldown = 2f, MoveSpeed = 0.5f },
                // m7 A2 平衡：AttackRange 8→4、ChaseRadius 32→20（追击半径收窄 +
                // 白天不追见 MobAI.Tick；僵尸昼夜压着玩家打是死亡循环威胁侧根因）
                9 => new Mob { MobTypeId = 9, Kind = MobKind.Zombie, Health = new Health(20), Position = position, AttackDamage = 2f, AttackRange = 4f, ChaseRadius = 20f, WanderCooldown = 2f, MoveSpeed = 3.5f },

                // Task D6：Villager 占位 mobTypeId。MobAI 走中立分支（stand still），
                // MoveSpeed 留 0（不靠 wander / flee 速度）。
                10 => new Mob { MobTypeId = 10, Kind = MobKind.Villager, Health = new Health(20), Position = position, WanderCooldown = 2f, MoveSpeed = 0f },

                _ => throw new System.ArgumentException($"未知 mobTypeId: {mobTypeId}"),
            };
        }

        public bool IsCreeper => MobTypeId == 5;
    }
}