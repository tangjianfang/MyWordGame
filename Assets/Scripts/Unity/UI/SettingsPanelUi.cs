using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// m8 B1：设置面板公共组件——三滑条（鼠标灵敏度 / 音量 / FOV）+ PlayerPrefs 持久化，
    /// 从 HelpMenuUi 的设置页平移而来（m6 B3 原实现），<b>行为零变化</b>：
    /// 三键名 / 量程 / 默认值 / 滑完即时生效语义原样。
    /// <para>
    /// av W1-7 追加第四滑条「音乐音量」：只管 BGM + 环境两类 AudioSource，音效走 AudioListener
    /// 全局音量不变。归一 0–1 写进 <see cref="MyWorld.Unity.Audio.MusicVolumeBus"/>。
    /// </para>
    /// <para>
    /// m11 W3-5 追加「和平模式」开关（布尔，非滑条）：敌对生物不刷新 + 死亡不掉落。
    /// 状态真源是 <see cref="PeaceMode"/>（PlayerPrefs 键 PeacefulMode + 静态缓存），
    /// 本面板只做读回与写入（<see cref="PeaceMode.SetEnabled"/>），玩法侧只读
    /// <see cref="PeaceMode.Enabled"/>。
    /// </para>
    /// <para>
    /// 本组件自己不开窗口、不响应按键——宿主（HelpMenuUi 的「设置」页、m8 B2 的暂停菜单）
    /// 在自己的 OnGUI 里调 <see cref="DrawPanel"/> 把它画进自己的面板区域，多处共用同一份代码。
    /// </para>
    /// </summary>
    public sealed class SettingsPanelUi : MonoBehaviour
    {
        // ─── PlayerPrefs 四键（键名是全局硬约束，改了旧档读不回） ───────────────
        // m11 W3-5：第五键「和平模式」不在这里——键名/读写全归 PeaceMode 单一真源
        public const string SensitivityKey = "m6.sensitivity";
        public const string VolumeKey = "m6.volume";
        public const string FovKey = "m6.fov";
        public const string MusicVolumeKey = "m6.musicVolume";  // av W1-7：仅 BGM/环境

        // ─── 量程（range 与默认值同样是全局硬约束） ────────────────────────────
        public const float SensitivityMin = 0.5f;
        public const float SensitivityMax = 2.0f;
        public const float SensitivityDefault = 1.0f;
        public const float VolumeMin = 0f;
        public const float VolumeMax = 100f;
        public const float VolumeDefault = 80f;
        public const float FovMin = 60f;
        public const float FovMax = 90f;
        public const float FovDefault = 70f;
        // av W1-7：音乐音量默认 80（与全局音量同步——绝大多数玩家的预期）
        public const float MusicDefault = 80f;

        // 当前设置值（Awake 从 PlayerPrefs 读回；滑条拖动时更新并即时生效）
        public float CurrentSensitivity { get; private set; }
        public float CurrentVolume { get; private set; }
        public float CurrentFov { get; private set; }
        public float CurrentMusicVolume { get; private set; }  // av W1-7

        /// <summary>m11 W3-5：和平模式开关当前态（Awake 经 <see cref="PeaceMode.Enabled"/>
        /// 读回；点击开关时经 <see cref="PeaceMode.SetEnabled"/> 写回并即时生效）。</summary>
        public bool CurrentPeacefulMode { get; private set; }

        /// <summary>m13 W2：宝宝模式开关当前态（Awake 经
        /// <see cref="DifficultyModeBridge.LoadAndApply"/> 同步：先盘后 Core 缓存；
        /// 点击 toggle 时经 <see cref="DifficultyModeBridge.Apply"/> 同时写盘 + 写 Core，
        /// 切难度立即生效，不改存档，详情见 <see cref="DifficultyModeBridge"/> 注释）。</summary>
        public bool CurrentBabyMode { get; private set; }

        private PlayerController _player;

        // ─── PlayerPrefs 封装（三设置同构：Load 带默认值 / Save 钳到量程） ────────

        public static float LoadSensitivity() =>
            PlayerPrefs.GetFloat(SensitivityKey, SensitivityDefault);

        public static void SaveSensitivity(float value) =>
            PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, SensitivityMin, SensitivityMax));

        public static float LoadVolume() =>
            PlayerPrefs.GetFloat(VolumeKey, VolumeDefault);

        public static void SaveVolume(float value) =>
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp(value, VolumeMin, VolumeMax));

        public static float LoadFov() =>
            PlayerPrefs.GetFloat(FovKey, FovDefault);

        public static void SaveFov(float value) =>
            PlayerPrefs.SetFloat(FovKey, Mathf.Clamp(value, FovMin, FovMax));

        // ─── av W1-7：音乐音量（同 Load/Save 同构，量程复用 VolumeMin/Max） ────

        public static float LoadMusicVolume() =>
            PlayerPrefs.GetFloat(MusicVolumeKey, MusicDefault);

        public static void SaveMusicVolume(float value) =>
            PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp(value, VolumeMin, VolumeMax));

        private void Awake()
        {
            CurrentSensitivity = LoadSensitivity();
            CurrentVolume = LoadVolume();
            CurrentFov = LoadFov();
            CurrentMusicVolume = LoadMusicVolume();  // av W1-7
            CurrentPeacefulMode = PeaceMode.Enabled; // m11 W3-5
            CurrentBabyMode = DifficultyModeBridge.LoadFromPrefs(); // m13 W2：仅读盘，Core 缓存由 WorldBootstrap.Awake 统一灌入（这里只是 UI 显隐）
            ApplySettings();
        }

        /// <summary>把四设置应用到实际系统：音量乘 AudioListener.volume（0–1 归一）、
        /// FOV 写 Camera.main.fieldOfView、灵敏度写 PlayerController 鼠标乘数、
        /// 音乐音量写 <see cref="MyWorld.Unity.Audio.MusicVolumeBus"/>（av W1-7）。
        /// Camera.main / PlayerController 拿不到时静默跳过（EditMode / 无相机场景照常工作）。</summary>
        private void ApplySettings()
        {
            AudioListener.volume = CurrentVolume / VolumeMax;
            MyWorld.Unity.Audio.MusicVolumeBus.Volume = CurrentMusicVolume / VolumeMax;  // av W1-7
            var cam = Camera.main;
            if (cam != null) cam.fieldOfView = CurrentFov;
            if (_player == null) _player = FindObjectOfType<PlayerController>();
            if (_player != null) _player.LookSensitivityMultiplier = CurrentSensitivity;
        }

        // ─── 绘制（宿主 OnGUI 内调；布局平移自 HelpMenuUi.DrawSettings 前半） ────

        /// <summary>面板内容总高度（px）：四行滑条各 70 + 和平模式开关行 70 + 宝宝难度行 70 + 提示行 20 = 440。
        /// 宿主按它在自己的面板里预留区域，并在 <see cref="Rect.yMax"/> 之下排列
        /// 后续内容（HelpMenuUi 的「保存并退出」按钮就是这么接在下面的）。
        /// m13 W2：370 → 440（追加宝宝难度行 +70）。</summary>
        public const float PanelHeight = 440f;  // m11 W3-5：300→370（和平 +70）；m13 W2：370→440（宝宝 +70）

        /// <summary>把四滑条 + 和平模式开关 + 「改动立即生效」提示行画进 <paramref name="area"/>
        /// （宿主 OnGUI 内调）。拖动任一滑条即写 PlayerPrefs 并即时生效——
        /// 与平移前的内联实现逐像素同布局：area.x / area.width 对应原 bg.x+24 / bg.width-48。</summary>
        public void DrawPanel(Rect area)
        {
            float y = area.y;

            // 灵敏度 0.5–2.0
            GUI.Label(new Rect(area.x, y, 660, 22),
                $"鼠标灵敏度：{CurrentSensitivity:0.00}（0.5 慢 – 2.0 快）", ItemSlotDrawer.WhiteStyle());
            float sens = GUI.HorizontalSlider(
                new Rect(area.x, y + 26, area.width, 20), CurrentSensitivity, SensitivityMin, SensitivityMax);
            if (!Mathf.Approximately(sens, CurrentSensitivity))
            {
                CurrentSensitivity = sens;
                SaveSensitivity(sens);
                ApplySettings();
            }
            y += 70;

            // 音量 0–100
            GUI.Label(new Rect(area.x, y, 660, 22),
                $"音量：{CurrentVolume:0}（0 静音 – 100 最大）", ItemSlotDrawer.WhiteStyle());
            float vol = GUI.HorizontalSlider(
                new Rect(area.x, y + 26, area.width, 20), CurrentVolume, VolumeMin, VolumeMax);
            if (!Mathf.Approximately(vol, CurrentVolume))
            {
                CurrentVolume = vol;
                SaveVolume(vol);
                ApplySettings();
            }
            y += 70;

            // FOV 60–90
            GUI.Label(new Rect(area.x, y, 660, 22),
                $"视野（FOV）：{CurrentFov:0}（60 窄 – 90 宽）", ItemSlotDrawer.WhiteStyle());
            float fov = GUI.HorizontalSlider(
                new Rect(area.x, y + 26, area.width, 20), CurrentFov, FovMin, FovMax);
            if (!Mathf.Approximately(fov, CurrentFov))
            {
                CurrentFov = fov;
                SaveFov(fov);
                ApplySettings();
            }
            y += 70;

            // 音乐音量 0–100（av W1-7：只管 BGM + 环境两类 AudioSource）
            GUI.Label(new Rect(area.x, y, 660, 22),
                $"音乐音量：{CurrentMusicVolume:0}（0 静音 – 100 最大，只管背景音乐与环境音）", ItemSlotDrawer.WhiteStyle());
            float music = GUI.HorizontalSlider(
                new Rect(area.x, y + 26, area.width, 20), CurrentMusicVolume, VolumeMin, VolumeMax);
            if (!Mathf.Approximately(music, CurrentMusicVolume))
            {
                CurrentMusicVolume = music;
                SaveMusicVolume(music);
                ApplySettings();
            }
            y += 70;

            // 和平模式开关（m11 W3-5）：布尔开关复用同一 70px 行节奏（标签行 22 +
            // 控件行）。点击即经 PeaceMode.SetEnabled 写 PlayerPrefs + 缓存，即时生效；
            // 无「拖动中」中间态，不需要 ApplySettings（玩法侧直读 PeaceMode.Enabled）
            GUI.Label(new Rect(area.x, y, 660, 22),
                "和平模式：敌对生物不再生成，死亡不掉落物品", ItemSlotDrawer.WhiteStyle());
            bool peace = GUI.Toggle(new Rect(area.x, y + 26, 200, 24), CurrentPeacefulMode, " 开启");
            if (peace != CurrentPeacefulMode)
            {
                CurrentPeacefulMode = peace;
                PeaceMode.SetEnabled(peace);
            }
            y += 70;

            // m13 W2：宝宝模式难度（仿和平模式同款 70px 行节奏，紧贴和平模式之后）。
            // 点击 toggle 经 DifficultyModeBridge.Apply 同时写 PlayerPrefs + Core 缓存，
            // MobAI.TakeHit 入口处即时读到新值——切难度不留半截状态，不改存档（键独立于
            // level.dat，进退出世界不变）。孩子档默认关（普通），与 PeaceMode 同款契约。
            GUI.Label(new Rect(area.x, y, 660, 22),
                "难度：宝宝模式（怪 1 血 / 一击必杀）/ 普通（现值）", ItemSlotDrawer.WhiteStyle());
            bool baby = GUI.Toggle(new Rect(area.x, y + 26, 200, 24), CurrentBabyMode, " 宝宝模式");
            if (baby != CurrentBabyMode)
            {
                CurrentBabyMode = baby;
                DifficultyModeBridge.Apply(baby); // 先盘后缓存，崩在任意一步都有合理回退
            }

            y += 70;  // m13 W2：六行内容 70×6 + 提示行 20 = 440
            GUI.Label(new Rect(area.x, y, 660, 20),
                "设置改动立即生效并自动保存。", ItemSlotDrawer.WhiteStyle());
        }
    }
}
