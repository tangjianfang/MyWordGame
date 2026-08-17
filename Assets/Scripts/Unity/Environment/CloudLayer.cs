using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Environment
{
    /// <summary>
    /// m11 W3-4：云层——<see cref="CloudCount"/> 片白色半透面片悬在
    /// <see cref="CloudY"/>（≈150，常规地表 ~98 之上、最高地形之下），沿 +X 缓慢漂移。
    /// **纯视觉**：无碰撞、无光照参与、不写任何玩法状态。
    /// <para>
    /// <b>半透明约定</b>：面片透明度走**着色器色 Alpha**（URP/Lit Transparent 表面，
    /// <see cref="UrpMaterialFactory.CreateOverlay"/> 白色 + <see cref="CloudAlpha"/>），
    /// 不是贴图 Alpha——与水同款两态材质约定；4 片共享一份材质，装一次用到底。
    /// 另设 <c>_Cull=0</c> 双面渲染：y&gt;150 的山顶往下看也见云面，不会整片消失。
    /// </para>
    /// <para>
    /// <b>位置策略</b>：每片有确定性基础偏移（哈希派生，不持随机数对象），
    /// X 向叠加随时间累加的 <see cref="DriftUnitsPerSecond"/> 漂移；最终世界坐标 =
    /// 玩家坐标 + 把基础偏移+漂移 wrap 进 <see cref="FollowRadius"/> 的相对量——
    /// 玩家走远云跟着来（头顶永远有云），漂移出界绕回另一侧，无缝循环。
    /// </para>
    /// </summary>
    public sealed class CloudLayer : MonoBehaviour
    {
        /// <summary>云面高度（y≈150：常规地表上方约 50 格）。</summary>
        public const float CloudY = 150f;

        /// <summary>云片数（任务卡 3-5 片取中值 4）。</summary>
        public const int CloudCount = 4;

        /// <summary>漂移速度（格/秒，沿 +X）——慢到要盯几秒才察觉。</summary>
        public const float DriftUnitsPerSecond = 0.8f;

        /// <summary>云相对玩家的包裹半径：基础偏移+漂移 wrap 进 [-半径, 半径)。</summary>
        public const float FollowRadius = 240f;

        /// <summary>面片 Alpha（着色器色 Alpha 通道，与水同款的半透面配方）。</summary>
        public const float CloudAlpha = 0.30f;

        private Transform _player;
        private readonly Transform[] _clouds = new Transform[CloudCount];
        private readonly Vector2[] _baseOffsets = new Vector2[CloudCount];
        private readonly float[] _sizes = new float[CloudCount];
        private float _drift;
        private bool _built;

        /// <summary>累计漂移量（格）。public 给 EditMode 测试断言推进。</summary>
        public float Drift => _drift;

        /// <summary>
        /// 装配：建 <see cref="CloudCount"/> 片 quad（确定性尺寸/偏移），并立刻摆位。
        /// 重复调用安全（已建过只换 player 引用——EditMode fixture 反复 Bind 不叠面片）。
        /// </summary>
        public void Bind(Transform player)
        {
            _player = player;
            if (_built) return;
            _built = true;

            // 4 片共享一份材质：白色半透（着色器色 Alpha，两态材质约定见类注释）+ 双面
            Material material = UrpMaterialFactory.CreateOverlay(new Color(1f, 1f, 1f, CloudAlpha));
            material.SetFloat("_Cull", 0f);

            for (int i = 0; i < CloudCount; i++)
            {
                // 确定性基础偏移/尺寸（整数哈希；Z 向不漂移，只 wrap 跟随玩家）
                _baseOffsets[i] = new Vector2(
                    (AtmosphereHash.Frac01(AtmosphereHash.Hash32(i, 0xC10D)) * 2f - 1f) * (FollowRadius - 40f),
                    (AtmosphereHash.Frac01(AtmosphereHash.Hash32(i, 0x5EED)) * 2f - 1f) * (FollowRadius - 40f));
                _sizes[i] = 60f + AtmosphereHash.Frac01(AtmosphereHash.Hash32(i, 0x512E)) * 50f;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "云" + i;
                // 碰撞体必须删：云不挡移动/射线（与 ItemDropView 删碰撞体同理）
                var collider = quad.GetComponent<Collider>();
                if (collider != null) DestroyImmediate(collider);
                quad.transform.SetParent(transform, false);
                quad.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // 法线朝下：玩家在云下抬头可见
                quad.transform.localScale = new Vector3(_sizes[i], _sizes[i], 1f);
                quad.GetComponent<Renderer>().sharedMaterial = material;
                _clouds[i] = quad.transform;
            }
            ApplyPositions();
        }

        private void Update() => TickClouds(Time.deltaTime);

        /// <summary>
        /// 漂移推进入口（时间注入点）：累加漂移 → 重摆全部面片。
        /// EditMode 测试直调注入 dt（EditMode 下 Update 不回调）。
        /// </summary>
        public void TickClouds(float dt)
        {
            if (!_built) return;
            if (dt > 0f) _drift += DriftUnitsPerSecond * dt;
            ApplyPositions();
        }

        /// <summary>把 4 片云摆到「玩家 + wrap 后的相对偏移」处（零分配，每帧纯算术）。</summary>
        private void ApplyPositions()
        {
            Vector3 center = _player != null ? _player.position : transform.position;
            for (int i = 0; i < CloudCount; i++)
            {
                if (_clouds[i] == null) continue;
                float relX = WrapRange(_baseOffsets[i].x + _drift, FollowRadius);
                float relZ = WrapRange(_baseOffsets[i].y, FollowRadius);
                _clouds[i].position = new Vector3(center.x + relX, CloudY, center.z + relZ);
            }
        }

        /// <summary>第 i 片云的世界坐标（测试读数；未建时返回零向量）。</summary>
        public Vector3 CloudPosition(int i) => _clouds[i] != null ? _clouds[i].position : Vector3.zero;

        /// <summary>第 i 片云的边长（测试读数）。</summary>
        public float CloudSize(int i) => _sizes[i];

        /// <summary>
        /// 把 v wrap 进 [-range, range)（模加两次折正——负漂移/负偏移也正确）。
        /// internal 给 EditMode 测试直接断言 wrap 数学。
        /// </summary>
        internal static float WrapRange(float v, float range)
        {
            float size = range * 2f;
            return ((v + range) % size + size) % size - range;
        }
    }
}
