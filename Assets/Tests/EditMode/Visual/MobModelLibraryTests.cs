#if UNITY_EDITOR
// m11 I1（INFRA-A）：五生物部位表外置 Assets/StreamingAssets/mobs/models/*.json 后的加载器守卫。
// MobModelLibrary 负责 Newtonsoft.Json 解析 + streamingAssetsPath 定位 + 按 kind 缓存模板，
// MobModels.Build 退为门面委托本库（签名与「每调用返回新数组」语义不变，MobView 零改动）。
//
// 断言集（真值从 C# 常量表平移到 JSON，迁移守恒靠这里守）：
//   1) 加载pig_json_部位数与现表一致（卡片 Step 1 首测，迁移守恒的锚点）
//   2) 五生物 JSON 部位数 == spec §1 表逐部位求和（猪 7/牛 8/鸡 6/僵尸 6/村民 5）且 ≥5
//   3) 部位数值与旧 C# 常量表逐字一致（每生物抽 2 部位全字段比对：尺寸/位置/色/腿标记/相位）
//   4) 守卫：部位 AABB 重叠不超过较小者一半（真值外置后加载层再守一道）
//   5) 守卫：腿相位 0/π 成对（四腿对角 FL=BR、FR=BL；双腿交替反相）
//   6) 每次调用返回新数组（内容一致、实例不同——调用方可安全修改）
//   7) MobModels.Build 门面与 Library 同源（五生物逐部位相等，防门面分叉）
//   8) 解析失败抛异常带文件名（结构坏/kind 不匹配/parts 空/数组非三元/未知字段）
//   9) 色值非法不抛异常——报错返品红立刻暴露（沿用 m8 评审 I-2 的配方）
// UNITY_EDITOR 包裹确保 dotnet 链不参与（本文件依赖 MyWorld.Unity 程序集与 StreamingAssets）。
using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class MobModelLibraryTests
    {
        [Test]
        public void 加载pig_json_部位数与现表一致()
        {
            // 迁移锚点：pig.json 的部位数/名称/顺序必须与外置前的 C# 常量表一字不差
            var parts = MobModelLibrary.Load(MobKind.Pig);
            Assert.That(parts, Is.Not.Null, "pig.json 应能加载出部位表");
            Assert.That(parts.Length, Is.EqualTo(7),
                "pig.json 部位数应与旧 C# 表一致（body+head+snout+4 腿 = 7，实际 " + parts.Length + "）");
            Assert.That(parts.Length, Is.EqualTo(MobModels.Build(MobKind.Pig).Length),
                "pig.json 部位数应与 MobModels.Build(Pig)（门面已委托 JSON）一致");

            // 部位名与顺序逐一对齐旧表（A2 按名建 GameObject，顺序错位 = 拼装错位）
            string[] expectedNames = { "body", "head", "snout", "legFL", "legFR", "legBL", "legBR" };
            Assert.That(parts.Length, Is.EqualTo(expectedNames.Length), "pig.json 部位数与期望名表长度一致");
            for (int i = 0; i < expectedNames.Length; i++)
            {
                Assert.That(parts[i].Name, Is.EqualTo(expectedNames[i]),
                    "pig.json 第 " + i + " 个部位名应为 " + expectedNames[i] + "，实际 " + parts[i].Name);
            }
        }

        [TestCase(MobKind.Pig, 7)]
        [TestCase(MobKind.Cow, 8)]
        [TestCase(MobKind.Chicken, 6)]
        [TestCase(MobKind.Zombie, 6)]
        [TestCase(MobKind.Villager, 5)]
        public void 加载五生物json_部位数与spec表一致(MobKind kind, int expectedCount)
        {
            var parts = MobModelLibrary.Load(kind);
            Assert.That(parts, Is.Not.Null, kind + " 造型 JSON 应能加载");
            Assert.That(parts.Length, Is.EqualTo(expectedCount),
                kind + " JSON 部位数应与 spec §1 表逐部位求和一致（实际 " + parts.Length + "）");
            Assert.That(parts.Length, Is.GreaterThanOrEqualTo(5),
                kind + " 五生物部位数应 ≥5（spec §3）");
        }

        // 逐字一致抽样（每生物 2 部位，覆盖 body/辨识部位/腿/π 相位腿）——
        // 数值全部照抄外置前的 MobModels.Build 常量表，迁移是搬真值不是重设计
        [TestCase(MobKind.Pig, "body", 0.9f, 0.6f, 0.6f, 0f, 0.5f, 0f, "#E8A0A8", false, 0f)]
        [TestCase(MobKind.Pig, "snout", 0.24f, 0.18f, 0.1f, 0f, 0.52f, 0.85f, "#C87880", false, 0f)]
        [TestCase(MobKind.Cow, "head", 0.5f, 0.5f, 0.5f, 0f, 1.0f, 0.6f, "#F0E8E0", false, 0f)]
        [TestCase(MobKind.Cow, "hornL", 0.1f, 0.15f, 0.1f, 0.16f, 1.28f, 0.55f, "#D8D0C0", false, 0f)]
        [TestCase(MobKind.Chicken, "beak", 0.14f, 0.09f, 0.09f, 0f, 0.57f, 0.335f, "#D9A03D", false, 0f)]
        [TestCase(MobKind.Chicken, "comb", 0.08f, 0.09f, 0.18f, 0f, 0.745f, 0.16f, "#C03028", false, 0f)]
        [TestCase(MobKind.Zombie, "armL", 0.2f, 0.2f, 0.7f, 0.13f, 1.35f, 0.5f, "#6FA05C", false, 0f)]
        [TestCase(MobKind.Zombie, "legR", 0.2f, 0.75f, 0.2f, -0.13f, 0.375f, 0f, "#5A8A4A", true, Mathf.PI)]
        [TestCase(MobKind.Villager, "armUpper", 0.4f, 0.12f, 0.12f, 0f, 0.98f, 0.21f, "#7A5C3E", false, 0f)]
        [TestCase(MobKind.Villager, "nose", 0.1f, 0.16f, 0.12f, 0f, 1.36f, 0.26f, "#C8986A", false, 0f)]
        public void 加载_部位数值与旧常量表逐字一致(MobKind kind, string partName,
            float sx, float sy, float sz, float px, float py, float pz,
            string colorHex, bool isLeg, float legPhase)
        {
            var part = Find(MobModelLibrary.Load(kind), partName);
            Assert.That(part, Is.Not.Null, kind + " JSON 应含部位 " + partName);
            var p = part.Value;
            Assert.That(p.Size.x, Is.EqualTo(sx).Within(0.0001f), kind + "/" + partName + " 尺寸 X");
            Assert.That(p.Size.y, Is.EqualTo(sy).Within(0.0001f), kind + "/" + partName + " 尺寸 Y");
            Assert.That(p.Size.z, Is.EqualTo(sz).Within(0.0001f), kind + "/" + partName + " 尺寸 Z");
            Assert.That(p.LocalPosition.x, Is.EqualTo(px).Within(0.0001f), kind + "/" + partName + " 位置 X");
            Assert.That(p.LocalPosition.y, Is.EqualTo(py).Within(0.0001f), kind + "/" + partName + " 位置 Y");
            Assert.That(p.LocalPosition.z, Is.EqualTo(pz).Within(0.0001f), kind + "/" + partName + " 位置 Z");
            Assert.That(p.IsLeg, Is.EqualTo(isLeg), kind + "/" + partName + " 腿标记");
            Assert.That(Mathf.Approximately(p.LegPhase, legPhase), Is.True,
                kind + "/" + partName + " 腿相位应 " + legPhase + "，实际 " + p.LegPhase);
            ColorUtility.TryParseHtmlString(colorHex, out var expected);
            Assert.That(p.Color.r, Is.EqualTo(expected.r).Within(0.005f), kind + "/" + partName + " 色 R");
            Assert.That(p.Color.g, Is.EqualTo(expected.g).Within(0.005f), kind + "/" + partName + " 色 G");
            Assert.That(p.Color.b, Is.EqualTo(expected.b).Within(0.005f), kind + "/" + partName + " 色 B");
        }

        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        [TestCase(MobKind.Villager)]
        public void 加载_部位AABB重叠不超过较小者一半(MobKind kind)
        {
            // 新增守卫：约束随真值一起外置，改 JSON 撞出半体积重叠在这里被抓（不靠肉眼）
            var parts = MobModelLibrary.Load(kind);
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
        public void 加载_腿相位0或π成对(MobKind kind)
        {
            // 新增守卫：四腿对角步态（FL=BR 同相、FR=BL 同相、FL/FR 反相），双腿交替反相
            var parts = MobModelLibrary.Load(kind);
            var legs = new List<MobPart>();
            foreach (var part in parts)
            {
                if (part.IsLeg) legs.Add(part);
            }
            Assert.That(legs.Count, Is.GreaterThan(0), kind + " 应有腿部位（IsLeg）");

            foreach (var leg in legs)
            {
                bool isZero = Mathf.Approximately(leg.LegPhase, 0f);
                bool isPi = Mathf.Approximately(leg.LegPhase, Mathf.PI);
                Assert.That(isZero || isPi, Is.True,
                    kind + " 腿 " + leg.Name + " 的 LegPhase 应 ∈ {0, π}，实际 " + leg.LegPhase);
            }

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
                    kind + " legFL/legFR 应反相");
            }
            else if (legs.Count == 2)
            {
                var legL = Find(parts, "legL");
                var legR = Find(parts, "legR");
                Assert.That(legL.HasValue && legR.HasValue, Is.True, kind + " 双腿命名应为 legL/legR");
                Assert.That(!Mathf.Approximately(legL.Value.LegPhase, legR.Value.LegPhase), Is.True,
                    kind + " 双腿应反相（交替步态）");
            }
        }

        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        [TestCase(MobKind.Villager)]
        public void 加载_每次调用返回新数组_内容一致(MobKind kind)
        {
            // 语义守恒：旧 Build 每次返回新数组、调用方可安全改槽位——Library 必须同样给新数组
            var first = MobModelLibrary.Load(kind);
            var second = MobModelLibrary.Load(kind);
            Assert.That(second, Is.Not.SameAs(first), kind + " 两次加载应返回不同数组实例");
            Assert.That(second.Length, Is.EqualTo(first.Length), kind + " 两次加载部位数一致");
            for (int i = 0; i < first.Length; i++)
            {
                Assert.That(second[i].Name, Is.EqualTo(first[i].Name), kind + " 部位 " + i + " 名一致");
                Assert.That(second[i].Size, Is.EqualTo(first[i].Size), kind + " 部位 " + i + " 尺寸一致");
                Assert.That(second[i].LocalPosition, Is.EqualTo(first[i].LocalPosition), kind + " 部位 " + i + " 位置一致");
                Assert.That(second[i].IsLeg, Is.EqualTo(first[i].IsLeg), kind + " 部位 " + i + " 腿标记一致");
                Assert.That(second[i].LegPhase, Is.EqualTo(first[i].LegPhase), kind + " 部位 " + i + " 相位一致");
            }
        }

        [TestCase(MobKind.Pig)]
        [TestCase(MobKind.Cow)]
        [TestCase(MobKind.Chicken)]
        [TestCase(MobKind.Zombie)]
        [TestCase(MobKind.Villager)]
        public void Build_五生物门面与Library同源(MobKind kind)
        {
            // MobModels.Build 已是门面——逐部位相等防门面与加载器分叉出两份真值
            var viaFacade = MobModels.Build(kind);
            var viaLibrary = MobModelLibrary.Load(kind);
            Assert.That(viaFacade.Length, Is.EqualTo(viaLibrary.Length), kind + " 门面与 Library 部位数一致");
            for (int i = 0; i < viaLibrary.Length; i++)
            {
                Assert.That(viaFacade[i].Name, Is.EqualTo(viaLibrary[i].Name), kind + " 部位 " + i + " 名一致");
                Assert.That(viaFacade[i].Size, Is.EqualTo(viaLibrary[i].Size), kind + " 部位 " + i + " 尺寸一致");
                Assert.That(viaFacade[i].LocalPosition, Is.EqualTo(viaLibrary[i].LocalPosition), kind + " 部位 " + i + " 位置一致");
                Assert.That(viaFacade[i].IsLeg, Is.EqualTo(viaLibrary[i].IsLeg), kind + " 部位 " + i + " 腿标记一致");
                Assert.That(viaFacade[i].LegPhase, Is.EqualTo(viaLibrary[i].LegPhase), kind + " 部位 " + i + " 相位一致");
                Assert.That(viaFacade[i].Color, Is.EqualTo(viaLibrary[i].Color), kind + " 部位 " + i + " 色一致");
            }
        }

        [TestCase(MobKind.Pig, "pig.json")]
        [TestCase(MobKind.Cow, "cow.json")]
        [TestCase(MobKind.Chicken, "chicken.json")]
        [TestCase(MobKind.Zombie, "zombie.json")]
        [TestCase(MobKind.Villager, "villager.json")]
        public void 模型路径_位于mobs_models目录(MobKind kind, string fileName)
        {
            string path = MobModelLibrary.ModelPath(kind);
            Assert.That(path, Does.EndWith(fileName), kind + " 模型文件名应为 " + fileName);
            Assert.That(path, Does.Contain("mobs"), "模型应位于 StreamingAssets/mobs 下");
            Assert.That(path, Does.Contain("models"), "模型应位于 StreamingAssets/mobs/models 下");
        }

        [Test]
        public void 解析_json结构坏_抛异常带文件名()
        {
            Assert.That(() => MobModelLibrary.Parse("{ 不是 JSON", "models/pig.json", MobKind.Pig),
                Throws.TypeOf<System.IO.InvalidDataException>().With.Message.Contains("pig.json"));
        }

        [Test]
        public void 解析_kind字段不匹配_抛异常带文件名()
        {
            string json = "{ \"kind\": \"Cow\", \"parts\": [ { \"name\": \"body\", \"size\": [1, 0.7, 0.7], " +
                          "\"position\": [0, 0.85, 0], \"color\": \"#6B4A35\", \"isLeg\": false, \"legPhase\": 0 } ] }";
            Assert.That(() => MobModelLibrary.Parse(json, "models/pig.json", MobKind.Pig),
                Throws.TypeOf<System.IO.InvalidDataException>().With.Message.Contains("pig.json"));
        }

        [Test]
        public void 解析_parts为空_抛异常带文件名()
        {
            Assert.That(() => MobModelLibrary.Parse("{ \"kind\": \"Pig\", \"parts\": [] }", "models/pig.json", MobKind.Pig),
                Throws.TypeOf<System.IO.InvalidDataException>().With.Message.Contains("pig.json"));
        }

        [Test]
        public void 解析_size数组不是三元_抛异常带文件名()
        {
            string json = "{ \"kind\": \"Pig\", \"parts\": [ { \"name\": \"body\", \"size\": [0.9, 0.6], " +
                          "\"position\": [0, 0.5, 0], \"color\": \"#E8A0A8\", \"isLeg\": false, \"legPhase\": 0 } ] }";
            Assert.That(() => MobModelLibrary.Parse(json, "models/pig.json", MobKind.Pig),
                Throws.TypeOf<System.IO.InvalidDataException>().With.Message.Contains("pig.json"));
        }

        [Test]
        public void 解析_未知字段_抛异常带文件名()
        {
            // 键名拼错不能静默取默认值（legPhase 拼错会全表变成 0 相位、步态瘫掉）——
            // MissingMemberHandling.Error 把拼错当解析失败拦下
            string json = "{ \"kind\": \"Pig\", \"parts\": [ { \"name\": \"body\", \"size\": [0.9, 0.6, 0.6], " +
                          "\"position\": [0, 0.5, 0], \"color\": \"#E8A0A8\", \"isLeg\": false, \"phase\": 0 } ] }";
            Assert.That(() => MobModelLibrary.Parse(json, "models/pig.json", MobKind.Pig),
                Throws.TypeOf<System.IO.InvalidDataException>().With.Message.Contains("pig.json"));
        }

        [Test]
        public void 解析_色值非法_报错返品红不抛异常()
        {
            // m8 评审 I-2 配方沿用：JSON 色值写错不能静默透明（部位整个隐形）——
            // 报错 + 返品红，进游戏第一眼就暴露；同时不能抛异常炸掉整只生物
            string json = "{ \"kind\": \"Pig\", \"parts\": [ { \"name\": \"body\", \"size\": [0.9, 0.6, 0.6], " +
                          "\"position\": [0, 0.5, 0], \"color\": \"#ZZZZZZ\", \"isLeg\": false, \"legPhase\": 0 } ] }";
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("MobModelLibrary\\.Hex"));
            var parts = MobModelLibrary.Parse(json, "models/pig.json", MobKind.Pig);
            Assert.That(parts.Length, Is.EqualTo(1), "色值非法不应中断解析（返品红占位）");
            Assert.That(parts[0].Color, Is.EqualTo(Color.magenta), "非法色值应返品红（magenta）暴露问题");
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
    }
}
#endif
