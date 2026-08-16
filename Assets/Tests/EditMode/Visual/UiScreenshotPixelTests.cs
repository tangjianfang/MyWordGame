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
        /// 宽 = max((2+1)*(40+4)+40, 9*(40+4)+40) = 436——m6 终审修 I1 起按主背包 9 列
        /// 实际跨度取宽（旧值 172 只罩住 2×2 合成区，9 列主背包大半画在框外），
        /// 高 380 为 m6 A2 写死的常量。
        /// </summary>
        private const int InventoryBoxW = 436;
        private const int InventoryBoxH = 380;

        /// <summary>旧背景框右缘（172）：修复前 x≥176 的主背包列画在框外，
        /// 是这条缺陷的回归哨兵采样带起点。</summary>
        private const int LegacyBoxRightEdge = 176;

        /// <summary>帮助菜单面板尺寸（HelpMenuUi.OnGUI 写死的 720×660，居中）。</summary>
        private const int HelpMenuW = 720;
        private const int HelpMenuH = 660;

        /// <summary>暂停菜单面板尺寸（PauseMenuUi.DrawMenu 的换算结果：宽 720，
        /// 高 = 标题 44 + 三按钮行 3×42 + 底距 12 = 182，设置未展开 / 无失败提示时）。</summary>
        private const int PauseMenuW = 720;
        private const int PauseMenuH = 182;

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
                // m6 终审修 I1：采样区随框宽扩到整个 436px——主背包 9 列（x=40..432）
                // 全部落在框内，这条断言从「只查合成区」升级成「全格在框内」的回归哨兵
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

                // 回归哨兵（m6 终审修 I1）：x∈[176,432) 是旧 172 宽框外的主背包列带——
                // 修复前这里是纯游戏画面（B2 实测列均亮度 ~101，接缝在 x≈192 处阶跃），
                // 修复后必须同样是深色框叠加。谁的 bgW 公式再只按合成区算，这条立刻红。
                int bandLum = 0, bandDark = 0, bandTotal = 0;
                for (int x = LegacyBoxRightEdge; x < InventoryBoxW - 4; x += 4)
                for (int y = 180; y < InventoryBoxH - 8; y += 4)
                {
                    int lum = Luminance(PixelAtScreenY(tex, x, y));
                    bandLum += lum;
                    if (lum < 128) bandDark++;
                    bandTotal++;
                }
                float bandAvg = (float)bandLum / bandTotal;
                float bandDarkFrac = (float)bandDark / bandTotal;
                Assert.That(bandAvg, Is.LessThan(110f),
                    $"旧框外列带（主背包第 3-9 列）平均亮度 {bandAvg:F1}（>=110）——" +
                    "背景框宽度疑似回退到只罩合成区的 172（m6 终审修 I1 的回归）");
                Assert.That(bandDarkFrac, Is.GreaterThan(0.8f),
                    $"旧框外列带暗像素占比 {bandDarkFrac * 100f:F1}%（<80%），主背包列没有框住");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Help_MenuPanel_HasDarkOverlay()
        {
            var tex = LoadOrNull("ui-help.png");
            if (tex == null) Assert.Ignore("无 --ui-shot 产物，本断言只在完整流水线生效");
            try
            {
                Assert.That(tex.width, Is.GreaterThan(HelpMenuW),
                    $"截图宽度 {tex.width} 装不下帮助菜单，产物异常");
                Assert.That(tex.height, Is.GreaterThan(HelpMenuH),
                    $"截图高度 {tex.height} 装不下帮助菜单，产物异常");

                // 菜单居中 720×660。采样 Tab 以下的内区（按键表双栏 + 四步玩法 + 任务进度区），
                // 阈值与背包同款：面板叠加后平均亮度 <110、暗像素 >80%；
                // 纯游戏画面该区域 ~130（B2 基线）。文字/格子边框像素占比小，不破坏阈值。
                int left = (tex.width - HelpMenuW) / 2;
                int top = (tex.height - HelpMenuH) / 2;
                int lumSum = 0, dark = 0, total = 0;
                for (int x = left + 24; x < left + HelpMenuW - 24; x += 4)
                for (int y = top + 78; y < top + HelpMenuH - 40; y += 4)
                {
                    int lum = Luminance(PixelAtScreenY(tex, x, y));
                    lumSum += lum;
                    if (lum < 128) dark++;
                    total++;
                }
                float avg = (float)lumSum / total;
                float darkFrac = (float)dark / total;
                Assert.That(avg, Is.LessThan(110f),
                    $"帮助菜单内区平均亮度 {avg:F1}（>=110）说明深色面板没画上（纯游戏画面）");
                Assert.That(darkFrac, Is.GreaterThan(0.8f),
                    $"帮助菜单内区暗像素占比 {darkFrac * 100f:F1}%（<80%），面板叠加可疑");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Pause_MenuPanel_HasDarkOverlay()
        {
            var tex = LoadOrNull("ui-pause.png");
            if (tex == null) Assert.Ignore("无 --ui-shot 产物，本断言只在完整流水线生效");
            try
            {
                Assert.That(tex.width, Is.GreaterThan(PauseMenuW + 80),
                    $"截图宽度 {tex.width} 装不下暂停菜单 + 两侧对照带，产物异常");
                Assert.That(tex.height, Is.GreaterThan(PauseMenuH + 40),
                    $"截图高度 {tex.height} 装不下暂停菜单，产物异常");

                // 菜单居中 720×182。三颗按钮横贯 x∈[bg.x+24, bg.x+width-48+24]（亮色
                // GUI.Button 皮肤），不能整块采样亮度；改采按钮到不了的左右边缘带
                //（x∈[left+4,left+20) 与 [left+700,left+716)，纯 GUI.Box 深色叠加），
                // 与紧邻面板外的同 y 对照带（纯游戏画面）比对：面板画上时边缘带显著更暗
                //（B2 基线：GUI.Box 叠加把区域亮度约减半，130→69）。相对比较 + 绝对上限
                // 双门槛：相对项保证「居中面板存在」本身，绝对项与其余 UI 断言同口径。
                int left = (tex.width - PauseMenuW) / 2;
                int top = (tex.height - PauseMenuH) / 2;
                int inLum = 0, inN = 0, outLum = 0, outN = 0;
                for (int y = top + 8; y < top + PauseMenuH - 8; y += 4)
                {
                    for (int x = left + 4; x < left + 20; x += 4)
                    {
                        inLum += Luminance(PixelAtScreenY(tex, x, y));
                        inN++;
                    }
                    for (int x = left + 700; x < left + 716; x += 4)
                    {
                        inLum += Luminance(PixelAtScreenY(tex, x, y));
                        inN++;
                    }
                    for (int x = left - 20; x < left - 4; x += 4)
                    {
                        outLum += Luminance(PixelAtScreenY(tex, x, y));
                        outN++;
                    }
                    for (int x = left + 724; x < left + 740; x += 4)
                    {
                        outLum += Luminance(PixelAtScreenY(tex, x, y));
                        outN++;
                    }
                }
                float inside = (float)inLum / inN;
                float outside = (float)outLum / outN;
                Assert.That(inside, Is.LessThan(100f),
                    $"暂停面板边缘带平均亮度 {inside:F1}（>=100）说明深色面板没画上（纯游戏画面）");
                Assert.That(inside, Is.LessThan(outside * 0.8f),
                    $"面板边缘带 {inside:F1} 未显著低于同 y 纯游戏画面带 {outside:F1}（应 <80%）——" +
                    "居中面板不存在（ui-pause.png 疑似没开菜单就截了）");
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }
}
#endif
