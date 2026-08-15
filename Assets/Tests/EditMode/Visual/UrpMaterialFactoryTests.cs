#if UNITY_EDITOR
using MyWorld.Core.Entities;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    /// <summary>
    /// m5 A3：URP 材质工厂。修复人物/生物粉红——裸 CreatePrimitive 的
    /// Default-Material 是 Standard shader，URP 下渲染洋红。
    /// </summary>
    [TestFixture]
    public class UrpMaterialFactoryTests
    {
        [Test]
        public void CreateLit_ReturnsUsableMaterialWithColor()
        {
            Material mat = UrpMaterialFactory.CreateLit(new Color(0.23f, 0.43f, 0.65f));
            Assert.That(mat, Is.Not.Null);
            Assert.That(mat.shader, Is.Not.Null, "shader 为 null 就会渲染成粉红");
            Assert.That(mat.HasProperty("_BaseColor"), Is.True, "URP Lit 必须有 _BaseColor");
        }

        [Test]
        public void CreateLit_SameColor_ReturnsCachedInstance()
        {
            // 同色复用同一实例——10 个 cube 只有 4 个材质，不泄漏
            Material a = UrpMaterialFactory.CreateLit(Color.red);
            Material b = UrpMaterialFactory.CreateLit(Color.red);
            Assert.That(a, Is.SameAs(b), "同色必须复用缓存实例，防材质泄漏");
        }

        [TestCase(MobKind.Pig, "#E8A0A8")]
        [TestCase(MobKind.Cow, "#6B4A35")]
        [TestCase(MobKind.Chicken, "#F0EDE5")]
        [TestCase(MobKind.Zombie, "#5A8A4A")]
        [TestCase(MobKind.Villager, "#7A5C3E")]
        public void MobBodyColor_MatchesSpec(MobKind kind, string hex)
        {
            Color expected = Color.clear;
            ColorUtility.TryParseHtmlString(hex, out expected);
            Assert.That(ColorEquals(UrpMaterialFactory.MobBodyColor(kind), expected), Is.True,
                $"{kind} 的身体色必须按 spec 表");
        }

        [TestCase(MobKind.Pig, "#E8B8C0")]
        [TestCase(MobKind.Cow, "#F0E8E0")]
        [TestCase(MobKind.Chicken, "#D94F3D")]
        [TestCase(MobKind.Zombie, "#6FA05C")]
        [TestCase(MobKind.Villager, "#E8B88A")]
        public void MobHeadColor_MatchesSpec(MobKind kind, string hex)
        {
            Color expected = Color.clear;
            ColorUtility.TryParseHtmlString(hex, out expected);
            Assert.That(ColorEquals(UrpMaterialFactory.MobHeadColor(kind), expected), Is.True,
                $"{kind} 的头部色必须按 spec 表（牛头白花/鸡红冠/僵尸浅绿/村民肤色）");
        }

        // 分量差 < 0.002 视为相等（ColorUtility 解析浮点容差），
        // 用 Is.True(ColorEquals(actual, expected)) 替代 Is.EqualTo
        private static bool ColorEquals(Color a, Color b) =>
            System.Math.Abs(a.r - b.r) < 0.002f && System.Math.Abs(a.g - b.g) < 0.002f
            && System.Math.Abs(a.b - b.b) < 0.002f && System.Math.Abs(a.a - b.a) < 0.002f;
    }
}
#endif
