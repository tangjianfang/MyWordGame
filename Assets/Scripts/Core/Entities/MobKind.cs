namespace MyWorld.Core.Entities
{
    /// <summary>动物阵营 / 类型。</summary>
    public enum MobKind
    {
        // 旧分类（既有 mobTypeId 1-5 用）
        Passive,  // 友好（猪、羊）
        Hostile,  // 敌对（僵尸、骷髅、苦力怕）
        Neutral,  // 中立（占位）

        // Phase D 新增（spec line 170）：type-specific enum 值
        Pig = 10,
        Cow = 11,
        Chicken = 12,
        Zombie = 13,

        // Task D6：Villager 占位（中立友好 mob，不走 Passive 的 flee，也不走 Hostile 的 chase）
        Villager = 14,
    }
}