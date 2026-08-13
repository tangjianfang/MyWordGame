using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
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

        private float _yaw;
        private float _pitch;

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
