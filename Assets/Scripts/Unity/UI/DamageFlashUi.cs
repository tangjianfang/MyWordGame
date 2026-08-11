using MyWorld.Core.Entities;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>受伤时全屏红闪 0.3 秒。订阅 <see cref="CombatEvents.OnDamageTaken"/>。</summary>
    public sealed class DamageFlashUi : MonoBehaviour
    {
        public float Duration = 0.3f;
        private float _flashRemaining;

        private void OnEnable()
        {
            CombatEvents.OnDamageTaken += OnTaken;
        }

        private void OnDisable()
        {
            CombatEvents.OnDamageTaken -= OnTaken;
        }

        private void OnTaken(DamageEvent ev)
        {
            // 只在玩家受伤时闪（VictimEntityId == 0 表示环境，但 DamageEvent 没有自带玩家 ID 区分——简化：玩家是唯一的非 mob victim，攻击者 AttackerEntityId != 0）
            // 玩家伤害的 VictimEntityId == 0，攻击者 ID == 0 表示环境
            if (ev.VictimEntityId == 0)
            {
                _flashRemaining = Duration;
            }
        }

        private void Update()
        {
            if (_flashRemaining > 0) _flashRemaining -= Time.deltaTime;
        }

        private void OnGUI()
        {
            if (_flashRemaining <= 0) return;
            float a = (_flashRemaining / Duration) * 0.4f;
            var prev = GUI.color;
            GUI.color = new Color(1, 0, 0, a);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
