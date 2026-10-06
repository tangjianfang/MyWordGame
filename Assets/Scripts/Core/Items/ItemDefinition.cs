namespace MyWorld.Core.Items
{
    /// <summary>
    /// m10 C1 装备加成作用的属性类别。取值来自 items/*.json 的
    /// <c>gearBonus.stat</c>（受控词表：defense / moveSpeed / maxHealth，
    /// 写错 <see cref="ItemDatabase"/> 加载即抛）。金系的攻击加成**不在此列**——
    /// 它叠加在武器既有 <see cref="ItemDefinition.AttackDamage"/> 上，不走 gearBonus 通道。
    /// </summary>
    public enum GearStat
    {
        /// <summary>无加成（绝大多数物品的缺省值）。</summary>
        None = 0,

        /// <summary>防御 +N 点：受伤减伤（<see cref="MyWorld.Core.Player.GearBonusMath.MitigateDamage"/>）。铁系。</summary>
        Defense,

        /// <summary>移速 +N 比例（0.05 = +5%）：行走目标速度乘 (1 + N)。夏季合金系。</summary>
        MoveSpeed,

        /// <summary>生命上限 +N 点：有效血上限 = 基础 + N。机元系。</summary>
        MaxHealth,
    }

    /// <summary>
    /// m11 W2-1 盔甲部位（items/*.json 的 <c>armorPart</c> 字段，受控词表）。
    /// 写了部位的物品是「盔甲」，可进玩家穿戴栏（<see cref="MyWorld.Core.Player.ArmorInventory"/>）；
    /// 部位决定进哪个穿戴槽，与 <see cref="GearStat"/> 正交——同材料四件共用一种属性：
    /// 铁系 defense / 夏季合金系 moveSpeed / 机元系 maxHealth；金系加成是攻击
    /// （走手持 <see cref="ItemDefinition.AttackDamage"/>），盔甲形态无 gearBonus。
    /// </summary>
    public enum ArmorPart
    {
        /// <summary>非盔甲（绝大多数物品的缺省值）。</summary>
        None = 0,

        /// <summary>头盔（头部槽 0）。</summary>
        Helmet = 1,

        /// <summary>胸甲（胸部槽 1）。</summary>
        Chest = 2,

        /// <summary>护腿（腿部槽 2）。</summary>
        Legs = 3,

        /// <summary>靴子（脚部槽 3）。</summary>
        Boots = 4,
    }

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

        /// <summary>
        /// m10 B1 耐久上限：能承受的消耗次数（次方块 / 挥击一次都算 1）。
        /// 0 = 无耐久概念（非工具或未声明，永不磨损）；取值 1..255——
        /// <see cref="ItemStack"/> 的 Metadata 只有 8 位存上限（m3 预留编码），
        /// 超范围在 <see cref="ItemDatabase"/> 加载层即抛。镐类必须显式声明
        /// （BlockGatingTests 有守卫），剑/斧/锹 m10 暂不启用（保持 0）。
        /// </summary>
        public int MaxDurability;

        /// <summary>
        /// m10 C1 手持装备属性加成的类别；<see cref="GearStat.None"/> = 无加成
        /// （绝大多数物品）。简化模型：手持该物品即生效、切走即失效
        /// （<see cref="MyWorld.Core.Player.GearBonuses.FromDefinition"/> 消费）。
        /// </summary>
        public GearStat GearStat = GearStat.None;

        /// <summary>
        /// m10 C1 手持该物品时的属性加成量：<see cref="GearStat.Defense"/> /
        /// <see cref="GearStat.MaxHealth"/> 按点数，<see cref="GearStat.MoveSpeed"/>
        /// 按比例（0.05 = +5%）。类别为 None 时无意义（保持 0）。
        /// </summary>
        public float GearAmount;

        /// <summary>
        /// m11 W2-1 盔甲部位（<c>items/*.json</c> 的 <c>armorPart</c>）。
        /// <see cref="ArmorPart.None"/> = 非盔甲；有部位的物品由
        /// <see cref="MyWorld.Core.Player.ArmorInventory"/> 按部位收进对应穿戴槽，
        /// gearBonus 改从穿戴源生效（手持它时仍按 m10 手持源生效，双源并存不重复计——
        /// 同一件物品不可能既穿着又拿着）。
        /// </summary>
        public ArmorPart ArmorPart = ArmorPart.None;

        /// <summary>
        /// m11 W3-1：该物品右键放置时对应的方块字符串 id（<c>items/*.json</c> 的 <c>blockId</c>）。
        /// null/空 = 无关联方块——右键<b>不放置</b>（评审 07#9 起 m3「恒放石头占位」路径退役）。
        /// 家具 9 件 + 附魔台 / 箱子 / 床 / 木门写这个字段，放置路由据此放对应方块并扣 1 个物品；
        /// 跨表一致性（blockId 必须能在 blocks 注册表解析到）由真数据守卫测试把守
        /// （BlockInteractionPlaceRoutingTests）。
        /// </summary>
        public string BlockId;

        /// <summary>
        /// m13 W3：远程武器射程（米）。弓 60（保留抛物线）/ 火枪 25（直射）等——
        /// 写 0 或不写 = 不可作为远程武器（近战/材料）。弹道飞行距离 <c>&gt;= Range</c>
        /// 时强制消亡（防无限射程破坏弹道手感与性能）。射程在 <see cref="ItemDatabase"/>
        /// 加载层校验：缺失默认 0、负数立刻抛（与 toolTier / maxDurability 同态度）。
        /// 弓保留重力抛物线（既有行为），枪走直射无重力——走
        /// <see cref="MyWorld.Core.Entities.ProjectileEntity.IsStraightLine"/>。
        /// </summary>
        public int Range;

        public override string ToString() => $"Item({Id}, id={NumericId})";
    }
}