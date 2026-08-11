using System;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 战斗事件总线。Core 不依赖 Unity，把事件挂到 <c>static event</c> 上，
    /// Unity 侧订阅后驱动渲染、音效、UI 红屏等。
    /// <para>测试用 <see cref="Reset"/> 清空订阅（[TearDown] 时调用）。</para>
    /// </summary>
    public static class CombatEvents
    {
        public static event System.Action<DamageEvent> OnDamageDealt;
        public static event System.Action<DamageEvent> OnDamageTaken;
        public static event System.Action<DamageEvent> OnEntityDied;

        public static void RaiseDealt(DamageEvent ev)
        {
            OnDamageDealt?.Invoke(ev);
        }

        public static void RaiseTaken(DamageEvent ev)
        {
            OnDamageTaken?.Invoke(ev);
        }

        public static void RaiseDied(DamageEvent ev)
        {
            OnEntityDied?.Invoke(ev);
        }

        /// <summary>清空所有订阅。仅用于测试或场景卸载。</summary>
        public static void Reset()
        {
            OnDamageDealt = null;
            OnDamageTaken = null;
            OnEntityDied = null;
        }
    }
}
