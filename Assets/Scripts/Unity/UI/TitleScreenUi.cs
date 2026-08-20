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
        /// <summary>启动菜单视频可选项（StreamingAssets/video 下文件名顺序固定）。</summary>
        /// <remarks>
        /// av W2-12 原本只有 1 个 menu-bg.mp4；m13 扩展为 4 项供玩家挑选氛围：
        /// 0 = 白天原版（menu-bg） / 1 = 夜空极光突发 / 2 = 暴风雪极光 / 3 = 矿道破晓。
        /// 4 项全部由 generate_media.py --videos + art/requests/video/videos/*.md 出，
        /// 缺失的项自动回退到第 0 项；全缺回退深蓝纯色。
        /// </remarks>
        public static readonly string[] VideoOptions =
        {
            "menu-bg.mp4",
            "aurora-burst.mp4",
            "aurora-storm.mp4",
            "minecraft-dawn.mp4",
        };

        /// <summary>4 个选项的短标签（OnGUI 按钮文字）。</summary>
        public static readonly string[] VideoLabels =
        {
            "白天原版",
            "极光突发",
            "暴风极光",
            "矿道破晓",
        };

        public const int DefaultVideoIndex = 0;
        private const string PrefKeyVideoIndex = "TitleScreenUi.VideoIndex";

        public bool IsVisible { get; private set; } = true;

        private int _selectedIndex;
        private RenderTexture _rt;
        private VideoPlayer _player;
        private Texture2D _fallback;
        private bool _warned;

        // 缓存 GUI 样式（IMGUI 风格按需 GC alloc，避免每帧 new GUIStyle）
        private GUIStyle _bigStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _videoPickStyle;

        private void Awake()
        {
            IsVisible = true;
            MyWorld.Unity.Player.BlockInteraction.InputLocked = true;
            MyWorld.Unity.UI.UiCursorGate.Open();
            _fallback = MakeFallbackTexture();
            _selectedIndex = LoadSelectedIndex();
            LoadVideo(_selectedIndex);
        }

        /// <summary>从 PlayerPrefs 读上次选项（越界/缺失回 DefaultVideoIndex）。</summary>
        private static int LoadSelectedIndex()
        {
            int idx = PlayerPrefs.GetInt(PrefKeyVideoIndex, DefaultVideoIndex);
            if (idx < 0 || idx >= VideoOptions.Length) idx = DefaultVideoIndex;
            return idx;
        }

        /// <summary>切到第 <paramref name="index"/> 个视频并立即开播（仅切 URL，不重建 player）。</summary>
        public void SelectVideo(int index)
        {
            if (index < 0 || index >= VideoOptions.Length) return;
            if (index == _selectedIndex && _player != null) return;
            _selectedIndex = index;
            PlayerPrefs.SetInt(PrefKeyVideoIndex, index);
            PlayerPrefs.Save();
            LoadVideo(index);
        }

        /// <summary>实际打开指定视频。mp4 缺失 → 回退到第 0 项；全缺用纯色。</summary>
        private void LoadVideo(int index)
        {
            // 先看请求的 index，找不到就在 [0, index) 区间内找第一个存在的 mp4
            int resolved = ResolveVideoIndex(index);
            if (resolved < 0)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning(
                        $"[TitleScreenUi] 视频 4 项全缺，回退纯色（目录: " +
                        $"{Path.Combine(Application.streamingAssetsPath, "video")}）");
                }
                return;
            }
            _selectedIndex = resolved;  // 实际生效的（player 点 alt 全缺时回到 default）
            string path = Path.Combine(
                Application.streamingAssetsPath, "video", VideoOptions[resolved]);
            EnsurePlayer();
            _player.Stop();
            _player.url = path;
            _player.Play();
        }

        private static int ResolveVideoIndex(int requested)
        {
            if (VideoFileExists(requested)) return requested;
            for (int i = 0; i < VideoOptions.Length; i++)
            {
                if (i == requested) continue;
                if (VideoFileExists(i)) return i;
            }
            return -1;
        }

        private static bool VideoFileExists(int index)
        {
            string path = Path.Combine(
                Application.streamingAssetsPath, "video", VideoOptions[index]);
            return File.Exists(path);
        }

        private void EnsurePlayer()
        {
            if (_player != null && _rt != null) return;
            if (_rt == null)
            {
                // 2026-08-19 视频分辨率升级（MiniMax 6s 1080P 上限）：RT 同步升到 1920×1080，
                // 避免源视频 1920×1080 → RT 1280×720 降采样造成 "2K/4K 显示器上看着糊"。
                _rt = new RenderTexture(1920, 1080, 0) { filterMode = FilterMode.Bilinear };
                _rt.Create();
            }
            if (_player == null)
            {
                _player = gameObject.AddComponent<VideoPlayer>();
                _player.renderMode = VideoRenderMode.RenderTexture;
                _player.targetTexture = _rt;
                _player.isLooping = true;
                _player.audioOutputMode = VideoAudioOutputMode.None;  // 菜单曲由 BgmAudioSystem 管
            }
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

            // 4 个视频选项（一行 4 个按钮，宽 110px 间隔 8px，居中）
            float btnW = 110f, btnH = 36f, gap = 8f;
            float totalW = VideoOptions.Length * btnW + (VideoOptions.Length - 1) * gap;
            float startX = Screen.width / 2f - totalW / 2f;
            float pickY = Screen.height * 0.45f;
            for (int i = 0; i < VideoOptions.Length; i++)
            {
                var rect = new Rect(startX + i * (btnW + gap), pickY, btnW, btnH);
                bool picked = i == _selectedIndex;
                GUI.color = picked ? new Color(1f, 0.85f, 0.3f) : Color.white;
                if (GUI.Button(rect, VideoLabels[i], _videoPickStyle))
                {
                    SelectVideo(i);
                }
            }
            GUI.color = Color.white;

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
            if (_videoPickStyle == null)
            {
                _videoPickStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            }
        }
    }
}