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

        /// <summary>右键吃的食物值：吃下后经 <see cref="MyWorld.Core.Player.HungerSystem.Eat"/> 喂饥饿。
        /// null 或 0 = 不可食用（bowl_of_water 之类"喝了没用"的物品不算食物，右键照常放方块）。</summary>
        public float? HealAmount;

        /// <summary>是否可作为食物右键吃。m7 A3 起作为进食判定唯一谓词：
        /// BlockInteraction 右键路由与 HotbarUI「右键食用」提示共用，避免两处各判各的漂移。</summary>
        public bool IsEdible => HealAmount > 0f;

        /// <summary>是否归类为工具（剑/镐/斧/锹）。用于耐久条与方块采集加速判定。</summary>
        public bool IsTool;

        /// <summary>挖掘等级：0=手 / 1=木 / 2=石 / 3=铁 / 4=钻石 / 5=下界合金 / 6=基岩。</summary>
        public int MiningLevel;

        /// <summary>
        /// m10 A3 镐门槛等级：0 徒手 / 1 木镐 / 2 石镐 / 3 铁镐 / 4 钻石镐（下界合金镐按
        /// MC 惯例与钻同级 = 4，基岩镐彩蛋 99）。<b>只有镐类物品写这个字段</b>——剑/斧/锹
        /// 不写（默认 0，视同徒手过门槛），因此它和 <see cref="MiningLevel"/>（耐久档位，
        /// 所有工具都有）分工不同，别混用。与 blocks/*.json 的 <c>minToolTier</c> 同尺度，
        /// <see cref="MyWorld.Core.Blocks.BlockGating.CanDrop"/> 拿两者比大小。
        /// </summary>
        public int ToolTier;

        public override string ToString() => $"Item({Id}, id={NumericId})";
    }
}
