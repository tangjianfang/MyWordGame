using MyWorld.Core.Entities;

namespace MyWorld.Core.Items
{
    /// <summary>
    /// Mob 死亡时的物品掉落表（静态 legacy 路径：MobAI.DropTable 未注入时的回退）。
    /// Core 纯数据：给定 <see cref="MobKind"/> 返回应掉落的
    /// <see cref="ItemStack"/> 列表。Unity 侧在 Dying 状态触发后根据 <c>Mob.LastDrops</c>
    /// 生成 <see cref="ItemDropEntity"/>。
    /// <para>
    /// 物品 numericId 参考 <c>Assets/StreamingAssets/items/*.json</c> 的现有分配
    /// （porkchop=1008, beef=1016, chicken=1017, rotten_flesh=1010）。
    /// </para>
    /// </summary>
    public static class ItemDropTable
    {
        // 物品 numericId（参考 ItemDatabase 当前 auto-assign 区间 1000-1701）
        public const int PorkchopItemId = 1008;
        public const int BeefItemId = 1016;
        public const int ChickenItemId = 1017;
        public const int RottenFleshItemId = 1010;

        /// <summary>返回该 mob 类型死亡时应掉落的物品栈列表。空数组表示不掉落。</summary>
        public static ItemStack[] Drop(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig:
                    return new[] { new ItemStack(PorkchopItemId, 1) };
                case MobKind.Cow:
                    return new[] { new ItemStack(BeefItemId, 1) };
                case MobKind.Chicken:
                    return new[] { new ItemStack(ChickenItemId, 1) };
                case MobKind.Zombie:
                    return new[] { new ItemStack(RottenFleshItemId, 1) };
                default:
                    return System.Array.Empty<ItemStack>();
            }
        }
    }
}