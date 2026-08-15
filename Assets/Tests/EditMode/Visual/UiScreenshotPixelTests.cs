#if UNITY_EDITOR
using System.IO;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    /// <summary>
    /// m6 B2：<c>--ui-shot</c> 产物（<c>Builds/screenshots/ui-*.png</c>）的像素断言。
    /// IMGUI 不进 Camera.Render（visual-smoke 1a/1b 的老盲区），B1 用 standalone 的
    /// <c>MyWordGame.exe --ui-shot</c> 抓完整 backbuffer 补拍 UI；本测试读那批 PNG 验证
    /// UI 真画上了：
    /// <list type="bullet">
    /// <item>hotbar 第 0 格非纯白——m6 修过的「选中框 fallback 实心白 1×1 盖住图标」回归哨兵</item>
    /// <item>背包背景框区域有半透明深色叠加（非纯游戏画面）</item>
    /// </list>
    /// <para>
    /// PNG 只在完整流水线（build-and-run.sh → visual-smoke.sh 1c）后存在，
    /// 文件缺失一律 <see cref="Assert.Ignore"/>——无 build 产物的环境不算失败。
    /// </para>
    /// </summary>
    [TestFixture]
    public class UiScreenshotPixelTests
    {
        /// <summary>hotbar 底部留白（HotbarUI.OnGUI 里写死的 16px，换算坐标需保持一致）。</summary>
        private const int HotbarBottomMargin = 16;

        /// <summary>
        /// 背包背景框尺寸（CraftingInventoryUi.OnGUI 的换算结果：
        /// 宽 = (2+1)*(40+4)+40 = 172，高 380 为 m6 A2 写死的常量）。
        /// </summary>
        private const int InventoryBoxW = 172;
        private const int InventoryBoxH = 380;

        /// <summary>截图目录（与 ScreenshotCapture / UiScreenshotOnArg 同一处 Builds/screenshots）。</summary>
        private static readonly string ScreenshotDir =
            Path.Combine(Application.dataPath, "..", "Builds", "screenshots");

        private static Texture2D LoadOrNull(string fileName)
        {
            string path = Path.Combine(ScreenshotDir, fileName);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        /// <summary>屏幕坐标（IMGUI 左上原点）→ 纹理采样（GetPixel 左下原点）。</summary>
        private static Color PixelAtScreenY(Texture2D tex, int x, int yFromTop) =>
            tex.GetPixel(x, tex.height - 1 - yFromTop);

        /// <summary>整数亮度 0-255（Rec.709 加权，与人眼感知一致）。</summary>
        private static int Luminance(Color c) =>
            Mathf.RoundToInt((0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) * 255f);

        /// <summary>纯白判定：三通道全 &gt;250 且相等（B2 brief 约定）。</summary>
        private static bool IsPureWhite(Color c)
        {
            int r = Mathf.RoundToInt(c.r * 255f);
            int g = Mathf.RoundToInt(c.g * 255f);
            int b = Mathf.RoundToInt(c.b * 255f);
            return r > 250 && g > 250 && b > 250 && r == g && g == b;
        }

        [Test]
        public void Hotbar_Slot0Center_NotPureWhite()
        {
            var tex = LoadOrNull("ui-hotbar.png");
            if (tex == null) Assert.Ignore("无 --ui-shot 产物，本断言只在完整流水线生效");
            try
            {
                // 坐标按 HotbarUI.OnGUI 的换算推导，分辨率无关（-screen-width 失效截出
                // 1920×1080 时同样成立）：startX=(w-总宽)/2，槽顶 y=h-64-16
                Assert.That(tex.width, Is.GreaterThan((HotbarUI.SlotSize + HotbarUI.Padding) * 9),
                    $"截图宽度 {tex.width} 装不下 9 格 hotbar，产物异常");
                Assert.That(tex.height, Is.GreaterThan(HotbarUI.SlotSize + HotbarBottomMargin),
                    $"截图高度 {tex.height} 装不下 hotbar，产物异常");
                int startX = (tex.width - (HotbarUI.SlotSize + HotbarUI.Padding) * 9) / 2;
                int slotY = tex.height - HotbarUI.SlotSize - HotbarBottomMargin;
                int cx = startX + HotbarUI.SlotSize / 2;
                int cy = slotY + HotbarUI.SlotSize / 2;

                Color center = PixelAtScreenY(tex, cx, cy);
                Assert.That(IsPureWhite(center), Is.False,
                    $"hotbar 第 0 格中心像素为纯白 ({center.r:F2},{center.g:F2},{center.b:F2})——" +
                    "疑似选中框 fallback 实心白块盖住了图标（m6 修过的回归）");

                // 中心单点可能恰好落在图标抗锯齿边缘，再扫整格内部兜底：
                // 纯白像素占多数才判失败（图标正常绘制时白色占比不可能过半）
                int white = 0, total = 0;
                for (int x = startX + 4; x < startX + HotbarUI.SlotSize - 4; x += 2)
                for (int y = slotY + 4; y < slotY + HotbarUI.SlotSize - 4; y += 2)
                {
                    if (IsPureWhite(PixelAtScreenY(tex, x, y))) white++;
                    total++;
                }
                Assert.That((float)white / total, Is.LessThan(0.5f),
                    $"第 0 格内部纯白像素 {white}/{total}——整格被白色块盖住");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Inventory_BackgroundBox_HasDarkOverlay()
        {
            var tex = LoadOrNull("ui-inventory.png");
            if (tex == null) Assert.Ignore("无 --ui-shot 产物，本断言只在完整流水线生效");
            try
            {
                Assert.That(tex.width, Is.GreaterThan(InventoryBoxW + 40),
                    $"截图宽度 {tex.width} 装不下背包框，产物异常");
                Assert.That(tex.height, Is.GreaterThan(InventoryBoxH + 40),
                    $"截图高度 {tex.height} 装不下背包框，产物异常");

                // 背包框固定在屏幕左上 (20,20) 起，坐标与分辨率无关。
                // 实测基线（B2）：开背包时区域平均亮度 ~69、暗像素占比 ~99%；
                // 同区域纯游戏画面（ui-hotbar）平均亮度 ~130。取中间留 buffer。
                int lumSum = 0, dark = 0, total = 0;
                for (int x = 24; x < InventoryBoxW - 4; x += 4)
                for (int y = 24; y < InventoryBoxH - 4; y += 4)
                {
                    int lum = Luminance(PixelAtScreenY(tex, x, y));
                    lumSum += lum;
                    if (lum < 128) dark++;
                    total++;
                }
                float avg = (float)lumSum / total;
                float darkFrac = (float)dark / total;
                Assert.That(avg, Is.LessThan(110f),
                    $"背包框区域平均亮度 {avg:F1}（>=110）说明半透明深色背景框没画上（纯游戏画面）");
                Assert.That(darkFrac, Is.GreaterThan(0.8f),
                    $"背包框区域暗像素占比 {darkFrac * 100f:F1}%（<80%），背景框叠加可疑");
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }
}
#endif
