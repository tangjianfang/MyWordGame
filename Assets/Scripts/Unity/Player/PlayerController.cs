using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using MyWorld.Core.Quests;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Audio;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.UI;
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 输入 → <see cref="PlayerMotor"/> → transform。
    /// <para>
    /// 这个组件刻意很薄：运动解算全在 Core 里（可 <c>dotnet test</c> 覆盖），这里只做三件事——
    /// 读输入、把输入按相机朝向旋转到世界空间、把结果写回 transform。
    /// </para>
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField] private float lookSensitivity = 2.5f;

        /// <summary>m6 B3：鼠标灵敏度乘数（帮助菜单「设置」页滑条写回，PlayerPrefs 持久化）。
        /// 1 = 默认手感；最终灵敏度 = <see cref="lookSensitivity"/> × 该乘数。</summary>
        public float LookSensitivityMultiplier { get; set; } = 1f;

        [Header("运动参数（留空用 Core 的默认值）")]
        [SerializeField] private float walkSpeed = 4.3f;
        [SerializeField] private float jumpSpeed = 8.4f;
        [SerializeField] private float gravity = -28f;

        private PlayerMotorSettings _settings;
        private PlayerState _state;
        private World _world;
        private BlockRegistry _registry;
        private PlayerAudioSystem _audio;

        private float _yaw;
        private float _pitch;
        private float _lastFootstepTime = -1f;

        /// <summary>m13 W1：飞行态状态机（纯 Core）。<c>Bind</c> 重新创建一份 =
        /// 存档不记飞行态的入口；玩家从读档位置直接落在步行态。EditMode 测试可走
        /// <see cref="InjectFlightStateForTest"/> 注入受控实例。</summary>
        private FlightState _flight = new FlightState();

        public PlayerState State => _state;
        public PlayerMotorSettings Settings => _settings;

        /// <summary>当前飞行态（true = 在飞）。HUD 与 Gameplay 侧只读用，
        /// 切换请走 <see cref="FlightTickToggle"/> 让双击窗口 / 触地退出走完整通道。</summary>
        public bool IsFlying => _flight.Enabled;

        /// <summary>飞行 HUD 文案。文案常量化锁死（与 <c>CraftingShiftHint</c> 模式一致），
        /// EditMode / 验收剧本都可直读断言。</summary>
        public const string FlightHudLabelOn = "飞行：开";
        public const string FlightHudLabelOff = "飞行：关";

        /// <summary>相机所在位置——射线拾取要用，所以公开出去。</summary>
        public Transform Eye => eye;

        /// <summary>存档恢复专用（m4 B2）：整体替换 Core 运动状态并立即同步 transform。
        /// 运行时不要调用——会跳过碰撞检测把玩家瞬移穿墙，仅供读档链路使用。</summary>
        public void RestoreCoreState(PlayerState state)
        {
            _state = state;
            ApplyToTransform();
        }

        /// <summary>m12 P0-b：腾空垫脚放置后一次性把玩家抬到 <paramref name="worldY"/>。
        /// 垫脚的 0.5 格容差允许"脚还低于新块顶"时就放（跳跃最高点附近），
        /// 靠物理解算会把人卡进方块里——这里照 <see cref="RestoreCoreState"/> 同款
        /// 「写 _state + ApplyToTransform」直接修正，仅允许向上（worldY 更低时 no-op），
        /// 竖直速度清零防继续下坠穿模。</summary>
        public void LiftTo(float worldY)
        {
            if (worldY <= _state.Position.Y)
            {
                return; // 只向上抬；已经更高就交给既有落地物理
            }

            _state = new PlayerState(
                new Float3(_state.Position.X, worldY, _state.Position.Z),
                new Float3(_state.Velocity.X, 0f, _state.Velocity.Z),
                _state.IsGrounded);
            ApplyToTransform();
        }

        // ─── 公开 jump / 着地信号（A4：HandController 与无头驱动依赖） ──────────────
        // 绑定到世界后这两个值同步自 <see cref="PlayerState"/>——Core 在 <see cref="Tick"/>
        // 里做碰撞检测、重力累积；绑定前保留本地默认值，避免测试 / 预览场景拿不到信号。
        // HandController 等客户端只读这两个属性，不必关心 Core 的状态机。

        /// <summary>玩家是否着地。绑定到世界后由 Core 碰撞检测驱动，
        /// 绑定前默认为 true（新生角色视为站在出生点上）。</summary>
        public bool IsGrounded => _world == null ? _defaultIsGrounded : _state.IsGrounded;

        /// <summary>竖直速度（m/s）。绑定后同步自 <see cref="PlayerState.Velocity.Y"/>，
        /// 绑定前返回本地默认（jump 后 = <see cref="JumpSpeed"/>）。</summary>
        public float VerticalVelocity => _world == null ? _defaultVerticalVelocity : _state.Velocity.Y;

        /// <summary>走路相位累加器，给 HandController 摆动动画用。无 dt 时不前进。</summary>
        public float WalkPhase { get; private set; } = 0f;

        private bool _defaultIsGrounded = true;
        private float _defaultVerticalVelocity = 0f;

        // 跳跃初速度（m/s）。与 Core <see cref="PlayerMotorSettings.JumpSpeed"/> 独立——
        // 这套公开 API 面向调用方直接触发跳跃（按 Space / HandController / 测试），
        // 不一定走 Core Tick 链路，所以保留独立常量。
        private const float JumpSpeed = 8f;

        // 重力加速度（m/s²）。Core 在 PlayerMotor.StepVertical 里已经按 gravity=-28 累加重力，
        // 这里留一个常量作为公开文档，不在 Update 里直接用——避免双重重力把玩家拉穿地板。
        private const float Gravity = -20f;

        /// <summary>由调用方（按 Space / HandController / 测试 / 无头驱动）触发跳跃。
        /// 必须在着地状态才生效——避免空中连跳破坏物理手感。</summary>
        public void Jump()
        {
            if (!IsGrounded) return;
            if (_world != null)
            {
                _state = new PlayerState(
                    _state.Position,
                    new Float3(_state.Velocity.X, JumpSpeed, _state.Velocity.Z),
                    false);
            }
            else
            {
                _defaultIsGrounded = false;
                _defaultVerticalVelocity = JumpSpeed;
            }
        }

        /// <summary>无头驱动一帧的水平步进。<paramref name="speed"/> 低于阈值时不计相位（停下脚步），
        /// <paramref name="dt"/> 钳位到 1e-4 防止 EditMode / batchmode 极小 dt 时 WalkPhase 累加爆炸。</summary>
        public void ApplyMovementTick(float speed, float dt)
        {
            dt = Mathf.Max(dt, 1e-4f);
            if (speed > 0.1f)
            {
                WalkPhase += dt * speed * 8f;
            }
        }

        /// <summary>测试助手（A5 用）——强制把玩家置为着地、清零竖直速度。
        /// 让 EditMode 测试在没真 World 的情况下也能从空中切回地面，
        /// 不依赖 Core 碰撞检测。仅 EditMode / 测试代码使用。</summary>
        public void ForceGroundedForTest()
        {
            _defaultIsGrounded = true;
            _defaultVerticalVelocity = 0f;
        }

        /// <summary>玩家受到伤害。<paramref name="amount"/> ≤ 0 直接忽略；生命归零时通知
        /// <see cref="MyWorld.Unity.UI.DeathScreenUi"/> 显示死亡画面。<paramref name="attacker"/>
        /// 保留给未来的伤害归属 / 成就系统，这里不用。
        /// <para>
        /// B8 起这是**多源**入口：摔落（<see cref="TickFallDamage"/>）、饥饿
        /// （<see cref="TickHungerDamage"/>）都走这里，死亡判定只有这一处，
        /// 不要在各伤害源里各写一份。m7 A1 fix1 起怪物伤害也并入：僵尸近战 /
        /// 苦力怕爆炸由 <c>MobManager.HandleDamageTaken</c> 把 <c>CombatEvents</c>
        /// 事件转发到这里（不再直写 <c>ctx.Health</c>），本方法成为玩家伤害唯一入口。
        /// </para>
        /// <para>
        /// m5 A2 起伤害**唯一真源是 <see cref="PlayerContext.Health"/>**（float Current/Max）——
        /// 血条 UI、死亡判定、存档全都读它。本组件不再持有私有的 int 血条；场景里没有
        /// PlayerContext（早期 / 纯逻辑测试）时静默跳过。
        /// </para></summary>
        public void TakeDamage(int amount, object attacker) => TakeDamage((float)amount, attacker);

        /// <summary>
        /// m10 B2：小数伤害入口（镐碎块扎脚 0.5 点）。<see cref="Health.Current"/> 本就是
        /// float，旧 int 重载从 B2 起委托到这里——复活无敌帧、≤0 忽略、死亡判定、
        /// 「玩家伤害唯一入口」的语义对两条重载完全一致，各伤害源不必关心精度。
        /// <para>
        /// m10 C1 起扣血前先过手持防御减伤：max(1, amount - <see cref="PlayerContext.Defense"/>)。
        /// 防御为 0（空手 / 非防御装备）时伤害**原样通过**——下限只防「减穿到 0」，
        /// 不把 B2 碎块的 0.5 抬成 1。
        /// </para>
        /// <para>
        /// m11 W1-1 起手持盾先打五折（×0.5）再走防御减伤，且每次挨打盾耐久 -1
        /// （<see cref="ApplyShieldMitigation"/>）；耐久归零盾当场碎裂（空格），
        /// 与镐碎裂同语义。盾的减伤是专属通道（按物品 id 判定），不走 gearBonus——
        /// 避免与装备防御点数叠出双重暗减。
        /// </para>
        /// </summary>
        public void TakeDamage(float amount, object attacker)
        {
            // m7 A1：复活无敌帧——无敌期内伤害一律忽略，给玩家脱离出生点周边
            // 危险的窗口，打断「复活即被守尸连杀」的死亡循环。fix1 起怪物近战 /
            // 苦力怕爆炸 / 摔落 / 饥饿四类伤害源都经本方法进入，无敌帧同时生效。
            if (Time.time < InvincibleUntil) return;
            // m13 W1：飞行中掉血豁免——避免低空飞过头顶时被僵尸一爪拍下来再摔残，
            // 也避免飞上生物头顶被苦力怕炸到。退出飞行（触地 / 切换）后伤害立即恢复。
            // 注意：必须走这里而不是各伤害源分头判——CLAUDE.md B8 唯一入口契约。
            if (_flight != null && _flight.Enabled) return;
            if (amount <= 0) return;
            var ctx = GetComponent<PlayerContext>();
            if (ctx == null) return;
            amount = ApplyShieldMitigation(ctx, amount);
            float finalAmount = GearBonusMath.MitigateDamage(amount, ctx.Defense);
            ctx.Health.Damage(finalAmount);
            // av W3-13：受伤音（受击）
            _audio?.PlayHurt();
            if (ctx.Health.IsDead)
            {
                // m11 W3-5（和平模式契约钉住处）：本项目自 m7 起**所有模式**死亡都
                // 不掉背包、不清物品（m7 spec 非目标「死了不掉东西，保持宽容」，
                // grep 全仓无任何死亡清空/倒包路径），所以「和平模式开 → 死亡不掉落」
                // 天然成立，无需开关分支。未来若实现普通模式的死亡掉落，必须先判
                // MyWorld.Unity.Gameplay.PeaceMode.Enabled 绕过——和平模式下掉落物
                // 直接回背包不清空（EditMode 守卫：PeaceModeTests.死亡_*）。
                _audio?.PlayDie();  // av W3-13：死亡音
                ctx.DeathScreen?.Show();
            }
        }

        /// <summary>盾物品的注册 id（与 items/shield.json 一致）——手持判定按它走。</summary>
        private const string ShieldItemId = "shield";

        /// <summary>
        /// m11 W1-1：手持盾减伤 ×0.5 且耐久 -1。非盾 / 空手原样返回（零行为变化）。
        /// 耐久走 <see cref="ItemStack.WithDurabilityUsed"/>（Metadata=0 的存量兼容：
        /// 先按物品表上限落编码再扣 1），扣到 0 当场碎成空格——先结算伤害再碎盾，
        /// 碎裂那一下仍然减半。
        /// </summary>
        private static float ApplyShieldMitigation(PlayerContext ctx, float amount)
        {
            var definition = ctx.GetSelectedDefinition();
            if (definition == null || definition.Id != ShieldItemId || definition.MaxDurability <= 0)
            {
                return amount;
            }

            var stack = ctx.Inventory.GetSelected();
            if (stack.IsEmpty)
            {
                return amount;
            }

            // m11 W2-2 C3：盾的耐久附魔按比例减缓磨损（5/(5+L) 概率真正扣 1 点，
            // 非耐久附魔恒 true 直通）。salt 用 frameCount——每次挨打大概率不同帧；
            // 减伤本身不受附魔影响（只有磨损掷骰让路）
            int idx = ctx.Inventory.SelectedHotbarIndex;
            if (MyWorld.Core.Enchanting.EnchantStore.Default.TryGet(
                    idx, stack.ItemId, out var ench, out int enchLevel)
                && !MyWorld.Core.Enchanting.EnchantSystem.ShouldWearDurability(
                    ench, enchLevel, Time.frameCount))
            {
                return amount * 0.5f; // 这次免磨损，减伤照常
            }

            ctx.Inventory.SetSlot(idx, stack.WithDurabilityUsed(definition.MaxDurability));
            return amount * 0.5f;
        }

        // ─── 伤害源 1：摔落 ───────────────────────────────────────────────────
        // 用「离地期间的最高点」而不是逐帧位移差：逐帧差只有一帧的下落量（几厘米），
        // 永远触发不了 3 格阈值。着地那一帧结算 (峰值 - 落点 - 3) 点伤害后重置峰值。

        private float _fallPeakY;
        private bool _trackingFall;

        /// <summary>摔落伤害免伤格数。落差不超过这个值不扣血。</summary>
        public const float FallDamageThreshold = 3f;

        /// <summary>每帧调用一次：离地时记录最高点，着地时按落差结算伤害。
        /// 公开出来是为了让 EditMode 测试 / 无头驱动手动步进。</summary>
        public void TickFallDamage()
        {
            // m13 W1：飞行中掉血豁免的另一端——本方法不积峰值 / 不扣血。起飞后
            // 触地那一帧会先经 <see cref="FlightApplyTick"/> 的 OnLanded 强制
            // _trackingFall = false（参见该方法），所以这里再判一次即可双保险。
            if (_flight != null && _flight.Enabled) return;

            float y = transform.position.y;
            if (!IsGrounded)
            {
                if (!_trackingFall)
                {
                    _trackingFall = true;
                    _fallPeakY = y;
                }
                else if (y > _fallPeakY)
                {
                    _fallPeakY = y;
                }
                return;
            }

            if (!_trackingFall) return;
            _trackingFall = false;

            float fallDistance = _fallPeakY - y;
            if (fallDistance > FallDamageThreshold)
            {
                TakeDamage((int)(fallDistance - FallDamageThreshold), null);
            }
        }

        // ─── 伤害源 2：饥饿 ───────────────────────────────────────────────────

        private float _starveTimer;

        /// <summary>饥饿归零后每隔多少秒扣 1 点血。</summary>
        public const float StarveDamageInterval = 10f;

        /// <summary>推进饥饿系统 <paramref name="dt"/> 秒，并在 Hunger=0 时按
        /// <see cref="StarveDamageInterval"/> 扣血。<see cref="PlayerContext.HungerSystem"/>
        /// 缺失时直接跳过（纯逻辑测试场景照常工作）。</summary>
        public void TickHungerDamage(float dt)
        {
            var ctx = GetComponent<PlayerContext>();
            if (ctx?.HungerSystem == null) return;

            ctx.HungerSystem.Tick(dt);
            if (!ctx.HungerSystem.IsStarving())
            {
                _starveTimer = 0f;
                return;
            }

            _starveTimer += dt;
            while (_starveTimer >= StarveDamageInterval)
            {
                TakeDamage(1, null);
                _starveTimer -= StarveDamageInterval;
            }
        }

        // ─── 拾取掉落物（m7 B1：吸附式） ─────────────────────────────────────

        /// <summary>每帧入口（<see cref="Update"/> 调）：用 <c>Time.deltaTime</c> 步进吸附拾取。
        /// 语义详见 <see cref="PickupNearbyDrops(float)"/>。</summary>
        public int PickupNearbyDrops() => PickupNearbyDrops(Time.deltaTime);

        /// <summary>把 <see cref="PlayerContext.ItemDrops"/> 里的掉落物按吸附语义收进背包，
        /// 返回本次实际拾取的物品总数。m7 B1 起**半径内不再立即入包**：
        /// 状态机在 Core 的 <c>ItemDropEntity.TickPickup</c>——进 2.5m 圈标记 Attracting、
        /// 每帧以 8m/s 向玩家飞、距玩家 &lt;0.3m 时本方法才入包（视觉上「吸过来」）。
        /// <paramref name="dt"/> 显式传入，EditMode 测试手动步进不依赖 Time.deltaTime。
        /// <para>背包塞不下时**保留**掉落物（部分塞入的按剩余量回写），玩家腾出格子后还能再捡。</para>
        /// <para>
        /// m6 C2：物品真正进包的这一刻发 ObtainItem 事件（Count=背包现存量）。
        /// 挖方块（<see cref="BlockInteraction.BreakAt"/>）spawn 的掉落物也走这里进包，
        /// 所以 ObtainItem **只在拾取点发一次**——挖矿路径天然被覆盖且不会双计。
        /// </para></summary>
        public int PickupNearbyDrops(float dt)
        {
            var ctx = GetComponent<PlayerContext>();
            if (ctx == null || ctx.Inventory == null || ctx.ItemDrops.Count == 0) return 0;

            var self = new Float3(transform.position.x, transform.position.y, transform.position.z);
            int total = 0;

            // 倒序遍历：边判边删，不用复制列表
            for (int i = ctx.ItemDrops.Count - 1; i >= 0; i--)
            {
                var drop = ctx.ItemDrops[i];
                if (drop == null || drop.Content == null)
                {
                    ctx.ItemDrops.RemoveAt(i);
                    continue;
                }

                // 状态机（进圈 → 飞行 → 贴脸）返回 false = 本帧未到位，继续飞
                if (!drop.TickPickup(self, Time.time, dt)) continue;

                var stack = drop.Content.Value;
                int picked = stack.Count;
                if (ctx.Inventory.TryAdd(stack, out int leftover))
                {
                    _audio?.PlayPickup();  // av W3-13：拾取音
                    drop.MarkPicked();
                    ctx.ItemDrops.RemoveAt(i);
                    total += picked;
                }
                else if (leftover < picked)
                {
                    // 背包只塞下一部分：掉落物按剩余量重建，等玩家腾格子后再捡。
                    // 位置就在玩家脚下（吸附刚到位），下一帧会重新进圈吸附重试
                    _audio?.PlayPickup();  // av W3-13：部分塞入也算拾取了一部分
                    var rebuilt = new Core.Items.ItemDropEntity(
                        stack.WithCount(leftover), drop.Position);
                    rebuilt.SpawnTime = drop.SpawnTime; // F1 follow-up：保留原 spawn 时刻，宽限期不重置
                    ctx.ItemDrops[i] = rebuilt;
                    total += picked - leftover;
                }
                else
                {
                    continue; // 一个都没塞进去：不发事件，掉落物原地保留
                }

                // m6 C2：ObtainItem 的 Count 语义是「背包现存量」（跨槽合并），
                // 由 PlayerInventory.CountOf 取数——不是本次拾取量
                QuestEventBus.Instance?.Raise(new QuestEvent
                {
                    Type = QuestEventType.ObtainItem,
                    ItemId = stack.ItemId,
                    Count = ctx.Inventory.CountOf(stack.ItemId),
                });
            }

            return total;
        }

        /// <summary>玩家复活到 <paramref name="spawnPoint"/>：传送 + 回满生命 + 清竖直速度 +
        /// 标记着地 + 通过 <see cref="PlayerContext.HungerSystem"/> 重置饥饿 / 饱和度。
        /// 绑定到 <see cref="World"/> 之后公开属性 <see cref="VerticalVelocity"/> / <see cref="IsGrounded"/>
        /// 实际由 <see cref="PlayerState"/> 驱动，所以这里必须重建 _state 让其 Velocity.Y=0、IsGrounded=true；
        /// 只写私有 _default* 字段（未绑定时的 fallback）会被覆盖回原值，导致生产环境 Respawn 失败。
        /// 生命回满写 <see cref="PlayerContext.Health"/>（m5 A2 起唯一真源）；
        /// m10 C1 起回满到**有效上限**（Health.Max + 手持生命上限装备加成）——
        /// 手持机元件复活是 22 血，切走后下一次 <see cref="PlayerContext.RefreshGearBonuses"/>
        /// 自然钳回 20。场景里没有 PlayerContext 时跳过（与 TakeDamage 的容忍策略一致）。
        /// Core 的 <see cref="PlayerState"/> 字段（Hunger / Saturation）暂不写回——B8 接 pickup 时
        /// 再决定是否把 HungerSystem.Hunger 同步到 PlayerState.Hunger。</summary>
        public void Respawn(Vector3 spawnPoint)
        {
            transform.position = spawnPoint;
            var ctx = GetComponent<PlayerContext>();
            if (ctx != null)
            {
                ctx.RefreshGearBonuses();
                ctx.Health.Current = ctx.EffectiveMaxHealth;
            }

            if (_world != null)
            {
                // 绑定态：重建 PlayerState 让公开属性立刻反映重生后的竖直速度 / 着地。
                var pos = new Float3(spawnPoint.x, spawnPoint.y, spawnPoint.z);
                var vel = new Float3(_state.Velocity.X, 0f, _state.Velocity.Z);
                _state = new PlayerState(pos, vel, true);
            }
            else
            {
                // 未绑定态（EditMode 测试场景）：写私有 fallback 字段。
                _defaultVerticalVelocity = 0f;
                _defaultIsGrounded = true;
            }

            if (ctx?.HungerSystem != null)
            {
                ctx.HungerSystem.Hunger = HungerSystem.MaxHunger;
                ctx.HungerSystem.Saturation = 5f;
            }

            // 多源伤害的累计状态也要清：否则重生瞬间会被上一次的落差 / 饥饿计时补刀
            _trackingFall = false;
            _fallPeakY = spawnPoint.y;
            _starveTimer = 0f;
        }

        // ─── m7 A1：安全重生（复活回出生点 + 短无敌帧） ─────────────────────
        // 旧版 DeathScreenUi 把 DeathSystem.LastDeathPosition 传回 Respawn，
        // 复活点 = 死亡位置：僵尸守尸时玩家原地复活立刻再被围殴，形成死亡循环。

        /// <summary>Bind 时记录的世界出生点（WorldBootstrap 传入 SurfaceHeightAt+2）。</summary>
        private Float3 _spawnPosition;

        /// <summary>复活无敌帧时长（秒）。复活后这段时间内 <see cref="TakeDamage"/> 全部忽略。</summary>
        public const float InvincibleSeconds = 3f;

        /// <summary>复活无敌截止时刻（<see cref="Time.time"/> 基准）。
        /// <c>Time.time &lt; InvincibleUntil</c> 期间伤害整体早退。公开字段是为了
        /// EditMode 测试直接改写以跳过真实等待 3 秒，运行时只由 RespawnAtSpawn 写。</summary>
        public float InvincibleUntil;

        /// <summary>复活回世界出生点：复用 <see cref="Respawn"/>（传送 + 回满血 / 饥饿 +
        /// 清摔落 / 饥饿累计状态，不双恢复）再开启 <see cref="InvincibleSeconds"/> 秒无敌。
        /// 未 Bind 过时 _spawnPosition 为默认 (0,0,0)，行为兜底与旧版 Respawn(Vector3.zero) 一致。</summary>
        public void RespawnAtSpawn()
        {
            Respawn(new Vector3(_spawnPosition.X, _spawnPosition.Y, _spawnPosition.Z));
            InvincibleUntil = Time.time + InvincibleSeconds;
        }

        /// <summary>由 <c>WorldBootstrap</c> 在世界准备好之后调用。</summary>
        public void Bind(World world, BlockRegistry registry, Float3 spawnPosition)
        {
            _world = world;
            _registry = registry;
            _settings = new PlayerMotorSettings
            {
                WalkSpeed = walkSpeed,
                JumpSpeed = jumpSpeed,
                Gravity = gravity
            };

            _spawnPosition = spawnPosition; // m7 A1：复活点 = 世界出生点
            _state = PlayerState.AtRest(spawnPosition);
            // m13 W1 存档不记飞行态：Bind = 读档 / 新建世界，重新 new 一份让 Enabled 默认 false。
            // 任何「读档后把旧 Enabled 持久化」的路径都属设计错误，这里是钉死入口。
            _flight = new FlightState();
            ApplyToTransform();
        }

        /// <summary>驱动一帧。公开出来是为了让无头验证能手动步进，不必真的进 Play 模式。</summary>
        public void Tick(PlayerInput input, float dt)
        {
            if (_world == null)
            {
                return;
            }

            // m10 C1：手持移速装备——把 PlayerContext 每帧刷新的加成同步进运动参数，
            // Core 的 PlayerMotor 目标速度乘 (1 + bonus)。PlayerContext 挂
            // DefaultExecutionOrder(-1000)，其 Update 先于本组件跑，同帧拿到的必是新值；
            // 场景里没有 PlayerContext（纯逻辑测试）时视同 0，行为与旧版一致。
            var gearCtx = GetComponent<PlayerContext>();
            _settings.MoveSpeedBonus = gearCtx != null ? gearCtx.MoveSpeedBonus : 0f;

            // m13 W1：飞行态整帧跳过 PlayerMotor —— Core 不知道飞行，Gravity/JumpSpeed
            // 都不适用。否则一秒内就被 -28m/s² 拽回地面。撞墙 / 撞地仍走 VoxelCollision，
            // 不然会穿墙。写完 transform 后直接返回，不调 PlayerMotor.Step。
            if (_flight != null && _flight.Enabled)
            {
                FlightApplyTick(out _);
                return;
            }

            var source = new WorldSolidSource(_world, _registry);
            _state = PlayerMotor.Step(source, _state, input, _settings, dt);
            ApplyToTransform();
        }

        // ─── m13 W1：飞行系统（创造式） ────────────────────────────────────────
        // 创造式飞行 = MC 自由模式同等品：双击空格 / F 键切换，飞行中六向 8m/s，
        // 触地或再切换退出，飞行掉血豁免。状态机本身放 Core（FlightState.cs）——
        // 本类只做三件事：读键盘喂切换、合成速度写 state、显示一行 IMGUI 提示。

        /// <summary>EditMode 注入用的飞行状态实例（替换默认 new FlightState()）。
        /// 必须由测试在 Bind 前调用；运行时不开放给 Gameplay 侧——切换入口走
        /// <see cref="FlightTickToggle"/> 走完整的双击 / F 键 / 触地退出通道。</summary>
        public void InjectFlightStateForTest(FlightState state) => _flight = state;

        /// <summary>每帧调用一次：检测 Space-down / F-down 切换飞行态。Space 与 F 都走
        /// 同一条 <see cref="FlightState.TryToggle"/>——「F 键等效」≠ 跳过双击窗口，
        /// 第一下空 F 不切换，第二下才生效。模态 UI 开着时本方法 no-op（与背包
        /// / 熔炉抢 Space）。</summary>
        public void FlightTickToggle()
        {
            if (_flight == null) return;
            if (UiCursorGate.IsOpen) return; // 模态 UI 占用 Space/F，不切飞行
            if (_world == null) return; // 未 Bind（早期 / 纯逻辑测试）跳过

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F))
            {
                _flight.TryToggle(Time.timeAsDouble);
            }
        }

        /// <summary>把飞行态下的速度合成（水平 / 竖直）合成后写进 <see cref="PlayerState"/>。
        /// dt 不参与水平目标速度——飞行不靠摩擦收敛，按住键就是 8m/s，绝对手感。
        /// <para>飞行中 <see cref="PlayerMotor.Step"/> 整段跳过——重力 / 跳跃初速都来自
        /// Core，按此路径走会把玩家立刻拽回地面。返回值：从飞行态退出（含触地 / 再切换）
        /// = false；仍在飞行 = true（已写入 transform）。</para>
        /// <para>触地退出走 <see cref="FlightState.OnLanded"/>：清竖直速度避免下一帧
        /// 残留自由落体负速度；插队 <see cref="_trackingFall"/> = false 让随后的
        /// <see cref="TickFallDamage"/> 不基于飞行峰值扣血。</para></summary>
        public bool FlightApplyTick(out bool leftFlight)
        {
            leftFlight = false;
            if (_flight == null || !_flight.Enabled) return false;

            // 触地退出：地面已重新接住玩家，强制落地 + 清竖直速度
            if (_state.IsGrounded)
            {
                _flight.OnLanded();
                leftFlight = true;
                _state = new PlayerState(_state.Position,
                    new Float3(_state.Velocity.X, 0f, _state.Velocity.Z),
                    _state.IsGrounded);
                _trackingFall = false; // 飞行后再落地不接续历史峰值
                _fallPeakY = _state.Position.Y;
                ApplyToTransform();
                return false;
            }

            // 读飞行按键（与步行 ReadInput 同一组：WASD + Space + Shift）
            float right = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            Vector3 direction = transform.right * right + transform.forward * forward;
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            bool ascend = Input.GetKey(KeyCode.Space);
            bool descend = Input.GetKey(KeyCode.LeftShift);

            Float3 dir = new Float3(direction.x, 0f, direction.z);
            Float3 v = FlightState.ComputeVelocity(dir, ascend, descend);

            // 飞行态：跳过 PlayerMotor 的重力 / 摩擦——Core 不能重置
            // （否则会叠加负向 Gravity 把竖直速度扣回 0）。碰撞检测仍要
            // 走——不然玩家会穿墙 / 穿地。
            float dt = Time.deltaTime;
            Float3 delta = new Float3(v.X * dt, v.Y * dt, v.Z * dt);
            var source = new WorldSolidSource(_world, _registry);
            Aabb box = Aabb.FromBottomCenter(_state.Position, _settings.Width, _settings.Height);
            MoveResult result = VoxelCollision.Move(source, box, delta);

            var pos = new Float3(
                _state.Position.X + result.Delta.X,
                _state.Position.Y + result.Delta.Y,
                _state.Position.Z + result.Delta.Z);

            // 飞行撞墙不归零速度——玩家会以为「没按键怎么不动」；竖直撞天花板 / 地板
            // 归零避免贴着顶角累积竖直速度。水平撞墙保留水平速度等下次输入调整。
            float vy = (result.HitY && v.Y != 0f) ? 0f : v.Y;
            _state = new PlayerState(pos, new Float3(v.X, vy, v.Z), result.IsGrounded);
            ApplyToTransform();
            return true;
        }

        /// <summary>每帧末尾的飞行提示行。在飞行态「关」时不画（零开销），「开」时画一行。
        /// 提示位置：屏幕底部中央偏上一行，与现有 IMGUI 提示（成就提示 / 教程文案）一致风格。
        /// <see cref="OnGUI"/> 每帧调用一次，flight 关时第一句 return。EditMode 测试可读
        /// <see cref="FlightHudLabelOn"/>/<see cref="FlightHudLabelOff"/> 常量直断文案。
        /// <para>简化实现：单 GameObject 挂 OnGUI 即可——不引入额外 GameObject / Canvas，
        /// 与 CraftingShiftHint 的「一行 HelpMenuUi OnGUI」同理。</para></summary>
        private void OnGUI()
        {
            if (_flight == null || !_flight.Enabled) return;
            var previous = GUI.color;
            GUI.color = Color.white;
            var rect = new Rect(Screen.width / 2f - 80f, Screen.height - 60f, 160f, 24f);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, FlightHudLabelOn, CenteredGuiStyle);
            GUI.color = previous;
        }

        private static GUIStyle _centeredGuiStyle;
        private static GUIStyle CenteredGuiStyle
        {
            get
            {
                if (_centeredGuiStyle == null)
                {
                    _centeredGuiStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                    };
                }
                return _centeredGuiStyle;
            }
        }

        // ─── m8 B2 fix1（I1）：暂停恢复的输入残留抑制 ──────────────────────────

        /// <summary>一帧运动步进的输入决策（m8 B2 fix1）。见 <see cref="InputDecision"/>。</summary>
        public enum StepInputKind
        {
            /// <summary>真暂停帧（timeScale=0）：整帧跳过运动步进——Core 状态一个字节不动。
            /// 不是喂 None 走 dt=0：<see cref="PlayerMotor"/> 的跳跃初速不乘 dt（按住 Space
            /// 照样把 Velocity.Y 顶成 JumpSpeed，位置 ×dt=0 没动、恢复后照样起飞），
            /// 且 <c>VoxelCollision.Move</c> 零位移步进会把 IsGrounded 判成 false
            /// （Delta.Y &lt; 0 才算着地）——零 dt 步进本身就在悄悄改状态。</summary>
            Skip,
            /// <summary>暂停恢复后的第一帧：喂一帧 <see cref="PlayerInput.None"/>——
            /// 暂停中按住的键（空格）不该在解冻瞬间生效。</summary>
            Blank,
            /// <summary>正常帧：读真实输入。</summary>
            Live,
        }

        /// <summary>本帧运动步进该喂什么输入（纯函数，EditMode 断言用，同
        /// <see cref="ShouldSkipLook"/> 模式）。真暂停期间 <see cref="Update"/> 逐帧调用，
        /// 暂停多帧 Skip、恢复首帧 Blank 仅一帧、之后 Live。</summary>
        public static StepInputKind InputDecision(bool timeScaleZero, bool wasPausedLastFrame)
        {
            if (timeScaleZero) return StepInputKind.Skip;
            return wasPausedLastFrame ? StepInputKind.Blank : StepInputKind.Live;
        }

        /// <summary>上一帧是否处于真暂停（<see cref="Time.timeScale"/> == 0）。
        /// 恢复首帧靠它识别（Blank 清残留）。暂停由 PauseMenuUi 置 timeScale=0，
        /// 这里只观察 timeScale、不与菜单组件耦合——任何路径的暂停 / 恢复都覆盖。</summary>
        private bool _wasPausedLastFrame;

        private void Update()
        {
            UpdateLook();

            // m13 W1：飞行切换检测。放在最前——和暂停键 Esc 同级（InputDecision 之前）
            // 让双击窗口计时先走（Time.timeAsDouble 在 Skip 帧也会推进），但本方法的
            // 内部 IsOpen / _world 守卫会把模态 UI / 未绑定情形都 no-op，不会越权。
            FlightTickToggle();

            switch (InputDecision(Time.timeScale == 0f, _wasPausedLastFrame))
            {
                case StepInputKind.Skip:
                    break; // 真暂停帧：不动 Core 状态（病灶分析见枚举注释）
                case StepInputKind.Blank:
                    Tick(PlayerInput.None, Time.deltaTime);
                    break;
                default:
                    Tick(ReadInput(), Time.deltaTime);
                    break;
            }
            _wasPausedLastFrame = Time.timeScale == 0f;
            TickFallDamage();
            TickHungerDamage(Time.deltaTime);
            PickupNearbyDrops();
        }

        private void Awake()
        {
            // 自动找子物体作为 eye Transform。
            // 优先级：tag=MainCamera 的 Camera transform > 名为"相机"的子物体 > 第一个子 transform。
            // 找不到也不报错（无视觉模块场景，如纯逻辑测试，照常工作）。
            if (eye == null)
            {
                eye = FindEyeInChildren(transform);
            }

            // 找音频系统（同 GameObject / 父链），找不到就 null（_audio?.PlayFootstep 安全跳过）。
            _audio = GetComponent<PlayerAudioSystem>();
            if (_audio == null) _audio = GetComponentInParent<PlayerAudioSystem>();
        }

        private static Transform FindEyeInChildren(Transform root)
        {
            // 优先：tag=MainCamera 的子 Camera
            var mainCam = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainCam != null && mainCam.transform.IsChildOf(root))
            {
                return mainCam.transform;
            }
            // 次选：名为"相机"的子物体（与 PreviewSceneBuilder.CreateCamera 一致）
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == "相机") return child;
            }
            // 最后：第一个子 transform（兜底）
            return root.childCount > 0 ? root.GetChild(0) : null;
        }

        /// <summary>
        /// m6 终审修 C1：<see cref="UpdateLook"/> 开头的模态 UI 门判定（纯函数，EditMode 行为断言用）。
        /// 任一模态 UI 开着（背包/工作台/口袋/熔炉/交易/帮助）时返回 true——本帧跳过视角更新
        /// 与「点击重锁」：指针必须停在解锁状态供 UI 点击，否则实机上点格子前都要先按一次 Esc，
        /// 且 Esc 后的第一次点击又会被这里的重锁吃掉。
        /// </summary>
        public static bool ShouldSkipLook(bool modalUiOpen) => modalUiOpen;

        private void UpdateLook()
        {
            if (ShouldSkipLook(UiCursorGate.IsOpen)) return;

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                // 未锁定指针时不转视角，否则在编辑器里点 UI 会把视角甩飞
                // （模态 UI 开着的情形已在上面被指针门拦下，不会走到这里的重锁）
                if (Input.GetMouseButtonDown(0))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            // m6 B3：基础灵敏度 × 帮助菜单设置页的乘数（默认 1，行为与旧版一致）
            float sens = lookSensitivity * LookSensitivityMultiplier;
            _yaw += Input.GetAxis("Mouse X") * sens;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * sens, -89f, 89f);

            // 身体只转 yaw，俯仰只给眼睛——身体跟着俯仰转的话包围盒会倾斜
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (eye != null)
            {
                eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private PlayerInput ReadInput()
        {
            float right = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);

            // Core 不知道相机朝哪，旋转在这里做完再传进去
            Vector3 direction = transform.right * right + transform.forward * forward;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            // 走路音效：实际有水平输入且距上次播放 > 0.5s 触发一次。
            // _audio 缺失就跳过（nice-to-have，不阻断游戏）。
            if (direction.sqrMagnitude > 0.01f && Time.time - _lastFootstepTime > 0.5f)
            {
                _audio?.PlayFootstep();
                _lastFootstepTime = Time.time;
            }

            return new PlayerInput(direction.x, direction.z,
                Input.GetKey(KeyCode.Space), Input.GetKey(KeyCode.LeftShift));
        }

        private void ApplyToTransform()
        {
            transform.position = new Vector3(_state.Position.X, _state.Position.Y, _state.Position.Z);
            if (eye != null)
            {
                eye.localPosition = new Vector3(0f, _settings.EyeHeight, 0f);
            }
        }
    }
}
