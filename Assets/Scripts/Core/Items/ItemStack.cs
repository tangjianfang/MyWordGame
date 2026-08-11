namespace MyWorld.Core.Items
{
    /// <summary>
    /// 不可变物品栈。ItemId = 0 表示空气（<see cref="IsEmpty"/>），Count = 0 也视为空。
    /// 用 <see cref="WithCount"/> 改 count 而保持其他字段。
    /// </summary>
    public readonly struct ItemStack
    {
        public readonly int ItemId;
        public readonly int Count;
        public readonly ushort Metadata;

        public ItemStack(int itemId, int count, ushort metadata = 0)
        {
            if (count < 0)
            {
                count = 0;
            }

            ItemId = itemId < 0 ? 0 : itemId;
            Count = count;
            Metadata = metadata;
        }

        public static readonly ItemStack Empty = new ItemStack(0, 0);

        public bool IsEmpty => ItemId == 0 || Count <= 0;

        public ItemStack WithCount(int newCount) => new ItemStack(ItemId, newCount, Metadata);

        public ItemStack WithMetadata(ushort newMetadata) => new ItemStack(ItemId, Count, newMetadata);

        public override string ToString() => IsEmpty ? "空" : $"ItemStack(id={ItemId}, count={Count}, meta={Metadata})";
    }
}
