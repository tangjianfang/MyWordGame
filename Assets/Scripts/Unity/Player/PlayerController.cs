using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Audio;
using MyWorld.Unity.Gameplay;
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

        public PlayerState State => _state;
        public PlayerMotorSettings Settings => _settings;

        /// <summary>相机所在位置——射线拾取要用，所以公开出去。</summary>
        public Transform Eye => eye;

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

        /// <summary>玩家当前生命值。归零时触发死亡画面（见 <see cref="TakeDamage"/>）。</summary>
        public int Health { get; private set; } = MaxHealth;

        /// <summary>生命值上限。默认 20（=10 颗心）。</summary>
        public const int MaxHealth = 20;

        /// <summary>玩家受到伤害。<paramref name="amount"/> ≤ 0 直接忽略；生命归零时通知
        /// <see cref="MyWorld.Unity.UI.DeathScreenUi"/> 显示死亡画面。<paramref name="attacker"/>
        /// 保留给未来的伤害归属 / 成就系统，这里不用。
        /// <para>
        /// B8 起这是**多源**入口：怪物近战（<c>CombatController</c>）、摔落
        /// （<see cref="TickFallDamage"/>）、饥饿（<see cref="TickHungerDamage"/>）都走这里，
        /// 死亡判定只有这一处，不要在各伤害源里各写一份。
        /// </para></summary>
        public void TakeDamage(int amount, object attacker)
        {
            if (amount <= 0) return;
            Health = System.Math.Max(0, Health - amount);
            if (Health == 0)
            {
                var ctx = GetComponent<PlayerContext>();
                ctx?.DeathScreen?.Show();
            }
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

        // ─── 拾取掉落物 ───────────────────────────────────────────────────────

        /// <summary>把 <see cref="PlayerContext.ItemDrops"/> 里落在拾取半径内的掉落物收进背包，
        /// 返回本次实际拾取的物品总数。背包塞不下时**保留**掉落物（部分塞入的按剩余量回写），
        /// 玩家腾出格子后还能再捡。</summary>
        public int PickupNearbyDrops()
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

                if (!drop.TryPickupBy(self, Time.time, out int picked)) continue;

                var stack = drop.Content.Value;
                if (ctx.Inventory.TryAdd(stack, out int leftover))
                {
                    drop.MarkPicked();
                    ctx.ItemDrops.RemoveAt(i);
                    total += picked;
                }
                else if (leftover < picked)
                {
                    // 背包只塞下一部分：掉落物按剩余量重建，等玩家腾格子后再捡
                    var rebuilt = new Core.Items.ItemDropEntity(
                        stack.WithCount(leftover), drop.Position);
                    rebuilt.SpawnTime = drop.SpawnTime; // F1 follow-up：保留原 spawn 时刻，宽限期不重置
                    ctx.ItemDrops[i] = rebuilt;
                    total += picked - leftover;
                }
            }

            return total;
        }

        /// <summary>玩家复活到 <paramref name="spawnPoint"/>：传送 + 回满生命 + 清竖直速度 +
        /// 标记着地 + 通过 <see cref="PlayerContext.HungerSystem"/> 重置饥饿 / 饱和度。
        /// 绑定到 <see cref="World"/> 之后公开属性 <see cref="VerticalVelocity"/> / <see cref="IsGrounded"/>
        /// 实际由 <see cref="PlayerState"/> 驱动，所以这里必须重建 _state 让其 Velocity.Y=0、IsGrounded=true；
        /// 只写私有 _default* 字段（未绑定时的 fallback）会被覆盖回原值，导致生产环境 Respawn 失败。
        /// Core 的 <see cref="PlayerState"/> 字段（Hunger / Saturation）暂不写回——B8 接 pickup 时
        /// 再决定是否把 HungerSystem.Hunger 同步到 PlayerState.Hunger。</summary>
        public void Respawn(Vector3 spawnPoint)
        {
            transform.position = spawnPoint;
            Health = MaxHealth;

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

            var ctx = GetComponent<PlayerContext>();
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

            _state = PlayerState.AtRest(spawnPosition);
            ApplyToTransform();
        }

        /// <summary>驱动一帧。公开出来是为了让无头验证能手动步进，不必真的进 Play 模式。</summary>
        public void Tick(PlayerInput input, float dt)
        {
            if (_world == null)
            {
                return;
            }

            var source = new WorldSolidSource(_world, _registry);
            _state = PlayerMotor.Step(source, _state, input, _settings, dt);
            ApplyToTransform();
        }

        private void Update()
        {
            UpdateLook();
            Tick(ReadInput(), Time.deltaTime);
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

        private void UpdateLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                // 未锁定指针时不转视角，否则在编辑器里点 UI 会把视角甩飞
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

            _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -89f, 89f);

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
