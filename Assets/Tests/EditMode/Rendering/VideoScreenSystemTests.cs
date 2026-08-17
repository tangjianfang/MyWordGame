#if UNITY_EDITOR
// av W2-11：VideoScreenSystem EditMode 测试。
//   - EditMode 下 StreamingAssets/video 不存在 → Apply 应早退、不改材质、只告警
//   - 注册表无 laptop-screen 贴图槽 → 告警不改材质
//
// 注意：本测试 EditMode 不能真起 VideoPlayer（缺文件 + 没有图像资源），只验证
// 缺失分支；mp4 存在时的接管路径由实机验证。
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Rendering
{
    [TestFixture]
    public class VideoScreenSystemTests
    {
        [Test]
        public void Apply_MissingVideoFile_MaterialUntouched()
        {
            // EditMode 下 StreamingAssets/video 无 mp4 → Apply 应早退不改材质、只告警
            var go = new GameObject("vss");
            try
            {
                var vss = go.AddComponent<VideoScreenSystem>();
                // mp4 已真入库（av W3-14）——「缺失分支」经测试注入口确定性驱动，
                // 不再依赖「测试机恰好没有这个文件」的巧合
                vss.VideoFilePathOverride = System.IO.Path.Combine(
                    Application.streamingAssetsPath, "video", "definitely-missing-test.mp4");
                // 注册表从 streamingAssetsPath/blocks 加载（照 BlockDefinitionFilesTests 的 SetUp 模式）
                string blocksDir = System.IO.Path.Combine(
                    Application.streamingAssetsPath, "blocks");
                var registry = BlockRegistryLoader.Load();
                var library = BlockMaterialLibrary.Load(registry,
                    System.IO.Path.Combine(blocksDir, "textures"));

                int slot = -1;
                var names = registry.TextureNames;
                for (int i = 0; i < names.Count; i++)
                {
                    if (names[i] == VideoScreenSystem.ScreenTextureName) { slot = i; break; }
                }
                Assert.That(slot, Is.GreaterThanOrEqualTo(0),
                    "laptop_block.json 拆面后注册表应含 laptop-screen 贴图槽");

                var before = library.Get(slot).mainTexture;
                Assert.DoesNotThrow(() => vss.Apply(library, registry),
                    "mp4 缺失不应抛");
                Assert.That(library.Get(slot).mainTexture, Is.EqualTo(before),
                    "mp4 缺失不改材质（保持占位）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif