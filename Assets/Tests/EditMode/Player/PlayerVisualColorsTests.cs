#if UNITY_EDITOR
// 本 fixture 验证 PlayerVisual 双段色四肢重构：每条 limb 拆 Upper + Lower 两段，
// 共 10 个 cube（Head + Torso + 4 limbs × 2）。与拆分前的 6 cube 形成对比。
// 整套写法参照同目录 PlayerVisualTests：用 AddComponent 触发 Awake（EditMode 下
// AddComponent 不会自动跑生命周期）、反射/transform.Find 拿到私有 / 命名子物体。
using System.Reflection;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class PlayerVisualColorsTests
    {
        [Test]
        public void PlayerVisual_HasTenChildCubes()
        {
            var go = new GameObject("PlayerVisualColorsTestHost");
            try
            {
                var visual = go.AddComponent<PlayerVisual>();
                // EditMode 下 AddComponent 不触发 Awake，手动调一次（与 PlayerVisualTests 同理）
                InvokeAwake(visual);
                int count = 0;
                foreach (Transform child in go.transform)
                    count++;
                Assert.That(count, Is.EqualTo(10),
                    "应创建 10 个 cube（Head/Torso + 4 limbs × 2 段）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayerVisual_ArmsHaveJacketAndSkinColors()
        {
            var go = new GameObject("PlayerVisualColorsTestHost");
            try
            {
                var visual = go.AddComponent<PlayerVisual>();
                InvokeAwake(visual);
                var armUpper = go.transform.Find("ArmL_Upper");
                Assert.That(armUpper, Is.Not.Null, "上臂 ArmL_Upper 不应为空");
                var armLower = go.transform.Find("ArmL_Lower");
                Assert.That(armLower, Is.Not.Null, "下臂 ArmL_Lower 不应为空");
                var armUpperR = go.transform.Find("ArmR_Upper");
                Assert.That(armUpperR, Is.Not.Null, "上臂 ArmR_Upper 不应为空");
                var armLowerR = go.transform.Find("ArmR_Lower");
                Assert.That(armLowerR, Is.Not.Null, "下臂 ArmR_Lower 不应为空");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PlayerVisual_LegsHavePantsAndBootsColors()
        {
            var go = new GameObject("PlayerVisualColorsTestHost");
            try
            {
                var visual = go.AddComponent<PlayerVisual>();
                InvokeAwake(visual);
                var legUpper = go.transform.Find("LegL_Upper");
                Assert.That(legUpper, Is.Not.Null, "大腿 LegL_Upper 不应为空");
                var legLower = go.transform.Find("LegL_Lower");
                Assert.That(legLower, Is.Not.Null, "小腿 LegL_Lower 不应为空");
                var legUpperR = go.transform.Find("LegR_Upper");
                Assert.That(legUpperR, Is.Not.Null, "大腿 LegR_Upper 不应为空");
                var legLowerR = go.transform.Find("LegR_Lower");
                Assert.That(legLowerR, Is.Not.Null, "小腿 LegR_Lower 不应为空");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void InvokeAwake(PlayerVisual visual)
        {
            var awake = typeof(PlayerVisual).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null, "PlayerVisual 没有 Awake 方法");
            awake.Invoke(visual, null);
        }
    }
}
#endif
