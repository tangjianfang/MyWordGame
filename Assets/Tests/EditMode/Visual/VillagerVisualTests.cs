#if UNITY_EDITOR
// m8 终审修（I-1）：交易村民（VillagerManager → VillagerView）统一走 MobAssembly 部位表
// 拼装的长袍村民，消灭与 MobView 路径并存的「单 cube 双形态」。本文件守住两条契约：
//   1) 拼装结构：VillagerView.Attach 后子物体数 == MobModels.Build(Villager).Length（5 部位
//      长袍村民），host cube Renderer 禁用（部位表全权负责视觉）
//   2) 职业辨识约定：袍部位（body/armLower/armUpper）染职业色，头/鼻保持部位表肤色
//      （#E8B88A / #C8986A）——穿什么看职业，脸永远是同一张村民脸
// 整个文件用 #if UNITY_EDITOR 包裹：依赖 MyWorld.Unity.Combat.VillagerView，dotnet 链跑不动。
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class VillagerVisualTests
    {
        // 长袍村民部位表里的袍部位（染职业色），头/鼻是肤色部位（不染）——约定写死在测试里守护
        private static readonly string[] RobeParts = { "body", "armLower", "armUpper" };

        private static GameObject CreateVillagerHost(VillagerProfession profession)
        {
            // 按 VillagerManager.TrySpawnOne 的方式建 host（cube + Attach），
            // 保证测试走的就是生产路径的拼装入口
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            host.name = "VillagerViewTestHost";
            VillagerView.Attach(host, Villager.Create(1, profession, new Float3(0f, 70f, 0f)));
            return host;
        }

        /// <summary>
        /// Attach 应按部位表拼装长袍村民：子物体数 == MobModels.Build(Villager).Length，
        /// 逐部位按名可寻（body/head/armLower/armUpper/nose），host cube Renderer 禁用。
        /// </summary>
        [Test]
        public void Attach_AssemblesLongRobeVillagerFromPartTable()
        {
            var host = CreateVillagerHost(VillagerProfession.Farmer);
            try
            {
                var parts = MobModels.Build(MobKind.Villager);
                Assert.That(host.transform.childCount, Is.EqualTo(parts.Length),
                    "交易村民子物体数应等于部位表部位数 " + parts.Length + "（长袍村民拼装，不再是单 cube）");
                foreach (var part in parts)
                {
                    Assert.That(host.transform.Find(part.Name), Is.Not.Null,
                        "部位 " + part.Name + " 应有同名直接子物体");
                }
                Assert.That(host.GetComponent<Renderer>().enabled, Is.False,
                    "host cube Renderer 应禁用（部位表全权负责视觉，消灭重合渲染）");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 职业辨识约定：袍部位（body/armLower/armUpper）染职业色（期望值写死在 TestCase
        /// 数据里，不引用生产代码），头/鼻保持部位表肤色。三个职业 + None 全枚举。
        /// </summary>
        [TestCase(VillagerProfession.Farmer, 0.55f, 0.40f, 0.20f)]
        [TestCase(VillagerProfession.Librarian, 0.45f, 0.35f, 0.50f)]
        [TestCase(VillagerProfession.Blacksmith, 0.30f, 0.30f, 0.35f)]
        [TestCase(VillagerProfession.None, 0.60f, 0.50f, 0.40f)]
        public void Attach_RobePartsGetProfessionColor_HeadNoseKeepSkin(
            VillagerProfession profession, float r, float g, float b)
        {
            var host = CreateVillagerHost(profession);
            try
            {
                var expected = new Color(r, g, b);
                foreach (var part in RobeParts)
                {
                    AssertColorNear(ReadPartColor(host, part), expected,
                        profession + " 的袍部位 " + part + " 应染职业色");
                }
                ColorUtility.TryParseHtmlString("#E8B88A", out var headSkin);
                ColorUtility.TryParseHtmlString("#C8986A", out var noseSkin);
                AssertColorNear(ReadPartColor(host, "head"), headSkin,
                    "head 应保持部位表肤色（职业只染袍，不染脸）");
                AssertColorNear(ReadPartColor(host, "nose"), noseSkin,
                    "nose 应保持部位表深肤色的部位表色值");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 三个职业的袍色应两两不同（Farmer 棕 / Librarian 紫 / Blacksmith 深灰）——
        /// 职业辨识信号必须真的可辨识。
        /// </summary>
        [Test]
        public void Attach_ProfessionsHaveDistinctRobeColors()
        {
            var colors = new System.Collections.Generic.List<Color>();
            try
            {
                foreach (var profession in new[]
                {
                    VillagerProfession.Farmer, VillagerProfession.Librarian, VillagerProfession.Blacksmith,
                })
                {
                    var host = CreateVillagerHost(profession);
                    colors.Add(ReadPartColor(host, "body"));
                }
            }
            finally
            {
                foreach (var go in Object.FindObjectsOfType<GameObject>())
                {
                    if (go.name == "VillagerViewTestHost") Object.DestroyImmediate(go);
                }
            }
            for (int i = 0; i < colors.Count; i++)
            {
                for (int j = i + 1; j < colors.Count; j++)
                {
                    Assert.That(colors[i], Is.Not.EqualTo(colors[j]),
                        "职业 " + i + " 与 " + j + " 的袍色应不同（否则职业不可辨识）");
                }
            }
        }

        /// <summary>受伤红闪涂满全部部位（对齐 MobView 语义），包括头和鼻。</summary>
        [Test]
        public void ApplyColor_PaintsAllParts()
        {
            var host = CreateVillagerHost(VillagerProfession.Farmer);
            try
            {
                var view = host.GetComponent<VillagerView>();
                Assert.That(view, Is.Not.Null, "Attach 后 host 上应有 VillagerView 组件");
                view.ApplyColor(Color.red);
                foreach (Transform child in host.transform)
                {
                    AssertColorNear(ReadPartColor(host, child.name), Color.red,
                        "红闪应涂满部位 " + child.name);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static Color ReadPartColor(GameObject host, string partName)
        {
            var part = host.transform.Find(partName);
            Assert.That(part, Is.Not.Null, "应找到部位 " + partName);
            var renderer = part.GetComponent<Renderer>();
            Assert.That(renderer, Is.Not.Null, "部位 " + partName + " 应有 Renderer");
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        /// <summary>NUnit 的 Within 不对 Color 逐通道生效，按通道断（容差 0.001）。</summary>
        private static void AssertColorNear(Color actual, Color expected, string context)
        {
            Assert.That(Mathf.Abs(actual.r - expected.r), Is.LessThan(0.001f),
                context + "（红色分量，期望 " + expected.r.ToString("F3") + " 实际 " + actual.r.ToString("F3") + "）");
            Assert.That(Mathf.Abs(actual.g - expected.g), Is.LessThan(0.001f),
                context + "（绿色分量，期望 " + expected.g.ToString("F3") + " 实际 " + actual.g.ToString("F3") + "）");
            Assert.That(Mathf.Abs(actual.b - expected.b), Is.LessThan(0.001f),
                context + "（蓝色分量，期望 " + expected.b.ToString("F3") + " 实际 " + actual.b.ToString("F3") + "）");
        }
    }
}
#endif
