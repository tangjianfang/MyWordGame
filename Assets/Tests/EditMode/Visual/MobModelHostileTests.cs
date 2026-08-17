#if UNITY_EDITOR
// m11 W1-1（战斗）：三敌对生物（骷髅/蜘蛛/苦力怕）造型 JSON 的加载守卫。
// 模式照 MobModelLibraryTests（m11 I1）：可加载、kind 匹配、AABB 重叠 ≤ 半、
// 腿相位 0/π 成对、配色与 art/requests/entities/*.md 的调色板同源。
// UNITY_EDITOR 包裹确保 dotnet 链不参与（本文件依赖 MyWorld.Unity 程序集与 StreamingAssets）。
using MyWorld.Core.Entities;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class MobModelHostileTests
    {
        [TestCase(MobKind.Skeleton, 7)]
        [TestCase(MobKind.Spider, 12)]
        [TestCase(MobKind.Creeper, 9)]
        public void 三敌对json_可加载且部位数符合设计(MobKind kind, int expectedCount)
        {
            var parts = MobModelLibrary.Load(kind);
            Assert.That(parts, Is.Not.Null, kind + " 造型 JSON 应能加载");
            Assert.That(parts.Length, Is.EqualTo(expectedCount),
                kind + " 部位数应与设计一致（实际 " + parts.Length + "）");
        }

        [TestCase(MobKind.Skeleton)]
        [TestCase(MobKind.Spider)]
        [TestCase(MobKind.Creeper)]
        public void 三敌对json_门面与库同源(MobKind kind)
        {
            // MobModels.Build 门面必须委托 JSON 库（否则 MobView 走保底灰双部位表，模型白做）
            var viaFacade = MobModels.Build(kind);
            var viaLibrary = MobModelLibrary.Load(kind);
            Assert.That(viaFacade.Length, Is.EqualTo(viaLibrary.Length), "门面与库部位数一致");
            for (int i = 0; i < viaFacade.Length; i++)
            {
                Assert.That(viaFacade[i].Name, Is.EqualTo(viaLibrary[i].Name), "部位 " + i + " 名一致");
            }
        }

        [TestCase(MobKind.Skeleton)]
        [TestCase(MobKind.Spider)]
        [TestCase(MobKind.Creeper)]
        public void 三敌对json_部位AABB重叠不超过较小者一半(MobKind kind)
        {
            // 约束随五生物守卫一起执行（m11 I1 确立的建模铁律）
            var parts = MobModelLibrary.Load(kind);
            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    float overlap = OverlapVolume(parts[i], parts[j]);
                    float smaller = Mathf.Min(Volume(parts[i]), Volume(parts[j]));
                    Assert.That(overlap, Is.LessThanOrEqualTo(smaller * 0.5f + 0.0001f),
                        kind + " 部位 " + parts[i].Name + " 与 " + parts[j].Name + " 重叠超半");
                }
            }
        }

        [TestCase(MobKind.Skeleton, 2)]
        [TestCase(MobKind.Spider, 8)]
        [TestCase(MobKind.Creeper, 4)]
        public void 三敌对json_腿数符合设计且相位只有零或π(MobKind kind, int expectedLegs)
        {
            var parts = MobModelLibrary.Load(kind);
            int legs = 0;
            int zeroPhase = 0, piPhase = 0;
            foreach (var p in parts)
            {
                if (!p.IsLeg) continue;
                legs++;
                if (Mathf.Approximately(p.LegPhase, 0f)) zeroPhase++;
                else if (Mathf.Approximately(p.LegPhase, Mathf.PI)) piPhase++;
                else Assert.Fail(kind + " 腿 " + p.Name + " 相位应为 0 或 π，实际 " + p.LegPhase);
            }
            Assert.That(legs, Is.EqualTo(expectedLegs), kind + " 腿数应与设计一致");
            Assert.That(zeroPhase, Is.EqualTo(piPhase),
                kind + " 对角步态要求 0/π 相位成对（0 相 " + zeroPhase + " vs π 相 " + piPhase + "）");
        }

        [TestCase(MobKind.Skeleton)]
        [TestCase(MobKind.Spider)]
        [TestCase(MobKind.Creeper)]
        public void 三敌对json_脚底原点面朝Z_站高不超一点九(MobKind kind)
        {
            var parts = MobModelLibrary.Load(kind);
            float top = 0f;
            foreach (var p in parts)
            {
                top = Mathf.Max(top, p.LocalPosition.y + p.Size.y * 0.5f);
                Assert.That(p.LocalPosition.y - p.Size.y * 0.5f, Is.GreaterThanOrEqualTo(-0.001f),
                    kind + " 部位 " + p.Name + " 不得探到脚底原点以下（模型会半埋地下）");
            }
            Assert.That(top, Is.GreaterThan(0.3f), kind + " 站高应明显可见");
            Assert.That(top, Is.LessThanOrEqualTo(1.9f + 0.001f), kind + " 站高 ≤1.9（m8 建模铁律）");
        }

        [TestCase(MobKind.Skeleton, "#C8C8B8", "#EAEAD8", "#634C33")]
        [TestCase(MobKind.Spider, "#4A362A", "#2A1E16", "#C42222")]
        [TestCase(MobKind.Creeper, "#6ED66A", "#3A8A3A", "#1A1A1A")]
        public void 三敌对json_配色与美术需求同源(MobKind kind, string mainHex, string darkHex, string accentHex)
        {
            // art/requests/entities/{skeleton,spider,creeper}.md 的调色板逐色对照——
            // 部位表颜色与图标需求不同源会让世界内模型与图鉴两副面孔
            var parts = MobModelLibrary.Load(kind);
            AssertColorUsed(parts, mainHex, kind + " 应含主色 " + mainHex);
            AssertColorUsed(parts, darkHex, kind + " 应含暗色 " + darkHex);
            AssertColorUsed(parts, accentHex, kind + " 应含点缀色 " + accentHex);

            // 任何部位都不得是品红占位（色值非法时 Hex 返品红——模型色必须在需求调色板内）
            foreach (var p in parts)
            {
                Assert.That(IsMagenta(p.Color), Is.False,
                    kind + " 部位 " + p.Name + " 解析成了品红占位（color 字段非法？）");
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
