namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 内置方块 ID。后续由 <c>StreamingAssets/blocks/*.json</c> 注册表接管，这里只保留生成器必需的基础方块。
    /// </summary>
    public static class BlockIds
    {
        public const ushort Air = 0;
        public const ushort Stone = 1;
        public const ushort Dirt = 2;
        public const ushort Grass = 3;
        public const ushort Sand = 4;
        public const ushort Water = 5;
        public const ushort Bedrock = 6;

        /// <summary>雪原地表方块。非 0–6 内置段，与 <c>blocks/snow.json</c> 的 numericId=1008 手动保持一致。</summary>
        public const ushort Snow = 1008;

        /// <summary>金矿石（m10）。与 <c>blocks/gold_ore.json</c> 的 numericId=1009 手动保持一致。</summary>
        public const ushort GoldOre = 1009;

        /// <summary>粗铁矿石（m10）。与 <c>blocks/raw_iron_ore.json</c> 的 numericId=1010 手动保持一致。</summary>
        public const ushort RawIronOre = 1010;

        /// <summary>夏季合金矿石（m10）。与 <c>blocks/summer_alloy_ore.json</c> 的 numericId=1011 手动保持一致。</summary>
        public const ushort SummerAlloyOre = 1011;

        /// <summary>机元矿石（m10，最稀有）。与 <c>blocks/machine_essence_ore.json</c> 的 numericId=1012 手动保持一致。</summary>
        public const ushort MachineEssenceOre = 1012;

        /// <summary>钻石矿石（评审 08 F0：钻石链路断点修复——钻镐/钻剑配方一直消耗 diamond
        /// 却无任何获取途径，y&lt;16 嵌矿）。与 <c>blocks/diamond_ore.json</c> 的 numericId=1065 手动保持一致。</summary>
        public const ushort DiamondOre = 1065;

        /// <summary>床（m11 W1-4）。与 <c>blocks/bed.json</c> 的 numericId=1022 手动保持一致。</summary>
        public const ushort Bed = 1022;

        /// <summary>箱子（m11 W1-4）。与 <c>blocks/chest.json</c> 的 numericId=1023 手动保持一致。</summary>
        public const ushort Chest = 1023;

        /// <summary>木门（m11 W1-4）。与 <c>blocks/wooden_door.json</c> 的 numericId=1026 手动保持一致。</summary>
        public const ushort WoodenDoor = 1026;
    }
}
