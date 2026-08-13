#if UNITY_EDITOR
// Phase D 第三批测试：MobView 按 MobKind 切换视觉。
// 旧 Passive/Hostile 路径保留单 cube 行为（既有 MobManager.Attach 不变），
// 新 Pig/Cow/Chicken/Zombie 路径创建 Body + Head 双段 mesh（body+head 缩放/位置见 brief）。
// 测试策略：直接构造 MobView host，AddComponent + 调 Setup(kind)，枚举 transform 子节点，
// 确认 Body/Head 命名、Renderer 染色正确。UNITY_EDITOR 包裹确保 dotnet 链不参与。
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Unity.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    [TestFixture]
    public class MobViewPerKindTests
    {
        [Test]
        public void Setup_Pig_CreatesBodyAndHead()
        {
            var host = new GameObject("MobViewPerKindTestHost");
            try
            {
                host.AddComponent<MobView>();
                var view = host.GetComponent<MobView>();
                InvokeSetup(view, MobKind.Pig);
                Assert.That(host.transform.Find("Body"), Is.Not.Null, "Pig 应创建 Body 子物体");
                Assert.That(host.transform.Find("Head"), Is.Not.Null, "Pig 应创建 Head 子物体");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Setup_Cow_CreatesBodyAndHead()
        {
            var host = new GameObject("MobViewPerKindTestHost");
            try
            {
                host.AddComponent<MobView>();
                var view = host.GetComponent<MobView>();
                InvokeSetup(view, MobKind.Cow);
                Assert.That(host.transform.Find("Body"), Is.Not.Null, "Cow 应创建 Body 子物体");
                Assert.That(host.transform.Find("Head"), Is.Not.Null, "Cow 应创建 Head 子物体");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Setup_Chicken_CreatesBodyAndHead()
        {
            var host = new GameObject("MobViewPerKindTestHost");
            try
            {
                host.AddComponent<MobView>();
                var view = host.GetComponent<MobView>();
                InvokeSetup(view, MobKind.Chicken);
                Assert.That(host.transform.Find("Body"), Is.Not.Null, "Chicken 应创建 Body 子物体");
                Assert.That(host.transform.Find("Head"), Is.Not.Null, "Chicken 应创建 Head 子物体");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Setup_Zombie_CreatesBodyAndHead()
        {
            var host = new GameObject("MobViewPerKindTestHost");
            try
            {
                host.AddComponent<MobView>();
                var view = host.GetComponent<MobView>();
                InvokeSetup(view, MobKind.Zombie);
                Assert.That(host.transform.Find("Body"), Is.Not.Null, "Zombie 应创建 Body 子物体");
                Assert.That(host.transform.Find("Head"), Is.Not.Null, "Zombie 应创建 Head 子物体");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 鸡头黄、身白：body 与 head 应分别用 BodyColor（白）与 HeadColor（黄）染色，
        /// 颜色不同才能看出是「两只不同颜色的鸡部件」，不是「整只白鸡带个白头」。
        /// </summary>
        [Test]
        public void Setup_Chicken_BodyAndHeadHaveDifferentColors()
        {
            var host = new GameObject("MobViewPerKindTestHost");
            try
            {
                host.AddComponent<MobView>();
                var view = host.GetComponent<MobView>();
                InvokeSetup(view, MobKind.Chicken);
                var body = host.transform.Find("Body").GetComponent<Renderer>();
                var head = host.transform.Find("Head").GetComponent<Renderer>();
                var bodyColor = ReadColor(body);
                var headColor = ReadColor(head);
                Assert.That(bodyColor, Is.Not.EqualTo(headColor),
                    "Chicken body 与 head 颜色应不同（身白头黄），否则视觉上分不清鸡头鸡身");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 重设 kind 时旧 Body/Head 必须清掉，避免「Pig Body + Cow Head」混搭。
        /// </summary>
        [Test]
        public void Setup_ReplacesExistingChildren()
        {
            var host = new GameObject("MobViewPerKindTestHost");
            try
            {
                host.AddComponent<MobView>();
                var view = host.GetComponent<MobView>();
                InvokeSetup(view, MobKind.Pig);
                InvokeSetup(view, MobKind.Cow);

                int bodyCount = 0, headCount = 0;
                foreach (Transform child in host.transform)
                {
                    if (child.name == "Body") bodyCount++;
                    else if (child.name == "Head") headCount++;
                }
                Assert.That(bodyCount, Is.EqualTo(1), "重设 kind 后 Body 应只剩 1 个（旧 Body 已清）");
                Assert.That(headCount, Is.EqualTo(1), "重设 kind 后 Head 应只剩 1 个（旧 Head 已清）");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void InvokeSetup(MobView view, MobKind kind)
        {
            var setup = typeof(MobView).GetMethod("Setup",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(setup, Is.Not.Null, "MobView 没有公共 Setup 方法");
            setup.Invoke(view, new object[] { kind });
        }

        private static Color ReadColor(Renderer r)
        {
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }
    }
}
#endif