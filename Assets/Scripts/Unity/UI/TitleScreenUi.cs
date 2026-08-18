using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// av W2-12：极简主菜单（开局遮罩）——全屏视频背景 + 标题 + 开始 / 退出两按钮。
    /// <para>
    /// WorldBootstrap 步骤序不动——本组件只在最上层遮罩，
    /// <see cref="StartGame"/> 销毁遮罩解锁输入（与 Esc 暂停菜单共用
    /// <see cref="MyWorld.Unity.Player.BlockInteraction.InputLocked"/> +
    /// <see cref="UiCursorGate"/> 指针门，不动 timeScale：底下世界加载不是暂停）。
    /// </para>
    /// <para>
    /// 视频缺失回退深蓝色 1×1 纯色（<see cref="MakeFallbackTexture"/>）——
    /// EditMode 无 mp4 是常态，不能阻断 Awake。
    /// </para>
    /// </summary>
    public sealed class TitleScreenUi : MonoBehaviour
    {
        /// <summary>StreamingAssets/video 下的主菜单 mp4 文件名（由 generate_media --videos 出）。</summary>
        public const string VideoFileName = "menu-bg.mp4";

        public bool IsVisible { get; private set; } = true;

        private RenderTexture _rt;
        private VideoPlayer _player;
        private Texture2D _fallback;
        private bool _warned;

        // 缓存 GUI 样式（IMGUI 风格按需 GC alloc，避免每帧 new GUIStyle）
        private GUIStyle _bigStyle;
        private GUIStyle _buttonStyle;

        private void Awake()
        {
            IsVisible = true;
            MyWorld.Unity.Player.BlockInteraction.InputLocked = true;
            MyWorld.Unity.UI.UiCursorGate.Open();
            _fallback = MakeFallbackTexture();
            string path = Path.Combine(Application.streamingAssetsPath, "video", VideoFileName);
            if (!File.Exists(path))
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning($"[TitleScreenUi] 视频缺失: {path}（纯色回退）");
                }
                return;
            }
            // 2026-08-19 视频分辨率升级（MiniMax 6s 1080P 上限）：RT 同步升到 1920×1080，
            // 避免源视频 1920×1080 → RT 1280×720 降采样造成 "2K/4K 显示器上看着糊"。
            // Bilinear 拉伸在 1920×1080 → 4K 显示器时仍平滑，主菜单视频是 GUI.DrawTexture
            // 全屏拉伸渲染——RT 是画质上限的决定点。
            _rt = new RenderTexture(1920, 1080, 0) { filterMode = FilterMode.Bilinear };
            _rt.Create();
            _player = gameObject.AddComponent<VideoPlayer>();
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _rt;
            _player.url = path;
            _player.isLooping = true;
            _player.audioOutputMode = VideoAudioOutputMode.None;  // 菜单曲由 BgmAudioSystem 管
            _player.Play();
        }

        /// <summary>开始游戏（UiScreenshotOnArg + 玩家点「开始」按钮都调）。</summary>
        public void StartGame()
        {
            if (!IsVisible) return;
            IsVisible = false;
            MyWorld.Unity.Player.BlockInteraction.InputLocked = false;
            MyWorld.Unity.UI.UiCursorGate.Close();
            if (_player != null) { _player.Stop(); _player = null; }
            if (_rt != null) { _rt.Release(); _rt = null; }
            // 通知 BGM：menu → day 曲（StartGame 是「菜单 → 进游戏」路径，Audio 接线用）
            FindObjectOfType<MyWorld.Unity.Audio.BgmAudioSystem>()?.StartGame();
        }

        /// <summary>1×1 深蓝纯色回退（mp4 缺失时仍能看出主菜单背景）。</summary>
        private static Texture2D MakeFallbackTexture()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGB24, false);
            tex.SetPixel(0, 0, new Color(0x2C / 255f, 0x4A / 255f, 0x6E / 255f));
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        private void OnGUI()
        {
            if (!IsVisible) return;
            GUI.depth = 100;  // 数值大越后画→盖住 hotbar 等常驻 UI（Unity depth 小先画）

            // 全屏视频 / 纯色背景
            GUI.DrawTexture(
                new Rect(0, 0, Screen.width, Screen.height),
                (Texture)_rt ?? _fallback, ScaleMode.ScaleAndCrop);

            EnsureStyles();

            // 标题
            GUI.Label(
                new Rect(Screen.width / 2f - 300, Screen.height * 0.22f, 600, 80),
                "MyWordGame", _bigStyle);

            // 开始按钮
            if (GUI.Button(
                new Rect(Screen.width / 2f - 110, Screen.height * 0.55f, 220, 46),
                "开始游戏", _buttonStyle))
            {
                StartGame();
            }
            // 退出按钮
            if (GUI.Button(
                new Rect(Screen.width / 2f - 110, Screen.height * 0.55f + 58, 220, 46),
                "退出", _buttonStyle))
            {
                Application.Quit();
            }
        }

        private void EnsureStyles()
        {
            if (_bigStyle == null)
            {
                _bigStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 44,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                };
            }
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            }
        }
    }
}