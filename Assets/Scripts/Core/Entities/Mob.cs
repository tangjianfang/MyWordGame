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

        // m9 A3：死因标记——MobAI.TakeHit 致死一击置 true（当前 TakeHit 的唯一生产调用方是
        // 玩家近战 CombatController.DoAttack）。Unity 侧 MobManager 在 Dying 序列里据此入账
        // 击杀经验（猪 3/牛 5/鸡 2/僵尸 10），发完复位（幂等）。
        // mob 不入存档（level.dat 只存 seed/时间/玩家/熔炉/掉落物），无序列化面。
        public bool KilledByPlayer;

        // m11 W1-6（集成点②）：视觉缩放（繁殖幼崽 = BreedingSystem.BabyScale 0.5，长大回 1）。
        // 纯视觉字段——碰撞/命中/掉落一律按成体结算，只有 MobView 读它写 transform.localScale。
        // 默认 1 = 既有行为；<=0 按兜底 1 处理（防御式，正常流程不会写出非正值）。
        public float VisualScale = 1f;

        // ─── m11 W3-3：机元守卫 Boss 专属状态（其余 mob 恒为默认值，不参与） ──

        /// <summary>Boss 冲撞进行中的剩余时间（秒，>0 = 正在冲）。MobAI.TickBoss 置
        /// <see cref="MyWorld.Core.Entities.MobAI.BossChargeDuration"/>，逐帧递减。</summary>
        public float BossChargeTimer;

        /// <summary>Boss 冲撞起手冷却（秒），归零后玩家在起手窗口内可再触发。</summary>
        public float BossChargeCooldown;

        /// <summary>Boss 震荡波冷却（秒），归零后玩家进 6m 即触发下一发。</summary>
        public float BossShockwaveCooldown;

        /// <summary>Boss 变招计数：只在「多招同时可用、哈希二选一」时递增——
        /// (EntityId, 计数) 唯一决定变招序列，同一场战斗重放一致。</summary>
        public int BossMoveCounter;

        /// <summary>半血召唤已发（每只 Boss 只召一次 2 骷髅，见 MobAI.TickBoss）。</summary>
        public bool BossSummoned;

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

                // m11 W1-1：12 新生物建档（P0 只接了枚举/分派，Create 曾对 15-26 一律抛
                // 「未知 mobTypeId」——spawn_rules 有条目后夜间刷怪会当场炸，这里是一次性补齐）。
                // 被动 9 个照猪组建档（wander + 受击逃 3s），血量按卡片：羊8 兔3 狐5 鹿8
                // 熊猫15 企鹅4 山羊8 浣熊4 仓鼠2（猪10/牛15/鸡4 的量级）；MoveSpeed 按体型：
                // 兔/狐/鹿敏捷、熊猫企鹅迟缓，其余贴猪 1.5 上下。
                15 => new Mob { MobTypeId = 15, Kind = MobKind.Sheep, Health = new Health(8), Position = position, WanderCooldown = 2f, MoveSpeed = 1.2f },
                16 => new Mob { MobTypeId = 16, Kind = MobKind.Rabbit, Health = new Health(3), Position = position, WanderCooldown = 2f, MoveSpeed = 2.4f },
                17 => new Mob { MobTypeId = 17, Kind = MobKind.Fox, Health = new Health(5), Position = position, WanderCooldown = 2f, MoveSpeed = 3.0f },
                18 => new Mob { MobTypeId = 18, Kind = MobKind.Deer, Health = new Health(8), Position = position, WanderCooldown = 2f, MoveSpeed = 2.8f },
                19 => new Mob { MobTypeId = 19, Kind = MobKind.Panda, Health = new Health(15), Position = position, WanderCooldown = 2f, MoveSpeed = 0.8f },
                20 => new Mob { MobTypeId = 20, Kind = MobKind.Penguin, Health = new Health(4), Position = position, WanderCooldown = 2f, MoveSpeed = 0.9f },
                21 => new Mob { MobTypeId = 21, Kind = MobKind.Goat, Health = new Health(8), Position = position, WanderCooldown = 2f, MoveSpeed = 1.4f },
                22 => new Mob { MobTypeId = 22, Kind = MobKind.Raccoon, Health = new Health(4), Position = position, WanderCooldown = 2f, MoveSpeed = 1.2f },
                23 => new Mob { MobTypeId = 23, Kind = MobKind.Hamster, Health = new Health(2), Position = position, WanderCooldown = 2f, MoveSpeed = 1.0f },

                // 敌对 3 个照卡片数值（量级对齐旧 mobTypeId 4 骷髅 / 5 苦力怕）：
                // 骷髅血16 远程风筝（射程窗口 8-12m 见 MobAI.SkeletonMin/MaxRange）、
                // 蜘蛛血16 近战2点 + 夜间追击速度 4.5（MobAI.SpiderChaseSpeed）、
                // 苦力怕血20 引信自爆（<3m 触发 / 1.5s / 爆炸见 Combat.Explosion）。
                // AttackRange 对骷髅取射程窗口上限 12（AI 分支的保持距离判定基准）、
                // 对苦力怕取触发半径 3，语义与旧僵尸 AttackRange（近战距离）一致可读。
                24 => new Mob { MobTypeId = 24, Kind = MobKind.Skeleton, Health = new Health(16), Position = position, AttackDamage = 2f, AttackRange = 12f, ChaseRadius = 20f, WanderCooldown = 2f, MoveSpeed = 3.0f },
                25 => new Mob { MobTypeId = 25, Kind = MobKind.Spider, Health = new Health(16), Position = position, AttackDamage = 2f, AttackRange = 4f, ChaseRadius = 20f, WanderCooldown = 2f, MoveSpeed = 4.5f },
                26 => new Mob { MobTypeId = 26, Kind = MobKind.Creeper, Health = new Health(20), Position = position, ChaseRadius = 20f, WanderCooldown = 2f, MoveSpeed = 3.0f },

                // m11 W3-3：机元守卫 Boss 建档——血 60（铁剑 4 伤 15 下 / 钻剑 7 伤 9 下，
                // 半血还有 2 骷髅搅局）/ 伤 6（近身冲撞与冲撞收尾共用）/ 速 3.5（与僵尸同档，
                // 冲撞窗口内翻倍到 7）/ 追击 20（同僵尸）。经验 50 在 Unity 侧
                // MobManager.KillExperience 常量表。只经图腾召唤，不进自然刷怪。
                27 => new Mob { MobTypeId = 27, Kind = MobKind.MachineGuardian, Health = new Health(60), Position = position, AttackDamage = 6f, AttackRange = 4f, ChaseRadius = 20f, WanderCooldown = 2f, MoveSpeed = 3.5f },

                _ => throw new System.ArgumentException($"未知 mobTypeId: {mobTypeId}"),
            };
        }

        public bool IsCreeper => MobTypeId == 5;
    }
}