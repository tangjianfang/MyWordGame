using System;
using System.IO;
using System.Reflection;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyWorld.Unity.EditorTools
{
    // 视觉回归截图工具。
    //
    // ⚠️ 重要：调用方（visual-smoke.sh / build-and-run.sh --with-visual）必须 **不带** `-nographics`，
    // 否则 RTX 2070 Super 会变 Null Device 拍空图（D1 fix 已证）。
    //
    // 用 0-arg `CaptureAllDefault` 重载（Unity -executeMethod 不支持带参）。
    public static class ScreenshotCapture
    {
        public const string DefaultScenePath = "Assets/Scenes/Preview.unity";

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
            string dir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(dir))
            {
                dir = Application.dataPath;  // 默认到 Assets/
                outputPath = Path.Combine(dir, Path.GetFileName(outputPath));
            }
            Directory.CreateDirectory(dir);

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
            Texture2D tex = null;
            try
            {
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            }
            finally
            {
                if (tex != null)
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        [MenuItem("MyWorld/截图：第三人称玩家")]
        public static void CaptureThirdPersonMenu()
        {
            string path = Path.Combine("Builds", "screenshots",
                $"thirdperson-{DateTime.Now:HHmmss}.png");
            CaptureThirdPerson(path);
            Debug.Log($"[ScreenshotCapture] {path}");
        }

        public static void CaptureThirdPerson(string outputPath, int width = 1280, int height = 720)
        {
            string dir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(dir))
            {
                dir = Application.dataPath;  // 默认到 Assets/
                outputPath = Path.Combine(dir, Path.GetFileName(outputPath));
            }
            Directory.CreateDirectory(dir);

            // EditMode 下 AddComponent 不会触发 Awake——手动驱动 PlayerVisual（A 建 6 个 Cube）
            // 和 CameraThirdPerson（A 建子相机），否则 GameObject.Find("ThirdPersonCamera") 找不到东西。
            EditModeAwakeWorkaround();

            var tpCam = GameObject.Find("ThirdPersonCamera");
            if (tpCam == null)
            {
                Debug.LogError("[ScreenshotCapture] 场景里没有 ThirdPersonCamera（需要玩家先 Bootstrap 一次）");
                return;
            }
            var cam = tpCam.GetComponent<Camera>();
            if (cam == null)
            {
                Debug.LogError("[ScreenshotCapture] ThirdPersonCamera 上没有 Camera 组件");
                return;
            }

            // EditMode 下 CameraThirdPerson.Update 不会跑。手动把第三人称相机放到玩家
            // 身后 3 格 + 抬 1.5 格，让 Render 能看到 PlayerVisual 建出来的 6 个 Cube。
            var player = GameObject.Find("玩家");
            if (player != null)
            {
                var playerPos = player.transform.position;
                tpCam.transform.position = playerPos + new Vector3(0f, 1.5f, -3f);
                tpCam.transform.LookAt(playerPos + Vector3.up * 1f);
            }

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            Texture2D tex = null;
            try
            {
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            }
            finally
            {
                if (tex != null)
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        /// <summary>
        /// EditMode 兜底：WorldBootstrap.Awake 里 AddComponent<PlayerVisual> 与
        /// <see cref="MyWorld.Unity.Player.CameraThirdPerson"/> 不会触发各自 Awake，
        /// 反射手动调一次。PlayerVisual.Awake 建 6 个身体 Cube，CameraThirdPerson.Awake
        /// 建 ThirdPersonCamera 子相机。
        /// 重复调用安全：被调的 PlayerVisual.Awake / CameraThirdPerson.Awake 各自内部有"已初始化则 return"守卫，
        /// 所以 CaptureAll → CaptureThirdPerson 连调两次不会双初始化。
        /// </summary>
        private static void EditModeAwakeWorkaround()
        {
            var bootstrapGo = GameObject.Find("玩家");
            if (bootstrapGo == null)
            {
                // 退回按组件找
                var bootstrap = UnityEngine.Object.FindObjectOfType<MyWorld.Unity.Bootstrap.WorldBootstrap>();
                if (bootstrap != null) bootstrapGo = bootstrap.gameObject;
            }
            if (bootstrapGo == null) return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            foreach (var component in bootstrapGo.GetComponents<Component>())
            {
                if (component == null) continue;
                var name = component.GetType().Name;
                if (name == "PlayerVisual" || name == "CameraThirdPerson")
                {
                    var awake = component.GetType().GetMethod("Awake", flags);
                    if (awake == null) continue;
                    try { awake.Invoke(component, null); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ScreenshotCapture] {name}.Awake 失败: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }
        }

        /// <summary>拍预设的 2 张关键场景到 outputDir：overworld.png（主视角）+ third-person.png（第三人称验证玩家身体）。</summary>
        public static void CaptureAll(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            // 编辑器非播放态下 WorldBootstrap.Awake/Update 不会自动跑，
            // 先手动驱动 bootstrap（一次 Awake + 若干帧 Update），否则 Camera.Render
            // 拍出来的全是相机背景色，没有任何方块 / UI / 手部。
            DriveBootstrapForCapture();
            Capture(Path.Combine(outputDir, "overworld.png"));
            CaptureThirdPerson(Path.Combine(outputDir, "third-person.png"));
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

            // EditMode -batchmode 下 Time.deltaTime = 0（captureDeltaTime 只影响 PlayMode 下的 deltaTime），
            // 玩家出生在 (0.5, 120, 0.5) 后永远不下落，BlockInteraction 射线从 (0.5, 121.62, 0.5)
            // 向 -0.5/-0.866 方向射出去后穿过空气层碰不到 y=98 的地形方块，SelectionBox 永远不显示。
            // 临时设 captureDeltaTime = 1/60 让 PlayMode 行为不变（如果之后跑 PlayMode），同时
            // 反射驱动 PlayerController.Tick(input, dt) 显式传 dt=1/60 让 EditMode 下玩家也能落到地面。
            // 不动 PlayerController.cs（保持 PlayMode 行为不变）。
            float prevCaptureDeltaTime = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;

            var bootstrap = UnityEngine.Object.FindObjectOfType<WorldBootstrap>();
            if (bootstrap == null)
            {
                Debug.LogWarning("[ScreenshotCapture] 场景里没有 WorldBootstrap；截图将只拍相机背景色。");
                Time.captureDeltaTime = prevCaptureDeltaTime;
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
                Time.captureDeltaTime = prevCaptureDeltaTime;
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

            // C-tooling：WorldBootstrap.Update × warmupFrames 只驱动了 bootstrap 自己。
            // 额外反射遍历场景里所有 MonoBehaviour 并调各自的 Update，让
            // BlockInteraction.Update（VoxelRaycaster → SelectionBox.ShowAt）、
            // PlayerVisual.Update（走路摆动）、PlayerController.Update（位置/物理）等
            // 运行时对象都能在 EditMode batchmode 截图里出现。
            // 已知不解决的限制：IMGUI 是事件驱动，OnGUI 不会在 Camera.Render 里渲染
            // （HotbarUI / HandController 仍拍不到）。
            // 同时显式 dt=1/60 反射调 PlayerController.Tick 走完重力步进（Time.deltaTime
            // 在 EditMode 是 0，PlayerController.Update 内部 Tick(dt=0) 不会动）。
            DriveAllMonoBehavioursWithPhysics(warmupFrames);

            Time.captureDeltaTime = prevCaptureDeltaTime;
        }

        /// <summary>
        /// 编辑器非播放态下逐帧驱动场景里所有 MonoBehaviour.Update，同时显式 dt=1/60
        /// 反射调 <see cref="PlayerController.Tick"/> 让玩家在 EditMode batchmode 下也能
        /// 落到地面（<see cref="Time.deltaTime"/> 在 EditMode 是 0，PlayerController.Update
        /// 内部 Tick(dt=0) 不会动）。不动 PlayerController.cs。BlockInteraction.Update 在
        /// 玩家落地后能拿到正确的 eye.position，VoxelRaycaster 命中方块 → SelectionBox 显示。
        /// </summary>
        private static void DriveAllMonoBehavioursWithPhysics(int frames)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var components = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(includeInactive: true);
            // 缓存 Update MethodInfo（避免每帧反射查）。排除 PlayerController，
            // 它的物理由 DrivePlayerPhysics 显式 dt=1/60 驱动。
            var updates = new System.Collections.Generic.List<(MonoBehaviour mb, MethodInfo update)>();
            foreach (var mb in components)
            {
                if (mb == null) continue;
                if (mb is PlayerController) continue;
                var update = mb.GetType().GetMethod("Update", flags);
                if (update == null) continue;
                updates.Add((mb, update));
            }

            var player = UnityEngine.Object.FindObjectOfType<PlayerController>();
            var tick = player != null ? typeof(PlayerController).GetMethod("Tick", flags) : null;

            // EditMode 下 AddComponent 不自动回调 Awake，PlayerController.Awake（自动找子 Camera 当 eye）
            // 不会跑；BlockInteraction.Update 看到 _player.Eye == null 就早退，SelectionBox 永不出来。
            // 反射手动调一次 Awake，让 eye 字段在 EditMode 也能正确指向子 Camera。
            if (player != null)
            {
                var playerAwake = typeof(PlayerController).GetMethod("Awake", flags);
                if (playerAwake != null)
                {
                    try { playerAwake.Invoke(player, null); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ScreenshotCapture] PlayerController.Awake 失败: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }

            object noneInput = null;
            if (tick != null)
            {
                var inputType = Type.GetType("MyWorld.Core.Player.PlayerInput, MyWorld.Core");
                if (inputType != null)
                {
                    var noneField = inputType.GetField("None", BindingFlags.Public | BindingFlags.Static);
                    if (noneField != null) noneInput = noneField.GetValue(null);
                }
            }

            const float dt = 1f / 60f;
            for (int frame = 0; frame < frames; frame++)
            {
                // 先让玩家走一步物理（落地），再让其它 MonoBehaviour.Update 拿到最新的 eye.position。
                // BlockInteraction.Update 在玩家落到底以后从新的 eye 位置发射射线才能命中方块。
                if (tick != null && noneInput != null)
                {
                    try { tick.Invoke(player, new[] { noneInput, dt }); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ScreenshotCapture] PlayerController.Tick 失败: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }

                foreach (var (mb, update) in updates)
                {
                    if (mb == null) continue;
                    try { update.Invoke(mb, null); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ScreenshotCapture] {mb.GetType().Name}.Update 失败: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 编辑器非播放态下逐帧驱动场景里所有 MonoBehaviour.Update。
        /// 让 BlockInteraction.Update（VoxelRaycaster → SelectionBox.ShowAt）、
        /// PlayerVisual.Update（走路摆动）、PlayerController.Update（位置/物理）等运行时对象
        /// 都能在 EditMode batchmode 截图里出现。
        /// 异常隔离：单个组件抛错不影响其它组件；用 WorldBootstrap.Update 的同一帧步进。
        /// </summary>
        private static void DriveAllMonoBehaviours(int frames)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var components = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(includeInactive: true);
            // 缓存 Update MethodInfo（避免每帧反射查）
            var updates = new System.Collections.Generic.List<(MonoBehaviour mb, MethodInfo update)>();
            foreach (var mb in components)
            {
                if (mb == null) continue;
                var update = mb.GetType().GetMethod("Update", flags);
                if (update == null) continue;
                updates.Add((mb, update));
            }
            for (int frame = 0; frame < frames; frame++)
            {
                foreach (var (mb, update) in updates)
                {
                    if (mb == null) continue;
                    try { update.Invoke(mb, null); }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ScreenshotCapture] {mb.GetType().Name}.Update 失败: {ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            }
        }
    }
}
