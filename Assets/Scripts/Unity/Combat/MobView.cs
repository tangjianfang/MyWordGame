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
    /// 拼装核心在 m8 终审修（I-1）抽到 <see cref="MobAssembly"/>，与交易村民
    /// <see cref="VillagerView"/> 共用同一份实现（消灭双形态分叉）。
    /// 部位表全权负责视觉：host cube 的 Renderer 在 MobManager.SpawnMob 与 MobAssembly.Assemble
    /// 双双禁用，消灭重合渲染。
    /// 旧 Passive/Hostile/Neutral：单 cube，按 mobTypeId 染色（既有 MobManager 路径）。
    /// 同步 Position / 朝向（面朝移动方向，模型约定面朝 +Z）/ 受伤红闪（涂满全部部位）。
    /// </summary>
    public sealed class MobView : MonoBehaviour
    {
        /// <summary>腿摆幅（度）：SetWalkPhase 的 sin 摆动上下限（brief 规定 ±20°）。</summary>
        public const float LegSwingDegrees = MobAssembly.LegSwingDegrees;

        public Mob Mob;
        public Renderer Renderer;             // 旧单 cube 路径的 host 渲染器（五生物拼装后为 null）
        public Color BaseColor;               // 旧单 cube 路径底色；五生物取 body 部位色（对外语义不变）
        public MobKind Kind;                  // 当前 kind，便于测试与调试

        // m8 A2 拼装态（终审修收拢成 MobAssembly 的产物句柄）：部位渲染器 + 底色 + 腿枢轴/相位
        private AssembledMob _assembled;
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
                // m11 W1-1：三敌对走部位表拼装（造型 JSON 同批入库；走保底灰双部位
                // 的话夜间刷出的骷髅/蜘蛛/苦力怕全是灰方块，模型白做）
                case MobKind.Skeleton:
                case MobKind.Spider:
                case MobKind.Creeper:
                    BuildFromPartTable(kind);
                    break;
                default:
                    LegacySingleCube();
                    break;
            }
        }

        /// <summary>
        /// m8 A2：按部位表拼装（每个部位恰好一个直接子物体，腿是「枢轴 + 下挂 cube」两层，
        /// 染色双保险——细节见 <see cref="MobAssembly.Assemble"/> 注释）。
        /// 终审修（I-1）：拼装核心抽到 <see cref="MobAssembly"/> 与 VillagerView 共用，
        /// 本方法只补 Mob 专属的对外语义。
        /// </summary>
        private void BuildFromPartTable(MobKind kind)
        {
            _assembled = MobAssembly.Assemble(transform, kind);

            BaseColor = UrpMaterialFactory.MobBodyColor(kind); // 对外底色语义保留（body 部位色）
            Renderer = null;                                   // 拼装后没有单一渲染器
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
            _assembled = null;
            ApplyColor(BaseColor);
        }

        /// <summary>
        /// m8 A2：按累计相位摆腿——每条腿绕髋部枢轴的 X 角 =
        /// sin(phase + LegPhase) × <see cref="LegSwingDegrees"/>。
        /// 对角步态由部位表的 LegPhase（0/π）天然给出（FL=BR 同相、FR=BL 反相）。
        /// phase 由 MobManager 按帧间位移累计（phase += 位移×8f）；站定时由 MobManager
        /// 把相位缓动到最近的 π 整数倍（fix1：sin(nπ + LegPhase)=0，腿摆回正直立，
        /// 不冻结在半摆位）。无腿生物（村民长袍到脚）是 no-op。
        /// </summary>
        public void SetWalkPhase(float phase)
        {
            if (_assembled == null) return;
            _assembled.SetWalkPhase(phase);
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

            // m11 W1-1：新苦力怕引信膨胀——引信倒数进度驱动整体放大（最高 1.3×），
            // 起爆 / 取消后回到 1。旧苦力怕（mobTypeId=5）不膨胀（保持既有视觉）。
            if (Mob.Kind == MobKind.Creeper)
            {
                float progress = 1f - Mathf.Clamp01(Mob.FuseTimer / MyWorld.Core.Entities.MobAI.NewCreeperFuseDuration);
                transform.localScale = Vector3.one * (1f + progress * 0.3f);
            }
            else if (transform.localScale != Vector3.one)
            {
                transform.localScale = Vector3.one;
            }

            // m8 A2 拼装路径：朝向 + 逐部位染色
            if (_assembled != null)
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
                for (int i = 0; i < _assembled.PartRenderers.Length; i++)
                {
                    Color c = _assembled.PartBaseColors[i];
                    if (Mob.HitFlashTimer > 0)
                    {
                        c = Color.red;
                    }
                    else if (FuseFlashing())
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
                        c = Color.Lerp(_assembled.PartBaseColors[i], Color.white, pulse * 0.7f);
                    }
                    SetInstanceColor(_assembled.PartRenderers[i], c);
                }
                return;
            }

            // 旧单 cube 路径：host 本体染色
            if (Mob.HitFlashTimer > 0)
            {
                ApplyColor(Color.red);
            }
            else if (FuseFlashing())
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

        /// <summary>引信白闪是否激活：旧苦力怕（mobTypeId=5）与新苦力怕（MobKind.Creeper）共用引信视觉。</summary>
        private bool FuseFlashing() => Mob.FuseTimer > 0f && (Mob.IsCreeper || Mob.Kind == MobKind.Creeper);
    }
}
