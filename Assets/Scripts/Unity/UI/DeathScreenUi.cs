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