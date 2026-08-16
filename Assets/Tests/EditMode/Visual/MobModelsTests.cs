#if UNITY_EDITOR
// m8 A1：MobModels 五生物部位表的纯数据断言（不建 GameObject，A2 才拼装）。
// 坐标约定：脚底中心为原点、面朝 +Z，整体高 ≤1.9（spec §1）。
//
// 断言集（brief Step 1）：
//   1) 部位数：五生物按 spec §1 表逐部位求和——猪 7（body+head+snout+4 腿）、
//      牛 8（+双角）、鸡 6（+嘴+冠）、僵尸 6、村民 5；且 ≥5（spec §3）。
//      注：计划文案写「猪 8/牛 9/鸡 7」，但 spec 非目标明确「尾巴/翅膀/耳朵不做」，
//      多出的那段无 spec 依据（计划自带的猪模板也只有 7 部位），故以 spec 表求和为准。
//      村民 spec 表只有 4 部位，为满足「≥5」补长鼻（鼻子在 spec 允许的部位类型
//      「腿/鼻/角/冠/臂」内，且是村民最主要辨识点）。
//   2) 必含 name=="head"，部位名不重复
//   3) 腿部位 LegPhase ∈ {0, π}；四腿对角配对（FL=BR、FR=BL），双腿交替反相
//   4) 全部部位落在以脚底为原点、高 ≤1.9 的包围盒内，且至少一部位贴地
//   5) 相邻部位 AABB 体积重叠不超过较小者一半
//   6) 色值：body/head 沿用 m5 表（UrpMaterialFactory.MobBodyColor/MobHeadColor），
//      新增部位色照 spec（猪鼻 #C87880、牛角 #D8D0C0、鸡嘴 #D9A03D、鸡冠 #C03028）
//   7) 旧三类（Passive/Hostile/Neutral）保底 body+head 两部位不倒退
// UNITY_EDITOR 包裹确保 dotnet 链不参与（dotnet 基线 473 不变）。
using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class MobModelsTests
    {
        [TestCase(MobKind.Pig, 7)]
        [TestCase(MobKind.Cow, 8)]
        [TestCase(MobKind.Chicken, 6)]
        [TestCase(MobKind.Zombie, 6)]
        [TestCase(MobKind.Villager, 5)]
        public void Build_FiveMobs_PartCountMatchesSpecTable(MobKind kind, int expectedCount)
        {
            var parts = MobModels.Build(kind);
            Assert.That(parts, Is.Not.Null, kind + " 部位表不应返回 null");
            Assert.That(parts.Length, Is.EqualTo(expectedCount),
                kind + " 部位数应与 spec §1 表逐部位求和一致（实际 " + parts.Length + "）");
            Assert.That(parts.Length, Is.GreaterThanOrEqualTo(5),
                kind + " 五生物部位数应 ≥5（spec §3）");
        }

        [TestCase(MobKind.Pig, "snout")]      // 猪鼻：矮胖粉身 + 深粉鼻
        [TestCase(MobKind.Cow, "hornL")]      // 牛角：高大 + 头顶双角
        [TestCase(MobKind.Cow, "hornR")]
        [TestCase(MobKind.Chicken, "beak")]   // 鸡嘴：黄嘴
        [TestCase(MobKind.Chicken, "comb")]   // 鸡冠：红冠
        [TestCase(MobKind.Zombie, "armL")]    // 僵尸：前伸双臂
        [TestCase(MobKind.Zombie, "armR")]
        [TestCase(MobKind.Villager, "armLower")] // 村民：双臂抱胸（两段小臂斜叠身前）
        [TestCase(MobKind.Villager, "armUpper")]
        [TestCase(MobKind.Villager, "nose")]  // 村民长鼻（第 5 部位，辨识点）
        public void Build_FiveMobs_SignaturePartExists(MobKind kind, string partName)
        {
            var part = Find(MobModels.Build(kind), partName);
            Assert.That(part, Is.Not.Null, kind + " 应包含辨识部位 " + partName);
        }

        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        [TestCase(MobKind.Villager)]
        public void Build_FiveMobs_ContainsHeadAndUniqueNames(MobKind kind)
        {
            var parts = MobModels.Build(kind);
            Assert.That(Find(parts, "head"), Is.Not.Null, kind + " 必含 name==\"head\" 部位");
            var names = new HashSet<string>();
            foreach (var part in parts)
            {
                Assert.That(part.Name, Is.Not.Null.And.Not.Empty, kind + " 部位名不能为空");
                Assert.That(names.Add(part.Name), Is.True,
                    kind + " 部位名重复: " + part.Name + "（A2 按名建 GameObject，不能撞名）");
            }
        }

        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        public void Build_LeggedMobs_LegPhaseZeroOrPiAndDiagonallyPaired(MobKind kind)
        {
            var parts = MobModels.Build(kind);
            var legs = new List<MobPart>();
            foreach (var part in parts)
            {
                if (part.IsLeg) legs.Add(part);
            }
            Assert.That(legs.Count, Is.GreaterThan(0), kind + " 应有腿部位（IsLeg）");

            int phaseZero = 0, phasePi = 0;
            foreach (var leg in legs)
            {
                bool isZero = Mathf.Approximately(leg.LegPhase, 0f);
                bool isPi = Mathf.Approximately(leg.LegPhase, Mathf.PI);
                Assert.That(isZero || isPi, Is.True,
                    kind + " 腿 " + leg.Name + " 的 LegPhase 应 ∈ {0, π}，实际 " + leg.LegPhase);
                if (isZero) phaseZero++; else phasePi++;
            }
            // 对角/交替步态：两种相位的腿数应均衡（差 ≤1）
            Assert.That(Mathf.Abs(phaseZero - phasePi), Is.LessThanOrEqualTo(1),
                kind + " 0/π 相位的腿数应均衡（实际 0 相 " + phaseZero + " 条、π 相 " + phasePi + " 条）");

            if (legs.Count == 4)
            {
                var legFL = Find(parts, "legFL");
                var legFR = Find(parts, "legFR");
                var legBL = Find(parts, "legBL");
                var legBR = Find(parts, "legBR");
                Assert.That(legFL.HasValue && legFR.HasValue && legBL.HasValue && legBR.HasValue,
                    Is.True, kind + " 四腿命名应为 legFL/legFR/legBL/legBR");
                Assert.That(Mathf.Approximately(legFL.Value.LegPhase, legBR.Value.LegPhase), Is.True,
                    kind + " 对角步态：legFL 与 legBR 应同相");
                Assert.That(Mathf.Approximately(legFR.Value.LegPhase, legBL.Value.LegPhase), Is.True,
                    kind + " 对角步态：legFR 与 legBL 应同相");
                Assert.That(!Mathf.Approximately(legFL.Value.LegPhase, legFR.Value.LegPhase), Is.True,
                    kind + " 同侧前腿与前腿相邻（legFL/legFR）应反相");
            }
            else if (legs.Count == 2)
            {
                var legL = Find(parts, "legL");
                var legR = Find(parts, "legR");
                Assert.That(legL.HasValue && legR.HasValue, Is.True,
                    kind + " 双腿命名应为 legL/legR");
                Assert.That(!Mathf.Approximately(legL.Value.LegPhase, legR.Value.LegPhase), Is.True,
                    kind + " 双腿应反相（交替步态）");
            }
        }

        [Test]
        public void Build_Villager_HasNoLegParts()
        {
            // spec §1：村民长袍到脚，无独立腿（走 A2 摆动逻辑时无腿可摆）
            foreach (var part in MobModels.Build(MobKind.Villager))
            {
                Assert.That(part.IsLeg, Is.False,
                    "村民长袍到脚无独立腿，部位 " + part.Name + " 不应标记 IsLeg");
            }
        }

        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        [TestCase(MobKind.Villager)]
        [TestCase(MobKind.Passive)]
        [TestCase(MobKind.Hostile)]
        [TestCase(MobKind.Neutral)]
        public void Build_AllKinds_PartsStayInFeetOriginBoundingBox(MobKind kind)
        {
            var parts = MobModels.Build(kind);
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var part in parts)
            {
                Vector3 min = part.LocalPosition - part.Size * 0.5f;
                Vector3 max = part.LocalPosition + part.Size * 0.5f;
                Assert.That(min.y, Is.GreaterThanOrEqualTo(-0.001f),
                    kind + " 部位 " + part.Name + " 穿地（脚底为原点，y 不得为负）");
                Assert.That(Mathf.Max(Mathf.Abs(min.x), Mathf.Abs(max.x)), Is.LessThanOrEqualTo(0.6f),
                    kind + " 部位 " + part.Name + " 横向超宽（|x| 应 ≤0.6）");
                Assert.That(Mathf.Max(Mathf.Abs(min.z), Mathf.Abs(max.z)), Is.LessThanOrEqualTo(1.0f),
                    kind + " 部位 " + part.Name + " 前后超界（|z| 应 ≤1.0）");
                minY = Mathf.Min(minY, min.y);
                maxY = Mathf.Max(maxY, max.y);
            }
            Assert.That(maxY, Is.LessThanOrEqualTo(1.9f + 0.001f),
                kind + " 整体高度应 ≤1.9（实际 " + maxY.ToString("F3") + "）");
            Assert.That(minY, Is.LessThanOrEqualTo(0.001f),
                kind + " 至少一个部位贴地（脚底为原点，最低点实际 " + minY.ToString("F3") + "）");
        }

        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        [TestCase(MobKind.Villager)]
        [TestCase(MobKind.Passive)]
        [TestCase(MobKind.Hostile)]
        [TestCase(MobKind.Neutral)]
        public void Build_AllKinds_AdjacentPartsOverlapAtMostHalfOfSmaller(MobKind kind)
        {
            var parts = MobModels.Build(kind);
            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    float overlap = OverlapVolume(parts[i], parts[j]);
                    float smaller = Mathf.Min(Volume(parts[i]), Volume(parts[j]));
                    Assert.That(overlap, Is.LessThanOrEqualTo(smaller * 0.5f + 0.0001f),
                        kind + " 部位 " + parts[i].Name + " 与 " + parts[j].Name +
                        " 重叠超半（重叠 " + overlap.ToString("F4") + "，较小者体积 " + smaller.ToString("F4") + "）");
                }
            }
        }

        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        [TestCase(MobKind.Villager)]
        public void Build_FiveMobs_BodyHeadColorsFollowM5Table(MobKind kind)
        {
            var parts = MobModels.Build(kind);
            var body = Find(parts, "body");
            var head = Find(parts, "head");
            Assert.That(body.HasValue, Is.True, kind + " 应有 body 部位");
            Assert.That(head.HasValue, Is.True, kind + " 应有 head 部位");
            AssertColorsClose(body.Value.Color, UrpMaterialFactory.MobBodyColor(kind), kind + " body 色");
            AssertColorsClose(head.Value.Color, UrpMaterialFactory.MobHeadColor(kind), kind + " head 色");
        }

        [Test]
        public void Build_SignaturePartColorsFollowSpecTable()
        {
            AssertColorsClose(Find(MobModels.Build(MobKind.Pig), "snout").Value.Color,
                Hex("#C87880"), "猪鼻深粉");
            AssertColorsClose(Find(MobModels.Build(MobKind.Cow), "hornL").Value.Color,
                Hex("#D8D0C0"), "牛角灰白");
            AssertColorsClose(Find(MobModels.Build(MobKind.Cow), "hornR").Value.Color,
                Hex("#D8D0C0"), "牛角灰白");
            AssertColorsClose(Find(MobModels.Build(MobKind.Chicken), "beak").Value.Color,
                Hex("#D9A03D"), "鸡嘴黄");
            AssertColorsClose(Find(MobModels.Build(MobKind.Chicken), "comb").Value.Color,
                Hex("#C03028"), "鸡冠红");
        }

        [TestCase(MobKind.Passive)]
        [TestCase(MobKind.Hostile)]
        [TestCase(MobKind.Neutral)]
        public void Build_LegacyKinds_FallbackBodyAndHead(MobKind kind)
        {
            var parts = MobModels.Build(kind);
            Assert.That(parts.Length, Is.GreaterThanOrEqualTo(2),
                kind + " 旧三类保底 body+head 两部位不倒退");
            Assert.That(Find(parts, "body"), Is.Not.Null, kind + " 保底应含 body");
            Assert.That(Find(parts, "head"), Is.Not.Null, kind + " 保底应含 head");
        }

        private static MobPart? Find(MobPart[] parts, string name)
        {
            foreach (var part in parts)
            {
                if (part.Name == name) return part;
            }
            return null;
        }

        private static float Volume(MobPart part)
        {
            return part.Size.x * part.Size.y * part.Size.z;
        }

        private static float OverlapVolume(MobPart a, MobPart b)
        {
            Vector3 aMin = a.LocalPosition - a.Size * 0.5f;
            Vector3 aMax = a.LocalPosition + a.Size * 0.5f;
            Vector3 bMin = b.LocalPosition - b.Size * 0.5f;
            Vector3 bMax = b.LocalPosition + b.Size * 0.5f;
            Vector3 lo = Vector3.Max(aMin, bMin);
            Vector3 hi = Vector3.Min(aMax, bMax);
            Vector3 delta = hi - lo;
            if (delta.x <= 0f || delta.y <= 0f || delta.z <= 0f) return 0f;
            return delta.x * delta.y * delta.z;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        private static void AssertColorsClose(Color actual, Color expected, string context)
        {
            // hex 量化步长 1/255≈0.0039，容差 0.005 覆盖
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.005f), context + " R 通道");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.005f), context + " G 通道");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.005f), context + " B 通道");
        }
    }
}
#endif
