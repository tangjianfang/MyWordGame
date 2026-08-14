namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 村民交易池：每职业 4-6 条交易模板，按 <c>(worldX, worldZ, profession)</c> 哈希挑 3-5 条。
    /// <para>
    /// X3 fix-up：原 <c>VillagerManager.BuildOffersFor</c> 只返回 2 条（spec D7 要求 3-5）。
    /// 现搬到 Core 层，纯数据 + 确定性哈希，无 <c>UnityEngine.Random</c>，dotnet 链可独立验证。
    /// </para>
    /// </summary>
    public static class VillagerOffers
    {
        /// <summary>Farmer 模板池：农产品 + 食物。</summary>
        public static readonly TradeOffer[] FarmerPool = new[]
        {
            new TradeOffer("emerald", 1, "beet", 4, 0, 8),
            new TradeOffer("emerald", 1, "mung_bean", 6, 0, 12),
            new TradeOffer("emerald", 3, "mung_bean_soup", 1, 0, 4),
            new TradeOffer("emerald", 4, "bread", 1, 0, 16),
            new TradeOffer("emerald", 2, "plank", 8, 0, 16),
            new TradeOffer("emerald", 1, "wool", 2, 0, 8),
        };

        /// <summary>Librarian 模板池：书 + 红石类。</summary>
        public static readonly TradeOffer[] LibrarianPool = new[]
        {
            new TradeOffer("emerald", 5, "enchanted_book", 1, 0, 3),
            new TradeOffer("book", 3, "emerald", 1, 0, 16),
            new TradeOffer("emerald", 1, "lapis", 1, 0, 8),
            new TradeOffer("emerald", 8, "globe", 1, 0, 4),
            new TradeOffer("book", 5, "emerald", 2, 0, 12),
            new TradeOffer("emerald", 2, "redstone", 4, 0, 16),
        };

        /// <summary>Blacksmith 模板池：金属工具 + 矿石。</summary>
        public static readonly TradeOffer[] BlacksmithPool = new[]
        {
            new TradeOffer("emerald", 10, "diamond_sword", 1, 0, 1),
            new TradeOffer("emerald", 3, "iron_ingot", 1, 0, 16),
            new TradeOffer("emerald", 6, "iron_sword", 1, 0, 4),
            new TradeOffer("emerald", 4, "iron_pickaxe", 1, 0, 8),
            new TradeOffer("emerald", 2, "coal", 8, 0, 16),
            new TradeOffer("emerald", 5, "iron_axe", 1, 0, 4),
        };

        /// <summary>
        /// 给定职业和世界坐标，返回 3-5 条交易。
        /// 同一 (worldX, worldZ, profession) 永远返回同样的 offer 列表（位置哈希稳定）。
        /// <para>
        /// None 职业返回 1 条 log 兜底（保持 <c>VillagerManager</c> 旧行为）。
        /// </para>
        /// </summary>
        public static TradeOffer[] Build(VillagerProfession profession, int worldX, int worldZ)
        {
            TradeOffer[] pool;
            switch (profession)
            {
                case VillagerProfession.Farmer: pool = FarmerPool; break;
                case VillagerProfession.Librarian: pool = LibrarianPool; break;
                case VillagerProfession.Blacksmith: pool = BlacksmithPool; break;
                default:
                    // None / 未知：单条 log 兜底（保留旧 BuildOffersFor 行为）
                    return new[]
                    {
                        new TradeOffer("emerald", 1, "log", 4, 0, 16),
                    };
            }

            // 哈希 (worldX, worldZ, profession) → 决定挑几条 + 从池子里取哪些
            uint h = unchecked((uint)(worldX * 73856093
                                      ^ worldZ * 19349663
                                      ^ (int)profession * 83492791));
            // count: 3, 4, 5（高 8 位 mod 3 → 0..2）
            int count = 3 + (int)((h >> 8) % 3);
            // start: 池子起点（中 16 位 mod pool.Length）
            int start = (int)((h >> 16) % pool.Length);
            var result = new TradeOffer[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = pool[(start + i) % pool.Length];
            }
            return result;
        }
    }
}