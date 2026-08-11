using MyWorld.Core.Math;

namespace MyWorld.Core.Entities
{
    /// <summary>玩家死亡状态机：刚死 → 死亡画面 → 复活。</summary>
    public enum DeathPhase { Alive, Dying, Respawning }

    /// <summary>
    /// 玩家死亡与复活控制器。Core 侧只管状态机与计时，
    /// Unity 侧根据 <see cref="Phase"/> 切换 UI 与控制权。
    /// </summary>
    public sealed class DeathSystem
    {
        /// <summary>死亡到显示「你死了」画面的延迟（秒）。</summary>
        public const float DeathScreenDelay = 1.0f;
        /// <summary>死亡画面停留时间（秒）后自动复活（也可由玩家按钮触发）。</summary>
        public const float RespawnDelay = 3.0f;

        public DeathPhase Phase;
        public float PhaseTimer;
        public Float3 LastDeathPosition;

        public bool IsAlive => Phase == DeathPhase.Alive;

        /// <summary>玩家 HP 归零时调用。记录死亡位置、启动 Dying。</summary>
        public void OnDeath(Float3 position)
        {
            LastDeathPosition = position;
            Phase = DeathPhase.Dying;
            PhaseTimer = DeathScreenDelay;
        }

        /// <summary>每帧推进。返回 true 表示仍处于非 Alive 状态（Unity 用来锁定控制）。</summary>
        public bool Tick(float dt)
        {
            if (Phase == DeathPhase.Alive) return false;

            PhaseTimer -= dt;
            if (Phase <= DeathPhase.Alive) return true;

            if (PhaseTimer <= 0)
            {
                if (Phase == DeathPhase.Dying)
                {
                    Phase = DeathPhase.Respawning;
                    PhaseTimer = RespawnDelay;
                }
                else if (Phase == DeathPhase.Respawning)
                {
                    // 复活完成
                    Phase = DeathPhase.Alive;
                    PhaseTimer = 0;
                    return false;
                }
            }
            return true;
        }

        /// <summary>玩家点击"复活"按钮：跳过等待。</summary>
        public void RequestRespawn()
        {
            if (Phase == DeathPhase.Dying) PhaseTimer = 0;
        }
    }
}