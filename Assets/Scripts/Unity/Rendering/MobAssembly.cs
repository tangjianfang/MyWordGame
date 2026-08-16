using System.Collections.Generic;
using MyWorld.Core.Entities;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// <see cref="MobAssembly.Assemble"/> 的拼装产物：全部位渲染器与底色 + 腿摆句柄。
    /// 部位底色是受伤红闪 / 职业染色的还原基准；<see cref="LegPivots"/> 为空数组
    /// （无腿生物，如长袍到脚的村民）时 <see cref="SetWalkPhase"/> 是 no-op。
    /// </summary>
    public sealed class AssembledMob
    {
        /// <summary>全部位的渲染器（与 <see cref="PartBaseColors"/> 一一对应）。</summary>
        public readonly Renderer[] PartRenderers;

        /// <summary>各部位底色（部位表色值，染色还原基准）。</summary>
        public readonly Color[] PartBaseColors;

        /// <summary>腿枢轴（髋部）。无腿生物为空数组。</summary>
        public readonly Transform[] LegPivots;

        /// <summary>各腿摆动相位（对角步态 0/π，与 <see cref="LegPivots"/> 一一对应）。</summary>
        public readonly float[] LegPhases;

        internal AssembledMob(Renderer[] partRenderers, Color[] partBaseColors,
            Transform[] legPivots, float[] legPhases)
        {
            PartRenderers = partRenderers;
            PartBaseColors = partBaseColors;
            LegPivots = legPivots;
            LegPhases = legPhases;
        }

        /// <summary>
        /// 按累计相位摆腿——每条腿绕髋部枢轴的 X 角 =
        /// sin(phase + LegPhase) × <see cref="MobAssembly.LegSwingDegrees"/>。
        /// 对角步态由部位表的 LegPhase（0/π）天然给出（FL=BR 同相、FR=BL 反相）。
        /// phase 由调用方（MobManager）按帧间位移累计；站定时缓动到最近的 π 整数倍
        /// （sin(nπ + LegPhase)=0，腿摆回正直立）。无腿生物是 no-op。
        /// </summary>
        public void SetWalkPhase(float phase)
        {
            for (int i = 0; i < LegPivots.Length; i++)
            {
                LegPivots[i].localRotation =
                    Quaternion.Euler(Mathf.Sin(phase + LegPhases[i]) * MobAssembly.LegSwingDegrees, 0f, 0f);
            }
        }
    }

    /// <summary>
    /// 生物按部位表拼装的共用 helper（m8 终审修 I-1：从 MobView.BuildFromPartTable 抽出）。
    /// MobView（MobManager 刷的五生物）与 VillagerView（VillagerManager 刷的交易村民）
    /// 共用同一份「部位 cube + 腿枢轴 + host Renderer 禁用」实现——消灭 A2 评审 M-5
    /// 承诺统一却未做的「同一物种两副面孔」双形态分叉。
    /// <para>
    /// 每个部位恰好一个直接子物体（腿部位是「枢轴 + 下挂 cube」两层）：
    /// 腿铰链手法——枢轴放在腿顶（髋部），cube 几何下移半高补偿回原部位位置，
    /// 绕枢轴 X 轴旋转即得前后摆腿，而不是绕腿中心「原地蹭」。
    /// 染色双保险（m5 A3 沿用）：sharedMaterial 换 URP/Lit（CreatePrimitive 的
    /// Default-Material 是 Standard，URP 下渲染洋红），MPB 承担实例色
    /// （_BaseColor 对 URP/Lit 有效，同色走 UrpMaterialFactory 缓存共享）。
    /// </para>
    /// </summary>
    public static class MobAssembly
    {
        /// <summary>腿摆幅（度）：SetWalkPhase 的 sin 摆动上下限（brief 规定 ±20°）。</summary>
        public const float LegSwingDegrees = 20f;

        /// <summary>
        /// 按 <see cref="MobModels.Build"/> 部位表在 <paramref name="root"/> 下逐部位拼装。
        /// 先清空 root 的既有子物体（重设 kind 不残留旧部位），再建部位 cube，
        /// 最后禁用 root 自带的 Renderer（拼装部位已覆盖 host 体积，双份渲染只会重合；
        /// MobManager / VillagerManager 建的 host cube 与直接 Setup 的测试宿主统一走这里消灭重合渲染）。
        /// </summary>
        public static AssembledMob Assemble(Transform root, MobKind kind)
        {
            // 用 DestroyImmediate 保证 EditMode 测试里子物体能被立刻回收。
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.GetChild(i).gameObject);
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
                    // cube 几何下移半高补偿回原部位位置。绕枢轴 X 轴旋转即得前后摆腿。
                    var pivot = new GameObject(part.Name);
                    pivot.transform.SetParent(root, false);
                    pivot.transform.localPosition =
                        part.LocalPosition + Vector3.up * (part.Size.y * 0.5f);
                    cube = CreatePartCube(part, pivot.transform,
                        Vector3.down * (part.Size.y * 0.5f));
                    legPivots.Add(pivot.transform);
                    legPhases.Add(part.LegPhase);
                }
                else
                {
                    cube = CreatePartCube(part, root, part.LocalPosition);
                }
                renderers[i] = cube.GetComponent<Renderer>();
                baseColors[i] = part.Color;
                ApplyColorToRenderer(renderers[i], part.Color);
            }

            var hostRenderer = root.GetComponent<Renderer>();
            if (hostRenderer != null) hostRenderer.enabled = false;

            return new AssembledMob(renderers, baseColors, legPivots.ToArray(), legPhases.ToArray());
        }

        /// <summary>
        /// 部位表站高（最高部位顶面）：host 碰撞体按它立起，判定范围与拼装模型一致。
        /// （m8 终审修：从 MobManager 私有方法移来，VillagerManager 交易村民路径共用。）
        /// </summary>
        public static float PartTableHeight(MobKind kind)
        {
            float height = 0f;
            foreach (var part in MobModels.Build(kind))
            {
                height = Mathf.Max(height, part.LocalPosition.y + part.Size.y * 0.5f);
            }
            return height;
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
            if (col != null) Object.DestroyImmediate(col);
            return cube;
        }

        /// <summary>
        /// 染色双保险（m5 A3 沿用）：先换 URP/Lit 材质（同色缓存复用），
        /// 再叠 MPB 实例色（调用方的红闪/职业色走同一通道覆盖）。
        /// </summary>
        private static void ApplyColorToRenderer(Renderer r, Color c)
        {
            if (r == null) return;
            r.sharedMaterial = UrpMaterialFactory.CreateLit(c);
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor(Shader.PropertyToID("_BaseColor"), c);
            r.SetPropertyBlock(block);
        }
    }
}
