namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 玩家经验条：当前值 + 等级。每升 1 级需要 100 经验（线性，Minecraft-like 简化版）。
    /// </summary>
    public struct Experience
    {
        public int Current;
        public int Level;

        public const int ExpPerLevel = 100;

        public Experience(int current = 0, int level = 0)
        {
            Current = current;
            Level = level;
        }

        /// <summary>获取升至下一级所需经验。</summary>
        public int ToNextLevel => System.Math.Max(0, ExpPerLevel - Current);

        /// <summary>获取当前等级的经验条比例 [0,1]。</summary>
        public float Fraction => (float)Current / ExpPerLevel;

        /// <summary>加经验，溢出时升级。当前等级内未溢出返回 false，升了级返回 true。</summary>
        public bool Add(int amount)
        {
            if (amount <= 0) return false;
            Current += amount;
            bool leveled = false;
            while (Current >= ExpPerLevel)
            {
                Current -= ExpPerLevel;
                Level++;
                leveled = true;
            }
            return leveled;
        }
    }
}