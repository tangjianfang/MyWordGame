namespace MyWorld.Core.Items
{
    /// <summary>
    /// 物品元数据。从 <c>items/*.json</c> 解析得到。
    /// 数值 ID 由 <see cref="ItemDatabase"/> 分配，与 <see cref="MyWorld.Core.Blocks.BlockRegistry"/> 互不干扰。
    /// </summary>
    public sealed class ItemDefinition
    {
        /// <summary>字符串 id（与方块同风格），存档里实际存的是 <see cref="NumericId"/>。</summary>
        public string Id;
        public string DisplayName;

        public int NumericId;
        public int MaxStack = 64;
        public string Texture;

        /// <summary>主手挥击伤害。null = 不可作为武器；手握该物品时主手单击不起作用。</summary>
        public float? AttackDamage;

        /// <summary>右键吃的回血量。null = 不可食用。</summary>
        public float? HealAmount;

        /// <summary>是否归类为工具（剑/镐/斧/锹）。用于耐久条与方块采集加速判定。</summary>
        public bool IsTool;

        /// <summary>挖掘等级：0=手 / 1=木 / 2=石 / 3=铁 / 4=钻石 / 5=下界合金 / 6=基岩。</summary>
        public int MiningLevel;

        public override string ToString() => $"Item({Id}, id={NumericId})";
    }
}
