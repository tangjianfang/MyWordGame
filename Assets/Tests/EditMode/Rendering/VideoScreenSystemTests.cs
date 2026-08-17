#if UNITY_EDITOR
// av W2-11：VideoScreenSystem EditMode 测试。
//   - EditMode 下 StreamingAssets/video 不存在 → Apply 应早退、不改材质、只告警
//   - 注册表无 laptop-screen 贴图槽 → 告警不改材质
//
// 注意：本测试 EditMode 不能真起 VideoPlayer（缺文件 + 没有图像资源），只验证
// 缺失分支；mp4 存在时的接管路径由实机验证。
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
                // 注册表从 streamingAssetsPath/blocks 加载（照 BlockDefinitionFilesTests 的 SetUp 模式）
                string blocksDir = System.IO.Path.Combine(
                    Application.streamingAssetsPath, "blocks");
                var registry = MyWorld.Core.Blocks.BlockRegistryLoader.Load();
                var library = BlockMaterialLibrary.Load(registry,
                    System.IO.Path.Combine(blocksDir, "textures"));

                int slot = System.Array.IndexOf(registry.TextureNames, VideoScreenSystem.ScreenTextureName);
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