namespace MyWorld.Core.Time
{
    /// <summary>
    /// 24000 tick 一天的世界时钟。Minecraft 同款刻度，便于后续把光照与敌对生物生成对齐。
    /// <para><see cref="Speed"/> 控制 <c>Advance(dt)</c> 时每秒推进多少 tick。默认 60（=7 分钟/天）。</para>
    /// </summary>
    public sealed class TimeOfDay
    {
        public const float DayLengthTicks = 24000f;
        public const float NightStartTick = 13000f;
        public const float NightEndTick = 23000f;

        public float CurrentTick;
        public float Speed = 60f;

        public TimeOfDay()
        {
            CurrentTick = 6000f; // 默认正午起步
        }

        public bool IsNight => CurrentTick >= NightStartTick && CurrentTick < NightEndTick;

        public bool IsDay => !IsNight;

        /// <summary>0 = 黎明, 1 = 白天, 2 = 黄昏, 3 = 夜晚。</summary>
        public int Phase
        {
            get
            {
                if (CurrentTick < 5000f) return 0;
                if (CurrentTick < 13000f) return 1;
                if (CurrentTick < 18000f) return 2;
                return 3;
            }
        }

        public float DayPhase01 => CurrentTick / DayLengthTicks;

        public void Advance(float dt)
        {
            CurrentTick += Speed * dt;
            if (CurrentTick >= DayLengthTicks) CurrentTick -= DayLengthTicks;
        }
    }
}
