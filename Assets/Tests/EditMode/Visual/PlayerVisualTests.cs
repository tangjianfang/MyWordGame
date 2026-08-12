#if UNITY_EDITOR
// 本 fixture 依赖 UnityEngine（GameObject / Transform / MonoBehaviour），
// 整个文件用 #if UNITY_EDITOR ... #endif 包裹：dotnet csproj 链跑纯 Core 测试时跳过，
// Unity EditMode 链跑这条。视觉/AI 角色测试统一走 Unity EditMode。
using System.Reflection;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    /// <summary>
    /// 玩家身体骨架 <see cref="PlayerVisual"/> 的最小约束：Awake 后 6 个子 Transform
    /// （头/躯干/双臂/双腿）都创建好。真正的走路动画由后续 B2 任务实现。
    /// <para>
    /// EditMode 测试里 <c>AddComponent</c> 不会自动触发 <c>Awake</c>（Unity 运行时生命周期
    /// 只在 PlayMode / 场景加载时跑），所以这里手动用反射调一次 <c>Awake</c>。读 6 个
    /// private 字段也走反射——避免在生产代码里暴露仅供测试的 getter。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PlayerVisualTests
    {
        [Test]
        public void Awake_CreatesAllSixBodyParts()
        {
            // 需要 PlayerController 组件（[RequireComponent]），所以一并加上
            var host = new GameObject("PlayerVisualTestHost");
            try
            {
                host.AddComponent<PlayerController>();
                var visual = host.AddComponent<PlayerVisual>();
                // EditMode 下 AddComponent 不触发 Awake，手动调一次
                InvokeAwake(visual);
                Assert.That(ReadTransform(visual, "_head"),  Is.Not.Null, "Head 子物体未创建");
                Assert.That(ReadTransform(visual, "_torso"), Is.Not.Null, "Torso 子物体未创建");
                Assert.That(ReadTransform(visual, "_armL"),  Is.Not.Null, "ArmL 子物体未创建");
                Assert.That(ReadTransform(visual, "_armR"),  Is.Not.Null, "ArmR 子物体未创建");
                Assert.That(ReadTransform(visual, "_legL"),  Is.Not.Null, "LegL 子物体未创建");
                Assert.That(ReadTransform(visual, "_legR"),  Is.Not.Null, "LegR 子物体未创建");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 验证 6 个 Cube 都挂在 PlayerVisual 自己下面（不是场景根的孤儿）。
        /// 挂载关系正确是 PlayerController 移动 transform 时身体跟随的前提。
        /// </summary>
        [Test]
        public void Awake_ParentsBodyPartsToSelf()
        {
            var host = new GameObject("PlayerVisualTestHost");
            try
            {
                host.AddComponent<PlayerController>();
                var visual = host.AddComponent<PlayerVisual>();
                InvokeAwake(visual);
                foreach (var name in new[] { "_head", "_torso", "_armL", "_armR", "_legL", "_legR" })
                {
                    var t = ReadTransform(visual, name);
                    Assert.That(t, Is.Not.Null, $"{name} 子物体未创建");
                    Assert.That(t.parent, Is.SameAs(host.transform),
                        $"{name} 没有挂在 PlayerVisual 自身下面（parent={t.parent}），" +
                        $"是场景根孤儿——PlayerController 移动时身体不会跟随");
                }
                // 同时确认 localPosition/localScale 在 parent 本地坐标系下被正确设置
                var head = ReadTransform(visual, "_head");
                Assert.That(head.localPosition, Is.EqualTo(new Vector3(0f, 1.65f, 0f)),
                    "Head localPosition 应为 (0, 1.65, 0)，偏移说明 SetParent/localPosition 顺序错了");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// 验证 OnDestroy 清理 6 个 Cube GameObject，避免 EditMode 测试间泄漏到场景根。
        /// DestroyImmediate(host) 会触发 PlayerVisual.OnDestroy，进而清理子 Cube。
        /// </summary>
        [Test]
        public void OnDestroy_CleansUpBodyParts()
        {
            var host = new GameObject("PlayerVisualTestHost");
            host.AddComponent<PlayerController>();
            var visual = host.AddComponent<PlayerVisual>();
            InvokeAwake(visual);

            // 收集子 Cube 引用（销毁后用于断言）
            var partRefs = new Transform[6];
            var partNames = new[] { "_head", "_torso", "_armL", "_armR", "_legL", "_legR" };
            for (int i = 0; i < partNames.Length; i++)
            {
                partRefs[i] = ReadTransform(visual, partNames[i]);
                Assert.That(partRefs[i], Is.Not.Null, $"{partNames[i]} 未创建");
            }

            // 销毁 host，触发 PlayerVisual.OnDestroy
            Object.DestroyImmediate(host);

            // OnDestroy 后 6 个 Cube GameObject 都不应存活
            for (int i = 0; i < partRefs.Length; i++)
            {
                // Unity 的"fake null"——== null 返回 true 表示已被销毁
                Assert.That(partRefs[i] == null, Is.True,
                    $"{partNames[i]} 在 PlayerVisual.OnDestroy 后未被清理，泄漏到场景根");
            }
        }

        /// <summary>
        /// 验证走路动画：玩家位置变化时应推进 WalkPhase。
        /// <para>
        /// EditMode 下 <c>AddComponent</c> 不触发 <c>Awake</c> / <c>Start</c> / <c>Update</c>
        /// （Unity 生命周期只在 PlayMode / 场景加载时跑），所以这里手动反射调一次 Start
        /// 把 <c>_controller</c> 和 <c>_lastPos</c> 初始化好，再循环调 Update 模拟走路。
        /// </para>
        /// </summary>
        [Test]
        public void Walk_AdvancesPhase_WhenPositionChanges()
        {
            var host = new GameObject("PlayerVisualTestHost");
            try
            {
                host.AddComponent<PlayerController>();  // [RequireComponent]
                var visual = host.AddComponent<PlayerVisual>();
                InvokeAwake(visual);
                InvokeStart(visual);

                var prevPhase = visual.WalkPhase;
                var updateMethod = typeof(PlayerVisual).GetMethod("Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(updateMethod, Is.Not.Null, "PlayerVisual 没有 Update 方法");

                // 模拟 60 帧：每帧手动移动 host.transform.position 并调一次 Update
                for (int i = 0; i < 60; i++)
                {
                    host.transform.position += new Vector3(0.01f * i, 0f, 0.005f * i);
                    updateMethod.Invoke(visual, null);
                }

                Assert.That(visual.WalkPhase, Is.GreaterThan(prevPhase),
                    "走路动画：位置变化应推进 WalkPhase");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void InvokeStart(PlayerVisual visual)
        {
            var start = typeof(PlayerVisual).GetMethod("Start",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (start != null) start.Invoke(visual, null);
        }

        private static void InvokeAwake(PlayerVisual visual)
        {
            var awake = typeof(PlayerVisual).GetMethod("Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null, "PlayerVisual 没有 Awake 方法");
            awake.Invoke(visual, null);
        }

        private static Transform ReadTransform(PlayerVisual visual, string fieldName)
        {
            var field = typeof(PlayerVisual).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"PlayerVisual 没有字段 {fieldName}");
            return field.GetValue(visual) as Transform;
        }
    }
}
#endif