#if UNITY_EDITOR
// m11 W3-3：机元守卫 Boss 造型 JSON（mobs/models/machine_guardian.json）的加载守卫。
// 模式照 MobModelHostileTests：可加载、门面同源、AABB 重叠 ≤ 半、腿相位 0/π 成对、
// 配色与 art/requests/entities/machine-guardian.md 调色板同源。
// 站高是唯一的例外断言：Boss 按卡片 2.5 格（普通生物铁律 ≤1.9 不适用于 Boss）。
using MyWorld.Core.Entities;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class MobModelBossTests
    {
        private const MobKind Kind = MobKind.MachineGuardian;

        [Test]
        public void Bossjson_可加载且部位数符合设计()
        {
            var parts = MobModelLibrary.Load(Kind);
            Assert.That(parts, Is.Not.Null, "Boss 造型 JSON 应能加载");
            Assert.That(parts.Length, Is.EqualTo(13),
                "部位数应与设计一致：双腿 + 腰带 + 躯干 + 胸甲 + 双肩 + 双臂 + 头 + 眼缝 + 眼核 + 冠饰（实际 " + parts.Length + "）");
        }

        [Test]
        public void Bossjson_门面与库同源()
        {
            // MobModels.Build 门面必须委托 JSON 库（否则 MobView 走保底灰双部位表，Boss 召唤出来是灰方块）
            var viaFacade = MobModels.Build(Kind);
            var viaLibrary = MobModelLibrary.Load(Kind);
            Assert.That(viaFacade.Length, Is.EqualTo(viaLibrary.Length), "门面与库部位数一致");
            for (int i = 0; i < viaFacade.Length; i++)
            {
                Assert.That(viaFacade[i].Name, Is.EqualTo(viaLibrary[i].Name), "部位 " + i + " 名一致");
            }
        }

        [Test]
        public void Bossjson_部位AABB重叠不超过较小者一半()
        {
            // 建模铁律对 Boss 同样成立（m11 I1 确立，与五生物/三敌对守卫同一条）
            var parts = MobModelLibrary.Load(Kind);
            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    float overlap = OverlapVolume(parts[i], parts[j]);
                    float smaller = Mathf.Min(Volume(parts[i]), Volume(parts[j]));
                    Assert.That(overlap, Is.LessThanOrEqualTo(smaller * 0.5f + 0.0001f),
                        "部位 " + parts[i].Name + " 与 " + parts[j].Name + " 重叠超半");
                }
            }
        }

        [Test]
        public void Bossjson_双腿对角步态_相位只有零或π且成对()
        {
            var parts = MobModelLibrary.Load(Kind);
            int legs = 0;
            int zeroPhase = 0, piPhase = 0;
            foreach (var p in parts)
            {
                if (!p.IsLeg) continue;
                legs++;
                if (Mathf.Approximately(p.LegPhase, 0f)) zeroPhase++;
                else if (Mathf.Approximately(p.LegPhase, Mathf.PI)) piPhase++;
                else Assert.Fail("腿 " + p.Name + " 相位应为 0 或 π，实际 " + p.LegPhase);
            }
            Assert.That(legs, Is.EqualTo(2), "Boss 双腿（对角步态 0/π 各一）");
            Assert.That(zeroPhase, Is.EqualTo(piPhase), "0/π 相位成对（各一条）");
        }

        [Test]
        public void Bossjson_脚底原点_站高按卡片约二点五格()
        {
            var parts = MobModelLibrary.Load(Kind);
            float top = 0f;
            foreach (var p in parts)
            {
                top = Mathf.Max(top, p.LocalPosition.y + p.Size.y * 0.5f);
                Assert.That(p.LocalPosition.y - p.Size.y * 0.5f, Is.GreaterThanOrEqualTo(-0.001f),
                    "部位 " + p.Name + " 不得探到脚底原点以下（模型会半埋地下）");
            }
            // Boss 特例：普通生物铁律 ≤1.9 不适用——卡片明定 2.5 格的紫金机甲巨人
            Assert.That(top, Is.InRange(2.4f, 2.6f),
                "Boss 站高应约 2.5 格（卡片数值；普通生物的 ≤1.9 铁律对 Boss 例外）");
        }

        [Test]
        public void Bossjson_配色与美术需求同源_六色全用且无品红占位()
        {
            // art/requests/entities/machine-guardian.md 的调色板逐色对照——
            // 部位表颜色与 Boss 图标需求不同源会让世界内模型与图鉴两副面孔
            var parts = MobModelLibrary.Load(Kind);
            AssertColorUsed(parts, "#6A4A9C", "应含紫甲主 #6A4A9C");
            AssertColorUsed(parts, "#463068", "应含紫甲暗 #463068");
            AssertColorUsed(parts, "#DCAE3A", "应含金饰主 #DCAE3A");
            AssertColorUsed(parts, "#A87322", "应含金饰暗 #A87322");
            AssertColorUsed(parts, "#4CC6C4", "应含眼蓝光 #4CC6C4");
            AssertColorUsed(parts, "#A8F2EF", "应含眼核亮 #A8F2EF");

            // 任何部位都不得是品红占位（色值非法时 Hex 返品红——模型色必须在需求调色板内）
            foreach (var p in parts)
            {
                Assert.That(IsMagenta(p.Color), Is.False,
                    "部位 " + p.Name + " 解析成了品红占位（color 字段非法？）");
            }
        }

        private static void AssertColorUsed(MobPart[] parts, string hex, string because)
        {
            ColorUtility.TryParseHtmlString(hex, out var expected);
            foreach (var p in parts)
            {
                if (Mathf.Approximately(p.Color.r, expected.r)
                    && Mathf.Approximately(p.Color.g, expected.g)
                    && Mathf.Approximately(p.Color.b, expected.b))
                {
                    return;
                }
            }
            Assert.Fail(because + "，但部位表里一处都没用到");
        }

        private static bool IsMagenta(Color c) =>
            c.r > 0.95f && Mathf.Approximately(c.g, 0f) && c.b > 0.95f;

        private static float Volume(MobPart p) => p.Size.x * p.Size.y * p.Size.z;

        private static float OverlapVolume(MobPart a, MobPart b)
        {
            float dx = Mathf.Min(a.LocalPosition.x + a.Size.x * 0.5f, b.LocalPosition.x + b.Size.x * 0.5f)
                     - Mathf.Max(a.LocalPosition.x - a.Size.x * 0.5f, b.LocalPosition.x - b.Size.x * 0.5f);
            float dy = Mathf.Min(a.LocalPosition.y + a.Size.y * 0.5f, b.LocalPosition.y + b.Size.y * 0.5f)
                     - Mathf.Max(a.LocalPosition.y - a.Size.y * 0.5f, b.LocalPosition.y - b.Size.y * 0.5f);
            float dz = Mathf.Min(a.LocalPosition.z + a.Size.z * 0.5f, b.LocalPosition.z + b.Size.z * 0.5f)
                     - Mathf.Max(a.LocalPosition.z - a.Size.z * 0.5f, b.LocalPosition.z - b.Size.z * 0.5f);
            if (dx <= 0f || dy <= 0f || dz <= 0f) return 0f;
            return dx * dy * dz;
        }
    }
}
#endif
