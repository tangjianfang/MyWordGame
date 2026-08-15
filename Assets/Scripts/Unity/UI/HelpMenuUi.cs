using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// m6 B3：H 键帮助菜单。两页 Tab：「怎么玩」（按键表 + 四步玩法 + 「当前目标」占位）
    /// 和「设置」（灵敏度 / 音量 / FOV 三个滑条，PlayerPrefs 持久化，滑完即时生效）。
    /// <para>
    /// 打开期间置 <see cref="Player.BlockInteraction.InputLocked"/> 抑制挖/放——
    /// 菜单里点滑条不应误挖方块。H 或 Esc 关闭。
    /// </para>
    /// <para>
    /// 「当前目标」区域是占位：C5 任务系统就绪后回来接真数据（见 task-B3 brief）。
    /// </para>
    /// </summary>
    public sealed class HelpMenuUi : MonoBehaviour
    {
        // ─── PlayerPrefs 三键（键名是全局硬约束，改了旧档读不回） ─────────────────
        public const string SensitivityKey = "m6.sensitivity";
        public const string VolumeKey = "m6.volume";
        public const string FovKey = "m6.fov";

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

        /// <summary>菜单是否打开。挖/放输入锁与它同步翻转。</summary>
        public bool IsOpen { get; private set; }

        // 当前设置值（Awake 从 PlayerPrefs 读回；滑条拖动时更新并即时生效）
        public float CurrentSensitivity { get; private set; }
        public float CurrentVolume { get; private set; }
        public float CurrentFov { get; private set; }

        private int _tab; // 0 = 怎么玩，1 = 设置
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

        private void Awake()
        {
            CurrentSensitivity = LoadSensitivity();
            CurrentVolume = LoadVolume();
            CurrentFov = LoadFov();
            ApplySettings();
        }

        /// <summary>开关菜单。同步维护 <see cref="Player.BlockInteraction.InputLocked"/>，
        /// 打开时解锁鼠标指针让玩家能拉滑条。</summary>
        public void Toggle()
        {
            IsOpen = !IsOpen;
            BlockInteraction.InputLocked = IsOpen;
            if (IsOpen) Cursor.lockState = CursorLockMode.None;
        }

        /// <summary>按键路由。EditMode 测试手动调它验证 H / Esc 的行为契约，
        /// Update 里轮询到按键后也走同一条路径。</summary>
        public void HandleKey(KeyCode key)
        {
            if (key == KeyCode.H)
            {
                Toggle();
            }
            else if (key == KeyCode.Escape && IsOpen)
            {
                // 只在打开时响应：菜单关着按 Esc 是「解锁鼠标」的既有语义（PlayerController），不要抢
                Toggle();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) HandleKey(KeyCode.H);
            else if (Input.GetKeyDown(KeyCode.Escape)) HandleKey(KeyCode.Escape);
        }

        /// <summary>把三设置应用到实际系统：音量乘 AudioListener.volume（0–1 归一）、
        /// FOV 写 Camera.main.fieldOfView、灵敏度写 PlayerController 鼠标乘数。
        /// Camera.main / PlayerController 拿不到时静默跳过（EditMode / 无相机场景照常工作）。</summary>
        private void ApplySettings()
        {
            AudioListener.volume = CurrentVolume / VolumeMax;
            var cam = Camera.main;
            if (cam != null) cam.fieldOfView = CurrentFov;
            if (_player == null) _player = FindObjectOfType<PlayerController>();
            if (_player != null) _player.LookSensitivityMultiplier = CurrentSensitivity;
        }

        // ─── 绘制 ─────────────────────────────────────────────────────────────

        private static readonly string[] KeyTable =
        {
            "W / A / S / D", "移动",
            "空格", "跳跃",
            "Shift", "下蹲 / 潜行",
            "鼠标移动", "转动视角",
            "鼠标左键", "挖方块（对着方块按）",
            "鼠标右键", "放方块 / 使用物品",
            "数字键 1-9", "选择热键栏物品",
            "鼠标滚轮", "切换热键栏",
            "E", "打开 / 关闭背包（合成）",
            "H", "打开 / 关闭帮助",
            "Esc", "关闭菜单 / 解锁鼠标",
        };

        private static readonly string[] Steps =
        {
            "1. 用左键挖树收集木材，走近地上的掉落物自动捡起",
            "2. 按 E 打开背包，把原木放进 2×2 口袋合成木板",
            "3. 用木板建工作台解锁 3×3 合成，做出更好的工具去挖石头",
            "4. 天黑前用方块搭一个庇护所，夜里小心怪物出没",
        };

        private void OnGUI()
        {
            if (!IsOpen) return;

            const float w = 720f;
            const float h = 560f;
            var bg = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            // 半透明深色背景，同背包（GUI.Box 默认皮肤）
            GUI.Box(bg, GUIContent.none);
            GUI.Label(new Rect(bg.x + 16, bg.y + 10, 400, 22), "帮助（H / Esc 关闭）", ItemSlotDrawer.WhiteStyle());

            // Tab 切换
            _tab = GUI.Toolbar(new Rect(bg.x + 16, bg.y + 38, 240, 28), _tab, new[] { "怎么玩", "设置" });

            if (_tab == 0) DrawHowToPlay(bg);
            else DrawSettings(bg);
        }

        private void DrawHowToPlay(Rect bg)
        {
            float y = bg.y + 78;

            // 按键表：11 行，左列按键名 180px、右列说明
            for (int i = 0; i < KeyTable.Length; i += 2)
            {
                GUI.Label(new Rect(bg.x + 24, y, 180, 22), KeyTable[i], ItemSlotDrawer.WhiteStyle());
                GUI.Label(new Rect(bg.x + 210, y, 460, 22), KeyTable[i + 1], ItemSlotDrawer.WhiteStyle());
                y += 24;
            }

            y += 10;
            GUI.Label(new Rect(bg.x + 24, y, 400, 22), "怎么开始：四步上手", ItemSlotDrawer.WhiteStyle());
            y += 26;
            for (int i = 0; i < Steps.Length; i++)
            {
                GUI.Label(new Rect(bg.x + 24, y, 660, 22), Steps[i], ItemSlotDrawer.WhiteStyle());
                y += 24;
            }

            // 当前目标占位区：C5 任务系统就绪后回来接真数据
            y += 10;
            var goalRect = new Rect(bg.x + 24, y, bg.width - 48, 48);
            GUI.Box(goalRect, GUIContent.none);
            GUI.Label(new Rect(goalRect.x + 10, goalRect.y + 6, goalRect.width - 20, 20),
                "当前目标", ItemSlotDrawer.WhiteStyle());
            GUI.Label(new Rect(goalRect.x + 10, goalRect.y + 26, goalRect.width - 20, 18),
                "（任务系统就绪后这里会显示当前目标）", ItemSlotDrawer.WhiteStyle());
        }

        private void DrawSettings(Rect bg)
        {
            float y = bg.y + 90;

            // 灵敏度 0.5–2.0
            GUI.Label(new Rect(bg.x + 24, y, 660, 22),
                $"鼠标灵敏度：{CurrentSensitivity:0.00}（0.5 慢 – 2.0 快）", ItemSlotDrawer.WhiteStyle());
            float sens = GUI.HorizontalSlider(
                new Rect(bg.x + 24, y + 26, bg.width - 48, 20), CurrentSensitivity, SensitivityMin, SensitivityMax);
            if (!Mathf.Approximately(sens, CurrentSensitivity))
            {
                CurrentSensitivity = sens;
                SaveSensitivity(sens);
                ApplySettings();
            }
            y += 70;

            // 音量 0–100
            GUI.Label(new Rect(bg.x + 24, y, 660, 22),
                $"音量：{CurrentVolume:0}（0 静音 – 100 最大）", ItemSlotDrawer.WhiteStyle());
            float vol = GUI.HorizontalSlider(
                new Rect(bg.x + 24, y + 26, bg.width - 48, 20), CurrentVolume, VolumeMin, VolumeMax);
            if (!Mathf.Approximately(vol, CurrentVolume))
            {
                CurrentVolume = vol;
                SaveVolume(vol);
                ApplySettings();
            }
            y += 70;

            // FOV 60–90
            GUI.Label(new Rect(bg.x + 24, y, 660, 22),
                $"视野（FOV）：{CurrentFov:0}（60 窄 – 90 宽）", ItemSlotDrawer.WhiteStyle());
            float fov = GUI.HorizontalSlider(
                new Rect(bg.x + 24, y + 26, bg.width - 48, 20), CurrentFov, FovMin, FovMax);
            if (!Mathf.Approximately(fov, CurrentFov))
            {
                CurrentFov = fov;
                SaveFov(fov);
                ApplySettings();
            }

            y += 60;
            GUI.Label(new Rect(bg.x + 24, y, 660, 20),
                "设置改动立即生效并自动保存。", ItemSlotDrawer.WhiteStyle());
        }
    }
}
