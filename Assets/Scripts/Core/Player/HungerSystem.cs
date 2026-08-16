namespace MyWorld.Core.Player
{
    /// <summary>
    /// 玩家饥饿系统。Hunger 0-20，Saturation 0-20（隐藏饱食度）。
    /// 每 30s（600 ticks @ 20 t/s）Hunger--；Saturation=0 时加速到 15s。
    /// </summary>
    public class HungerSystem
    {
        public const int MaxHunger = 20;
        public const float MaxSaturation = 20f;
        public const float DecayIntervalNormal = 30f;
        public const float DecayIntervalStarving = 15f;

        public int Hunger { get; set; } = MaxHunger;
        public float Saturation { get; set; } = 5f;
        public float TimeSinceLastDecay { get; set; } = 0f;

        public void Tick(float dt)
        {
            // 饱食度先消耗（按 1:1 比例，每 dt 秒消耗 dt 饱食度）
            // 饱食度在的时长会"屏蔽"等量的饥饿衰减计时，让 Saturation=0 真正加速衰减
            if (Saturation > 0f)
            {
                float shielded = System.Math.Min(Saturation, dt);
                Saturation -= shielded;
                dt -= shielded;
            }

            TimeSinceLastDecay += dt;
            float interval = (Saturation <= 0f) ? DecayIntervalStarving : DecayIntervalNormal;
            while (TimeSinceLastDecay >= interval)
            {
                Hunger = System.Math.Max(0, Hunger - 1);
                TimeSinceLastDecay -= interval;
            }
        }

        /// <summary>
        /// m7 A3：唯一进食入口。foodValue 取自 <see cref="MyWorld.Core.Items.ItemDefinition.HealAmount"/>：
        /// Hunger +foodValue（钳 <see cref="MaxHunger"/>），Saturation +foodValue×0.5（钳
        /// <see cref="MaxSaturation"/>）。foodValue ≤ 0 是 no-op——进食参数不该由调用方各拼各的，
        /// 统一从物品定义推导（旧的双参重载已删，此前它零调用，饥饿因此永远无法恢复）。
        /// </summary>
        public void Eat(int foodValue)
        {
            if (foodValue <= 0) return;
            Hunger = System.Math.Min(MaxHunger, Hunger + foodValue);
            Saturation = System.Math.Min(MaxSaturation, Saturation + foodValue * 0.5f);
        }

        public bool IsStarving() => Hunger <= 0;
    }
}
