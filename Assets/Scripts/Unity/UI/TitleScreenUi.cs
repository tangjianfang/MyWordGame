using System.IO;
using MyWorld.Core.Persistence;
using UnityEngine;
using UnityEngine.Video;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 主菜单（开局遮罩，av W2-12 起）——全屏视频背景 + 标题 + 三态世界管理（m12 P1）。
    /// <para>
    /// WorldBootstrap 步骤序不动——本组件只在最上层遮罩，
    /// <see cref="StartGame"/> 销毁遮罩解锁输入（与 Esc 暂停菜单共用
    /// <see cref="MyWorld.Unity.Player.BlockInteraction.InputLocked"/> +
    /// <see cref="UiCursorGate"/> 指针门，不动 timeScale：底下世界加载不是暂停）。
    /// </para>
    /// <para>
    /// <b>m12 P1 三态</b>（根因 3「主菜单无世界管理」修复）：
    /// 主菜单（继续上次 / 新世界 / 世界列表 / 退出）↔ 新世界（种子输入 + 随机）↔
    /// 世界列表（seed + 日期，进入 / 两段确认删除）。选中的种子 ≠ 当前种子时走
    /// <see cref="MyWorld.Unity.Bootstrap.WorldBootstrap.RequestWorldSwitch"/>（场景重载
    /// + 种子握手），等于当前种子直接 <see cref="StartGame"/>。世界编目走 Core
    /// <see cref="WorldCatalog"/>（纯 System.IO，双链同源可测）。
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

        /// <summary>m12 P1：换世界场景重载后不再弹菜单，直接进游戏
        ///（<see cref="MyWorld.Unity.Bootstrap.WorldBootstrap.RequestWorldSwitch"/> 写入）。</summary>
        public static bool SkipMenuNextLoad;

        public bool IsVisible { get; private set; } = true;

        /// <summary>菜单模式（m12 P1 三态；EditMode 断言当前态）。</summary>
        private enum MenuMode
        {
            Main,
            NewWorld,
            WorldList,
        }

        private MenuMode _mode = MenuMode.Main;
        private string _seedInput = "";
        private string _seedError;
        private long _deleteArmedSeed;
        private bool _deleteArmed;
        /// <summary>评审 05 T-B2：删除失败的提示文案（目录被杀毒/备份占用等）——
        /// 此前 TryDelete 的失败被静默吞掉，孩子点删除「没反应」没有任何解释。</summary>
        private string _deleteError;
        private System.Collections.Generic.List<WorldCatalog.Entry> _worlds;

        private int _selectedIndex;
        private RenderTexture _rt;
        private VideoPlayer _player;
        private Texture2D _fallback;
        private bool _warned;

        // 缓存 GUI 样式（IMGUI 风格按需 GC alloc，避免每帧 new GUIStyle）
        private GUIStyle _bigStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _videoPickStyle;
        private GUIStyle _inputStyle;
        private GUIStyle _labelStyle;

        private void Awake()
        {
            // m12 P1：换世界重载后跳过菜单直接进——视频都不用建，最短路径到可玩
            if (SkipMenuNextLoad)
            {
                SkipMenuNextLoad = false;
                IsVisible = false;
                MyWorld.Unity.Player.BlockInteraction.InputLocked = false;
                MyWorld.Unity.UI.UiCursorGate.Reset();
                FindObjectOfType<MyWorld.Unity.Audio.BgmAudioSystem>()?.StartGame();
                return;
            }

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

        /// <summary>开始游戏（UiScreenshotOnArg + 各进入路径都调）。</summary>
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

        // ─── m12 P1：世界管理三态 ────────────────────────────────────────────────

        /// <summary>「继续上次」的种子：PlayerPrefs 记的最近进入（目录还在才算数），
        /// 缺失/失效回落 WorldCatalog 的 level.dat mtime 最新者；都没有返回 null（按钮灰）。</summary>
        public static long? ResolveContinueSeed(string saveRoot)
        {
            string pref = PlayerPrefs.GetString(
                MyWorld.Unity.Bootstrap.WorldBootstrap.PrefKeyLastWorldSeed, "");
            if (WorldCatalog.TryParseSeed(pref, out long prefSeed)
                && Directory.Exists(Path.Combine(saveRoot, prefSeed.ToString())))
            {
                return prefSeed;
            }

            var latest = WorldCatalog.Latest(saveRoot);
            return latest?.Seed;
        }

        /// <summary>m12 P1：进入指定种子的世界——记 PlayerPrefs，当前种子已在本场景
        /// 跑着就直接 StartGame，否则整场景重载换种子（见 WorldBootstrap.RequestWorldSwitch）。</summary>
        private void EnterWorld(long seedValue)
        {
            PlayerPrefs.SetString(
                MyWorld.Unity.Bootstrap.WorldBootstrap.PrefKeyLastWorldSeed,
                seedValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();

            var bootstrap = FindObjectOfType<MyWorld.Unity.Bootstrap.WorldBootstrap>();
            if (bootstrap != null && bootstrap.CurrentSeed == seedValue)
            {
                StartGame(); // 已是这个种子在跑（默认 42 首次直进，省一次重载）
                return;
            }

            MyWorld.Unity.Bootstrap.WorldBootstrap.RequestWorldSwitch(seedValue);
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
                new Rect(Screen.width / 2f - 300, Screen.height * 0.16f, 600, 80),
                "MyWordGame", _bigStyle);

            // 4 个视频选项（一行 4 个按钮，宽 110px 间隔 8px，居中）
            float btnW = 110f, btnH = 36f, gap = 8f;
            float totalW = VideoOptions.Length * btnW + (VideoOptions.Length - 1) * gap;
            float startX = Screen.width / 2f - totalW / 2f;
            float pickY = Screen.height * 0.36f;
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

            // m12 P1：三态按钮区（换掉 av 时代的「开始游戏 / 退出」两按钮）
            switch (_mode)
            {
                case MenuMode.NewWorld: DrawNewWorldPanel(); break;
                case MenuMode.WorldList: DrawWorldListPanel(); break;
                default: DrawMainPanel(); break;
            }
        }

        private void DrawMainPanel()
        {
            float cx = Screen.width / 2f;
            float y = Screen.height * 0.48f;

            // 继续上次：有可续的世界才亮（种子进按钮文案，孩子知道续的是哪个）
            long? continueSeed = ResolveContinueSeed(
                MyWorld.Unity.Bootstrap.WorldBootstrap.SaveRoot);
            bool canContinue = continueSeed.HasValue;
            GUI.enabled = canContinue;
            if (GUI.Button(new Rect(cx - 130, y, 260, 44),
                    canContinue ? $"继续上次（种子 {continueSeed.Value}）" : "继续上次（还没有世界）",
                    _buttonStyle))
            {
                EnterWorld(continueSeed.Value);
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(cx - 130, y + 52, 260, 44), "新世界", _buttonStyle))
            {
                _mode = MenuMode.NewWorld;
                _seedInput = "";
                _seedError = null;
            }

            if (GUI.Button(new Rect(cx - 130, y + 104, 260, 44), "世界列表", _buttonStyle))
            {
                _mode = MenuMode.WorldList;
                _worlds = WorldCatalog.List(
                    MyWorld.Unity.Bootstrap.WorldBootstrap.SaveRoot);
                _deleteArmed = false;
                _deleteError = null;
            }

            if (GUI.Button(new Rect(cx - 130, y + 156, 260, 44), "退出", _buttonStyle))
            {
                Application.Quit();
            }
        }

        private void DrawNewWorldPanel()
        {
            float cx = Screen.width / 2f;
            float y = Screen.height * 0.48f;

            GUI.Label(new Rect(cx - 130, y, 260, 26),
                "输入种子（整数，相同种子 = 相同世界）", _labelStyle);
            _seedInput = GUI.TextField(new Rect(cx - 130, y + 30, 180, 36),
                _seedInput, 20, _inputStyle);

            if (GUI.Button(new Rect(cx + 58, y + 30, 72, 36), "随机", _buttonStyle))
            {
                // 菜单侧掷骰不属于世界生成确定性范畴（种子定了世界照样可复现）
                _seedInput = UnityEngine.Random.Range(int.MinValue, int.MaxValue).ToString();
                _seedError = null;
            }

            if (_seedError != null)
            {
                GUI.color = new Color(1f, 0.45f, 0.4f);
                GUI.Label(new Rect(cx - 130, y + 72, 260, 24), _seedError, _labelStyle);
                GUI.color = Color.white;
            }

            if (GUI.Button(new Rect(cx - 130, y + 100, 260, 44), "创建并进入", _buttonStyle))
            {
                if (WorldCatalog.TryParseSeed(_seedInput, out long newSeed))
                {
                    // 同种子的世界已存在也不拦——同种子本来就是同一个世界，直接进去续玩
                    EnterWorld(newSeed);
                }
                else
                {
                    _seedError = string.IsNullOrWhiteSpace(_seedInput)
                        ? "请输入种子（或点「随机」）"
                        : "种子只能是整数（允许负号）";
                }
            }

            if (GUI.Button(new Rect(cx - 130, y + 152, 260, 44), "返回", _buttonStyle))
            {
                _mode = MenuMode.Main;
            }
        }

        private void DrawWorldListPanel()
        {
            float cx = Screen.width / 2f;
            float y = Screen.height * 0.46f;

            // 评审 05 T-B2：删除失败提示画在列表正上方（同 _seedError 的展示模式）
            if (!string.IsNullOrEmpty(_deleteError))
            {
                GUI.Label(new Rect(cx - 230, y - 32, 460, 26), _deleteError, _labelStyle);
            }

            if (_worlds == null || _worlds.Count == 0)
            {
                GUI.Label(new Rect(cx - 200, y, 400, 30),
                    "还没有世界——回主菜单点「新世界」建一个", _labelStyle);
            }
            else
            {
                // 最多显示 8 行（最近玩的在前，List 已按 mtime 降序）
                int rows = System.Math.Min(_worlds.Count, 8);
                for (int i = 0; i < rows; i++)
                {
                    var entry = _worlds[i];
                    float rowY = y + i * 40f;
                    string label = $"世界 · 种子 {entry.Seed} · " +
                        entry.LastPlayedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                    GUI.Label(new Rect(cx - 230, rowY, 280, 32), label, _labelStyle);

                    if (GUI.Button(new Rect(cx + 56, rowY - 2, 64, 32), "进入", _buttonStyle))
                    {
                        EnterWorld(entry.Seed);
                    }

                    // 两段确认删除：第一次点变红「确认?」，再点才真删（回收站式改名）
                    bool armed = _deleteArmed && _deleteArmedSeed == entry.Seed;
                    GUI.color = armed ? new Color(1f, 0.4f, 0.35f) : Color.white;
                    if (GUI.Button(new Rect(cx + 126, rowY - 2, armed ? 84 : 64, 32),
                            armed ? "确认删除?" : "删除", _buttonStyle))
                    {
                        if (armed)
                        {
                            // 评审 05 T-B2：删除失败（目录被杀毒/备份占用）要有可见反馈，
                            // 不再静默吞——孩子点删除「没反应」是可预防的挫败点
                            _deleteError = WorldCatalog.TryDelete(
                                MyWorld.Unity.Bootstrap.WorldBootstrap.SaveRoot,
                                entry.Seed, out _)
                                ? null
                                : "删除失败：世界文件被其它程序占用（杀毒/备份），请稍后再试";
                            _worlds = WorldCatalog.List(
                                MyWorld.Unity.Bootstrap.WorldBootstrap.SaveRoot);
                            _deleteArmed = false;
                        }
                        else
                        {
                            _deleteArmed = true;
                            _deleteArmedSeed = entry.Seed;
                        }
                    }
                    GUI.color = Color.white;
                }
            }

            if (GUI.Button(new Rect(cx - 130, y + 340, 260, 44), "返回", _buttonStyle))
            {
                _mode = MenuMode.Main;
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
            if (_inputStyle == null)
            {
                _inputStyle = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter,
                };
            }
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white },
                };
            }
        }
    }
}
