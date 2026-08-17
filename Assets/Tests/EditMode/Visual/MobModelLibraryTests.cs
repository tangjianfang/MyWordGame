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
//  10) m11 W1-2 九被动生物（Sheep…Hamster）JSON 可加载：部位数/部位名不撞/贴地/整体高与设计一致
//  11) W1-2 九被动生物 AABB 重叠守卫 + 腿相位守卫（腿数照真实动物：企鹅 2 腿、四足 4 腿）
//  12) W1-2 九被动生物配色与 art/requests/entities/*.md 图标需求调色板一字不差（色源同源）
// UNITY_EDITOR 包裹确保 dotnet 链不参与（本文件依赖 MyWorld.Unity 程序集与 StreamingAssets）。
using System.Collections.Generic;
using System.IO;
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

        // ---------- m11 W1-2：九被动生物造型 JSON（纯数据增量）守卫 ----------
        // P0（m11 第 1 波预接线）已把 Sheep=15…Hamster=23 接进枚举/猪行为组/MobManager
        // 候选表，但 MobModelLibrary.FileNameOf 尚未含九 kind（集成点②统一接线，届时
        // Load 会抛 ArgumentException）。这里按 Library 自己的落盘约定（ModelPath 注释：
        // mobs/models/<kind 小写>.json）直读文件走同一条 Parse 检查路径——kind 自校验/
        // 结构/三元数组/未知字段防线全部照常生效，②接线后本节无需改动。
        // 配色色源：art/requests/entities/<kind 小写>.md 的「## 调色板」（模型与图标同源，
        // 由下方 NineKindPalettes 逐字照抄守卫）。

        /// <summary>W1-2 落地的九被动生物 kind。</summary>
        private static readonly MobKind[] NinePassiveKinds =
        {
            MobKind.Sheep, MobKind.Rabbit, MobKind.Fox, MobKind.Deer, MobKind.Panda,
            MobKind.Penguin, MobKind.Goat, MobKind.Raccoon, MobKind.Hamster,
        };

        /// <summary>九被动生物模型配色白名单（逐字照抄 art/requests/entities/*.md 的「## 调色板」）。</summary>
        private static readonly Dictionary<MobKind, string[]> NineKindPalettes =
            new Dictionary<MobKind, string[]>
        {
            { MobKind.Sheep,   new[] { "#F2F2F2", "#C8C8C8", "#D2A48A", "#1A1A1A" } },
            { MobKind.Rabbit,  new[] { "#C8A888", "#A08060", "#E8B8B0", "#F2F2F2", "#1A1A1A" } },
            { MobKind.Fox,     new[] { "#E07B28", "#B85A18", "#F2E8DC", "#2A2A2A", "#1A1A1A" } },
            { MobKind.Deer,    new[] { "#A5754A", "#7A5433", "#F2E8D8", "#C8A878", "#1A1A1A" } },
            { MobKind.Panda,   new[] { "#F2F2F2", "#C8C8C8", "#2A2A2A", "#4A4A4A", "#0F0F0F" } },
            { MobKind.Penguin, new[] { "#2A2A32", "#1A1A22", "#F2F2F2", "#E8912E", "#F2B03A" } },
            { MobKind.Goat,    new[] { "#DCD6CC", "#B0AAA0", "#8A8378", "#D8B0A8", "#1A1A1A" } },
            { MobKind.Raccoon, new[] { "#8A8580", "#5A5550", "#262626", "#E8E4DC", "#1A1A1A" } },
            { MobKind.Hamster, new[] { "#E8B878", "#C89050", "#F2E0C8", "#E8A898", "#1A1A1A" } },
        };

        /// <summary>
        /// 直读九被动生物的模型 JSON 并走 Library 的 Parse（FileNameOf 未接线期间的加载入口，
        /// 理由见本节块注释）。文件缺失时断言失败（W1-2 交付物即这 9 份文件，缺了必须红）。
        /// </summary>
        private static MobPart[] LoadNineKindModel(MobKind kind)
        {
            string path = Path.Combine(Application.streamingAssetsPath, "mobs", "models",
                kind.ToString().ToLowerInvariant() + ".json");
            Assert.That(File.Exists(path), Is.True,
                kind + " 造型 JSON 应存在于 " + path + "（W1-2 九被动生物模型交付物）");
            return MobModelLibrary.Parse(File.ReadAllText(path), path, kind);
        }

        [TestCase(MobKind.Sheep, 8, 1.2f)]
        [TestCase(MobKind.Rabbit, 9, 0.495f)]
        [TestCase(MobKind.Fox, 11, 0.77f)]
        [TestCase(MobKind.Deer, 11, 1.89f)]
        [TestCase(MobKind.Panda, 10, 1.395f)]
        [TestCase(MobKind.Penguin, 9, 0.79f)]
        [TestCase(MobKind.Goat, 11, 1.255f)]
        [TestCase(MobKind.Raccoon, 14, 0.635f)]
        [TestCase(MobKind.Hamster, 9, 0.315f)]
        public void 加载九被动生物json_部位数与整体高与设计一致(
            MobKind kind, int expectedCount, float expectedHeight)
        {
            var parts = LoadNineKindModel(kind);
            Assert.That(parts, Is.Not.Null, kind + " 造型 JSON 应能加载（kind/结构/字段全走 Parse 防线）");
            Assert.That(parts.Length, Is.EqualTo(expectedCount),
                kind + " JSON 部位数与设计一致（实际 " + parts.Length + "）");
            Assert.That(parts.Length, Is.GreaterThanOrEqualTo(5),
                kind + " 部位数应 ≥5（与五生物 spec §3 同约束）");

            // 部位名不撞（MobAssembly 按名建 GameObject）；最低部位贴地（底面 ≥0）；
            // 整体高与设计一致且 ≤1.9（MobModels 坐标约定的硬上限）
            var seen = new HashSet<string>();
            float bottom = float.MaxValue;
            float top = float.MinValue;
            foreach (var p in parts)
            {
                Assert.That(seen.Add(p.Name), Is.True, kind + " 部位名撞名: " + p.Name);
                bottom = Mathf.Min(bottom, p.LocalPosition.y - p.Size.y * 0.5f);
                top = Mathf.Max(top, p.LocalPosition.y + p.Size.y * 0.5f);
            }
            Assert.That(bottom, Is.GreaterThanOrEqualTo(-0.0001f),
                kind + " 部位不得低于地面（脚底中心为原点约定），最低底面 " + bottom.ToString("F4"));
            Assert.That(top, Is.EqualTo(expectedHeight).Within(0.001f),
                kind + " 整体高与设计一致（实际 " + top.ToString("F4") + "）");
            Assert.That(top, Is.LessThanOrEqualTo(1.9f), kind + " 整体高 ≤1.9（MobModels 坐标约定）");
        }

        [TestCase(MobKind.Sheep)]
        [TestCase(MobKind.Rabbit)]
        [TestCase(MobKind.Fox)]
        [TestCase(MobKind.Deer)]
        [TestCase(MobKind.Panda)]
        [TestCase(MobKind.Penguin)]
        [TestCase(MobKind.Goat)]
        [TestCase(MobKind.Raccoon)]
        [TestCase(MobKind.Hamster)]
        public void 加载_九被动生物部位AABB重叠不超过较小者一半(MobKind kind)
        {
            // 与五生物同一道加载层守卫：改 JSON 撞出半体积重叠在这里被抓（不靠肉眼）
            var parts = LoadNineKindModel(kind);
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

        [TestCase(MobKind.Sheep, 4)]
        [TestCase(MobKind.Rabbit, 4)]
        [TestCase(MobKind.Fox, 4)]
        [TestCase(MobKind.Deer, 4)]
        [TestCase(MobKind.Panda, 4)]
        [TestCase(MobKind.Penguin, 2)]
        [TestCase(MobKind.Goat, 4)]
        [TestCase(MobKind.Raccoon, 4)]
        [TestCase(MobKind.Hamster, 4)]
        public void 加载_九被动生物腿相位0或π成对(MobKind kind, int expectedLegs)
        {
            // 腿数照真实动物：鸟型企鹅 2 腿（legL/legR 交替），四足 4 腿（对角步态）
            var parts = LoadNineKindModel(kind);
            var legs = new List<MobPart>();
            foreach (var part in parts)
            {
                if (part.IsLeg) legs.Add(part);
            }
            Assert.That(legs.Count, Is.EqualTo(expectedLegs),
                kind + " 腿数应照真实动物（实际 " + legs.Count + "）");

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
            else
            {
                var legL = Find(parts, "legL");
                var legR = Find(parts, "legR");
                Assert.That(legL.HasValue && legR.HasValue, Is.True, kind + " 双腿命名应为 legL/legR");
                Assert.That(!Mathf.Approximately(legL.Value.LegPhase, legR.Value.LegPhase), Is.True,
                    kind + " 双腿应反相（交替步态）");
            }
        }

        [Test]
        public void 加载_九被动生物配色与图标需求调色板同源()
        {
            // 色源同源守卫：模型部位色必须逐字取自 art/requests/entities/<kind 小写>.md
            // 的「## 调色板」（NineKindPalettes 为其照抄）——模型与图标两套配色不得漂移
            foreach (var pair in NineKindPalettes)
            {
                var allowed = new List<Color>();
                foreach (var hex in pair.Value)
                {
                    Assert.That(ColorUtility.TryParseHtmlString(hex, out var c), Is.True,
                        pair.Key + " 白名单色值非法: " + hex);
                    allowed.Add(c);
                }
                foreach (var p in LoadNineKindModel(pair.Key))
                {
                    bool matched = false;
                    foreach (var c in allowed)
                    {
                        if (Mathf.Abs(p.Color.r - c.r) < 0.0001f &&
                            Mathf.Abs(p.Color.g - c.g) < 0.0001f &&
                            Mathf.Abs(p.Color.b - c.b) < 0.0001f)
                        {
                            matched = true;
                            break;
                        }
                    }
                    Assert.That(matched, Is.True,
                        pair.Key + " 部位 " + p.Name + " 的色值必须在 art/requests/entities/" +
                        pair.Key.ToString().ToLowerInvariant() + ".md 调色板内（模型与图标配色同源）");
                }
            }
            Assert.That(NineKindPalettes.Count, Is.EqualTo(NinePassiveKinds.Length),
                "配色白名单应覆盖全部九被动生物");
        }

        [Test]
        public void 集成点二接线_九被动走Load与Build门面_与直读JSON同源()
        {
            // m11 集成点②：FileNameOf 补了 9 被动映射、MobModels.Build 门面同步转调——
            // 此前测试只能绕过 Load 直读文件（LoadNineKindModel），接线后主链路
            // （MobManager 刷怪 → MobView.Setup → MobModels.Build）全走 Load。
            // 本测守「门面 == 直读 JSON」逐部位一致，防映射/缓存层分叉。
            foreach (MobKind kind in NinePassiveKinds)
            {
                MobPart[] viaLoad = MobModelLibrary.Load(kind);
                MobPart[] viaBuild = MobModels.Build(kind);
                MobPart[] viaFile = LoadNineKindModel(kind);

                Assert.That(viaLoad.Length, Is.EqualTo(viaFile.Length),
                    kind + " 经 FileNameOf 定位加载的部位数应与直读 JSON 一致");
                for (int i = 0; i < viaFile.Length; i++)
                {
                    Assert.That(viaLoad[i].Name, Is.EqualTo(viaFile[i].Name),
                        kind + " 第 " + i + " 部位名经 Load 与直读应一致");
                    Assert.That(viaBuild[i].Color.r, Is.EqualTo(viaFile[i].Color.r).Within(0.0001f),
                        kind + " Build 门面与直读 JSON 同源（色 R）");
                }
            }
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
