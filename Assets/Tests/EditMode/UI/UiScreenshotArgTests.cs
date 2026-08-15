#if UNITY_EDITOR
using NUnit.Framework;
using MyWorld.Unity;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m6 B1：--ui-shot 启动参数解析的纯函数契约测试。
    /// <para>
    /// visual-smoke 走 Camera.Render 拍不到 IMGUI（OnGUI 是事件驱动、不进相机管线），
    /// 这是 UI 验证的长期盲区。<see cref="UiScreenshotOnArg"/> 用
    /// ScreenCapture.CaptureScreenshot 抓完整 backbuffer（含 IMGUI）补上这块。
    /// 这里只测 <see cref="UiScreenshotOnArg.ShouldCapture"/> 纯函数——
    /// 真实截图行为由实机 <c>MyWordGame.exe --ui-shot</c> 流程验证。
    /// </para>
    /// </summary>
    [TestFixture]
    public class UiScreenshotArgTests
    {
        [Test]
        public void ShouldCapture_WithUiShotArg_ReturnsTrue()
        {
            Assert.That(UiScreenshotOnArg.ShouldCapture(new[] { "--ui-shot" }), Is.True,
                "带 --ui-shot 参数时应启用 UI 截图流程");
        }

        [Test]
        public void ShouldCapture_WithOtherArgs_ReturnsFalse()
        {
            Assert.That(UiScreenshotOnArg.ShouldCapture(new[] { "-projectPath", "x" }), Is.False,
                "无关启动参数（如 -projectPath）不应触发 UI 截图");
            Assert.That(UiScreenshotOnArg.ShouldCapture(new[] { "-screen-width", "1280", "--ui-sho" }), Is.False,
                "前缀相似的参数不算 --ui-shot（精确匹配，不做前缀/子串匹配）");
        }

        [Test]
        public void ShouldCapture_NullArgs_ReturnsFalse()
        {
            Assert.That(UiScreenshotOnArg.ShouldCapture(null), Is.False,
                "GetCommandLineArgs 拿到 null 时必须安全返回 false，不能抛异常");
        }
    }
}
#endif
