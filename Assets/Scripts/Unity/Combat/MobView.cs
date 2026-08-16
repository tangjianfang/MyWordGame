using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Combat
{
    /// <summary>
    /// 把 Core 的 <see cref="Mob"/> 绑定到一个 GameObject 上。
    /// m8 A2：五生物（Pig/Cow/Chicken/Zombie/Villager）按 <see cref="MobModels"/> 部位表拼装——
    /// 每个部位一个子 cube，腿用「枢轴在腿顶 + cube 几何下移半高补偿」的铰链手法
    /// （绕髋部枢轴转 X 轴即得前后摆腿），供 <see cref="SetWalkPhase"/> 摆动。
    /// 部位表全权负责视觉：host cube 的 Renderer 在 MobManager.SpawnMob 与本类 Setup
    /// 双双禁用，消灭重合渲染。
    /// 旧 Passive/Hostile/Neutral：单 cube，按 mobTypeId 染色（既有 MobManager 路径）。
    /// 同步 Position / 朝向（面朝移动方向，模型约定面朝 +Z）/ 受伤红闪（涂满全部部位）。
    /// </summary>
    public sealed class MobView : MonoBehaviour
    {
        /// <summary>腿摆幅（度）：SetWalkPhase 的 sin 摆动上下限（brief 规定 ±20°）。</summary>
        public const float LegSwingDegrees = 20f;

        public Mob Mob;
        public Renderer Renderer;             // 旧单 cube 路径的 host 渲染器（五生物拼装后为 null）
        public Color BaseColor;               // 旧单 cube 路径底色；五生物取 body 部位色（对外语义不变）
        public MobKind Kind;                  // 当前 kind，便于测试与调试

        // m8 A2 拼装态：全部部位渲染器 + 各自底色（红闪要涂满全身），腿枢轴 + 相位
        private Renderer[] _partRenderers;
        private Color[] _partBaseColors;
        private Transform[] _legPivots;
        private float[] _legPhases;
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
        /// 五生物（m8 A2）：清旧子物体 → 按 <see cref="MobModels.Build"/> 部位表逐部位拼装，
        /// 色值直接来自部位表（同色走 UrpMaterialFactory 缓存共享）。
        /// 旧 Passive/Hostile/Neutral：单 cube（host 自带 Renderer），按 mobTypeId 染色，行为不变。
        /// </summary>
        public void Setup(MobKind kind)
        {
            Kind = kind;
            switch (kind)
            {
                case MobKind.Pig:
                case MobKind.Cow:
                case MobKind.Chicken:
                case MobKind.Zombie:
                case MobKind.Villager:
                    BuildFromPartTable(kind);
                    break;
                default:
                    LegacySingleCube();
                    break;
            }
        }

        /// <summary>
        /// m8 A2：按部位表拼装。每个部位恰好一个直接子物体（测试按 childCount==部位数断言）：
        /// 非腿部位直接建 cube；腿部位建「枢轴 + 下挂 cube」两层（铰链手法见下）。
        /// 染色双保险（m5 A3 沿用）：sharedMaterial 换 URP/Lit（CreatePrimitive 的
        /// Default-Material 是 Standard，URP 下渲染洋红），MPB 继续承担实例色
        /// （LateUpdate 的受伤红闪依赖它，_BaseColor 对 URP/Lit 有效）。
        /// </summary>
        private void BuildFromPartTable(MobKind kind)
        {
            // 重设 kind 不残留旧部位（Pig 的腿不能留在 Cow 身上）。
            // 用 DestroyImmediate 保证 EditMode 测试里能被立刻回收。
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            var parts = MobModels.Build(kind);
            var renderers = new Renderer[parts.Length];
            var baseColors = new Color[parts.Length];
            var legPivots = new List<Transform>();
            var legPhases = new List<float>();

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                GameObject cube;
                if (part.IsLeg)
                {
                    // 腿铰链（brief 模板）：枢轴放在腿顶（髋部）——LocalPosition + up×半高，
                    // cube 几何下移半高补偿回原部位位置。绕枢轴 X 轴旋转即得前后摆腿，
                    // 而不是绕腿中心「原地蹭」。
                    var pivot = new GameObject(part.Name);
                    pivot.transform.SetParent(transform, false);
                    pivot.transform.localPosition =
                        part.LocalPosition + Vector3.up * (part.Size.y * 0.5f);
                    cube = CreatePartCube(part, pivot.transform,
                        Vector3.down * (part.Size.y * 0.5f));
                    legPivots.Add(pivot.transform);
                    legPhases.Add(part.LegPhase);
                }
                else
                {
                    cube = CreatePartCube(part, transform, part.LocalPosition);
                }
                renderers[i] = cube.GetComponent<Renderer>();
                baseColors[i] = part.Color;
                ApplyColorToRenderer(renderers[i], part.Color);
            }

            _partRenderers = renderers;
            _partBaseColors = baseColors;
            _legPivots = legPivots.ToArray();
            _legPhases = legPhases.ToArray();

            BaseColor = UrpMaterialFactory.MobBodyColor(kind); // 对外底色语义保留（body 部位色）
            Renderer = null;                                   // 拼装后没有单一渲染器

            // 部位表全权负责视觉：host 若自带 cube Renderer（MobManager 建的 host）则禁用，
            // 拼装部位已覆盖 host 体积，双份渲染只会重合。MobManager.SpawnMob 也禁一次——
            // 双保险，直接 Setup 的宿主（如测试）同样消灭重合渲染。
            var hostRenderer = GetComponent<Renderer>();
            if (hostRenderer != null) hostRenderer.enabled = false;
        }

        /// <summary>
        /// 建一个部位 cube 并挂到 <paramref name="parent"/> 下。
        /// 移除部位自带的 BoxCollider：攻击射线只认 host 的 BoxCollider
        /// （CombatController 用 hit.collider.GetComponent&lt;MobView&gt;()，命中部位 cube 拿不到 MobView）。
        /// </summary>
        private static GameObject CreatePartCube(MobPart part, Transform parent, Vector3 localPosition)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = part.IsLeg ? part.Name + "Mesh" : part.Name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = part.Size;
            var col = cube.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            return cube;
        }

        /// <summary>
        /// 旧三类（Passive/Hostile/Neutral）单 cube 路径：host 就是本体，按 mobTypeId 染色。
        /// 从拼装 kind 切回时把 host Renderer 重新点亮（拼装路径禁用过它）。
        /// </summary>
        private void LegacySingleCube()
        {
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
            if (Renderer != null) Renderer.enabled = true;
            _partRenderers = null;
            _partBaseColors = null;
            _legPivots = null;
            _legPhases = null;
            ApplyColor(BaseColor);
        }

        /// <summary>
        /// m8 A2：按累计相位摆腿——每条腿绕髋部枢轴的 X 角 =
        /// sin(phase + LegPhase) × <see cref="LegSwingDegrees"/>。
        /// 对角步态由部位表的 LegPhase（0/π）天然给出（FL=BR 同相、FR=BL 反相）。
        /// phase 由 MobManager 按帧间位移累计（phase += 位移×8f）：站定不增，
        /// 停步后随 sin 过零自然回正（选简方案，不做停步缓动）。
        /// 无腿生物（村民长袍到脚）是 no-op。
        /// </summary>
        public void SetWalkPhase(float phase)
        {
            if (_legPivots == null) return;
            for (int i = 0; i < _legPivots.Length; i++)
            {
                _legPivots[i].localRotation =
                    Quaternion.Euler(Mathf.Sin(phase + _legPhases[i]) * LegSwingDegrees, 0f, 0f);
            }
        }

        private static void ApplyColorToRenderer(Renderer r, Color c)
        {
            if (r == null) return;
            // 先换 URP/Lit 材质（同色缓存复用），再叠 MPB 实例色——见 BuildFromPartTable 注释
            r.sharedMaterial = UrpMaterialFactory.CreateLit(c);
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, c);
            r.SetPropertyBlock(block);
        }

        public void ApplyColor(Color c)
        {
            if (Renderer == null) return;
            SetInstanceColor(Renderer, c);
        }

        /// <summary>只写 MPB 实例色（材质在拼装/SpawnMob 时已换好，不必每帧重设）。</summary>
        private void SetInstanceColor(Renderer r, Color c)
        {
            if (r == null) return;
            // EditMode 下 AddComponent 不触发 Awake，_block 懒建（否则 ArgumentNullException）
            if (_block == null) _block = new MaterialPropertyBlock();
            r.GetPropertyBlock(_block);
            _block.SetColor(ColorId, c);
            r.SetPropertyBlock(_block);
        }

        private void LateUpdate()
        {
            if (Mob == null) return;
            transform.position = new Vector3(Mob.Position.X, Mob.Position.Y, Mob.Position.Z);

            // m8 A2 拼装路径：朝向 + 逐部位染色
            if (_partRenderers != null)
            {
                // 面朝移动方向（模型约定面朝 +Z，速度只取水平分量；
                // 速度归零时保持原朝向，站定不闪转）
                var v = Mob.Velocity;
                float horizSq = v.X * v.X + v.Z * v.Z;
                if (horizSq > 1e-6f)
                {
                    transform.rotation =
                        Quaternion.LookRotation(new Vector3(v.X, 0f, v.Z), Vector3.up);
                }

                // 受伤红闪 / 苦力怕引信白闪：涂满全部部位，各自以部位底色为基准
                for (int i = 0; i < _partRenderers.Length; i++)
                {
                    Color c = _partBaseColors[i];
                    if (Mob.HitFlashTimer > 0)
                    {
                        c = Color.red;
                    }
                    else if (Mob.IsCreeper && Mob.FuseTimer > 0f)
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
                        c = Color.Lerp(_partBaseColors[i], Color.white, pulse * 0.7f);
                    }
                    SetInstanceColor(_partRenderers[i], c);
                }
                return;
            }

            // 旧单 cube 路径：host 本体染色
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
