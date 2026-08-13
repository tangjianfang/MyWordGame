#if UNITY_EDITOR
using MyWorld.Core.Blocks;
using MyWorld.Unity.Bootstrap;
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
            //
            // 注意：BlockMaterialLibrary 对每个缺失贴图会 Debug.LogError，这是有意为之的
            // 「贴图缺失哨兵」（让运维在日志里立刻看到）。本测试就是在验证 placeholder
            // 材质机制——缺贴图是预期路径，不能让这些 expected error 把测试判 fail。
            // 用 LogAssert.Expect 把 10 条已知缺失贴图逐一登记；以后若新增缺失贴图而测试
            // 报错，按这条路径再加一行 expect 即可。
            // Task A2 已补齐所有方块贴图，expectedMissing 列表已清空。
            // 历史背景：早期 milestone-3 阶段基础方块贴图尚未生成，BlockMaterialLibrary
            // 对每个缺失贴图会 Debug.LogError（哨兵），本测试用 LogAssert.Expect 登记这些
            // 预期错误，避免把哨兵判成 fail。A2 把所有 10 个贴图补完后，不再有缺失哨兵，
            // expectedMissing 维持空数组 + 不再调用 LogAssert.Expect。
            string[] expectedMissing = { };
            if (expectedMissing.Length > 0)
            {
                foreach (var name in expectedMissing)
                {
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                        new System.Text.RegularExpressions.Regex($".*{name}.*"));
                }
            }

            var registry = BlockRegistryLoader.Load();
            var lib = BlockMaterialLibrary.Load(registry, BlockRegistryLoader.TextureDirectory);
            try
            {
                for (int i = 0; i < lib.Count; i++)
                {
                    // missing 材质含 magenta 是有意为之的贴图缺失哨兵，跳过它：
                    // 这条测试只断言「真实贴图就位的方块不应含 magenta」，不要替缺失贴图背书。
                    if (lib.IsMissing(i)) continue;
                    var m = lib.Get(i);
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
