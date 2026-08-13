using MyWorld.Core.Entities;
using MyWorld.Unity.Gameplay;
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
            ctx.Death.Tick(Time.deltaTime);
            if (ctx.Death.Phase == DeathPhase.Alive && _visible)
            {
                _visible = false;
            }
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

            // 文字
            var bigStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(0, Screen.height / 2 - 80, Screen.width, 80), "你死了", bigStyle);

            // 复活按钮（Dying 时不可点；Respawning 时可点）
            if (death.Phase == DeathPhase.Respawning)
            {
                var btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fixedWidth = 200, fixedHeight = 50 };
                GUI.backgroundColor = Color.white;
                if (GUI.Button(new Rect(Screen.width / 2 - 100, Screen.height / 2 + 20, 200, 50), "复活", btnStyle))
                {
                    death.RequestRespawn();
                }
                GUI.backgroundColor = Color.white;
            }
            else
            {
                var subStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(0, Screen.height / 2 + 20, Screen.width, 30),
                    $"复活倒计时 {Mathf.CeilToInt(death.PhaseTimer)}", subStyle);
            }
        }
    }
}