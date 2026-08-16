namespace MyWorld.Core.Blocks
{
    /// <summary>单个方块的静态属性，由 <c>StreamingAssets/blocks/*.json</c> 定义。</summary>
    public sealed class BlockDefinition
    {
        public string Id { get; internal set; }

        public string DisplayName { get; internal set; }

        public ushort NumericId { get; internal set; }

        /// <summary>六个面的贴图名，索引见 <see cref="BlockFace"/>。空气为 null。</summary>
        public string[] Textures { get; internal set; }

        public bool Solid { get; internal set; }

        public bool Opaque { get; internal set; }

        public byte LightEmission { get; internal set; }

        /// <summary>破坏耗时基准（秒）。负数表示不可破坏。</summary>
        public float Hardness { get; internal set; }

        /// <summary>
        /// 挖掉该方块所需的最低镐等级（m10 工具门槛矩阵）：0 徒手 / 1 木镐 / 2 石镐 /
        /// 3 铁镐 / 4 钻石镐，与物品表既有 <c>miningLevel</c> 同尺度。等级不够时挖得掉方块但不掉落。
        /// 默认 0（徒手可挖），数据驱动，加矿不改代码。
        /// </summary>
        public int MinToolTier { get; internal set; }

        public bool Liquid { get; internal set; }

        public bool IsUnbreakable => Hardness < 0f;

        public override string ToString() => $"{Id}#{NumericId}";
    }
}
