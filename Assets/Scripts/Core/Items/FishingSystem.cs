namespace MyWorld.Core.Items
{
    /// <summary>钓鱼的阶段（评审 08 F3：点亮四个沉睡零件——fisherman 成就钩子 /
    /// 水生生物 / 水体玩法 / 熟鱼烹饪链）。</summary>
    public enum FishingPhase
    {
        /// <summary>未抛竿。</summary>
        Idle,

        /// <summary>已抛竿，等鱼咬钩（3-10s 确定性哈希）。</summary>
        Waiting,

        /// <summary>咬钩窗口内（1.5s）——此刻收杆才上鱼。</summary>
        Biting,
    }

    /// <summary>一次垂钓会话（每玩家一个，BlockInteraction 持有）。纯数据状态机、
    /// 双链可测；咬钩时刻由抛竿时的确定性哈希决定（项目铁律：不持随机数对象）。</summary>
    public sealed class FishingSession
    {
        /// <summary>最短等待（秒）。</summary>
        public const float MinWaitSeconds = 3f;

        /// <summary>最长等待（秒）。儿童版收窄 MC 的 5-45s——一局的耐心预算就一两分钟。</summary>
        public const float MaxWaitSeconds = 10f;

        /// <summary>咬钩窗口（秒）：窗口内收杆上鱼，过窗口脱钩。</summary>
        public const float BiteWindowSeconds = 1.5f;

        /// <summary>当前阶段。</summary>
        public FishingPhase Phase { get; private set; }

        /// <summary>咬钩开始时刻（绝对秒，Phase=Biting 期间有效）。</summary>
        public double BiteAt { get; private set; }

        /// <summary>抛竿：Idle → Waiting。hash 决定等待时长（确定性：
        /// MinWait + hash % (Max-Min+1) 秒）。返回 false = 已在垂钓中（重复抛竿 no-op）。</summary>
        public bool Cast(double now, int hash)
        {
            if (Phase != FishingPhase.Idle) return false;
            int waitMillis = (int)(MinWaitSeconds * 1000)
                + System.Math.Abs(hash) % ((int)((MaxWaitSeconds - MinWaitSeconds) * 1000) + 1);
            BiteAt = now + waitMillis / 1000.0;
            Phase = FishingPhase.Waiting;
            return true;
        }

        /// <summary>步进：Waiting 到点转 Biting；Biting 超窗回 Idle（脱钩）。
        /// 返回「本步是否刚进入 Biting」——调用方据此提示「咬钩了」。</summary>
        public bool Tick(double now)
        {
            if (Phase == FishingPhase.Waiting && now >= BiteAt)
            {
                Phase = FishingPhase.Biting;
                return true;
            }

            if (Phase == FishingPhase.Biting && now >= BiteAt + BiteWindowSeconds)
            {
                Phase = FishingPhase.Idle; // 脱钩：没在窗口内收杆
            }

            return false;
        }

        /// <summary>收杆：Biting 窗口内 → true（上鱼，回 Idle）；Waiting 提前收 / 已 Idle → false
        /// （空竿收回，同样回 Idle——玩家随时可以放弃垂钓）。</summary>
        public bool TryReel(double now)
        {
            bool caught = Phase == FishingPhase.Biting
                && now < BiteAt + BiteWindowSeconds;
            Phase = FishingPhase.Idle;
            return caught;
        }
    }

    /// <summary>鱼获掷骰（评审 08 F3）：鱼 80% / 垃圾 15% / 宝藏 5%；
    /// <see cref="MyWorld.Core.Items.EnchantmentType.LuckOfTheSea"/> 每级宝藏 +5%
    /// （从鱼里挪——宝藏变多、总量守恒）。获得幸运附魔的路径（书池扩展）留后续里程碑。</summary>
    public static class FishingRoll
    {
        /// <summary>生鱼物品 id（与 <c>items/raw_fish.json</c> 的 numericId=1623 手动一致）。</summary>
        public const int RawFishItemId = 1623;

        /// <summary>垃圾池：木棍 1002 / 线 1501（与 items JSON 手动一致）。</summary>
        private const int StickItemId = 1002;
        private const int StringItemId = 1501;

        /// <summary>宝藏池：绿宝石 1600（与 items JSON 手动一致）。</summary>
        private const int EmeraldItemId = 1600;

        /// <summary>掷一次鱼获，返回物品 id。确定性：同 hash 同结果。</summary>
        public static int RollItemId(int hash, int luckLevel)
        {
            // 千分位掷骰：宝藏 50 + 50/级（上限 500——运气再好宝藏也不会过半），
            // 垃圾 150，其余全是鱼
            int treasurePerMille = System.Math.Min(500, 50 + 50 * System.Math.Max(0, luckLevel));
            int roll = System.Math.Abs(hash) % 1000;
            if (roll < treasurePerMille) return EmeraldItemId;
            if (roll < treasurePerMille + 150) return (hash & 1) == 0 ? StickItemId : StringItemId;
            return RawFishItemId;
        }
    }
}
