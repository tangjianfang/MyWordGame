namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 生命值。当前降到 0 时 <see cref="IsDead"/> 为 true。
    /// 默认最大值 20（=10 颗心），通过 <see cref="Max"/> 调整。
    /// </summary>
    public struct Health
    {
        public float Current;
        public float Max;

        public Health(float max)
        {
            Max = max < 0 ? 0 : max;
            Current = Max;
        }

        public bool IsDead => Current <= 0f;

        public float Fraction => Max <= 0 ? 0 : Current / Max;

        public void Damage(float amount)
        {
            if (amount <= 0) return;
            Current -= amount;
            if (Current < 0) Current = 0;
        }

        public void Heal(float amount)
        {
            if (amount <= 0) return;
            Current += amount;
            if (Current > Max) Current = Max;
        }

        public void ResetToFull() => Current = Max;
    }
}
