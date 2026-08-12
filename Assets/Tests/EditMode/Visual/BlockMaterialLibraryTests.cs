#if UNITY_EDITOR
using MyWorld.Core.Blocks;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Visual
{
    /// <summary>
    /// 视觉/AI 角色测试：检测方块材质是否带 magenta (#FF00FF) 占位像素。
    /// 整个 fixture 依赖 UnityEngine + MyWorld.Unity，所以用 #if UNITY_EDITOR
    /// 隔离——dotnet 测试链只跑纯 Core 测试，Unity EditMode 测试链跑这批。
    /// </summary>
    [TestFixture]
    public class BlockMaterialLibraryTests
    {
        [Test]
        public void MissingMaterial_PureMagentaPixels()
        {
            // 创建一个内部 magenta 占位材质，断言 HasMagentaPixels 返回 true
            // 用 reflection 访问 CreateMissingMaterial 不行（private static），
            // 改方案：直接 new 一个 4×4 magenta 贴图 + URP/Lit 材质，断言为 true。
            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < 16; i++) pixels[i] = Color.magenta;
            tex.SetPixels(pixels);
            tex.Apply();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                mainTexture = tex,
            };
            try
            {
                Assert.That(BlockMaterialLibrary.HasMagentaPixels(mat), Is.True,
                    "magenta 占位材质必须被识别出来，让视觉测试有钩子报错");
            }
            finally
            {
                Object.DestroyImmediate(mat);
                Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void GrassTopMaterial_NoMagentaPixels()
        {
            // 走真实 BlockRegistryLoader.Load() + BlockMaterialLibrary.Load()，
            // 断言任何槽位的材质都不含 magenta（之前因为 grass-top 等基础贴图都齐全，应过）。
            // 这一条是「现有基础方块贴图无 magenta」的回归保险。
            var registry = BlockRegistryLoader.Load();
            var lib = BlockMaterialLibrary.Load(registry, BlockRegistryLoader.TextureDirectory);
            try
            {
                for (int i = 0; i < lib.Count; i++)
                {
                    var m = lib.Get(i);
                    if (m == null) continue;
                    Assert.That(BlockMaterialLibrary.HasMagentaPixels(m), Is.False,
                        $"slot {i} 的材质含 magenta 像素（贴图缺失）");
                }
            }
            finally
            {
                lib.Dispose();
            }
        }
    }
}
#endif