using System;
using System.Collections.Generic;
using MyWorld.Core.Quests;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Persistence;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// m6 B3：H 键帮助菜单。两页 Tab：「怎么玩」（按键表 + 四步玩法 + 任务进度区）
    /// 和「设置」（灵敏度 / 音量 / FOV 三个滑条，PlayerPrefs 持久化，滑完即时生效）。
    /// <para>
    /// m8 B1：三滑条抽到公共组件 <see cref="SettingsPanelUi"/>（B2 暂停菜单复用同一面板，
    /// 不复制代码）——本类 Awake 在同物体懒挂一个，设置页 OnGUI 调它的 DrawPanel 画进去。
    /// PlayerPrefs 键 / 量程 / 默认值 / 即时生效语义平移前后零变化。
    /// </para>
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
    /// <para>
    /// m7 A4：设置页底部加红色系「保存并退出游戏」按钮——点击即
    /// <see cref="SaveLoadService.SaveNow(bool)"/>(async:false) 同步落盘，
    /// <b>存成后</b>半秒停留窗显示「已保存，正在退出…」再退出；保存失败（fix1）
    /// 绝不退出——按钮留原地可重试，红字提示带原因与 Alt+F4 退路。
    /// 退出动作 / 时钟 / 存档服务 / 同步保存调用均可注入
    /// （EditMode 测试不真退、不真写用户目录）。
    /// </para>
    /// </summary>
    public sealed class HelpMenuUi : MonoBehaviour
    {
        /// <summary>菜单是否打开。挖/放输入锁与它同步翻转。</summary>
        public bool IsOpen { get; private set; }

        private int _tab; // 0 = 怎么玩，1 = 设置

        // ─── 嵌入的公共设置面板（m8 B1） ───────────────────────────────────────

        private SettingsPanelUi _settings;

        /// <summary>设置页用的公共设置面板（m8 B1 从内联三滑条抽出）。
        /// Awake 在同物体懒挂一个——旧场景里已存的 HelpMenuUi 无需重存就能用；
        /// B2 暂停菜单用同样的方式各自持有一份，共用同一类不复制代码。</summary>
        public SettingsPanelUi SettingsPanel => _settings;

        private void Awake()
        {
            // PlayMode 下 AddComponent 即回调其 Awake → 读 PlayerPrefs 三键并即时生效，
            // 与平移前 HelpMenuUi.Awake 自己读自己应用的行为等价
            _settings = GetComponent<SettingsPanelUi>();
            if (_settings == null) _settings = gameObject.AddComponent<SettingsPanelUi>();
        }

        /// <summary>开关菜单。同步维护 <see cref="Player.BlockInteraction.InputLocked"/> 抑制挖/放，
        /// 并登记 <see cref="UiCursorGate"/> 指针门——打开时解锁鼠标指针让玩家能拉滑条、点 Tab。</summary>
        public void Toggle() => SetOpen(!IsOpen);

        /// <summary>m6 终审修 C1/M5：程序化开关（--ui-shot 第 4 张截图与 EditMode 测试用），
        /// 与 H / Esc 按键开关走同一条路径，指针门与输入锁不会漏维护。</summary>
        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            BlockInteraction.InputLocked = open;
            if (open) UiCursorGate.Open();
            else UiCursorGate.Close();
        }

        /// <summary>按键路由。EditMode 测试手动调它验证 H / Esc 的行为契约，
        /// Update 里轮询到按键后也走同一条路径。</summary>
        public void HandleKey(KeyCode key)
        {
            if (key == KeyCode.H)
            {
                // m6 终审修 C1（B3-③）：别的模态 UI 开着时不叠开帮助菜单（先按对应键关掉它）
                if (!IsOpen && UiCursorGate.IsOpen) return;
                Toggle();
            }
            else if (key == KeyCode.Escape && IsOpen)
            {
                // 只在打开时响应：菜单关着按 Esc 是「解锁鼠标」的既有语义（PlayerController），不要抢
                Toggle();
            }
        }

        private void OnDisable()
        {
            // m6 终审修 C1（B3-②）：禁用/销毁时若还开着，把输入锁与指针门一并复位，
            // 不留下「挖不动 + 指针永久解锁」的残局
            if (IsOpen) SetOpen(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) HandleKey(KeyCode.H);
            else if (Input.GetKeyDown(KeyCode.Escape)) HandleKey(KeyCode.Escape);
            TickQuit(); // m7 A4：半秒停留窗到点退出
        }

        // ─── 保存并退出（m7 A4：全游戏唯一的玩家退出入口） ─────────────────────

        /// <summary>点击「保存并退出」到真正退出之间的停留时长（秒）：同步落盘完成后
        /// 留半秒把「已保存，正在退出…」亮出来再退——确认要能被看见，不能点了没反应。</summary>
        public const float QuitDelaySeconds = 0.5f;

        /// <summary>退出动作。默认 <see cref="Application.Quit"/>；EditMode 测试替换成
        /// 计数器断言「满半秒才触发、且只触发一次」，不真退测试进程。</summary>
        internal Action QuitRequested = () => Application.Quit();

        /// <summary>可注入时钟（默认 <see cref="Time.unscaledTime"/>），测半秒停留窗用。
        /// 与 <see cref="QuitRequested"/> 同为测试缝，生产代码不改。
        /// m8 B2 起默认从 <see cref="Time.time"/> 换成 unscaledTime：暂停菜单的保存退出
        /// 全程保持 timeScale=0（Time.time 冻结，停留窗永远走不完、进程退不出去）；
        /// timeScale=1 时两者数值相同，帮助菜单自己的退出路径行为不变。</summary>
        internal Func<float> QuitClock = DefaultQuitClock;

        /// <summary>存档服务。生产留空——首次点退出时懒查找（WorldBootstrap 把
        /// <see cref="SaveLoadService"/> 与本组件挂同一物体，查一次后缓存）；
        /// EditMode 测试直接注入绑好临时目录的实例，不碰用户真实存档。</summary>
        internal SaveLoadService SaveService;

        /// <summary>同步保存调用（可注入，fix1）。默认
        /// <see cref="SaveLoadService.SaveNow(bool)"/>(async:false)，返回本轮是否存成。
        /// EditMode 测试注入抛异常的假保存——真实 SaveNow 对 IO 错误是捕获后记录
        /// （log-and-continue）返回 false 不抛，异常分支是快照收集段没有兜底的形态。</summary>
        internal Func<SaveLoadService, bool> SaveNowSync = service => service.SaveNow(async: false);

        private static float DefaultQuitClock() => Time.unscaledTime;

        private bool _quitPending;  // 已点「保存并退出」且存成：按钮换成确认文本，防手抖双击
        private bool _quitSaved;    // 本次退出流程是否真的落过盘（无存档服务时不谎报「已保存」）
        private bool _quitFired;    // QuitRequested 已触发：只退一次，不每帧重复调
        private float _quitAt;      // 同步保存完成的时刻（QuitClock 基准）
        private string _quitError;  // 最近一次保存失败的提示（null = 无失败 / 已被新一轮尝试清除）

        /// <summary>保存到退出之间的确认文本；未在退出流程中为 null（按钮照常显示）。
        /// fix1：保存失败<b>不</b>进退出流程，此文本保持 null、按钮留在原处可重试。</summary>
        public string QuitStatusText => !_quitPending
            ? null
            : (_quitSaved ? "已保存，正在退出…" : "正在退出…");

        /// <summary>保存失败的提示文本（带原因与 Alt+F4 退路）；无失败为 null。
        /// 失败期间按钮保持可点——重试成功即清提示、照常进入退出流程。</summary>
        public string QuitErrorText => _quitError;

        /// <summary>点「保存并退出游戏」按钮（OnGUI 回调，测试也可直调）：
        /// 同步落盘——<see cref="SaveLoadService.SaveNow(bool)"/>(async:false) 返回即写完，
        /// <b>存成后</b>才进入半秒停留窗再退出。重复调用（手抖双击）整轮忽略：退出流程只进一次。
        /// <para>fix1：保存失败（SaveNow 返回 false / 抛异常）绝不退出——档可能没写全，
        /// 退了就是丢档。留在原地：按钮恢复可点可重试，红字提示带原因与 Alt+F4 退路
        /// （Alt+F4 走 OnApplicationQuit 同样同步落盘）。场景里没有存档服务（早期场景）
        /// 时只退不存不抛异常。</para></summary>
        public void RequestSaveAndQuit()
        {
            if (_quitPending) return;
            if (SaveService == null) SaveService = FindObjectOfType<SaveLoadService>();
            _quitError = null; // 新一轮尝试，先清上轮失败提示

            if (SaveService != null)
            {
                bool saved;
                try
                {
                    saved = SaveNowSync(SaveService); // 退出路径必须同步：半秒后进程就没了
                }
                catch (Exception ex)
                {
                    // SaveNow 对 IO 错误是捕获后记录（返回 false），正常不抛；这里的异常
                    // 来自快照收集段没兜住的部分——同样不得退出，留给玩家重试
                    _quitError = "保存失败：" + ex.Message + "，重试或 Alt+F4";
                    return;
                }
                if (!saved)
                {
                    string reason = SaveService.LastSaveError;
                    _quitError = "保存失败：" + (string.IsNullOrEmpty(reason) ? "未知错误" : reason)
                        + "，重试或 Alt+F4";
                    return;
                }
            }

            // 存成（或无存档系统）才进入退出流程
            _quitSaved = SaveService != null;
            _quitPending = true;
            _quitAt = QuitClock();
        }

        /// <summary>步进半秒停留窗（<see cref="Update"/> 每帧调）。独立成 internal 方法：
        /// EditMode 不跑 Update，测试直调它 + 注入 <see cref="QuitClock"/>，
        /// 把「保存后半秒才退」断言成确定行为。</summary>
        internal void TickQuit()
        {
            if (!_quitPending || _quitFired) return;
            if (QuitClock() - _quitAt >= QuitDelaySeconds)
            {
                _quitFired = true;
                QuitRequested();
            }
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

        // ─── 按键表（m6 终审修 I2：双栏） ────────────────────────────────────────
        // 补 P 工作台 / B 口袋合成 / V 交易 / X 附魔 / F11 全屏五个真实按键（spec §3 明列，
        // 任务 desc 自己就在指引「按 B 打开口袋合成」「按 P 开工作台」，表里查不到不行）。
        // 16 行单栏 × 24px = 384px，加上四步玩法与任务进度区会顶破 660px 高的菜单——
        // 改左右双栏各 8 行：左栏基础操作、右栏菜单开关。
        // m7 A4：右栏补第 9 行「Alt+F4 直接退出（自动存档）」——全游戏有退出按钮了，
        // 按键表也得交代 Alt+F4 关窗口不会丢档（走 OnApplicationQuit 同步落盘）。
        // 两栏自此不等长：绘制按 KeyTableRowCount（较长者）遍历，短的一侧末尾留空。

        private static readonly string[] KeyTableLeft =
        {
            "W / A / S / D", "移动",
            "空格", "跳跃",
            "Shift", "下蹲 / 潜行",
            "鼠标移动", "转动视角",
            "鼠标左键", "挖方块（对着方块按）",
            "鼠标右键", "放方块 / 使用物品",
            "数字键 1-9", "选择热键栏物品",
            "鼠标滚轮", "切换热键栏",
        };

        private static readonly string[] KeyTableRight =
        {
            "E", "打开 / 关闭背包（合成）",
            "P", "打开 / 关闭工作台",
            "B", "打开 / 关闭口袋合成",
            "V", "与村民交易",
            "X", "打开 / 关闭附魔台",
            "F11", "全屏开关",
            "H", "打开 / 关闭帮助",
            "Esc", "关闭菜单 / 解锁鼠标",
            "Alt+F4", "直接退出（自动存档）",
        };

        /// <summary>按键表总行数 = 两栏中较长者的条目对数（左右栏允许不等长）。</summary>
        internal static int KeyTableRowCount =>
            Math.Max(KeyTableLeft.Length, KeyTableRight.Length) / 2;

        /// <summary>按键表第 rowIndex 行（0 起）右栏的（键名, 说明）。写严格：越界抛
        /// <see cref="ArgumentOutOfRangeException"/>（绘制循环按 <see cref="KeyTableRowCount"/> 走，
        /// 越界只可能是调用方 bug）。抽出来给 EditMode 断言「Alt+F4 行在渲染范围内」——
        /// 两栏不等长后循环若仍只按左栏行数走，右栏末行会被静默截掉。</summary>
        internal static (string Key, string Desc) GetRightColumnRow(int rowIndex)
        {
            int i = rowIndex * 2;
            if (rowIndex < 0 || i >= KeyTableRight.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rowIndex), "按键表右栏行下标越界：" + rowIndex + "（共 " + KeyTableRight.Length / 2 + " 行）");
            }
            return (KeyTableRight[i], KeyTableRight[i + 1]);
        }

        private static readonly string[] Steps =
        {
            "1. 用左键挖树收集木材，走近地上的掉落物自动捡起",
            "2. 按 E 打开背包，把原木放进 2×2 口袋合成木板",
            "3. 用木板建工作台解锁 3×3 合成，做出更好的工具去挖石头",
            "4. 天黑前用方块搭一个庇护所，夜里小心怪物出没",
        };

        // ─── 挖矿门槛提示行（m10 C3，spec §4） ────────────────────────────────
        // 按键表正下方的一行整宽提示：更深的矿石需要更好的镐。链文本塞不进左栏
        // 说明列的 190px（约 24 字 ≈ 312px，会压到右栏键名），放表下走 660px 整宽。
        // 材料顺序即门槛递增顺序，与 blocks/*.json 的 minToolTier 阶梯一一对应
        // （石 1=木镐 / 粗铁 2=石镐 / 金与合金 3=铁镐 / 机元 4=钻石镐）——
        // EditMode 测试 HelpMenuUiTests.挖矿提示行_* 拿真实 JSON 对照，单边改动会被抓住。

        /// <summary>m10 C3（spec §4）：挖矿门槛提示行全文（「怎么玩」页按键表正下方）。
        /// 括号里的链与 BlockRegistry 各矿石 minToolTier 阶梯同向（EditMode 测试对照真数据）。</summary>
        internal const string MiningTierHint = "挖到不同矿石需要更好的镐（石→铁→金/合金→机元）";

        /// <summary>m13 P0 修：合成网格 SHIFT+click 主背包教学行——孩子"M1和MP背包和工作台里面的物品都没办法合成"
        /// 真正的根因不是配方也不是 FindMatch，是 UX 没教孩子怎么从主背包（不只是 hotbar）送材料入合成区。
        /// 提示用户：按 SHIFT+点击主背包物品（或 hotbar 任意格）就能放进合成网格。EditMode 测试钉死文案不漂移。</summary>
        internal const string CraftingShiftHint = "合成技巧：按 SHIFT + 点击背包物品直接放入合成区（不必先移到 hotbar）";

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

            // Tab 切换（m12 第 1 波起四页：怎么玩 / 成就 / 图鉴 / 设置）
            _tab = GUI.Toolbar(new Rect(bg.x + 16, bg.y + 38, 480, 28), _tab,
                new[] { "怎么玩", "成就", "图鉴", "设置" });

            var menuCtx = MyWorld.Unity.Gameplay.PlayerContext.Instance;
            if (_tab == 0) DrawHowToPlay(bg);
            else if (_tab == 1) AchievementUi.DrawTab(bg, menuCtx != null ? menuCtx.Achievements : null);
            else if (_tab == 2) CodexUi.DrawTab(bg, menuCtx != null ? menuCtx.Codex : null);
            else DrawSettings(bg);
        }

        private void DrawHowToPlay(Rect bg)
        {
            float y = bg.y + 78;

            // 按键表双栏（m6 终审修 I2）：左栏基础操作 8 行、右栏菜单开关 9 行（m7 A4 补 Alt+F4）。
            // 列宽：键名 140 / 说明 190（默认字体 13px，最长说明 10 个汉字 ≈ 130px，不溢出）
            var white = ItemSlotDrawer.WhiteStyle();
            GUI.Label(new Rect(bg.x + 24, y, 200, 22), "基础操作", white);
            GUI.Label(new Rect(bg.x + 372, y, 200, 22), "菜单开关", white);
            y += 26;
            // 按 KeyTableRowCount（两栏较长者）遍历：条目恒成对（键名+说明），判 i 即可；
            // 只按左栏行数走的话右栏第 9 行（Alt+F4）会被静默截掉
            for (int row = 0; row < KeyTableRowCount; row++)
            {
                int i = row * 2;
                if (i < KeyTableLeft.Length)
                {
                    GUI.Label(new Rect(bg.x + 24, y, 140, 22), KeyTableLeft[i], white);
                    GUI.Label(new Rect(bg.x + 168, y, 190, 22), KeyTableLeft[i + 1], white);
                }
                if (i < KeyTableRight.Length)
                {
                    GUI.Label(new Rect(bg.x + 372, y, 140, 22), KeyTableRight[i], white);
                    GUI.Label(new Rect(bg.x + 516, y, 180, 22), KeyTableRight[i + 1], white);
                }
                y += 24;
            }

            // m10 C3（spec §4）：按键表正下方补一行挖矿门槛提示——m10 起挖矿有工具
            // 门槛（低于 minToolTier 挖得掉但无掉落），按键表查不到「为什么挖不动矿」，
            // 这行直接把镐的进阶链写给孩子。行高 22 + 布局间距与上方分组一致，
            // 任务进度区整体下移 26px 后底部仍有约 50px 余量（菜单高 660）。
            y += 10;
            GUI.Label(new Rect(bg.x + 24, y, 660, 22), MiningTierHint, white);
            y += 26;
            // m13 P0：合成技巧提示行（SHIFT+click 主背包教学）——紧贴挖矿提示之后，
            // 与 m10 C3 模式对齐（同位置同风格同点击测试守护契约）
            GUI.Label(new Rect(bg.x + 24, y, 660, 22), CraftingShiftHint, white);
            y += 26;

            GUI.Label(new Rect(bg.x + 24, y, 400, 22), "怎么开始：四步上手", white);
            y += 26;
            for (int i = 0; i < Steps.Length; i++)
            {
                GUI.Label(new Rect(bg.x + 24, y, 660, 22), Steps[i], white);
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
            // m8 B1：三滑条抽到公共 SettingsPanelUi（B2 暂停菜单复用同一面板）。
            // 区域给到逐像素等价于平移前的内联布局：x = bg.x+24、y = bg.y+90、宽 = bg.width-48
            var content = new Rect(bg.x + 24, bg.y + 90, bg.width - 48, SettingsPanelUi.PanelHeight);
            _settings.DrawPanel(content);

            // ── 保存并退出（m7 A4）：全游戏唯一的玩家退出入口，放设置页最底部 ──
            // 平移前按钮在提示行顶 +44（= 面板底 +24），像素位置不变
            float y = content.yMax + 24;
            if (_quitPending)
            {
                // 点击后按钮被确认文本顶替：既给「存好了」的反馈，也物理上防手抖双击
                GUI.Label(new Rect(bg.x + 24, y, 660, 26), QuitStatusText, ItemSlotDrawer.WhiteStyle());
            }
            else
            {
                // 保存失败时按钮留在这里可重试（fix1），红字在按钮下方给原因与 Alt+F4 退路
                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = QuitButtonColor;
                if (GUI.Button(new Rect(bg.x + 24, y, bg.width - 48, 34), "保存并退出游戏"))
                    RequestSaveAndQuit();
                GUI.backgroundColor = prevColor;
                if (_quitError != null)
                {
                    GUI.Label(new Rect(bg.x + 24, y + 40, bg.width - 48, 40), _quitError, QuitErrorStyle());
                }
            }
        }

        /// <summary>保存失败提示的文字色（fix1）：比按钮底色亮一档的红，错误状态一眼可辨。
        /// m8 B2 起 internal：暂停菜单的保存退出走本类状态机，失败提示样式共用这一份。</summary>
        internal static readonly Color QuitErrorColor =
            new Color(0.98f, 0.38f, 0.32f, 1f);

        private static GUIStyle _quitErrorStyle;

        /// <summary>红字提示样式（必须在 OnGUI 内首用构造，同 CountStyle 模式）。
        /// m8 B2 起 internal：暂停菜单复用，不复制样式代码。</summary>
        internal static GUIStyle QuitErrorStyle()
        {
            if (_quitErrorStyle == null)
            {
                _quitErrorStyle = new GUIStyle(ItemSlotDrawer.WhiteStyle())
                {
                    wordWrap = true,
                    normal = { textColor = QuitErrorColor },
                    hover = { textColor = QuitErrorColor },
                    active = { textColor = QuitErrorColor },
                };
            }
            return _quitErrorStyle;
        }

        /// <summary>「保存并退出」按钮的红色系底色（spec §4：退出是不可逆操作，
        /// 用红色系与普通按钮区分；GUI.backgroundColor 染默认按钮皮肤即可，不引新贴图）。
        /// m8 B2 起 internal：暂停菜单的退出按钮同款红色，共用这一份。</summary>
        internal static readonly Color QuitButtonColor =
            new Color(0.82f, 0.28f, 0.24f, 1f);
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
