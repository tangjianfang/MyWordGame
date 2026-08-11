using System.Collections.Generic;

namespace MyWorld.Core.Items
{
    /// <summary>附魔类型（简化为 4 种常见效果）。</summary>
    public enum EnchantmentType
    {
        None = 0,
        Sharpness,    // +attack damage
        Efficiency,   // 暂存于 metadata
        Unbreaking,   // 减少耐久消耗概率
        LuckOfTheSea, // 占位
    }

    /// <summary>单条附魔记录。</summary>
    public readonly struct Enchantment
    {
        public readonly EnchantmentType Type;
        public readonly int Level;     // 等级 1-5

        public Enchantment(EnchantmentType type, int level)
        {
            Type = type;
            Level = level;
        }

        /// <summary>附魔带来的攻击加成（用于 Sharpness）。每级 +1 攻击。</summary>
        public float AttackBonus => Type == EnchantmentType.Sharpness ? Level : 0f;
    }

    /// <summary>
    /// 附魔台逻辑：消耗经验 + 青金石随机附魔。简化版只支持 Sharpness。
    /// </summary>
    public static class EnchantingTable
    {
        public const int LapisCostPerLevel = 1;

        /// <summary>
        /// 给定目标附魔等级（1-5），返回需要的经验值与青金石数。
        /// </summary>
        public static (int ExpCost, int LapisCost) CostForLevel(int level)
        {
            if (level < 1) level = 1;
            if (level > 5) level = 5;
            return (level * 5, LapisCostPerLevel);
        }

        /// <summary>
        /// 投一次随机附魔。返回附魔（可能 None）+ 等级。
        /// </summary>
        public static Enchantment Roll(int level)
        {
            if (level < 1) return new Enchantment(EnchantmentType.None, 0);
            // 简化：每级附魔都随机给一个 Sharpness（最高等级）
            return new Enchantment(EnchantmentType.Sharpness, level);
        }
    }
}