using System;
using System.IO;
using System.Reflection;
using MyWorld.Unity.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyWorld.Unity.EditorTools
{
    /// <summary>
    /// 批处理截图工具：把场景里 "相机" GameObject 的 Camera 强制渲染到 RenderTexture，
    /// 拷到 Texture2D 落盘 PNG。可被 -executeMethod 无头调用。
    /// </summary>
    public static class ScreenshotCapture
    {
        public const string DefaultScenePath = "Assets/Scenes/Preview.unity";

        public static string DefaultCameraName = "相机";

        [MenuItem("MyWorld/截图：当前场景")]
        public static void CaptureCurrentSceneMenu()
        {
            string path = Path.Combine("Builds", "screenshots",
                $"manual-{DateTime.Now:HHmmss}.png");
            Capture(path);
            Debug.Log($"[ScreenshotCapture] {path}");
        }

        public static void Capture(string outputPath, int width = 1280, int height = 720)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            // 找到主相机（用 tag=MainCamera 更稳，不要靠 GameObject 名）
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[ScreenshotCapture] 场景里没有 MainCamera");
                return;
            }

            // 预渲染：让所有 MonoBehaviour OnEnable + Start + 几帧 Tick 完成。
            // 编辑器非播放态下 Camera.Render() 会触发整个 URP 管线一次。
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            try
            {
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>拍预设 5 张关键场景。要求场景里有 WorldBootstrap 且 bootstrap 流程跑通。</summary>
        public static void CaptureAll(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            // 编辑器非播放态下 WorldBootstrap.Awake/Update 不会自动跑，
            // 先手动驱动 bootstrap（一次 Awake + 若干帧 Update），否则 Camera.Render
            // 拍出来的全是相机背景色，没有任何方块 / UI / 手部。
            DriveBootstrapForCapture();
            Capture(Path.Combine(outputDir, "overworld.png"));
            // TODO（不在本任务范围，由后续任务在 ScreenshotCapture 加更多预设位）
        }

        /// <summary>
        /// 无 args 重载，专门给 <c>-executeMethod</c> 调用：默认输出到 <c>Builds/screenshots</c>。
        /// Unity 的 <c>-executeMethod</c> 不支持带参数的方法签名。
        /// </summary>
        /// <remarks>
        /// -batchmode 下 Unity 不会自动打开任何场景——如果当前没场景，强制打开
        /// <see cref="DefaultScenePath"/>。已加载其他场景则保留原状。
        /// </remarks>
        public static void CaptureAllDefault()
        {
            // 兜底：batchmode 启动后场景列表可能是空的。
            if (!EditorSceneManager.GetSceneByPath(DefaultScenePath).IsValid())
            {
                EditorSceneManager.OpenScene(DefaultScenePath, OpenSceneMode.Single);
            }
            CaptureAll(Path.Combine("Builds", "screenshots"));
        }

        /// <summary>
        /// 编辑器非播放态下驱动 <see cref="WorldBootstrap"/>：先调一次 Awake（注册表 / 世界 /
        /// 材质库 / 流式加载器 / 玩家 / UI 全部初始化），再调若干次 Update（让 ChunkStreamer
        /// 把出生点周围的区块建好）。不依赖 PlayMode，purely reflection on private methods。
        /// </summary>
        private static void DriveBootstrapForCapture()
        {
            const int warmupFrames = 240;
            var bootstrap = UnityEngine.Object.FindObjectOfType<WorldBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[ScreenshotCapture] 场景里没有 WorldBootstrap；截图将只拍相机背景色。");
                return;
            }

            var type = typeof(WorldBootstrap);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var awake = type.GetMethod("Awake", flags);
            var update = type.GetMethod("Update", flags);

            if (awake != null)
            {
                try { awake.Invoke(bootstrap, null); }
                catch (Exception ex) { Debug.LogWarning($"[ScreenshotCapture] bootstrap.Awake 失败: {ex.InnerException?.Message ?? ex.Message}"); }
            }
            else
            {
                Debug.LogWarning("[ScreenshotCapture] WorldBootstrap.Awake 未找到；无法初始化。");
                return;
            }

            if (update != null)
            {
                for (var i = 0; i < warmupFrames; i++)
                {
                    try { update.Invoke(bootstrap, null); }
                    catch (Exception ex) { Debug.LogWarning($"[ScreenshotCapture] bootstrap.Update 失败: {ex.InnerException?.Message ?? ex.Message}"); break; }
                }
            }
        }
    }
}