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

        /// <summary>真正执行复活：传送玩家到 <see cref="DeathSystem.LastDeathPosition"/>，
        /// 内部 <see cref="PlayerController.Respawn"/> 已经会回满 HP / Hunger / 重置速度状态。
        /// 按钮点击 / 倒计时结束都走这里，确保两条路径一致。</summary>
        private void TriggerRespawn(PlayerContext ctx)
        {
            var pc = ctx.GetComponent<PlayerController>();
            if (pc != null)
            {
                var pos = ctx.Death.LastDeathPosition;
                pc.Respawn(new Vector3(pos.X, pos.Y, pos.Z));
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