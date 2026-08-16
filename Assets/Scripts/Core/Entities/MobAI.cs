using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 动物 AI tick。每帧调用，移动 + 状态切换。
    /// 不依赖 Unity——可单测。
    /// <para>
    /// Phase D 扩展（spec line 171）：加 <c>MobKind.Pig / Cow / Chicken / Zombie</c> switch，
    /// type-specific 行为——猪/牛/鸡 走 Passive 流（wander + 受击逃跑），
    /// 新僵尸走 Hostile 流（chase/attack 半径由 <c>mob.ChaseRadius / mob.AttackRange</c> 控制）
    /// + 死亡自动触发 <see cref="MobDropTable"/>。
    /// m7 A2 平衡：新僵尸 AttackRange 8→4、ChaseRadius 32→20，且白天（<c>isNight=false</c>）
    /// 不追——与友好动物一样走 wander，威胁只在夜里成立。
    /// m9 A2 修打猎断环②：被动动物不再「玩家靠近就惊跑」（旧 ScareRadius=8 / 5m/s 退役——
    /// 惊跑 5m/s &gt; 玩家走速 4.3m/s、半径 8m &gt; 攻击距离 4m，玩家永远贴不了身），
    /// 改为 <see cref="TakeHit"/> 受击才逃：逃 <see cref="FleeDuration"/> 秒后停、
    /// 速度 <see cref="FleeSpeed"/>=4.0（追得上但不轻松）——「打一下→追→再打」的节奏。
    /// 既有 mobTypeId 1-5（Passive/Hostile/Neutral 分类）行为同步此语义。
    /// </para>
    /// </summary>
    public static class MobAI
    {
        public const float HostileChaseRadius = 16f;
        public const float HostileChaseRadiusSq = HostileChaseRadius * HostileChaseRadius;
        public const float AttackRange = 1.6f;
        public const float AttackRangeSq = AttackRange * AttackRange;
        public const float WanderSpeed = 1.5f;
        public const float ChaseSpeed = 3.5f;

        /// <summary>m9 A2：受击逃跑速度 4.0m/s——&lt; 玩家走速 4.3m/s，追得上但不轻松。</summary>
        public const float FleeSpeed = 4f;

        /// <summary>m9 A2：受击逃跑持续时间（秒），过期回落 wander 流。</summary>
        public const float FleeDuration = 3f;

        /// <summary>m9 A2：受击红闪时长（秒），Unity 侧 MobView 按 HitFlashTimer 闪红。
        /// m9 B1 对齐 spec §4 的 0.15s（此前承自 A1 的 0.2s，A2 评审 Minor 1 预告的一处对齐），
        /// 与 Unity 侧 MobHitFeedback.FlashDuration 同值——同一个红闪的两条渲染通道不漂移。</summary>
        public const float HitFlashDuration = 0.15f;

        public const float DefaultDeathTimer = 0.5f;

        /// <summary>
        /// X4.5：JSON 驱动的概率掉落表（<c>Entities.MobDropTable</c>）。
        /// 由 <c>WorldBootstrap</c> 在 Awake 末尾从 <c>StreamingAssets/mobs/drop_tables.json</c>
        /// 加载并注入；null 时 <see cref="Tick"/> 回退到静态 <c>Items.ItemDropTable.Drop</c>
        /// （legacy 路径，保证 MobAI.cs:48 的历史契约仍可独立单测）。
        /// </summary>
        public static MobDropTable DropTable { get; set; }

        /// <summary>
        /// m7 A2：昼夜开关（是否夜晚）。false 时僵尸（<see cref="MobKind.Zombie"/>）不追玩家、
        /// 走 wander。生产侧由 <c>MobManager.IsNightPhase</c>（单一真源）算好传入；
        /// 默认 true 保持既有调用与测试的语义（Core 单测常不传时钟）。
        /// 旧 Hostile 分支的昼夜判定仍以 <paramref name="time"/> 为准，不受本参数影响。
        /// </summary>
        /// <param name="isNight">是否夜晚（默认 true）。</param>
        public static void Tick(Mob mob, Float3 playerPos, World world, TimeOfDay time, float dt,
            bool isNight = true)
        {
            if (!mob.IsAlive) return;
            if (mob.HitFlashTimer > 0) mob.HitFlashTimer -= dt;
            if (mob.AttackCooldown > 0) mob.AttackCooldown -= dt;

            // Phase D 新增：自动死亡检测。若 Health 已经降到 0 但 State 还没转 Dying
            // （Core-only 路径：测试直扣 Health / 未来环境伤害；玩家近战自 m9 A3 起由
            // TakeHit 致死分支同步转），转 Dying + 调 MobDropTable 写入 LastDrops。
            // X4.5：优先用 JSON 驱动的 DropTable（RollAll 独立掷每条 entry，支持
            // Pig 1-3 porkchop / Zombie 0-2 rotten_flesh + 5% iron_ingot）；未注入时
            // 回退到静态 Items.ItemDropTable.Drop（legacy count=1，保持单测/旧场景）。
            if (mob.Health.IsDead)
            {
                TransitionToDying(mob);
                return;
            }

            float distSq = DistanceSquared(playerPos, mob.Position);
            // 旧 Hostile 分支沿用时钟判定（既有契约：白天当友好动物）
            bool timeIsNight = time != null && time.IsNight;

            switch (mob.Kind)
            {
                case MobKind.Passive:
                    TickPassive(mob, dt);
                    break;
                case MobKind.Hostile:
                    // 旧 Hostile 分支沿用时钟判定（既有契约：白天当友好动物）。
                    // m9 A2：TakeHit 只对被动动物触发逃跑，Hostile 永不进 FleeingFromAttacker，
                    // 原「FleeingFromAttacker 时白天仍走敌对流」的条件随之退役。
                    if (!timeIsNight)
                    {
                        // 白天：和友好动物一样行为
                        TickPassive(mob, dt);
                    }
                    else
                    {
                        TickHostile(mob, playerPos, distSq, dt, world);
                    }
                    break;

                // Phase D 新增 type-specific（spec line 173）
                case MobKind.Pig:
                case MobKind.Cow:
                case MobKind.Chicken:
                    // 友好动物：wander + 受击逃跑（m9 A2 起靠近不再惊跑），不追玩家
                    TickPassive(mob, dt);
                    break;
                case MobKind.Zombie:
                    // m7 A2：白天（isNight=false）不追——与友好动物一样走 wander（TickPassive），
                    // 威胁只在夜里成立。isNight 由宿主传入（MobManager.IsNightPhase 单一真源），
                    // 不在此读第二套时钟。chase/attack 半径由 mob.ChaseRadius / mob.AttackRange 控制。
                    if (isNight)
                    {
                        TickHostile(mob, playerPos, distSq, dt, world);
                    }
                    else
                    {
                        TickPassive(mob, dt);
                    }
                    break;

                // Task D6：Villager 占位。中立友好：保持 Idle、零速度，不 flee、不 chase。
                // 不调 TickPassive / TickHostile 也就不更新 Position（或留给宿主处理）。
                case MobKind.Villager:
                    mob.State = MobState.Idle;
                    mob.Velocity = default;
                    break;
            }
        }

        /// <summary>
        /// m9 A2：Core 统一受击入口（Unity 侧 <c>CombatController.DoAttack</c> 调它）。
        /// 扣血 + 记录攻击者位置（<see cref="Mob.LastAttackerPos"/>，逃跑方向基准）+
        /// 受击红闪（<see cref="HitFlashDuration"/>）。
        /// 被动动物（Passive/Pig/Cow/Chicken）额外触发逃跑：进
        /// <see cref="MobState.FleeingFromAttacker"/>、<see cref="Mob.FleeUntil"/> 重置为
        /// <see cref="FleeDuration"/> 秒；敌对（Hostile/Zombie）与 Villager 受击不逃，
        /// 按原 AI 行动（spec 非目标「AI 大改」）。
        /// <para>
        /// m9 A3（修断环③）：致死一击<b>内联</b>走 <see cref="TransitionToDying"/> 死亡序列
        /// （Dying + LastDrops + <see cref="Mob.KilledByPlayer"/> 死因标记），不再等下一帧
        /// Tick 兜底——旧 Unity 路径由 CombatController 直置 Dying，绕得 LastDrops 永远
        /// 不可达（Tick 首行 <c>!IsAlive</c> 早退），打死不掉肉。苦力怕自爆仍在 TickCreeper
        /// 里直置 Dying（它没有玩家击杀语义，也不走本入口），两路互不影响。
        /// </para>
        /// <para>
        /// 尸体（Dying/Dead）再受击整体短路（A2 评审 Minor 2）：不闪红、不重掷掉落、
        /// 不重发死亡序列。
        /// </para>
        /// </summary>
        /// <param name="mob">被击中的 mob。</param>
        /// <param name="attackerPos">攻击者位置（逃跑方向 = 远离它）。</param>
        /// <param name="damage">伤害值（&lt;=0 由 Health.Damage 忽略）。</param>
        /// <returns>true = 本次受击是致死一击（已转 Dying + 写 LastDrops）。</returns>
        public static bool TakeHit(Mob mob, Float3 attackerPos, float damage)
        {
            if (!mob.IsAlive) return false; // 尸体免再伤（A2 评审 Minor 2）

            mob.Health.Damage(damage);
            mob.LastAttackerPos = attackerPos;
            mob.HitFlashTimer = HitFlashDuration;
            if (mob.Health.IsDead)
            {
                // 打死不逃：死亡序列同步走完（TakeHit 当前唯一生产调用方是玩家近战）
                mob.KilledByPlayer = true;
                TransitionToDying(mob);
                return true;
            }

            switch (mob.Kind)
            {
                case MobKind.Passive:
                case MobKind.Pig:
                case MobKind.Cow:
                case MobKind.Chicken:
                    mob.State = MobState.FleeingFromAttacker;
                    mob.FleeUntil = FleeDuration;
                    break;
                // Hostile / Zombie / Villager / Neutral：不逃，保持既有行为
            }
            return false;
        }

        /// <summary>
        /// m9 A3：统一死亡序列——转 <see cref="MobState.Dying"/> + 默认倒计时 + 写
        /// <see cref="Mob.LastDrops"/>（JSON 表优先，legacy 静态表兜底）。
        /// <see cref="TakeHit"/> 致死分支与 <see cref="Tick"/> 的 IsDead 兜底共用，
        /// 保证「玩家击杀」与「Core-only 路径死亡」掉落语义一致（修断环③：掉肉必经此处）。
        /// 幂等：LastDrops 已写过（非 null）不重掷；DeathTimer 已置（&gt;0）不重置。
        /// </summary>
        private static void TransitionToDying(Mob mob)
        {
            mob.State = MobState.Dying;
            if (mob.DeathTimer <= 0f) mob.DeathTimer = DefaultDeathTimer;
            if (mob.LastDrops == null)
            {
                mob.LastDrops = DropTable != null
                    ? DropTable.RollAll(mob.Kind, ComputeDropSeed(mob))
                    : Items.ItemDropTable.Drop(mob.Kind);
            }
        }

        private static void TickPassive(Mob mob, float dt)
        {
            // m7 A2：从追击切回被动流（僵尸入昼 / 旧 Hostile 天亮）时清掉 Chasing 残留——
            // 否则 Chasing 不进下面的 switch，State 永远停在 Chasing 且速度保持夜里的追击向量
            if (mob.State == MobState.Chasing) mob.State = MobState.Idle;

            // m9 A2：受击逃跑计时（TakeHit 置 FleeUntil=3s）。归零即过期——退出逃跑
            // 回落 wander 流：先站定 Idle（速度清零），WanderCooldown 烧完继续漫游。
            // 可被再次攻击重开逃跑窗，构成「打一下→追→再打」的猎杀节奏。
            if (mob.State == MobState.FleeingFromAttacker)
            {
                mob.FleeUntil -= dt;
                if (mob.FleeUntil <= 0f)
                {
                    mob.State = MobState.Idle;
                    mob.WanderCooldown = 2f;
                    mob.Velocity = default;
                }
            }

            float speed = mob.MoveSpeed > 0f ? mob.MoveSpeed : WanderSpeed;

            switch (mob.State)
            {
                case MobState.Idle:
                    mob.Velocity = default;
                    mob.WanderCooldown -= dt;
                    if (mob.WanderCooldown <= 0)
                    {
                        mob.State = MobState.Wander;
                        mob.WanderTarget = mob.Position + new Float3(
                            (RandomSigned() * 6f), 0, (RandomSigned() * 6f));
                        mob.WanderCooldown = 3f + RandomUnit() * 4f;
                    }
                    break;
                case MobState.Wander:
                {
                    var to = mob.WanderTarget - mob.Position;
                    float d = (float)System.Math.Sqrt(to.X * to.X + to.Z * to.Z);
                    if (d < 0.5f)
                    {
                        mob.State = MobState.Idle;
                        mob.WanderCooldown = 2f;
                    }
                    else
                    {
                        mob.Velocity = new Float3(to.X / d * speed, 0, to.Z / d * speed);
                    }
                    break;
                }
                case MobState.FleeingFromAttacker:
                {
                    // 逃跑方向 = 远离攻击者（TakeHit 记录的 LastAttackerPos 快照）
                    var away = mob.Position - mob.LastAttackerPos;
                    float d = (float)System.Math.Sqrt(away.X * away.X + away.Z * away.Z);
                    if (d < 0.0001f)
                    {
                        // 与攻击者重合：任取 +X 方向逃开，避免除零
                        mob.Velocity = new Float3(FleeSpeed, 0, 0);
                    }
                    else
                    {
                        mob.Velocity = new Float3(away.X / d * FleeSpeed, 0, away.Z / d * FleeSpeed);
                    }
                    break;
                }
            }

            mob.Position = new Float3(
                mob.Position.X + mob.Velocity.X * dt,
                mob.Position.Y,
                mob.Position.Z + mob.Velocity.Z * dt);
        }

        private static void TickHostile(Mob mob, Float3 playerPos, float distSq, float dt, World world)
        {
            if (mob.IsCreeper)
            {
                TickCreeper(mob, playerPos, distSq, dt, world);
                return;
            }

            // Phase D：用 mob.ChaseRadius（默认 16 = 既有 HostileChaseRadius）替代硬编码常量，
            // 让新僵尸可以单独配 32 格 aggro，旧 mobTypeId 不受影响。
            float chaseRadius = mob.ChaseRadius > 0f ? mob.ChaseRadius : HostileChaseRadius;
            float chaseRadiusSq = chaseRadius * chaseRadius;

            if (distSq < chaseRadiusSq)
            {
                mob.State = MobState.Chasing;
            }
            else
            {
                mob.State = MobState.Idle;
            }

            if (mob.State == MobState.Chasing)
            {
                float range = mob.AttackRange;
                float rangeSq = range * range;
                if (distSq < rangeSq)
                {
                    mob.Velocity = default;
                    // 攻击
                    if (mob.AttackCooldown <= 0)
                    {
                        mob.AttackCooldown = 1f;
                        mob.LastAttackTime = 0;
                        CombatEvents.RaiseDealt(new DamageEvent(
                            DamageSource.Melee, mob.AttackDamage,
                            attacker: mob.EntityId, victim: 0, hit: mob.Position));
                        CombatEvents.RaiseTaken(new DamageEvent(
                            DamageSource.Melee, mob.AttackDamage,
                            attacker: mob.EntityId, victim: 0, hit: playerPos));
                    }
                }
                else
                {
                    var to = playerPos - mob.Position;
                    float d = (float)System.Math.Sqrt(to.X * to.X + to.Z * to.Z);
                    mob.Velocity = new Float3(to.X / d * ChaseSpeed, 0, to.Z / d * ChaseSpeed);
                }
            }
            else
            {
                mob.Velocity = default;
            }

            mob.Position = new Float3(
                mob.Position.X + mob.Velocity.X * dt,
                mob.Position.Y,
                mob.Position.Z + mob.Velocity.Z * dt);
        }

        /// <summary>
        /// 苦力怕：进入 3 格内开始 2 秒自爆倒计时，倒计时结束自爆（Explosive 源，10 伤害）。
        /// 玩家跑出 5 格则取消引爆。倒计时 = 0 时把自己设为 Dying。
        /// </summary>
        private const float CreeperTriggerRadius = 3f;
        private const float CreeperTriggerRadiusSq = CreeperTriggerRadius * CreeperTriggerRadius;
        private const float CreeperAbortRadius = 5f;
        private const float CreeperAbortRadiusSq = CreeperAbortRadius * CreeperAbortRadius;
        private const float CreeperFuseDuration = 2f;
        private const float CreeperExplosionDamage = 10f;

        private static void TickCreeper(Mob mob, Float3 playerPos, float distSq, float dt, World world)
        {
            if (distSq < CreeperTriggerRadiusSq)
            {
                if (mob.FuseTimer <= 0)
                {
                    mob.FuseTimer = CreeperFuseDuration;
                }
            }
            else if (distSq > CreeperAbortRadiusSq && mob.FuseTimer > 0)
            {
                mob.FuseTimer = 0;
            }

            if (mob.FuseTimer > 0)
            {
                mob.FuseTimer -= dt;
                mob.Velocity = default;
                if (mob.FuseTimer <= 0)
                {
                    // 自爆：发一个 Explosion 伤害事件，把自身转 Dying
                    CombatEvents.RaiseTaken(new DamageEvent(
                        DamageSource.Environmental, CreeperExplosionDamage,
                        attacker: mob.EntityId, victim: 0, hit: mob.Position));
                    mob.Health = new Health(0);
                    mob.State = MobState.Dying;
                    mob.DeathTimer = 0.4f;
                    return;
                }
            }
            else
            {
                // 移动：朝玩家走
                var to = playerPos - mob.Position;
                float d = (float)System.Math.Sqrt(to.X * to.X + to.Z * to.Z);
                if (d > 0.001f)
                {
                    mob.Velocity = new Float3(to.X / d * ChaseSpeed, 0, to.Z / d * ChaseSpeed);
                }
            }

            mob.Position = new Float3(
                mob.Position.X + mob.Velocity.X * dt,
                mob.Position.Y,
                mob.Position.Z + mob.Velocity.Z * dt);
        }

        public static float DistanceSquared(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        /// <summary>
        /// X4.5：为 <see cref="MobDropTable.RollAll"/> 派生确定性整数种子。
        /// 用 mob.EntityId（生产由 MobManager 唯一分配）+ mob.Position 三轴整数 cast，
        /// 跨平台 hash 一致；同位置同 ID 必出同一结果（replay-safe），不同 mob 不会耦合。
        /// </summary>
        public static int ComputeDropSeed(Mob mob)
        {
            unchecked
            {
                int ix = (int)mob.Position.X;
                int iy = (int)mob.Position.Y;
                int iz = (int)mob.Position.Z;
                // EntityId 决定生产唯一性；Position 给测试（EntityId=0）一个区分维度
                // 用 uint 装 Knuth 黄金比 2654435761（> int.MaxValue），最后截 int 即可
                uint h = (uint)mob.EntityId * 73856093u;
                h ^= (uint)ix * 19349663u;
                h ^= (uint)iy * 83492791u;
                h ^= (uint)iz * 2654435761u;
                return (int)h;
            }
        }

        private static System.Random _rng = new System.Random(0xC0FFEE);
        private static float RandomSigned() => (float)(_rng.NextDouble() * 2 - 1);
        private static float RandomUnit() => (float)_rng.NextDouble();
    }
}