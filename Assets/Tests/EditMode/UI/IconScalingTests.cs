#if UNITY_EDITOR
using System.IO;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// Task B4（#6 图标边界锯齿）：物品贴图 16×16，之前 hotbar 用 56×56（3.5 倍）
    /// 非整数倍缩放 + 双线性过滤，像素宽窄不均出锯齿。这里锁定两条契约：
    /// 1. 图标绘制边长必须是 16 的整数倍（HotbarUI 定格 48 = 16×3）；
    /// 2. 走 UI 加载路径（<see cref="HotbarUI.LoadItemTexturePng"/>）的贴图
    ///    必须是 Point 过滤——双线性在整数倍缩放下也会糊掉像素边界。
    /// </summary>
    [TestFixture]
    public class IconScalingTests
    {
        /// <summary>生成一张 16×16 的纯色 PNG 写入临时文件，模拟磁盘上的物品贴图。</summary>
        private static string WriteTestPng()
        {
            var src = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var pixels = new Color32[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(200, 40, 40, 255);
            src.SetPixels32(pixels);
            src.Apply();
            string path = Path.Combine(Path.GetTempPath(), "m5-b4-icon-test.png");
            File.WriteAllBytes(path, src.EncodeToPNG());
            Object.DestroyImmediate(src);
            return path;
        }

        [Test]
        public void HotbarUI_IconDrawSize_IsMultipleOf16_AndIs48()
        {
            Assert.That(HotbarUI.IconDrawSize % 16, Is.EqualTo(0),
                "图标绘制边长必须是 16 的整数倍——16×16 贴图非整数倍缩放会让像素宽窄不均出锯齿");
            Assert.That(HotbarUI.IconDrawSize, Is.EqualTo(48),
                "图标边长定格为 48（16×3）：槽位 64 内图标 48 + 两侧各留 8，布局不塌");
        }

        [Test]
        public void HandController_HandIconSize_IsMultipleOf16()
        {
            Assert.That(HandController.HandIconSize % 16, Is.EqualTo(0),
                "手部图标边长同样必须是 16 的整数倍（当前 80 = 16×5）");
        }

        [Test]
        public void HotbarUI_LoadPath_AppliesPointFiltering()
        {
            string path = WriteTestPng();
            try
            {
                var tex = HotbarUI.LoadItemTexturePng(path);
                Assert.That(tex, Is.Not.Null, "测试 PNG 应能正常加载");
                Assert.That(tex.width, Is.EqualTo(16), "LoadImage 应按 PNG 实际尺寸重建为 16 宽");
                Assert.That(tex.height, Is.EqualTo(16), "LoadImage 应按 PNG 实际尺寸重建为 16 高");
                Assert.That(tex.filterMode, Is.EqualTo(FilterMode.Point),
                    "UI 加载路径必须设 Point 过滤——默认双线性会把像素边界糊出锯齿");
                Object.DestroyImmediate(tex);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
#endif
