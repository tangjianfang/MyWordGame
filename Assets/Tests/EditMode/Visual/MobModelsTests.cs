#if UNITY_EDITOR
// m8 A1：MobModels 五生物部位表的纯数据断言（不建 GameObject，A2 才拼装）。
// m11 I1 迁移：五生物真值外置 Assets/StreamingAssets/mobs/models/*.json，
// MobModels.Build 退为门面委托 MobModelLibrary——本套断言全部经 Build 走 JSON 加载验证
// （旧 C# 常量表已删，断言本身一条不减，改守 JSON 真值；加载层另有 MobModelLibraryTests）。
// 坐标约定：脚底中心为原点、面朝 +Z，整体高 ≤1.9（spec §1）。
//
// 断言集（brief Step 1，m11 起新增 10/11 两条守卫）：
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
//   8) 体型序：鸡 ≈0.8 且 < 猪 < 牛（评审 I-1，鸡「最小」辨识点守卫）
//   9) 非 body/head 的 9 处附属部位色逐个比对 + Hex 非法字面量报错返品红（评审 I-2）
//  10) m11 I1 守卫：五生物 Build 与 MobModelLibrary 加载逐部位一致（门面与 JSON 真值不分叉）
//  11) m11 I1 守卫：腿部位数符合 spec 步态（猪/牛 4 腿、鸡/僵尸 2 腿、村民长袍 0 腿）
// UNITY_EDITOR 包裹确保 dotnet 链不参与（依赖 MyWorld.Unity 程序集与 StreamingAssets）。
using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        [Test]
        public void Build_Chicken_SmallestMobWithHeightAboutPointEight()
        {
            // spec §1 辨识点：鸡「最小+红冠」——站高 ≈0.8，且必须比猪（矮胖 ~0.85）还矮，
            // 否则「体型最小」的辨识点失效（评审 I-1）
            float chicken = HeightOf(MobKind.Chicken);
            Assert.That(chicken, Is.EqualTo(0.8f).Within(0.05f),
                "鸡站高应 ≈0.8（spec 体高 ~0.8，实际 " + chicken.ToString("F3") + "）");
            Assert.That(chicken, Is.LessThan(HeightOf(MobKind.Pig)),
                "鸡应是最小生物，站高须 < 猪（实际 鸡 " + chicken.ToString("F3") +
                " / 猪 " + HeightOf(MobKind.Pig).ToString("F3") + "）");
            Assert.That(HeightOf(MobKind.Pig), Is.LessThan(HeightOf(MobKind.Cow)),
                "体型序守卫：猪（矮胖 ~0.85）应 < 牛（高大 ~1.35）");
        }

        [Test]
        public void Build_NonBodyHeadColorsFollowSpecTable()
        {
            // 评审 I-2：9 处非 body/head 色值字面量逐个守卫（写错靠断言暴露，不靠肉眼）
            var chicken = MobModels.Build(MobKind.Chicken);
            AssertColorsClose(Find(chicken, "legL").Value.Color, Hex("#D9A03D"), "鸡腿（同鸡嘴黄）");
            AssertColorsClose(Find(chicken, "legR").Value.Color, Hex("#D9A03D"), "鸡腿（同鸡嘴黄）");

            var zombie = MobModels.Build(MobKind.Zombie);
            AssertColorsClose(Find(zombie, "armL").Value.Color, Hex("#6FA05C"), "僵尸臂（外露肤色，同 m5 头色）");
            AssertColorsClose(Find(zombie, "armR").Value.Color, Hex("#6FA05C"), "僵尸臂（外露肤色，同 m5 头色）");
            AssertColorsClose(Find(zombie, "legL").Value.Color, Hex("#5A8A4A"), "僵尸腿（同 m5 body 色）");
            AssertColorsClose(Find(zombie, "legR").Value.Color, Hex("#5A8A4A"), "僵尸腿（同 m5 body 色）");

            var villager = MobModels.Build(MobKind.Villager);
            AssertColorsClose(Find(villager, "armLower").Value.Color, Hex("#7A5C3E"), "村民抱胸臂（袍色）");
            AssertColorsClose(Find(villager, "armUpper").Value.Color, Hex("#7A5C3E"), "村民抱胸臂（袍色）");
            AssertColorsClose(Find(villager, "nose").Value.Color, Hex("#C8986A"), "村民长鼻（头肤色加深一档）");

            foreach (var legacy in new[] { MobKind.Passive, MobKind.Hostile, MobKind.Neutral })
            {
                var parts = MobModels.Build(legacy);
                AssertColorsClose(Find(parts, "body").Value.Color, Hex("#8A8A8A"), legacy + " 保底 body 中性灰");
                AssertColorsClose(Find(parts, "head").Value.Color, Hex("#9A9A9A"), legacy + " 保底 head 中性灰");
            }

            // 猪/牛四条腿 = 各自 body 色（m5 表），一并守卫
            AssertColorsClose(Find(MobModels.Build(MobKind.Pig), "legFL").Value.Color,
                UrpMaterialFactory.MobBodyColor(MobKind.Pig), "猪腿（body 色）");
            AssertColorsClose(Find(MobModels.Build(MobKind.Cow), "legFL").Value.Color,
                UrpMaterialFactory.MobBodyColor(MobKind.Cow), "牛腿（body 色）");
        }

        [Test]
        public void Hex_IllegalLiteral_LogsErrorAndReturnsMagenta()
        {
            // 评审 I-2：色值字面量写错不能静默透明（部位隐形看不见）——必须报错并返品红立刻暴露。
            // m11 I1：Hex 检查随 JSON 解析迁到 MobModelLibrary（五生物色值 + 旧三类保底色共用一条路径）
            var hex = typeof(MobModelLibrary).GetMethod("Hex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(hex, Is.Not.Null, "MobModelLibrary 应有私有的 Hex 封装");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("MobModelLibrary\\.Hex"));
            var color = (Color)hex.Invoke(null, new object[] { "#ZZZZZZ" });
            Assert.That(color, Is.EqualTo(Color.magenta), "非法色值应返品红（magenta）暴露问题");
            // 防分叉：MobModels 不再私留第二份 Hex（两份实现迟早漂移出两条容错路径）
            Assert.That(typeof(MobModels).GetMethod("Hex",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static),
                Is.Null, "Hex 检查应只在 MobModelLibrary 一处（MobModels 门面不自留副本）");
        }

        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        [TestCase(MobKind.Villager)]
        public void Build_FiveMobs_MatchesMobModelLibraryJsonSource(MobKind kind)
        {
            // m11 I1：真值唯一源是 mobs/models/*.json——Build（门面）与 Library（加载器）
            // 逐部位相等，防止门面里私留第二份表把「改 JSON 生效」变成空话
            var viaFacade = MobModels.Build(kind);
            var viaLibrary = MobModelLibrary.Load(kind);
            Assert.That(viaFacade.Length, Is.EqualTo(viaLibrary.Length),
                kind + " 门面与 Library 部位数应一致");
            for (int i = 0; i < viaLibrary.Length; i++)
            {
                Assert.That(viaFacade[i].Name, Is.EqualTo(viaLibrary[i].Name),
                    kind + " 部位 " + i + " 名应与 JSON 一致");
                Assert.That(viaFacade[i].Size, Is.EqualTo(viaLibrary[i].Size),
                    kind + " 部位 " + i + " 尺寸应与 JSON 一致");
                Assert.That(viaFacade[i].LocalPosition, Is.EqualTo(viaLibrary[i].LocalPosition),
                    kind + " 部位 " + i + " 位置应与 JSON 一致");
                Assert.That(viaFacade[i].Color, Is.EqualTo(viaLibrary[i].Color),
                    kind + " 部位 " + i + " 色应与 JSON 一致");
                Assert.That(viaFacade[i].IsLeg, Is.EqualTo(viaLibrary[i].IsLeg),
                    kind + " 部位 " + i + " 腿标记应与 JSON 一致");
                Assert.That(viaFacade[i].LegPhase, Is.EqualTo(viaLibrary[i].LegPhase),
                    kind + " 部位 " + i + " 相位应与 JSON 一致");
            }
        }

        [TestCase(MobKind.Pig, 4)]      // 四腿对角步态
        [TestCase(MobKind.Cow, 4)]      // 四腿对角步态
        [TestCase(MobKind.Chicken, 2)]  // 双腿交替步态
        [TestCase(MobKind.Zombie, 2)]   // 双腿交替步态（双臂 IsLeg=false，不参与摆动）
        [TestCase(MobKind.Villager, 0)] // 长袍到脚，无独立腿
        public void Build_FiveMobs_LegPartCountMatchesSpecGait(MobKind kind, int expectedLegs)
        {
            // m11 I1 守卫：JSON 表把臂误标 isLeg、或腿多写/漏写一条，步态配对全乱——按 spec 步态数腿
            int legs = 0;
            foreach (var part in MobModels.Build(kind))
            {
                if (part.IsLeg) legs++;
            }
            Assert.That(legs, Is.EqualTo(expectedLegs),
                kind + " 腿部位数应为 " + expectedLegs + "（实际 " + legs + "，检查 JSON 的 isLeg 字段）");
        }

        private static float HeightOf(MobKind kind)
        {
            float maxY = float.MinValue;
            foreach (var part in MobModels.Build(kind))
            {
                maxY = Mathf.Max(maxY, part.LocalPosition.y + part.Size.y * 0.5f);
            }
            return maxY;
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
