using MyWorld.Core.Entities;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 把 Core 的 <see cref="Mob"/> 绑定到一个 GameObject 上。
    /// 旧 Passive/Hostile：单 cube，按 mobTypeId 染色（既有 MobManager 路径）。
    /// Phase D Pig/Cow/Chicken/Zombie：Body + Head 双段，按 kind 染色（spec line 173 type-specific 视觉）。
    /// 同步 Position/Color（受伤红闪；苦力怕 fuse 白闪）。
    /// </summary>
    public sealed class MobView : MonoBehaviour
    {
        public Mob Mob;
        public Renderer Renderer;
        public Color BaseColor;
        public MobKind Kind;                  // Phase D：当前 kind，便于测试与调试
        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        public static MobView Attach(GameObject host, Mob mob)
        {
            var view = host.AddComponent<MobView>();
            view.Mob = mob;
            view.Setup(mob.Kind);
            return view;
        }

        /// <summary>
        /// 按 <paramref name="kind"/> 切换视觉。
        /// 旧 Passive/Hostile/Neutral：单 cube（host 自带 Renderer），按 mobTypeId 染色，行为不变。
        /// Phase D Pig/Cow/Chicken/Zombie：清旧子物体 → 创建 Body + Head 两个 cube，按 kind 染色。
        /// </summary>
        public void Setup(MobKind kind)
        {
            Kind = kind;
            switch (kind)
            {
                case MobKind.Pig:
                    BaseColor = new Color(0.9f, 0.7f, 0.7f);
                    BuildBodyHead(
                        bodyScale: new Vector3(0.9f, 0.6f, 1.2f),
                        headOffset: new Vector3(0f, 0.5f, 0.4f),
                        headScale: new Vector3(0.5f, 0.5f, 0.5f),
                        bodyColor: BaseColor, headColor: BaseColor);
                    break;
                case MobKind.Cow:
                    BaseColor = new Color(0.3f, 0.2f, 0.1f);
                    BuildBodyHead(
                        bodyScale: new Vector3(1.0f, 0.8f, 1.4f),
                        headOffset: new Vector3(0f, 0.7f, 0.6f),
                        headScale: new Vector3(0.6f, 0.6f, 0.6f),
                        bodyColor: BaseColor, headColor: BaseColor);
                    break;
                case MobKind.Chicken:
                    BaseColor = new Color(1f, 1f, 0.9f);
                    BuildBodyHead(
                        bodyScale: new Vector3(0.4f, 0.4f, 0.5f),
                        headOffset: new Vector3(0f, 0.4f, 0.3f),
                        headScale: new Vector3(0.3f, 0.3f, 0.3f),
                        bodyColor: BaseColor, headColor: new Color(1f, 0.9f, 0.1f));
                    break;
                case MobKind.Zombie:
                    BaseColor = new Color(0.4f, 0.6f, 0.4f);
                    BuildBodyHead(
                        bodyScale: new Vector3(0.6f, 1.8f, 0.4f),
                        headOffset: new Vector3(0f, 1.0f, 0f),
                        headScale: new Vector3(0.5f, 0.5f, 0.5f),
                        bodyColor: BaseColor, headColor: BaseColor);
                    break;
                case MobKind.Villager:
                    // Task D6：棕色袍（褐色头巾 + 棕色袍）。
                    // 体型与 Zombie 同（人形），但颜色明显区分：body 棕色 0.55/0.4/0.2，head 头巾 0.4/0.3/0.15。
                    BaseColor = new Color(0.55f, 0.4f, 0.2f);
                    BuildBodyHead(
                        bodyScale: new Vector3(0.6f, 1.8f, 0.4f),
                        headOffset: new Vector3(0f, 1.0f, 0f),
                        headScale: new Vector3(0.5f, 0.5f, 0.5f),
                        bodyColor: BaseColor, headColor: new Color(0.4f, 0.3f, 0.15f));
                    break;
                default:
                    // Passive/Hostile/Neutral：单 cube 既有路径。
                    // host 是 MobManager 创建的 Cube primitive，自带 Renderer；直接染色 host 本身。
                    BaseColor = Mob.MobTypeId switch
                    {
                        1 => new Color(0.95f, 0.7f, 0.7f),   // pig
                        2 => new Color(0.95f, 0.95f, 0.95f), // sheep
                        3 => new Color(0.4f, 0.7f, 0.3f),    // zombie
                        4 => new Color(0.93f, 0.93f, 0.85f), // skeleton（白骨）
                        5 => new Color(0.4f, 0.85f, 0.4f),   // creeper（草绿）
                        _ => Color.gray,
                    };
                    Renderer = GetComponent<Renderer>();
                    ApplyColor(BaseColor);
                    break;
            }
        }

        /// <summary>
        /// 创建 Body + Head 两个 cube 作为 transform 子物体，按颜色分别染色。
        /// 先清掉旧子物体（重设 kind 时不残留 Pig Body + Cow Head）。
        /// 移除 BoxCollider 避免与 ChunkStreamer 玩家位置冲突；用 DestroyImmediate
        /// 保证 EditMode 测试里能被立刻回收。
        /// </summary>
        private void BuildBodyHead(Vector3 bodyScale, Vector3 headOffset, Vector3 headScale,
            Color bodyColor, Color headColor)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localScale = bodyScale;
            body.transform.localPosition = Vector3.zero;
            var bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) DestroyImmediate(bodyCol);
            ApplyColorToRenderer(body.GetComponent<Renderer>(), bodyColor);

            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.transform.SetParent(transform, false);
            head.transform.localScale = headScale;
            head.transform.localPosition = headOffset;
            var headCol = head.GetComponent<Collider>();
            if (headCol != null) DestroyImmediate(headCol);
            ApplyColorToRenderer(head.GetComponent<Renderer>(), headColor);

            // Renderer 指向 Body（LateUpdate 染色沿用既有路径）。
            Renderer = body.GetComponent<Renderer>();
        }

        private static void ApplyColorToRenderer(Renderer r, Color c)
        {
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }

        public void ApplyColor(Color c)
        {
            if (Renderer == null) return;
            Renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, c);
            Renderer.SetPropertyBlock(_block);
        }

        private void LateUpdate()
        {
            if (Mob == null) return;
            transform.position = new Vector3(Mob.Position.X, Mob.Position.Y, Mob.Position.Z);
            if (Mob.HitFlashTimer > 0)
            {
                ApplyColor(Color.red);
            }
            else if (Mob.IsCreeper && Mob.FuseTimer > 0f)
            {
                // 苦力怕引信中：颜色随剩余时间变白闪烁
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
                ApplyColor(Color.Lerp(BaseColor, Color.white, pulse * 0.7f));
            }
            else
            {
                ApplyColor(BaseColor);
            }
        }
    }
}
