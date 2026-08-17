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

        // m11 P0（前置·串行独占）：第 1 波 12 新生物预接线。枚举值固定 15-26、
        // 顺序/数值一字不差——存档 spawn 数据按值序列化，W1 任务卡按名引用。
        // P0 只接线、不写专属 AI：9 被动 kind 行为等价猪组（wander + 受击逃 3s，
        // 见 MobAI 两处 switch），骷髅/蜘蛛/苦力怕暂等价僵尸组（夜里追白天不追，
        // W1-1 替换成专属 AI）。安全性依据：spawn_rules.json 尚未加这 12 个名字的
        // 条目，接线后不会真的刷出（模型 mobs/models/*.json 同批未就绪）；
        // 等 W1 代理补条目 + 模型 JSON 后自然生效。
        Sheep = 15,
        Rabbit = 16,
        Fox = 17,
        Deer = 18,
        Panda = 19,
        Penguin = 20,
        Goat = 21,
        Raccoon = 22,
        Hamster = 23,
        Skeleton = 24,
        Spider = 25,
        Creeper = 26,
    }
}