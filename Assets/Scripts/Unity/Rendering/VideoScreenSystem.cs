using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// av W2-11：笔记本 / 黑客电脑方块屏幕视频——把 laptop-screen 贴图槽的共享材质
    /// <c>mainTexture</c> 换成 <see cref="VideoPlayer"/> 的 RenderTexture（材质按贴图名
    /// 共享——全世界同款播同一段，贪心网格零改动）。
    /// <para>
    /// mp4 缺失 / 贴图槽缺失 → 告警一次并保持原贴图（占位图仍可见）。
    /// EditMode / 无 Resources 目录也可调——缺失路径只告警不抛。
    /// </para>
    /// </summary>
    public sealed class VideoScreenSystem : MonoBehaviour
    {
        /// <summary>方块顶面贴图名——与 laptop_block.json / hacker_pc_block.json 的 top 一致。</summary>
        public const string ScreenTextureName = "laptop-screen";

        /// <summary>StreamingAssets 下的 mp4 文件名（由 generate_media --videos 出）。</summary>
        public const string VideoFileName = "laptop-loop.mp4";

        private bool _warned;

        /// <summary>
        /// 屏幕贴图接管：找到 <see cref="ScreenTextureName"/> 贴图槽、把它的材质 mainTexture
        /// 换成 VideoPlayer 的 RenderTexture。EditMode 下无 mp4 → 告警一次并早退。
        /// </summary>
        public void Apply(BlockMaterialLibrary library, MyWorld.Core.Blocks.BlockRegistry registry)
        {
            int slot = -1;
            var names = registry.TextureNames;
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] == ScreenTextureName) { slot = i; break; }
            }
            if (slot < 0)
            {
                Warn($"注册表无贴图槽 {ScreenTextureName}（laptop_block.json 顶面应引用）");
                return;
            }
            string path = Path.Combine(Application.streamingAssetsPath, "video", VideoFileName);
            if (!File.Exists(path))
            {
                Warn($"视频缺失: {path}（保持占位贴图）");
                return;
            }

            // 320×180 屏幕 RT（16:9），点采样保持像素艺术风格
            var rt = new RenderTexture(320, 180, 0) { filterMode = FilterMode.Point };
            rt.Create();
            var player = gameObject.AddComponent<VideoPlayer>();
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = rt;
            player.url = path;
            player.isLooping = true;
            player.audioOutputMode = VideoAudioOutputMode.None;  // 静音；菜单 / 屏音由 BGM/环境管
            player.Play();
            library.Get(slot).mainTexture = rt;
        }

        private void Warn(string msg)
        {
            if (_warned) return;
            _warned = true;
            Debug.LogWarning($"[VideoScreenSystem] {msg}");
        }
    }
}