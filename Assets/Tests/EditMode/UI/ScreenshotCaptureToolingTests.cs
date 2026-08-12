#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// Task C-tooling：锁定 <see cref="ScreenshotCapture"/> 的反射驱动扩展存在，
    /// 防止有人误删导致视觉验证工具链退化。
    /// </summary>
    /// <remarks>
    /// ScreenshotCapture 位于 <c>MyWorld.Unity.Editor</c> 程序集；
    /// <c>MyWorld.Core.Tests</c> asmdef 未引用 Editor 程序集（避免测试对编辑器代码产生编译期依赖），
    /// 所以这里走 <see cref="Type.GetType(string)"/> 反射查类型，不引 using。
    /// </remarks>
    [TestFixture]
    public class ScreenshotCaptureToolingTests
    {
        [Test]
        public void DriveAllMonoBehaviours_PrivateMethodExists()
        {
            // 通过反射拿 ScreenshotCapture 类型（避免在测试 asmdef 加 Editor 程序集引用）。
            var type = Type.GetType(
                "MyWorld.Unity.EditorTools.ScreenshotCapture, MyWorld.Unity.Editor",
                throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "MyWorld.Unity.Editor 程序集必须已加载且含 ScreenshotCapture 类型");
            var method = type.GetMethod(
                "DriveAllMonoBehaviours",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                "ScreenshotCapture.DriveAllMonoBehaviours 必须存在（C-tooling 修复）");
            // 验证方法签名：int 入参，void 返回
            var parameters = method.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(1),
                "DriveAllMonoBehaviours 应只接受 1 个 frames 参数");
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(int)),
                "DriveAllMonoBehaviours 的入参必须是 int（帧数）");
        }
    }
}
#endif