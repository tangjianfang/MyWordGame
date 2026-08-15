using System.Collections.Generic;
using MyWorld.Core.Quests;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// m6 B3：H 键帮助菜单。两页 Tab：「怎么玩」（按键表 + 四步玩法 + 任务进度区）
    /// 和「设置」（灵敏度 / 音量 / FOV 三个滑条，PlayerPrefs 持久化，滑完即时生效）。
    /// <para>
    /// 打开期间置 <see cref="Player.BlockInteraction.InputLocked"/> 抑制挖/放——
    /// 菜单里点滑条不应误挖方块。H 或 Esc 关闭。
    /// </para>
    /// <para>
    /// m6 C5：「怎么玩」页的任务进度区接真数据——当前目标全文（任务名 + desc + 进度 x/y）
    /// + 全链 8 格进度条（完成亮金 / 未完成暗灰 / 当前亮白描边）。数据每帧经
    /// <see cref="GetProgressSummary"/> 从 <see cref="QuestEventBus"/> 挂的
    /// <see cref="QuestSystem"/> 现读（单一真源），本组件不记账。
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

        // ─── 任务进度（m6 C5：进度页取数单一入口） ─────────────────────────────

        /// <summary>
        /// 「怎么玩」页任务进度区的纯数据快照（不碰 GUI 上下文，EditMode 可直接断言）。
        /// 从 <see cref="QuestEventBus.Instance"/> 挂的 <see cref="QuestSystem"/> 现读：
        /// 完成计数 / 完成 id 集合 / 当前任务全文与进度 x/y 全部由 Core 计账（单一真源），
        /// 本方法只做投影，自己不持有任何任务状态。无总线（含 Instance 残留指向已销毁组件的
        /// 场景卸载形态——Unity 重载判空兜底）或链文件缺失时返回 <see cref="QuestProgressSummary.Empty"/>。
        /// </summary>
        public static QuestProgressSummary GetProgressSummary()
        {
            // 注意这里的判空走 UnityEngine.Object 的重载 ==：场景卸载后 Instance 残留的
            // 已销毁引用也判 null（EditMode 的 DestroyImmediate 不回调 OnDestroy 清 Instance）
            QuestEventBus bus = QuestEventBus.Instance;
            QuestSystem quests = bus == null ? null : bus.Quests;
            if (quests == null)
            {
                return QuestProgressSummary.Empty;
            }

            var ids = new string[quests.Quests.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = quests.Quests[i].Id;
            }
            // 链式解锁 → 已完成 id 恒为链前缀；CompletedCount 正常 ≤ 链长，Min 夹一下防坏档
            int done = Mathf.Min(quests.CompletedCount, ids.Length);
            var completed = new string[done];
            System.Array.Copy(ids, completed, done);

            Quest current = quests.Current;
            return new QuestProgressSummary(
                ids,
                completed,
                current != null ? current.Id : null,
                current != null ? current.Name : null,
                current != null ? current.Desc : null,
                quests.CurrentProgress,
                current != null ? current.Condition.RequiredCount : 0);
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
            // m6 C5：560 → 660——「怎么玩」页底部接了任务进度区（目标全文 + 8 格进度条）
            const float h = 660f;
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

            // 任务进度区（m6 C5）：当前目标全文 + 全链 8 格进度条，接 QuestSystem 真数据
            y += 10;
            DrawQuestSection(bg, y);
        }

        // ─── 任务进度区绘制（m6 C5） ────────────────────────────────────────────

        /// <summary>完成格亮金 #F5D76E（全局硬约束，别改）。</summary>
        private static readonly Color CompletedSlotColor =
            new Color(0xF5 / 255f, 0xD7 / 255f, 0x6E / 255f, 1f);

        /// <summary>未完成格暗灰：比菜单半透明深底亮一档，锁定格仍然可辨。</summary>
        private static readonly Color LockedSlotColor = new Color(0.32f, 0.32f, 0.32f, 1f);

        /// <summary>进度格边长 / 间距 / 当前格亮白描边宽度（px）。</summary>
        private const float SlotSize = 32f;
        private const float SlotGap = 10f;
        private const float CurrentSlotBorder = 3f;

        private static GUIStyle _countStyle;

        /// <summary>右对齐白字（标题行右侧的「x/8 完成」计数）。必须在 OnGUI 内首用。</summary>
        private static GUIStyle CountStyle()
        {
            if (_countStyle == null)
            {
                _countStyle = new GUIStyle(ItemSlotDrawer.WhiteStyle())
                {
                    alignment = TextAnchor.MiddleRight,
                };
            }
            return _countStyle;
        }

        /// <summary>
        /// 画任务进度区：标题行（左「当前目标」右「x/8 完成」）→ 当前目标全文
        /// （任务名 + 进度 x/y、下一行 desc）→ 全链 8 格进度条。无链时退回 B3 的
        /// 占位语义（显示提示文字、不画格子），绝不用假数据冒充进度。
        /// </summary>
        private void DrawQuestSection(Rect bg, float y)
        {
            QuestProgressSummary summary = GetProgressSummary();

            var section = new Rect(bg.x + 16, y, bg.width - 32, bg.yMax - 8 - y);
            GUI.Box(section, GUIContent.none);
            float x = section.x + 12;
            float width = section.width - 24;

            GUI.Label(new Rect(x, y + 8, 200, 20), "当前目标", ItemSlotDrawer.WhiteStyle());

            if (!summary.HasChain)
            {
                // 无总线 / 链文件缺失（早期场景）：C2 语义「游戏照常玩」，帮助菜单也不许炸
                GUI.Label(new Rect(x, y + 32, width, 18),
                    "（任务链未加载，暂时没有目标）", ItemSlotDrawer.WhiteStyle());
                return;
            }

            GUI.Label(new Rect(section.xMax - 12 - 160, y + 8, 160, 20),
                summary.CountText + " 完成", CountStyle());

            if (summary.ChainComplete)
            {
                GUI.Label(new Rect(x, y + 32, width, 20),
                    "首章完成 ✓　8 个目标全部达成", ItemSlotDrawer.WhiteStyle());
                GUI.Label(new Rect(x, y + 54, width, 18),
                    "第一夜也赢下来了，接下来自由建造吧！", ItemSlotDrawer.WhiteStyle());
            }
            else
            {
                // 当前目标全文：任务名 + 进度 x/y（分子来自 Core 的单一真源，挖到/合成的
                // 同一帧刷新），下一行 desc 给孩子具体的操作指引
                GUI.Label(new Rect(x, y + 32, width, 20),
                    summary.CurrentQuestName + "　" + summary.CurrentProgress + "/" + summary.CurrentRequired,
                    ItemSlotDrawer.WhiteStyle());
                GUI.Label(new Rect(x, y + 54, width, 18),
                    summary.CurrentQuestDesc, ItemSlotDrawer.WhiteStyle());
            }

            DrawQuestSlots(summary, x, y + 78);
        }

        /// <summary>
        /// 画全链进度格：完成亮金、未完成暗灰、当前任务外圈亮白描边。全部用
        /// GUI.Box 染 <see cref="GUI.backgroundColor"/>（默认皮肤自带描边，风格与全菜单统一），
        /// 不引新贴图。
        /// </summary>
        private static void DrawQuestSlots(QuestProgressSummary summary, float x, float y)
        {
            var prevColor = GUI.backgroundColor;
            int count = summary.TotalCount;
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(x + i * (SlotSize + SlotGap), y, SlotSize, SlotSize);
                QuestSlotState state = summary.GetSlotState(i);
                if (state == QuestSlotState.Current)
                {
                    // 当前任务：先画放大 3px 的白盒，正常尺寸的状态盒叠上去，露出的边就是亮白描边
                    GUI.backgroundColor = Color.white;
                    GUI.Box(new Rect(rect.x - CurrentSlotBorder, rect.y - CurrentSlotBorder,
                        rect.width + CurrentSlotBorder * 2f, rect.height + CurrentSlotBorder * 2f), GUIContent.none);
                }
                GUI.backgroundColor = state == QuestSlotState.Completed ? CompletedSlotColor : LockedSlotColor;
                GUI.Box(rect, GUIContent.none);
            }
            GUI.backgroundColor = prevColor;
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

    /// <summary>帮助菜单全链进度条中一格的显示状态（m6 C5）。</summary>
    public enum QuestSlotState
    {
        /// <summary>未轮到的任务：暗灰。</summary>
        Locked,
        /// <summary>当前进行中的任务：暗灰底 + 亮白描边。</summary>
        Current,
        /// <summary>已完成：亮金 #F5D76E。</summary>
        Completed,
    }

    /// <summary>
    /// 帮助菜单「怎么玩」页任务进度区的纯数据快照（m6 C5）。
    /// 由 <see cref="HelpMenuUi.GetProgressSummary"/> 从总线的 <see cref="QuestSystem"/>
    /// 现读投影而成，GUI 只照着画——不持有游戏状态、不记账，进度数字的单一真源始终在 Core。
    /// 全属性只读，构造后不可变。
    /// </summary>
    public sealed class QuestProgressSummary
    {
        /// <summary>共享空快照（无总线 / 链文件缺失）。全属性只读，共享安全。</summary>
        public static readonly QuestProgressSummary Empty = new QuestProgressSummary(
            new string[0], new string[0], null, null, null, 0, 0);

        /// <summary>
        /// 从链数据构造快照。正常只经 <see cref="HelpMenuUi.GetProgressSummary"/> 产生；
        /// 直接 new 留给需要合成状态的测试 / 预览。
        /// </summary>
        public QuestProgressSummary(
            IReadOnlyList<string> questIds,
            IReadOnlyList<string> completedIds,
            string currentQuestId,
            string currentQuestName,
            string currentQuestDesc,
            int currentProgress,
            int currentRequired)
        {
            QuestIds = questIds;
            CompletedIds = completedIds;
            CurrentQuestId = currentQuestId;
            CurrentQuestName = currentQuestName;
            CurrentQuestDesc = currentQuestDesc;
            CurrentProgress = currentProgress;
            CurrentRequired = currentRequired;
        }

        /// <summary>全链任务 id（顺序即解锁顺序；进度条按它画格数，首章 8 个）。</summary>
        public IReadOnlyList<string> QuestIds { get; }

        /// <summary>已完成任务 id（链式解锁 → 恒为 <see cref="QuestIds"/> 的前缀；对应格子亮金）。</summary>
        public IReadOnlyList<string> CompletedIds { get; }

        /// <summary>当前任务 id；全链完成为 null（对应格子亮白描边）。</summary>
        public string CurrentQuestId { get; }

        /// <summary>当前任务名（进度页目标全文的一行）；无链 / 全链完成为 null。</summary>
        public string CurrentQuestName { get; }

        /// <summary>当前任务描述（给孩子具体的操作指引）；无链 / 全链完成为 null。</summary>
        public string CurrentQuestDesc { get; }

        /// <summary>当前条件进度分子（Core 计账，单一真源；全链完成为 0）。</summary>
        public int CurrentProgress { get; }

        /// <summary>当前条件分母（RequiredCount；全链完成为 0）。</summary>
        public int CurrentRequired { get; }

        /// <summary>已完成任务数。</summary>
        public int CompletedCount => CompletedIds.Count;

        /// <summary>链上任务总数（= 进度条格数）。</summary>
        public int TotalCount => QuestIds.Count;

        /// <summary>进度计数文本，如「3/8」。</summary>
        public string CountText => CompletedCount + "/" + TotalCount;

        /// <summary>是否有任务链（无总线 / 链文件缺失时 false，进度页显示占位）。</summary>
        public bool HasChain => TotalCount > 0;

        /// <summary>全链完成（进度页改显示「首章完成」）。注意无链是 false，两者别混。</summary>
        public bool ChainComplete => HasChain && CurrentQuestId == null;

        /// <summary>
        /// 第 <paramref name="index"/> 格的显示状态：完成亮金 / 当前亮白描边 / 未解锁暗灰。
        /// 下标越界抛 <see cref="System.ArgumentOutOfRangeException"/>（写严格——绘制循环按
        /// <see cref="TotalCount"/> 走，越界只可能是调用方 bug）。
        /// </summary>
        public QuestSlotState GetSlotState(int index)
        {
            if (index < 0 || index >= QuestIds.Count)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(index), "进度格下标越界：" + index + "（链上共 " + QuestIds.Count + " 格）");
            }
            if (index < CompletedIds.Count)
            {
                return QuestSlotState.Completed;
            }
            return CurrentQuestId != null && QuestIds[index] == CurrentQuestId
                ? QuestSlotState.Current
                : QuestSlotState.Locked;
        }
    }
}
