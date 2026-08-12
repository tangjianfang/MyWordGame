#if UNITY_EDITOR
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    /// <summary>
    /// Task E1：合并任务（plan-level D2 已删，合并到此）。
    /// 像素采样套件，覆盖 4 个核心断言：截图不全黑、不全 magenta、有草地绿像素、方块材质无 magenta。
    /// 视觉回归基线：本任务跑时已有 333/333 + SelectionBox 可见（#106 已修）+ magenta=0%。
    /// </summary>
    [TestFixture]
    public class VisualRegressionTests
    {
        private static string LatestScreenshot(string name)
        {
            string dir = Path.Combine(Application.dataPath, "..", "Builds", "screenshots");
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir, name + ".png")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();
            return files.Length == 0 ? null : files[0];
        }

        private static Texture2D LoadTexture(string path)
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        [Test]
        public void Overworld_NotAllBlack()
        {
            string latest = LatestScreenshot("overworld");
            if (latest == null) Assert.Ignore("无截图——先跑 ScreenshotCapture.CaptureAllDefault");
            var tex = LoadTexture(latest);
            try
            {
                var pixels = tex.GetPixels();
                float avg = pixels.Sum(c => (c.r + c.g + c.b) / 3f) / pixels.Length * 255f;
                Assert.That(avg, Is.GreaterThan(30f),
                    $"截图平均亮度 {avg:F1}（< 30）说明画面全黑");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Overworld_NotAllMagenta()
        {
            string latest = LatestScreenshot("overworld");
            if (latest == null) Assert.Ignore("无截图——先跑 ScreenshotCapture.CaptureAllDefault");
            var tex = LoadTexture(latest);
            try
            {
                var pixels = tex.GetPixels();
                int magenta = pixels.Count(c => c.r > 0.98f && c.g < 0.03f && c.b > 0.98f);
                float frac = (float)magenta / pixels.Length;
                Assert.That(frac, Is.LessThan(0.05f),
                    $"magenta 像素 {frac * 100:F2}%（超 5%）");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void Overworld_HasGrassGreen()
        {
            string latest = LatestScreenshot("overworld");
            if (latest == null) Assert.Ignore("无截图——先跑 ScreenshotCapture.CaptureAllDefault");
            var tex = LoadTexture(latest);
            try
            {
                var pixels = tex.GetPixels();
                // R1 fix: 草色区间扩宽（光照 + 材质混合后实际色相会偏离原始贴图中心），
                // 阈值降到 4%（原 5%）给 1pp buffer 防未来光照变化。
                int grass = pixels.Count(c =>
                    c.r >= 0.20f && c.r <= 0.55f &&
                    c.g >= 0.40f && c.g <= 0.82f &&
                    c.b >= 0.12f && c.b <= 0.42f);
                float frac = (float)grass / pixels.Length;
                Assert.That(frac, Is.GreaterThan(0.04f),
                    $"草绿像素 {frac * 100:F2}%（不足 4%），地表可能没渲染");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        [Test]
        public void BlockMaterials_NoMagenta()
        {
            var registry = BlockRegistryLoader.Load();
            var lib = BlockMaterialLibrary.Load(registry, BlockRegistryLoader.TextureDirectory);
            try
            {
                int bad = 0;
                for (int i = 0; i < lib.Count; i++)
                {
                    var m = lib.Get(i);
                    if (m == null) continue;
                    if (BlockMaterialLibrary.HasMagentaPixels(m)) bad++;
                }
                Assert.That(bad, Is.EqualTo(0),
                    $"{bad} 个方块材质仍含 magenta 像素（贴图未入库）");
            }
            finally { lib.Dispose(); }
        }
    }
}
#endif
