using MyWorld.Core.Entities;

namespace MyWorld.Core.Items
{
    /// <summary>
    /// Mob 死亡时的物品掉落表。Core 纯数据：给定 <see cref="MobKind"/> 返回应掉落的
    /// <see cref="ItemStack"/> 列表。Unity 侧在 Dying 状态触发后根据 <c>Mob.LastDrops</c>
    /// 生成 <see cref="ItemDropEntity"/>。
    /// <para>
    /// 物品 numericId 参考 <c>Assets/StreamingAssets/items/*.json</c> 的现有分配
    /// （porkchop=1008, rotten_flesh=1010）。当前 cow/chicken 物品 JSON 未到位，
    /// 暂用 porkchop 占位；spec line 173 允许后续替换。
    /// </para>
    /// </summary>
    public static class ItemDropTable
    {
        // 物品 numericId（参考 ItemDatabase 当前 auto-assign 区间 1000-1701）
        public const int PorkchopItemId = 1008;
        public const int RottenFleshItemId = 1010;

        /// <summary>返回该 mob 类型死亡时应掉落的物品栈列表。空数组表示不掉落。</summary>
        public static ItemStack[] Drop(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig:
                    return new[] { new ItemStack(PorkchopItemId, 1) };
                case MobKind.Cow:
                    // TODO: beef JSON 入库后替换为 BeefItemId
                    return new[] { new ItemStack(PorkchopItemId, 1) };
                case MobKind.Chicken:
                    // TODO: chicken JSON 入库后替换为 ChickenItemId
                    return new[] { new ItemStack(PorkchopItemId, 1) };
                case MobKind.Zombie:
                    return new[] { new ItemStack(RottenFleshItemId, 1) };
                default:
                    return System.Array.Empty<ItemStack>();
            }
        }
    }
}