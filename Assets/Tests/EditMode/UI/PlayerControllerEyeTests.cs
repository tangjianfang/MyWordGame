#if UNITY_EDITOR
using System.Reflection;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// Task #105：验证 PlayerController.Awake 自动从子物体找 eye。
    /// 优先级：tag=MainCamera 的子 Camera transform > 名为"相机"的子物体 > 第一个子 transform。
    /// 找不到也不报错（无视觉模块场景兼容）。
    /// <para>
    /// EditMode 批处理下 <c>AddComponent</c> 不会自动触发 Awake（Unity 只在
    /// PlayMode / 场景加载时回调）。测试用反射显式调用私有 Awake。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PlayerControllerEyeTests
    {
        private static void InvokeAwake(PlayerController controller)
        {
            var method = typeof(PlayerController).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "PlayerController 应有私有 Awake 方法");
            method.Invoke(controller, null);
        }

        [Test]
        public void Awake_BindsEyeToNamedChild_WhenChildNamed相机()
        {
            var host = new GameObject("玩家");
            var camChild = new GameObject("相机");
            try
            {
                camChild.transform.SetParent(host.transform);
                var controller = host.AddComponent<PlayerController>();
                InvokeAwake(controller);
                var eye = controller.Eye;
                Assert.That(eye, Is.Not.Null,
                    "PlayerController.Awake 应找到名为 '相机' 的子物体作为 eye");
                Assert.That(eye.name, Is.EqualTo("相机"));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Awake_EyeIsNull_WhenNoChildren()
        {
            var host = new GameObject("PlayerNoChildren");
            try
            {
                var controller = host.AddComponent<PlayerController>();
                InvokeAwake(controller);
                var eye = controller.Eye;
                Assert.That(eye, Is.Null,
                    "没有子物体时 eye 应为 null（不报错，无视觉模块场景兼容）");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
#endif