using MyWorld.Core.Entities;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 死亡画面：玩家 HP=0 时全屏暗红色 "你死了" + 复活按钮。
    /// 复活完成（Phase=Alive）后自动隐藏。
    /// </summary>
    public sealed class DeathScreenUi : MonoBehaviour
    {
        public Color OverlayColor = new Color(0.5f, 0f, 0f, 0.65f);

        /// <summary>当前是否在显示死亡画面。由 <see cref="Show"/> / <see cref="OnPlayerDied"/> 触发，
        /// 复活完成（<see cref="DeathSystem.Phase"/> 回 <see cref="DeathPhase.Alive"/>）后自动收起。</summary>
        public bool IsVisible => _visible;

        private bool _visible;

        /// <summary>m5 C1：死亡画面期间 OnGUI 每帧跑，原先每帧 new 2-3 个 GUIStyle
        /// （标题 / 按钮 / 倒计时），改为只构造一次缓存复用，消除该状态下的 GC 分配。</summary>
        private GUIStyle _bigStyle;
        private GUIStyle _btnStyle;
        private GUIStyle _subStyle;

        private void Awake()
        {
            // 把自身挂到 PlayerContext 上，方便 PlayerController 在 HP=0 时反查 Show()，
            // 无需走 FindObjectOfType（OnGUI / 战斗中频繁调用，全局查找开销不可忽略）。
            var ctx = PlayerContext.Instance;
            if (ctx != null) ctx.DeathScreen = this;
        }

        /// <summary>触发死亡画面。同步把 <see cref="DeathSystem"/> 推进到
        /// <see cref="DeathPhase.Dying"/>，让 Core 侧的状态机与 UI 侧保持一致。</summary>
        public void Show()
        {
            _visible = true;
            var ctx = PlayerContext.Instance;
            if (ctx != null && ctx.Death != null)
            {
                var pos = transform.position;
                ctx.Death.OnDeath(new MyWorld.Core.Math.Float3(pos.x, pos.y, pos.z));
            }
        }

        /// <summary>玩家死亡事件入口（与 <see cref="Show"/> 等价，供未来事件总线接入）。
        /// B3 测试用此方法验证 OnPlayerDied 后 IsVisible=true。</summary>
        public void OnPlayerDied() => Show();

        /// <summary>m10 C2 右键复活的纯路由谓词（raw 值版）：死亡画面激活期间，
        /// 任意位置的**右键按下**等效点「复活」按钮（spec §4 顺手小改）。
        /// 只认 MouseDown + button 1：抬起/左键/布局事件都不算，避免一次右键触发多次。
        /// 注意测试必须走这个 raw 版——EditMode 批处理下 <c>Event.type</c> 的 setter
        /// 不落值（探针实测 <c>new Event { type = MouseDown }</c> 读回 Ignore），
        /// 无 GUI 上下文构造不出真右键事件。</summary>
        public static bool IsRespawnRightClick(EventType type, int button)
            => type == EventType.MouseDown && button == 1;

        /// <summary>Event 包装版（生产路径：<see cref="OnGUI"/> 每帧喂 <c>Event.current</c>）。</summary>
        public static bool IsRespawnRightClick(Event e)
            => e != null && IsRespawnRightClick(e.type, e.button);

        /// <summary>右键复活的执行入口（<see cref="OnGUI"/> 每帧喂 <c>Event.current</c>）。
        /// 与按钮同一道门：只在 <see cref="DeathPhase.Respawning"/> 生效——Dying 阶段
        /// 按钮也是灰的，右键不该抢先。命中后与按钮同款把 PhaseTimer 清零，
        /// 由 <see cref="Update"/> 检测 Respawning→Alive 转换统一走 TriggerRespawn。</summary>
        internal void HandleRightClick(Event e)
        {
            if (e == null) return;
            HandleRightClickCore(e.type, e.button);
        }

        /// <summary>右键复活的执行体（raw 值版，EditMode 直测入口）。</summary>
        internal void HandleRightClickCore(EventType type, int button)
        {
            if (!IsRespawnRightClick(type, button)) return;

            var ctx = PlayerContext.Instance;
            var death = ctx != null ? ctx.Death : null;
            if (death == null || death.Phase != DeathPhase.Respawning) return;

            death.PhaseTimer = 0f;
        }

        private void Update()
        {
            // 每帧推进 DeathSystem 状态机：Phase=Alive 时收起 IsVisible。
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Death == null) return;

            var prevPhase = ctx.Death.Phase;
            ctx.Death.Tick(Time.deltaTime);

            // 检测 Respawning → Alive 转换（无论自然倒计时结束还是按钮加速）：
            // 真正调用 PlayerController.Respawn 传回死亡位置。
            // 之前按钮只调 death.RequestRespawn()（Phase=Dying 时才生效），按钮实际是死 UI；
            // 这里补上绑定，Update 与按钮两条路径都收敛到 TriggerRespawn。
            if (prevPhase == DeathPhase.Respawning && ctx.Death.Phase == DeathPhase.Alive)
            {
                TriggerRespawn(ctx);
            }

            if (ctx.Death.Phase == DeathPhase.Alive && _visible)
            {
                _visible = false;
            }
        }

        /// <summary>真正执行复活：m7 A1 起一律回世界出生点
        /// （<see cref="PlayerController.Bind"/> 记录的 spawn），不再把
        /// <see cref="DeathSystem.LastDeathPosition"/> 传回去——旧版复活点 = 死亡位置，
        /// 僵尸守尸时原地复活立刻再被围殴，形成死亡循环。
        /// 内部 <see cref="PlayerController.RespawnAtSpawn"/> 复用 Respawn 既有逻辑
        /// 回满 HP / Hunger / 重置速度状态，并额外开启 3 秒无敌帧。
        /// 按钮点击 / 倒计时结束都走这里，确保两条路径一致。</summary>
        private void TriggerRespawn(PlayerContext ctx)
        {
            var pc = ctx.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.RespawnAtSpawn();
            }
            _visible = false;
        }

        private void OnGUI()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            var death = ctx.Death;
            if (death.Phase == DeathPhase.Alive) return;

            // 全屏红幕
            GUI.color = OverlayColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 文字（样式首帧构造一次缓存复用，见字段注释）
            if (_bigStyle == null)
            {
                _bigStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 48,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
            }
            GUI.Label(new Rect(0, Screen.height / 2 - 80, Screen.width, 80), "你死了", _bigStyle);

            // 复活按钮（Dying 时不可点；Respawning 时可点）
            if (death.Phase == DeathPhase.Respawning)
            {
                // m10 C2：右键等效复活按钮——任何位置的右键按下都算，不用瞄准按钮
                HandleRightClick(Event.current);

                if (_btnStyle == null)
                {
                    _btnStyle = new GUIStyle(GUI.skin.button)
                    {
                        fontSize = 22,
                        fixedWidth = 200,
                        fixedHeight = 50,
                    };
                }
                GUI.backgroundColor = Color.white;
                if (GUI.Button(new Rect(Screen.width / 2 - 100, Screen.height / 2 + 20, 200, 50), "复活", _btnStyle))
                {
                    // 把 PhaseTimer 强制为 0，让 Tick 下一帧把 Respawning → Alive，
                    // Update 会在同一帧 / 下一帧检测到转换并 TriggerRespawn。
                    // 注：death.RequestRespawn() 只处理 Dying 阶段，Respawning 阶段是 no-op，
                    // 所以这里直接写 PhaseTimer。
                    death.PhaseTimer = 0f;
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                if (_subStyle == null)
                {
                    _subStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 18,
                        normal = { textColor = Color.white }
                    };
                }
                GUI.Label(new Rect(0, Screen.height / 2 + 20, Screen.width, 30),
                    $"复活倒计时 {Mathf.CeilToInt(death.PhaseTimer)}", _subStyle);
            }
        }
    }
}