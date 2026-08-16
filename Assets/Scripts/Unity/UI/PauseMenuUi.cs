using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// m8 B2：Esc 真暂停菜单。打开即 <c>Time.timeScale = 0</c>——世界生成、生物、
    /// 掉落物、昼夜全部冻结；关闭恢复 1。保存退出流程期间保持 0（见下）。
    /// 居中三按钮：「继续」/「设置」/「保存并退出」，半透明深底 + 白字，同帮助菜单风格。
    /// <para>
    /// <b>Esc 路由</b>（在 OnGUI 事件层做，<see cref="HandleKey"/> 是同一入口）：
    /// 死亡画面可见 → Esc 让位不弹（复活按钮才是那时的主操作）；帮助菜单开着 →
    /// 暂停打开时先把它关掉（互斥，同帧不双开）；暂停开着 → Esc 关闭。
    /// IMGUI 事件与 Update 输入不受 timeScale 影响是引擎既有行为——
    /// timeScale=0 下菜单照常响应正是暂停菜单需要的。
    /// fix1 M1：按住 Esc 时 IMGUI 持续发自动重复的 KeyDown，<see cref="HandleGuiEvent"/>
    /// 去抖——按住只响应第一次，KeyUp 后才能再触发，菜单不抖动。
    /// </para>
    /// <para>
    /// fix1 I1：暂停期间玩家运动输入的残留由 <see cref="Player.PlayerController"/>
    /// 按 timeScale 自行抑制（暂停帧跳过运动步进、恢复首帧喂
    /// <see cref="MyWorld.Core.Player.PlayerInput.None"/>），本类不与其耦合——任何路径的
    /// 暂停 / 恢复都被覆盖。
    /// </para>
    /// <para>
    /// <b>设置</b>：「设置」按钮展开 <see cref="SettingsPanelUi"/> 公共面板（m8 B1 抽出），
    /// 与帮助菜单「设置」页共享同一实例（Awake 同物体懒挂，后挂的 GetComponent 复用，
    /// 全场只建一份），不复制滑条代码。
    /// </para>
    /// <para>
    /// <b>保存并退出</b>：整体委托给同物体的 <see cref="HelpMenuUi.RequestSaveAndQuit"/>
    /// ——同步落盘 → 失败可重试 → 半秒停留窗 → 只退一次的状态机 m7 A4 起就在帮助菜单里，
    /// 本类不复制一份，只共用它的 <see cref="HelpMenuUi.QuitStatusText"/> /
    /// <see cref="HelpMenuUi.QuitErrorText"/> 画确认与失败提示。停留窗由帮助菜单自己的
    /// Update 每帧 <c>TickQuit</c> 推进（菜单关着也照跑），时钟默认
    /// <see cref="Time.unscaledTime"/>（m8 B2 改的：暂停期间 Time.time 冻结）。
    /// 退出挂起期间 Esc /「继续」都被挡住——保存退出全程保持 timeScale=0，
    /// 半秒后进程就没了，这窗口里不该解冻世界。
    /// </para>
    /// <para>
    /// 打开期间与帮助菜单同款登记：置 <see cref="Player.BlockInteraction.InputLocked"/>
    /// 抑制挖/放 + <see cref="UiCursorGate"/> 指针门解锁鼠标（菜单开着能点按钮、拉滑条）。
    /// </para>
    /// </summary>
    public sealed class PauseMenuUi : MonoBehaviour
    {
        /// <summary>菜单是否打开。timeScale / 挖放输入锁 / 指针门都随它同步翻转。</summary>
        public bool IsOpen { get; private set; }

        private bool _showSettings;

        /// <summary>「设置」子面板是否展开（<see cref="ToggleSettings"/> 翻转；
        /// 重新打开菜单时复位为主按钮页）。</summary>
        public bool SettingsShown => _showSettings;

        private HelpMenuUi _help;

        /// <summary>保存退出复用的帮助菜单（Awake 同物体懒挂）。公开给测试注入
        /// <c>SaveService</c> / <c>QuitClock</c> / <c>QuitRequested</c>——
        /// 状态机的注入缝全在它那一侧。</summary>
        public HelpMenuUi HelpMenu => _help;

        private SettingsPanelUi _settings;

        /// <summary>设置子面板用的公共面板（m8 B1）。Awake 同物体懒挂，与
        /// <see cref="HelpMenuUi"/> 的面板共享同一实例——后挂的一方先 GetComponent，
        /// 全场不会建出第二份。</summary>
        public SettingsPanelUi SettingsPanel => _settings;

        private void Awake()
        {
            // 先挂帮助菜单（PlayMode 下它的 Awake 会懒挂 SettingsPanelUi），
            // 再 GetComponent 拿面板：无论谁先建，两个宿主最终共用同一实例
            _help = GetComponent<HelpMenuUi>();
            if (_help == null) _help = gameObject.AddComponent<HelpMenuUi>();
            _settings = GetComponent<SettingsPanelUi>();
            if (_settings == null) _settings = gameObject.AddComponent<SettingsPanelUi>();
        }

        /// <summary>开关菜单。同步维护真暂停（timeScale 0↔1）、挖/放输入锁与
        /// <see cref="UiCursorGate"/> 指针门；打开时若帮助菜单开着先关掉（互斥）。</summary>
        public void Toggle() => SetOpen(!IsOpen);

        /// <summary>程序化开关（测试与按钮回调用），与 Esc 按键走同一条路径。
        /// 低层总阀：保存退出挂起期间 Esc /「继续」按钮被挡，但本方法不做挡——
        /// 禁用复位（<see cref="OnDisable"/>）也靠它收拾残局。</summary>
        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            // 先关帮助菜单再翻自己的状态：它 SetOpen(false) 会顺带清输入锁 / 关指针门，
            // 后置的话会把我们刚设的「锁 + 门登记」冲掉
            if (open && _help != null && _help.IsOpen) _help.SetOpen(false);
            IsOpen = open;
            BlockInteraction.InputLocked = open;
            Time.timeScale = open ? 0f : 1f;
            if (open)
            {
                UiCursorGate.Open();
                _showSettings = false; // 每次打开都从主按钮页开始
            }
            else
            {
                UiCursorGate.Close();
            }
        }

        /// <summary>Esc 路由（OnGUI 的 KeyDown 事件与测试直调共用入口）。
        /// 优先级：开着 → 关闭（退出挂起时挡住，保存退出期间保持 timeScale=0）；
        /// 关着 → 死亡画面可见让位，否则打开。</summary>
        public void HandleKey(KeyCode key)
        {
            if (key != KeyCode.Escape) return;
            if (IsOpen)
            {
                if (!QuitPending) SetOpen(false);
            }
            else if (!DeathScreenVisible())
            {
                SetOpen(true);
            }
        }

        /// <summary>「设置」按钮回调（OnGUI 点击与测试直调）：展开 / 收起公共设置面板。</summary>
        public void ToggleSettings() => _showSettings = !_showSettings;

        /// <summary>保存退出流程是否已挂起（帮助菜单状态机里已存成 / 已进入退出流程，
        /// 正处半秒停留窗）。以 <see cref="HelpMenuUi.QuitStatusText"/> 非空为准——
        /// 它的公开契约是「未在退出流程中为 null」。</summary>
        public bool QuitPending => _help != null && _help.QuitStatusText != null;

        /// <summary>「保存并退出」按钮回调（OnGUI 点击与测试直调）：整体委托
        /// <see cref="HelpMenuUi.RequestSaveAndQuit"/>——同步落盘、失败可重试、
        /// 半秒停留窗、只退一次，全部是帮助菜单 m7 A4 状态机的既有行为。</summary>
        public void RequestSaveAndQuit()
        {
            if (_help != null) _help.RequestSaveAndQuit();
        }

        /// <summary>死亡画面是否可见（Esc 让位判定）。经 <see cref="PlayerContext.Instance"/>
        /// 反查，与 <see cref="PlayerController"/> 死亡时取 <c>Show()</c> 同一条路；
        /// 无上下文 / 无死亡画面（早期场景）视为不可见，Esc 照常开门。
        /// 判空走 UnityEngine.Object 重载：场景卸载后残留的已销毁引用同样判 null。</summary>
        private static bool DeathScreenVisible()
        {
            PlayerContext ctx = PlayerContext.Instance;
            DeathScreenUi screen = ctx != null ? ctx.DeathScreen : null;
            return screen != null && screen.IsVisible;
        }

        private void OnDisable()
        {
            // 禁用 / 销毁时若还开着，把 timeScale / 指针门 / 输入锁一并复位——
            // 漏了这条，暂停状态会跟着组件死亡把整个游戏永久冻结
            if (IsOpen) SetOpen(false);
        }

        // ─── 绘制（样式同帮助菜单：半透明深底 GUI.Box + 白字） ──────────────────

        private bool _escHeld;

        /// <summary>Esc 的 OnGUI 事件入口（fix1 M1）。IMGUI 对按住的键会持续发
        /// 自动重复的 KeyDown——逐个响应会让菜单开了又关来回抖动。去抖门：按住期间
        /// 只响应第一次 KeyDown，收到 KeyUp 才允许下一次触发（KeyUp 在菜单关闭时
        /// 也照常处理，松手状态不残留）。internal：EditMode 泵不了真 IMGUI 事件，测试直调。</summary>
        internal void HandleGuiEvent(EventType type, KeyCode keyCode)
        {
            if (keyCode != KeyCode.Escape) return;
            if (type == EventType.KeyUp)
            {
                _escHeld = false; // 松手复位：下一次按下才是新的按压
                return;
            }
            if (type != EventType.KeyDown) return;
            bool firstPress = !_escHeld;
            _escHeld = true;
            if (firstPress) HandleKey(KeyCode.Escape); // 自动重复的后续 KeyDown 不再触发
        }

        private void OnGUI()
        {
            // Esc 路由放在 OnGUI 事件层（brief 指定）：IMGUI 事件不受 timeScale 影响，
            // timeScale=0 下照常响应正是暂停菜单要的；菜单关着也先查键——Esc 要能直接开门。
            // fix1 M1：KeyDown / KeyUp 都经过去抖门再 Use() 吃掉，不传给同帧后画的控件
            var evt = Event.current;
            if (evt.keyCode == KeyCode.Escape
                && (evt.type == EventType.KeyDown || evt.type == EventType.KeyUp))
            {
                HandleGuiEvent(evt.type, evt.keyCode);
                evt.Use();
            }
            if (!IsOpen) return;
            DrawMenu();
        }

        /// <summary>菜单宽度：与帮助菜单一致（720），让设置面板拿到逐像素相同的可用宽度
        ///（面板内容 x = bg.x+24、宽 = bg.width-48，同帮助菜单「设置」页）。</summary>
        private const float MenuWidth = 720f;

        private void DrawMenu()
        {
            // 高度按内容动态拼：标题 44 + 三按钮行 3×42 + 底距 12，
            // 设置子面板展开时 +（面板 220 + 上距 16），失败提示 + 40
            float h = 44f + 3f * 42f + 12f;
            if (_showSettings) h += SettingsPanelUi.PanelHeight + 16f;
            string error = _help != null ? _help.QuitErrorText : null;
            if (error != null) h += 40f;

            var bg = new Rect((Screen.width - MenuWidth) / 2f, (Screen.height - h) / 2f, MenuWidth, h);
            GUI.Box(bg, GUIContent.none); // 半透明深色背景（GUI.Box 默认皮肤），同帮助菜单 / 背包

            GUI.Label(new Rect(bg.x + 16, bg.y + 10, 400, 22), "游戏已暂停（Esc 继续）", ItemSlotDrawer.WhiteStyle());

            float y = bg.y + 44f;
            bool pending = QuitPending;

            // 继续按钮：退出流程挂起时禁用（保存退出期间保持 timeScale=0，别在这窗口解冻）
            var prevEnabled = GUI.enabled;
            if (pending) GUI.enabled = false;
            if (GUI.Button(new Rect(bg.x + 24, y, bg.width - 48, 34), "继续")) SetOpen(false);
            GUI.enabled = prevEnabled;
            y += 42f;

            if (GUI.Button(new Rect(bg.x + 24, y, bg.width - 48, 34),
                    _showSettings ? "设置（收起）" : "设置"))
            {
                ToggleSettings();
            }
            y += 42f;

            if (pending)
            {
                // 已存成：按钮换确认文本（反馈「存好了」+ 物理防手抖双击），同帮助菜单
                GUI.Label(new Rect(bg.x + 24, y, bg.width - 48, 26),
                    _help.QuitStatusText, ItemSlotDrawer.WhiteStyle());
            }
            else
            {
                // 红色系退出按钮 + 失败红字可重试，样式 / 状态机都与帮助菜单共用
                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = HelpMenuUi.QuitButtonColor;
                if (GUI.Button(new Rect(bg.x + 24, y, bg.width - 48, 34), "保存并退出游戏"))
                {
                    RequestSaveAndQuit();
                }
                GUI.backgroundColor = prevColor;
            }
            y += 42f;

            if (_showSettings)
            {
                // m8 B1 公共设置面板：与帮助菜单「设置」页同一份代码、同一实例
                y += 16f;
                _settings.DrawPanel(new Rect(bg.x + 24, y, bg.width - 48, SettingsPanelUi.PanelHeight));
                y += SettingsPanelUi.PanelHeight;
            }

            if (error != null)
            {
                GUI.Label(new Rect(bg.x + 24, y + 4, bg.width - 48, 40), error, HelpMenuUi.QuitErrorStyle());
            }
        }
    }
}
