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
