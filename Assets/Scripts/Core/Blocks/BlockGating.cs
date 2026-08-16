using MyWorld.Core.Items;

namespace MyWorld.Core.Blocks
{
    /// <summary>
    /// m10 A3 工具门槛矩阵（spec §1）的纯函数：孩子的「木镐挖铁 40 小时」按 MC 制
    /// 修正为门槛 + 固定倍率——镐等级 &lt; 方块 minToolTier 时<b>挖得掉方块但无掉落</b>，
    /// 且挖掘耗时按徒手档（×4）走。Unity 侧 <c>BlockInteraction</c> 的门槛分支与
    /// 挖掘时间表都查这里；本类零 Unity 依赖，dotnet 链直接测（<c>BlockGatingTests</c>）。
    /// </summary>
    public static class BlockGating
    {
        /// <summary>
        /// 低于门槛时的挖掘耗时倍率：石头达标镐 1s、徒手 4s——
        /// spec §1「徒手 4s 不掉落」一行就是这个 ×4 的出处。门槛只影响掉落与
        /// 「镐帮不上忙」，不是 24 小时式的挂机惩罚。
        /// </summary>
        public const float BelowTierTimeMultiplier = 4f;

        /// <summary>
        /// 门槛判定（唯一入口）：toolTier ≥ blockMinToolTier 才允许走 BlockDrops；
        /// 不够 = 挖得掉但无掉落。等级取物品的 <see cref="ItemDefinition.ToolTier"/>
        /// （只有镐类物品写这个字段），与方块 <see cref="BlockDefinition.MinToolTier"/> 同尺度。
        /// </summary>
        public static bool CanDrop(int blockMinToolTier, int toolTier)
            => toolTier >= blockMinToolTier;

        /// <summary>
        /// 选中物品 → 镐等级：空手 / 非镐类（剑/斧/锹/材料，未写 toolTier）一律 0。
        /// <see cref="ItemDefinition.MiningLevel"/> 是耐久档位，别拿来冒充镐门槛。
        /// </summary>
        public static int ResolveToolTier(ItemDefinition selected)
            => selected?.ToolTier ?? 0;

        /// <summary>
        /// (block, toolTier) → 秒：达标 = <paramref name="hardness"/>（spec §1
        /// 「挖掘时间（对应镐）」列，即达标镐挖它的时间）；不达标 = hardness ×
        /// <see cref="BelowTierTimeMultiplier"/>（镐帮不上忙，按徒手档）。
        /// 超配不奖励：spec 只定义了「对应镐」一列，钻镐挖石与木镐同速。
        /// 负 hardness（不可破坏，如基岩）由调用方用 <see cref="BlockDefinition.IsUnbreakable"/>
        /// 先拦，本函数只做乘法不判符号。
        /// </summary>
        public static float BreakSeconds(float hardness, int blockMinToolTier, int toolTier)
            => CanDrop(blockMinToolTier, toolTier) ? hardness : hardness * BelowTierTimeMultiplier;
    }
}
