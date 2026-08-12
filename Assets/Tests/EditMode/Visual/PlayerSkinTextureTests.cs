#if UNITY_EDITOR
// 本 fixture 依赖 UnityEngine（Texture2D.LoadImage / GetPixels / Object.DestroyImmediate），
// 整个文件用 #if UNITY_EDITOR ... #endif 包裹：dotnet csproj 链跑纯 Core 测试时跳过，
// Unity EditMode 链跑这条。视觉/AI 角色测试统一走 Unity EditMode。
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    /// <summary>
    /// 玩家皮肤 skin.png 的最小约束：64×64、第一层头区全不透明。
    /// 真正的皮肤细节（眼睛高光、头发走向、上衣衣襟竖线）由手工或 AI 取色参考后填，
    /// 本测试只保证占位图满足结构约束，确保后续美术流程替换皮肤时不会破洞。
    /// </summary>
    [TestFixture]
    public class PlayerSkinTextureTests
    {
        private static string SkinPath()
        {
            // streamingAssetsPath = <project>/Assets/StreamingAssets
            // ".." 一层回到 <project>/Assets，再接 Art/Player/skin.png
            string path = Path.Combine(Application.streamingAssetsPath,
                "..", "Art", "Player", "skin.png");
            return Path.GetFullPath(path);
        }

        private static Texture2D LoadSkin()
        {
            // LoadImage 默认 markNonReadable=true 会让 CPU 端 GetPixels 读到 0，
            // 必须显式传 false 才能回读到真正的像素数据
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(SkinPath()), false);
            return tex;
        }

        [Test]
        public void Skin_ExistsAndIs64x64()
        {
            string path = SkinPath();
            Assert.That(File.Exists(path), Is.True, $"皮肤图缺失: {path}");
            var tex = LoadSkin();
            try
            {
                Assert.That(tex.width, Is.EqualTo(64), $"皮肤宽度={tex.width}，必须 64");
                Assert.That(tex.height, Is.EqualTo(64), $"皮肤高度={tex.height}，必须 64");
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void Skin_FirstLayer_FullyOpaque()
        {
            // 头区 (0,0)-(32,16) 必须全不透明（不能破洞）
            string path = SkinPath();
            Assert.That(File.Exists(path), Is.True, $"皮肤图缺失: {path}");
            var tex = LoadSkin();
            try
            {
                var pixels = tex.GetPixels(0, 0, 32, 16);
                for (int i = 0; i < pixels.Length; i++)
                {
                    Assert.That(pixels[i].a, Is.GreaterThan(0.99f),
                        $"头区像素 alpha={pixels[i].a:F2}，必须全不透明");
                }
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }
    }
}
#endif