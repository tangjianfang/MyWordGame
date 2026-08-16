namespace MyWorld.Core.Items
{
    /// <summary>
    /// 不可变物品栈。ItemId = 0 表示空气（<see cref="IsEmpty"/>），Count = 0 也视为空。
    /// 用 <see cref="WithCount"/> 改 count 而保持其他字段。
    /// <para>
    /// Metadata 编码（耐久 + 附魔 placeholder）：bits 0-7 = 剩余耐久，bits 8-15 = 最大耐久（≤255，
    /// 8 位是 m3 预留编码的硬上限）。两段都是 0 = 还没启用耐久（<see cref="HasDurability"/> 为
    /// false），<b>视为满耐久</b>——旧存档/预填/刚合成的工具在首次消耗时才落编码。已写入上限后
    /// 剩余从 max 递减，扣到 0 的那一刻物品即损坏变 <see cref="Empty"/>（cur=0 且 max&gt;0 的
    /// 状态不可达）。非工具物品保持 0。
    /// </para>
    /// </summary>
    public readonly struct ItemStack
    {
        public const ushort MaxDurabilityMask = 0xFF00;
        public const ushort CurDurabilityMask = 0x00FF;

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

        public bool HasDurability => (Metadata & MaxDurabilityMask) != 0;

        public int CurrentDurability => Metadata & CurDurabilityMask;

        public int MaxDurability => (Metadata & MaxDurabilityMask) >> 8;

        /// <summary>给一个全新工具设置耐久（cur=max）。</summary>
        public ItemStack WithMaxDurability(int max)
        {
            if (max < 0) max = 0;
            if (max > 255) max = 255;
            ushort md = (ushort)((max << 8) | max);
            return WithMetadata(md);
        }

        /// <summary>消耗 1 点耐久，返回新 ItemStack；cur=0 时把物品变空（已损坏）。</summary>
        public ItemStack DamageOnce()
        {
            if (!HasDurability) return this;
            int cur = CurrentDurability - 1;
            if (cur < 0) cur = 0;
            ushort md = (ushort)((MaxDurability << 8) | cur);
            if (cur == 0) return new ItemStack(0, 0);   // 损坏：变空
            return WithMetadata(md);
        }

        /// <summary>
        /// m10 B1：消耗 1 点耐久（挖掉一个方块成功 / 挥击一次后调用）。
        /// <paramref name="maxDurability"/> 取自物品表的 <c>maxDurability</c>；
        /// ≤0（非工具 / 未声明耐久）原样返回，不碰 Metadata。
        /// <para>
        /// <b>Metadata=0 的存量兼容</b>：视为满耐久——先按 max 初始化再扣 1，
        /// m10 之前的旧存档 / 预填 / 刚合成的工具绝不因缺编码被当成已损坏。
        /// 已有耐久位则沿用 Metadata 里存的上限（一次写入终身有效，后来改 JSON
        /// 不追溯旧工具，否则存档里的镐会凭空变耐久）。耐久扣尽返回
        /// <see cref="Empty"/>（调用方负责移除与提示；碎块散落是 B2）。
        /// </para>
        /// </summary>
        public ItemStack WithDurabilityUsed(int maxDurability)
        {
            if (maxDurability <= 0) return this;
            if (!HasDurability) return WithMaxDurability(maxDurability).DamageOnce();
            return DamageOnce();
        }

        public override string ToString() => IsEmpty ? "空" : $"ItemStack(id={ItemId}, count={Count}, meta={Metadata})";
    }
}
