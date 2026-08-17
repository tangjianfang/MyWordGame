using MyWorld.Core.Entities;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// 生物拼装部位（m8 A1 纯静态表数据，A2 由 MobView 按表拼装）。
    /// </summary>
    public readonly struct MobPart
    {
        public readonly string Name;          // "body"/"head"/"legFL"...（调试可见，A2 按名建 GameObject）
        public readonly Vector3 Size;         // 部位尺寸（格）
        public readonly Vector3 LocalPosition; // 局部位置：脚底中心为原点、面朝 +Z
        public readonly Color Color;          // 经 UrpMaterialFactory 缓存染色
        public readonly bool IsLeg;           // 腿：参与行走摆动
        public readonly float LegPhase;       // 摆动相位（对角步态 0/π）

        // 参数名与字段/命名实参一致（brief 模板按 IsLeg:/LegPhase: 调用）
        public MobPart(string name, Vector3 size, Vector3 localPosition, Color color,
            bool IsLeg = false, float LegPhase = 0f)
        {
            Name = name;
            Size = size;
            LocalPosition = localPosition;
            Color = color;
            this.IsLeg = IsLeg;
            this.LegPhase = LegPhase;
        }
    }

    /// <summary>
    /// 五生物部位表门面（spec §1「五生物拼装定义」）。
    /// <para>
    /// m11 I1 起五生物（Pig/Cow/Chicken/Zombie/Villager）真值外置到
    /// <c>Assets/StreamingAssets/mobs/models/*.json</c>，由 <see cref="MobModelLibrary"/>
    /// 解析并缓存；本类只保留 <see cref="Build"/> 门面——签名与「每调用返回新数组」
    /// 语义不变，MobView/MobAssembly 零改动。数值逐字照抄旧 C# 常量表（迁移守恒由
    /// MobModelLibraryTests/MobModelsTests 守卫）。
    /// </para>
    /// <para>
    /// 坐标约定：脚底中心为原点、面朝 +Z，整体高 ≤1.9；相邻部位 AABB 重叠不超过较小者一半。
    /// 腿一律对角步态（legFL/legBR 同相 0，legFR/legBL 反相 π）。各部位的具体注释
    /// （配色来源、鸡的比例缩放缘由等）随真值留在 JSON 同目录的 _format 说明与测试断言里。
    /// </para>
    /// <para>
    /// 旧三类（Passive/Hostile/Neutral）：保底 body+head 两部位，不倒退不重做
    /// （旧 kind 的最终颜色仍由 MobView 按 mobTypeId 染色覆盖）。
    /// </para>
    /// </summary>
    public static class MobModels
    {
        /// <summary>
        /// 按生物类型取部位表（每次调用返回新数组，调用方可安全修改）。
        /// 五生物委托 <see cref="MobModelLibrary.Load"/>（JSON 真值）；旧三类走下方 C# 保底表。
        /// </summary>
        public static MobPart[] Build(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig:
                case MobKind.Cow:
                case MobKind.Chicken:
                case MobKind.Zombie:
                case MobKind.Villager:
                    // 五生物真值已外置 mobs/models/*.json（m11 I1）——
                    // 门面只转调，加载/缓存/报错策略统一在 Library
                    return MobModelLibrary.Load(kind);
                default:
                    // 旧三类（Passive/Hostile/Neutral）：保底 body+head 两部位不倒退；
                    // 中性灰只占位，最终颜色由 MobView 按 mobTypeId 染色覆盖。
                    // Hex 检查走 MobModelLibrary（同一条「非法报错返品红」路径，评审 I-2 配方）。
                    return new[]
                    {
                        new MobPart("body", new Vector3(0.8f, 0.9f, 0.8f), new Vector3(0f, 0.45f, 0f), MobModelLibrary.Hex("#8A8A8A")),
                        new MobPart("head", new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 1.15f, 0f), MobModelLibrary.Hex("#9A9A9A")),
                    };
            }
        }
    }
}
