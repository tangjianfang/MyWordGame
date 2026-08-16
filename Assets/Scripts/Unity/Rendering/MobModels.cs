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
    /// 五生物部位表（spec §1「五生物拼装定义」，数值照抄）。
    /// <para>
    /// 坐标约定：脚底中心为原点、面朝 +Z，整体高 ≤1.9；相邻部位 AABB 重叠不超过较小者一半
    /// （由 MobModelsTests 守护）。色值：body/head 沿用 UrpMaterialFactory 的 m5 表，
    /// 新增部位色照 spec——猪鼻 #C87880、牛角 #D8D0C0、鸡嘴 #D9A03D、鸡冠 #C03028。
    /// 腿一律对角步态（legFL/legBR 同相 0，legFR/legBL 反相 π）。
    /// </para>
    /// <para>
    /// 旧三类（Passive/Hostile/Neutral）：保底 body+head 两部位，不倒退不重做
    /// （旧 kind 的最终颜色仍由 MobView 按 mobTypeId 染色覆盖）。
    /// </para>
    /// </summary>
    public static class MobModels
    {
        /// <summary>按生物类型取部位表（每次调用返回新数组，调用方可安全修改）。</summary>
        public static MobPart[] Build(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig:
                    return new[]
                    {
                        // 矮胖横身体 + 前端方头 + 头前深粉鼻，体高 ~0.9
                        new MobPart("body", new Vector3(0.9f, 0.6f, 0.6f), new Vector3(0f, 0.5f, 0f), Hex("#E8A0A8")),
                        new MobPart("head", new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 0.6f, 0.55f), Hex("#E8B8C0")),
                        new MobPart("snout", new Vector3(0.24f, 0.18f, 0.1f), new Vector3(0f, 0.52f, 0.85f), Hex("#C87880")),
                        new MobPart("legFL", new Vector3(0.15f, 0.3f, 0.15f), new Vector3(0.28f, 0.15f, 0.2f), Hex("#E8A0A8"), IsLeg: true, LegPhase: 0f),
                        new MobPart("legFR", new Vector3(0.15f, 0.3f, 0.15f), new Vector3(-0.28f, 0.15f, 0.2f), Hex("#E8A0A8"), IsLeg: true, LegPhase: Mathf.PI),
                        new MobPart("legBL", new Vector3(0.15f, 0.3f, 0.15f), new Vector3(0.28f, 0.15f, -0.2f), Hex("#E8A0A8"), IsLeg: true, LegPhase: Mathf.PI),
                        new MobPart("legBR", new Vector3(0.15f, 0.3f, 0.15f), new Vector3(-0.28f, 0.15f, -0.2f), Hex("#E8A0A8"), IsLeg: true, LegPhase: 0f), // 对角步态：FL=BR、FR=BL
                    };
                case MobKind.Cow:
                    return new[]
                    {
                        // 高大横身体 + 白花头 + 头顶灰白双角（0.1×0.1×0.15，长边竖直向上），体高 ~1.35
                        new MobPart("body", new Vector3(1.0f, 0.7f, 0.7f), new Vector3(0f, 0.85f, 0f), Hex("#6B4A35")),
                        new MobPart("head", new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 1.0f, 0.6f), Hex("#F0E8E0")),
                        new MobPart("hornL", new Vector3(0.1f, 0.15f, 0.1f), new Vector3(0.16f, 1.28f, 0.55f), Hex("#D8D0C0")),
                        new MobPart("hornR", new Vector3(0.1f, 0.15f, 0.1f), new Vector3(-0.16f, 1.28f, 0.55f), Hex("#D8D0C0")),
                        new MobPart("legFL", new Vector3(0.2f, 0.5f, 0.2f), new Vector3(0.3f, 0.25f, 0.25f), Hex("#6B4A35"), IsLeg: true, LegPhase: 0f),
                        new MobPart("legFR", new Vector3(0.2f, 0.5f, 0.2f), new Vector3(-0.3f, 0.25f, 0.25f), Hex("#6B4A35"), IsLeg: true, LegPhase: Mathf.PI),
                        new MobPart("legBL", new Vector3(0.2f, 0.5f, 0.2f), new Vector3(0.3f, 0.25f, -0.25f), Hex("#6B4A35"), IsLeg: true, LegPhase: Mathf.PI),
                        new MobPart("legBR", new Vector3(0.2f, 0.5f, 0.2f), new Vector3(-0.3f, 0.25f, -0.25f), Hex("#6B4A35"), IsLeg: true, LegPhase: 0f),
                    };
                case MobKind.Chicken:
                    return new[]
                    {
                        // 最小竖身体 + 红头 + 黄嘴 + 头顶红冠（0.2×0.1×0.15，长边沿前后向铺成脊）。
                        // spec 原尺寸按比例缩（腿 0.3→0.22、身高 0.5→0.38、头 0.3→0.26、嘴/冠同步缩），
                        // 站高 ~0.79（spec 体高 ~0.8）且严格小于猪 0.85——「体型最小」是鸡的核心辨识点（评审 I-1）
                        new MobPart("body", new Vector3(0.4f, 0.38f, 0.45f), new Vector3(0f, 0.41f, 0f), Hex("#F0EDE5")),
                        new MobPart("head", new Vector3(0.26f, 0.26f, 0.26f), new Vector3(0f, 0.6f, 0.16f), Hex("#D94F3D")),
                        new MobPart("beak", new Vector3(0.14f, 0.09f, 0.09f), new Vector3(0f, 0.57f, 0.335f), Hex("#D9A03D")),
                        new MobPart("comb", new Vector3(0.08f, 0.09f, 0.18f), new Vector3(0f, 0.745f, 0.16f), Hex("#C03028")),
                        new MobPart("legL", new Vector3(0.08f, 0.22f, 0.08f), new Vector3(0.08f, 0.11f, 0f), Hex("#D9A03D"), IsLeg: true, LegPhase: 0f),
                        new MobPart("legR", new Vector3(0.08f, 0.22f, 0.08f), new Vector3(-0.08f, 0.11f, 0f), Hex("#D9A03D"), IsLeg: true, LegPhase: Mathf.PI),
                    };
                case MobKind.Zombie:
                    return new[]
                    {
                        // 人形：竖身体 + 方头 + 水平前伸双臂（臂用肤色、腿用袍色区分），体高 ~1.9
                        new MobPart("body", new Vector3(0.5f, 0.75f, 0.3f), new Vector3(0f, 1.125f, 0f), Hex("#5A8A4A")),
                        new MobPart("head", new Vector3(0.4f, 0.4f, 0.4f), new Vector3(0f, 1.7f, 0f), Hex("#6FA05C")),
                        new MobPart("armL", new Vector3(0.2f, 0.2f, 0.7f), new Vector3(0.13f, 1.35f, 0.5f), Hex("#6FA05C")),
                        new MobPart("armR", new Vector3(0.2f, 0.2f, 0.7f), new Vector3(-0.13f, 1.35f, 0.5f), Hex("#6FA05C")),
                        new MobPart("legL", new Vector3(0.2f, 0.75f, 0.2f), new Vector3(0.13f, 0.375f, 0f), Hex("#5A8A4A"), IsLeg: true, LegPhase: 0f),
                        new MobPart("legR", new Vector3(0.2f, 0.75f, 0.2f), new Vector3(-0.13f, 0.375f, 0f), Hex("#5A8A4A"), IsLeg: true, LegPhase: Mathf.PI),
                    };
                case MobKind.Villager:
                    return new[]
                    {
                        // 人形长袍：竖身体下到脚（无独立腿）+ 双臂抱胸（两段小臂斜叠身前）+ 长鼻，体高 ~1.6。
                        // 长鼻为第 5 部位（spec §3 要求五生物部位数 ≥5，鼻子在允许部位类型内且是村民主要辨识点）；
                        // 鼻色 #C8986A = 头肤色 #E8B88A 各通道加深一档（沿用猪鼻「头色加深」的配方）。
                        new MobPart("body", new Vector3(0.5f, 1.2f, 0.3f), new Vector3(0f, 0.6f, 0f), Hex("#7A5C3E")),
                        new MobPart("head", new Vector3(0.4f, 0.4f, 0.4f), new Vector3(0f, 1.4f, 0f), Hex("#E8B88A")),
                        new MobPart("armLower", new Vector3(0.4f, 0.12f, 0.12f), new Vector3(0f, 0.84f, 0.18f), Hex("#7A5C3E")),
                        new MobPart("armUpper", new Vector3(0.4f, 0.12f, 0.12f), new Vector3(0f, 0.98f, 0.21f), Hex("#7A5C3E")),
                        new MobPart("nose", new Vector3(0.1f, 0.16f, 0.12f), new Vector3(0f, 1.36f, 0.26f), Hex("#C8986A")),
                    };
                default:
                    // 旧三类（Passive/Hostile/Neutral）：保底 body+head 两部位不倒退；
                    // 中性灰只占位，最终颜色由 MobView 按 mobTypeId 染色覆盖。
                    return new[]
                    {
                        new MobPart("body", new Vector3(0.8f, 0.9f, 0.8f), new Vector3(0f, 0.45f, 0f), Hex("#8A8A8A")),
                        new MobPart("head", new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0f, 1.15f, 0f), Hex("#9A9A9A")),
                    };
            }
        }

        private static Color Hex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c))
            {
                // 色值字面量写错不能静默透明（out 参数是 0,0,0,0，部位会整个隐形）——
                // 报错 + 返品红，跑测试/进游戏第一眼就暴露（评审 I-2）
                Debug.LogError("MobModels.Hex 颜色字面量非法: " + hex + "（应为 #RRGGBB 格式，检查部位表色值）");
                return Color.magenta;
            }
            return c;
        }
    }
}
