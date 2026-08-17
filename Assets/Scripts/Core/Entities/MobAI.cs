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
    /// <para>
    /// m11 W1-1：三敌对专属 AI 落地——骷髅 8-12m 风筝 + 每 2s 一箭
    /// （<see cref="ProjectileEntity"/> 经 <see cref="OnProjectileFired"/> 抛出）、
    /// 蜘蛛夜间 <see cref="SpiderChaseSpeed"/>=4.5 追击、苦力怕 &lt;3m 引信 1.5s 后
    /// <see cref="MyWorld.Core.Combat.Explosion"/> 自爆（半径 3 / 最高 6 伤 / 基岩幸存）。
    /// 9 被动 kind（Sheep…Hamster）仍等价猪组（wander + 受击逃 3s）。
    /// 昼夜门（isNight 参数）与追击半径语义对三敌对与僵尸保持一致。
    /// </para>
    /// <para>
    /// m11 W3-3：机元守卫 Boss（<see cref="MobKind.MachineGuardian"/>）三招状态机
    /// （<see cref="TickBoss"/> 私有）：近身冲撞伤 6 / 6m 震荡波 AOE 伤 3 / 半血
    /// 一次性召唤 2 骷髅（<see cref="OnBossSummon"/> 事件出口），各带冷却 +
    /// 确定性哈希变招；Boss 不看昼夜——图腾召唤的对手局白天照打。
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

        // ─── m11 W1-1：三敌对专属行为参数（卡片数值） ────────────────────────

        /// <summary>骷髅射击间隔（秒）：每 2s 一箭。</summary>
        public const float SkeletonShootInterval = 2f;

        /// <summary>骷髅保持距离窗口下限（米）：玩家比这近就开始后撤。</summary>
        public const float SkeletonMinRange = 8f;

        /// <summary>骷髅保持距离窗口上限（米）：玩家比这远就逼近，窗口内站定射击。</summary>
        public const float SkeletonMaxRange = 12f;

        /// <summary>蜘蛛夜间追击速度（格/s）。4.5 &gt; 玩家走速 4.3——夜里甩不掉，白天才安全。</summary>
        public const float SpiderChaseSpeed = 4.5f;

        /// <summary>新苦力怕引信触发半径（米）：玩家进入即点燃。</summary>
        public const float NewCreeperTriggerRadius = 3f;

        /// <summary>新苦力怕引信脱离半径（米）：玩家跑出该距离取消引信（照旧苦力怕的 3/5 节奏）。</summary>
        public const float NewCreeperAbortRadius = 5f;

        /// <summary>新苦力怕引信时长（秒）：膨胀 1.5s 后起爆（Combat.Explosion 半径 3 / 最高 6 伤）。</summary>
        public const float NewCreeperFuseDuration = 1.5f;

        // ─── m11 W3-3：机元守卫 Boss 三招参数（卡片数值） ────────────────────

        /// <summary>Boss 震荡波半径（米）：玩家进入即吃一发 AOE——脉冲不是射线，躲掩体没用。</summary>
        public const float BossShockwaveRadius = 6f;

        /// <summary>Boss 震荡波伤害（点）。</summary>
        public const float BossShockwaveDamage = 3f;

        /// <summary>Boss 震荡波冷却（秒）。</summary>
        public const float BossShockwaveCooldown = 6f;

        /// <summary>Boss 冲撞起手距离上限（米）：玩家在攻击距离（4m）到该值之间可起手冲撞。</summary>
        public const float BossChargeTriggerRange = 8f;

        /// <summary>Boss 冲撞持续（秒）：起手后以 <see cref="BossChargeSpeed"/> 冲向玩家的窗口。</summary>
        public const float BossChargeDuration = 0.6f;

        /// <summary>Boss 冲撞速度（格/s）：常速 3.5 的两倍——玩家得跑位才甩得开。</summary>
        public const float BossChargeSpeed = 7f;

        /// <summary>Boss 冲撞冷却（秒）。</summary>
        public const float BossChargeCooldown = 4f;

        /// <summary>Boss 近身攻击冷却（秒）：伤 6 的重击比僵尸 1s 慢一倍，不能连发。</summary>
        public const float BossMeleeCooldown = 2f;

        /// <summary>Boss 半血一次性召唤的骷髅数。</summary>
        public const int BossSummonCount = 2;

        /// <summary>
        /// m11 W3-3：Boss 半血召唤出口。Core 不持世界级 mob 容器——宿主（MobManager）
        /// 订阅后在 Boss 附近刷骷髅（参数 mob + 第 i 只序号）；null 时召唤判定照走
        /// （<see cref="Mob.BossSummoned"/> 仍置位），只是无人接管实体。
        /// 照 <see cref="OnProjectileFired"/> 同款事件出口模式。
        /// </summary>
        public static System.Action<Mob, int> OnBossSummon;

        /// <summary>
        /// m11 W1-1：骷髅开火的箭实体出口。Unity 侧（集成点②接线）订阅后接管箭的
        /// tick 列表与视觉（箭本体 Core 只在此抛出，不持有世界级容器）；
        /// Core 单测订阅捕获做弹道断言。null 时箭实体仍构造，只是无人接管。
        /// </summary>
        public static System.Action<ProjectileEntity> OnProjectileFired;

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
                        TickHostile(mob, playerPos, distSq, dt, world, ChaseSpeed);
                    }
                    break;

                // Phase D 新增 type-specific（spec line 173）
                case MobKind.Pig:
                case MobKind.Cow:
                case MobKind.Chicken:
                // m11 P0：9 新被动 kind 预接线进猪组（wander + 受击逃，专属节奏待 W1-2；
                // spawn_rules.json 未加条目前不会真的刷出，安全）
                case MobKind.Sheep:
                case MobKind.Rabbit:
                case MobKind.Fox:
                case MobKind.Deer:
                case MobKind.Panda:
                case MobKind.Penguin:
                case MobKind.Goat:
                case MobKind.Raccoon:
                case MobKind.Hamster:
                    // 友好动物：wander + 受击逃跑（m9 A2 起靠近不再惊跑），不追玩家
                    TickPassive(mob, dt);
                    break;
                case MobKind.Zombie:
                    // m7 A2：白天（isNight=false）不追——与友好动物一样走 wander（TickPassive），
                    // 威胁只在夜里成立。isNight 由宿主传入（MobManager.IsNightPhase 单一真源），
                    // 不在此读第二套时钟。chase/attack 半径由 mob.ChaseRadius / mob.AttackRange 控制。
                    if (isNight)
                    {
                        TickHostile(mob, playerPos, distSq, dt, world, ChaseSpeed);
                    }
                    else
                    {
                        TickPassive(mob, dt);
                    }
                    break;

                // m11 W1-1：三敌对专属 AI（P0 曾暂等价僵尸，本波替换）。
                // 昼夜门与追击半径语义沿用僵尸组：夜里威胁、白天回落 wander。
                case MobKind.Skeleton:
                    if (isNight)
                    {
                        TickSkeleton(mob, playerPos, distSq, dt);
                    }
                    else
                    {
                        TickPassive(mob, dt);
                    }
                    break;
                case MobKind.Spider:
                    if (isNight)
                    {
                        TickHostile(mob, playerPos, distSq, dt, world, SpiderChaseSpeed);
                    }
                    else
                    {
                        TickPassive(mob, dt);
                    }
                    break;
                case MobKind.Creeper:
                    // 注意 MobKind.Creeper ≠ 旧 mobTypeId=5（Mob.IsCreeper 判 MobTypeId）：
                    // 旧苦力怕走 TickHostile→TickCreeper，新 kind 走下面的专属引信分支。
                    if (isNight)
                    {
                        TickCreeperKind(mob, playerPos, distSq, dt, world);
                    }
                    else
                    {
                        if (mob.FuseTimer > 0) mob.FuseTimer = 0; // 入昼熄引信，不带着半截引信进白天闪白
                        TickPassive(mob, dt);
                    }
                    break;

                // Task D6：Villager 占位。中立友好：保持 Idle、零速度，不 flee、不 chase。
                // 不调 TickPassive / TickHostile 也就不更新 Position（或留给宿主处理）。
                case MobKind.Villager:
                    mob.State = MobState.Idle;
                    mob.Velocity = default;
                    break;

                // m11 W3-3：机元守卫 Boss——**不看昼夜**（isNight 不进本分支）：
                // 它是玩家右键图腾主动召唤的对手局，白天召唤也得打完，
                // 与「夜里才威胁」的自然刷怪组（Zombie/Skeleton/Spider/Creeper）语义不同。
                case MobKind.MachineGuardian:
                    TickBoss(mob, playerPos, distSq, dt);
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
                // m11 P0：9 新被动 kind 同进逃跑组（与 Tick 的猪组行为分派保持同构）
                case MobKind.Sheep:
                case MobKind.Rabbit:
                case MobKind.Fox:
                case MobKind.Deer:
                case MobKind.Panda:
                case MobKind.Penguin:
                case MobKind.Goat:
                case MobKind.Raccoon:
                case MobKind.Hamster:
                    mob.State = MobState.FleeingFromAttacker;
                    mob.FleeUntil = FleeDuration;
                    break;
                // Hostile / Zombie / Skeleton / Spider / Creeper / Villager / Neutral：
                // 敌对受击不逃，保持既有行为（m11 P0 三新敌对与 Zombie 同语义）
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

        private static void TickHostile(Mob mob, Float3 playerPos, float distSq, float dt, World world,
            float chaseSpeed)
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
                    mob.Velocity = new Float3(to.X / d * chaseSpeed, 0, to.Z / d * chaseSpeed);
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
        /// m11 W1-1：骷髅远程 AI——保持 8-12m 距离风筝：太近后撤、太远逼近、
        /// 窗口内站定每 <see cref="SkeletonShootInterval"/> 秒射一箭（箭实体经
        /// <see cref="OnProjectileFired"/> 抛给宿主接管，箭伤由 ProjectileEntity 结算）。
        /// </summary>
        private static void TickSkeleton(Mob mob, Float3 playerPos, float distSq, float dt)
        {
            float chaseRadius = mob.ChaseRadius > 0f ? mob.ChaseRadius : HostileChaseRadius;
            if (distSq > chaseRadius * chaseRadius)
            {
                mob.State = MobState.Idle;
                mob.Velocity = default;
                return;
            }

            mob.State = MobState.Chasing;
            float speed = mob.MoveSpeed > 0f ? mob.MoveSpeed : ChaseSpeed;
            float minSq = SkeletonMinRange * SkeletonMinRange;
            float maxSq = SkeletonMaxRange * SkeletonMaxRange;

            if (distSq < minSq)
            {
                // 玩家贴脸：后撤拉开距离（方向 = 远离玩家）
                var away = mob.Position - playerPos;
                float d = (float)System.Math.Sqrt(away.X * away.X + away.Z * away.Z);
                if (d > 0.001f)
                {
                    mob.Velocity = new Float3(away.X / d * speed, 0, away.Z / d * speed);
                }
            }
            else if (distSq > maxSq)
            {
                // 玩家太远：逼近到射击窗口
                var to = playerPos - mob.Position;
                float d = (float)System.Math.Sqrt(to.X * to.X + to.Z * to.Z);
                if (d > 0.001f)
                {
                    mob.Velocity = new Float3(to.X / d * speed, 0, to.Z / d * speed);
                }
            }
            else
            {
                // 射击窗口内：站定开火
                mob.Velocity = default;
                if (mob.AttackCooldown <= 0f)
                {
                    FireArrow(mob, playerPos);
                    mob.AttackCooldown = SkeletonShootInterval;
                }
            }

            mob.Position = new Float3(
                mob.Position.X + mob.Velocity.X * dt,
                mob.Position.Y,
                mob.Position.Z + mob.Velocity.Z * dt);
        }

        /// <summary>
        /// m11 W1-1：骷髅开火——从持弓高度（脚底 +1.4）向玩家胸口（脚底 +0.9）射一支
        /// 重力补偿瞄准的箭（<see cref="ProjectileEntity.ComputeLaunchVelocity"/>）。
        /// </summary>
        private static void FireArrow(Mob mob, Float3 playerPos)
        {
            var origin = mob.Position + new Float3(0f, 1.4f, 0f);
            var target = playerPos + new Float3(0f, 0.9f, 0f);
            var velocity = ProjectileEntity.ComputeLaunchVelocity(
                origin, target, ProjectileEntity.DefaultSpeed, ProjectileEntity.Gravity);
            OnProjectileFired?.Invoke(new ProjectileEntity(origin, velocity, mob.EntityId));
        }

        /// <summary>
        /// m11 W1-1：新苦力怕（MobKind.Creeper）——距玩家 &lt;
        /// <see cref="NewCreeperTriggerRadius"/> 进入 <see cref="NewCreeperFuseDuration"/> 秒
        /// 引信（站定膨胀，视觉由 MobView 按 FuseTimer 驱动），烧完起爆：
        /// <see cref="MyWorld.Core.Combat.Explosion"/> 半径 3 破坏方块（基岩/不可破坏幸存）+
        /// 距离衰减伤害最高 6 点，自身转 Dying。玩家跑出 <see cref="NewCreeperAbortRadius"/>
        /// 取消引信（照旧苦力怕的触发/脱离节奏）。
        /// </summary>
        private static void TickCreeperKind(Mob mob, Float3 playerPos, float distSq, float dt, World world)
        {
            // 与其他敌对同语义：追击半径内保持 Chasing（含引信站定阶段——仍在交战），
            // 半径外 Idle。下游（walk phase 驱动等）按速度而非 State 判断移动，不受影响。
            float chaseRadius = mob.ChaseRadius > 0f ? mob.ChaseRadius : HostileChaseRadius;
            mob.State = distSq <= chaseRadius * chaseRadius ? MobState.Chasing : MobState.Idle;

            if (distSq < NewCreeperTriggerRadius * NewCreeperTriggerRadius)
            {
                if (mob.FuseTimer <= 0)
                {
                    mob.FuseTimer = NewCreeperFuseDuration;
                }
            }
            else if (distSq > NewCreeperAbortRadius * NewCreeperAbortRadius && mob.FuseTimer > 0)
            {
                mob.FuseTimer = 0;
            }

            if (mob.FuseTimer > 0)
            {
                mob.FuseTimer -= dt;
                mob.Velocity = default;
                if (mob.FuseTimer <= 0)
                {
                    // 起爆：爆心取身体中心（脚底 +0.9）。伤害事件与方块破坏都在
                    // Explosion.Detonate 内结算，这里只负责把自身转 Dying。
                    MyWorld.Core.Combat.Explosion.Detonate(
                        world,
                        mob.Position + new Float3(0f, 0.9f, 0f),
                        playerPos,
                        attackerEntityId: mob.EntityId);
                    mob.Health = new Health(0);
                    mob.State = MobState.Dying;
                    mob.DeathTimer = 0.4f;
                    return;
                }
            }
            else
            {
                // 引信未起：朝玩家逼近
                var to = playerPos - mob.Position;
                float d = (float)System.Math.Sqrt(to.X * to.X + to.Z * to.Z);
                if (d > 0.001f)
                {
                    float speed = mob.MoveSpeed > 0f ? mob.MoveSpeed : ChaseSpeed;
                    mob.Velocity = new Float3(to.X / d * speed, 0, to.Z / d * speed);
                }
            }

            mob.Position = new Float3(
                mob.Position.X + mob.Velocity.X * dt,
                mob.Position.Y,
                mob.Position.Z + mob.Velocity.Z * dt);
        }

        /// <summary>
        /// m11 W3-3：机元守卫 Boss 三招状态机——①近身冲撞（&lt;AttackRange 伤 6，
        /// 冲撞窗口 4-8m 起手以 <see cref="BossChargeSpeed"/> 突进 0.6s，收尾撞上就是近身这一下）、
        /// ②震荡波（≤<see cref="BossShockwaveRadius"/> 的 AOE 伤 3）、③半血一次性召唤
        /// 2 骷髅（经 <see cref="OnBossSummon"/> 抛给宿主接管）。
        /// <para>
        /// 三招各带冷却（近身 2s / 冲撞 4s / 震荡波 6s）；「震荡波」与「冲撞/近身」
        /// 同时可用的 tick（距离落在重叠带）用确定性哈希二选一变招
        /// （<see cref="Mob.BossMoveCounter"/> 只在此递增，(EntityId, 招数) 唯一决定变招序列）。
        /// 每 tick 至多发一招。昼夜门不进本分支（召唤出的 Boss 白天照打，见 Tick 分派注释）。
        /// </para>
        /// </summary>
        private static void TickBoss(Mob mob, Float3 playerPos, float distSq, float dt)
        {
            // ③ 半血召唤（一次性）：×2 比较避开半血的取整歧义（60 血的线 = 30）。
            // 判定照走、事件无人订阅也只是少刷骷髅——BossSummoned 置位保证不重发。
            if (!mob.BossSummoned && mob.Health.Current * 2f <= mob.Health.Max)
            {
                mob.BossSummoned = true;
                for (int i = 0; i < BossSummonCount; i++)
                {
                    OnBossSummon?.Invoke(mob, i);
                }
            }

            if (mob.BossChargeCooldown > 0f) mob.BossChargeCooldown -= dt;
            if (mob.BossShockwaveCooldown > 0f) mob.BossShockwaveCooldown -= dt;

            float chaseRadius = mob.ChaseRadius > 0f ? mob.ChaseRadius : HostileChaseRadius;
            if (distSq > chaseRadius * chaseRadius)
            {
                mob.State = MobState.Idle; // 追击半径外站定（走远由 MobManager despawn 兜底）
                mob.Velocity = default;
                return;
            }
            mob.State = MobState.Chasing;

            float dist = (float)System.Math.Sqrt(distSq);

            // ── 招式可用性（三段距离带：近身 <4 ≤ 冲撞 ≤8；震荡波整段 ≤6） ──
            bool shockReady = dist <= BossShockwaveRadius && mob.BossShockwaveCooldown <= 0f;
            bool meleeReady = dist < mob.AttackRange && mob.AttackCooldown <= 0f;
            bool chargeReady = dist >= mob.AttackRange && dist <= BossChargeTriggerRange
                && mob.BossChargeCooldown <= 0f;

            // 变招：震荡波与另一招同 tick 可用时哈希二选一（重叠带 = 0-6m 的近身带 +
            // 4-6m 的冲撞带）。同一 Boss 重放同序列；不同 EntityId 各走各的序列。
            if (shockReady && (meleeReady || chargeReady))
            {
                if (BossMoveHash(mob) % 2u == 0u)
                {
                    meleeReady = false;
                    chargeReady = false;
                }
                else
                {
                    shockReady = false;
                }
            }

            if (shockReady)
            {
                // ② 震荡波：原地点发（本帧不走位），伤害走与近战同一条 CombatEvents 通道
                mob.BossShockwaveCooldown = BossShockwaveCooldown;
                mob.Velocity = default;
                CombatEvents.RaiseDealt(new DamageEvent(
                    DamageSource.Melee, BossShockwaveDamage,
                    attacker: mob.EntityId, victim: 0, hit: mob.Position));
                CombatEvents.RaiseTaken(new DamageEvent(
                    DamageSource.Melee, BossShockwaveDamage,
                    attacker: mob.EntityId, victim: 0, hit: playerPos));
                return;
            }

            if (meleeReady)
            {
                // ① 近身重击（冲撞窗口冲到脸上的收尾也是它）：伤 6、冷却 2s
                mob.AttackCooldown = BossMeleeCooldown;
                CombatEvents.RaiseDealt(new DamageEvent(
                    DamageSource.Melee, mob.AttackDamage,
                    attacker: mob.EntityId, victim: 0, hit: mob.Position));
                CombatEvents.RaiseTaken(new DamageEvent(
                    DamageSource.Melee, mob.AttackDamage,
                    attacker: mob.EntityId, victim: 0, hit: playerPos));
            }

            if (chargeReady && mob.BossChargeTimer <= 0f)
            {
                // ① 冲撞起手：开 0.6s 突进窗口（速度在下面取 BossChargeSpeed）
                mob.BossChargeTimer = BossChargeDuration;
                mob.BossChargeCooldown = BossChargeCooldown;
            }

            // ── 移动：常规追击 3.5，冲撞窗口内 7；贴脸（<0.5m）不再推挤 ──
            float speed = mob.MoveSpeed > 0f ? mob.MoveSpeed : ChaseSpeed;
            if (mob.BossChargeTimer > 0f)
            {
                mob.BossChargeTimer -= dt;
                speed = BossChargeSpeed;
            }
            var to = playerPos - mob.Position;
            float d = (float)System.Math.Sqrt(to.X * to.X + to.Z * to.Z);
            mob.Velocity = d > 0.001f && dist > 0.5f
                ? new Float3(to.X / d * speed, 0, to.Z / d * speed)
                : default;

            mob.Position = new Float3(
                mob.Position.X + mob.Velocity.X * dt,
                mob.Position.Y,
                mob.Position.Z + mob.Velocity.Z * dt);
        }

        /// <summary>
        /// m11 W3-3：Boss 变招哈希——(EntityId, 第 N 次变招) 进、非负 uint 出
        /// （Knuth 乘 + xorshift，与 ComputeDropSeed 同思路）。只读不写：
        /// <see cref="Mob.BossMoveCounter"/> 的递增放在调用方（TickBoss 变招分支）。
        /// </summary>
        private static uint BossMoveHash(Mob mob)
        {
            unchecked
            {
                uint h = (uint)mob.EntityId * 2654435761u;
                h ^= (uint)(++mob.BossMoveCounter) * 40503u;
                h ^= h >> 13;
                h *= 2654435761u;
                h ^= h >> 16;
                return h;
            }
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