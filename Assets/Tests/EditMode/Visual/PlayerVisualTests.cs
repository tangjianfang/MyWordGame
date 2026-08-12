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