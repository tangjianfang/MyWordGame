using System.Collections.Generic;

namespace MyWorld.Core.Items
{
    /// <summary>
    /// 配方档位：口袋（1×1）、背包内嵌（2×2）、工作台（3×3）。
    /// 网格宽度决定最多支持几列摆放，长度不固定。
    /// </summary>
    public enum CraftingTier
    {
        Pocket1x1,
        Inventory2x2,
        Workbench3x3,
    }

    /// <summary>
    /// 单条配方。
    /// <para>
    /// Shaped=true 时 <see cref="Pattern"/> 必须按位置完全匹配（空位也必须空），
    /// 用 <c>_</c> (id=0) 表示空格。Shaped=false 时只检查 <see cref="Ingredients"/>
    /// 的非空集合是否在输入里完整出现，不要求排列。
    /// </para>
    /// </summary>
    public sealed class Recipe
    {
        public string Id;
        public CraftingTier Tier;
        public int Width;
        public int Height;
        public ItemStack[] Pattern;     // 长度 = Width * Height，0 = 空
        public bool Shaped;
        public ItemStack Output;

        public IReadOnlyList<ItemStack> Ingredients
        {
            get
            {
                var set = new List<ItemStack>();
                var seen = new HashSet<int>();
                foreach (var p in Pattern)
                {
                    if (p.IsEmpty) continue;
                    if (seen.Add(p.ItemId))
                    {
                        set.Add(p);
                    }
                }
                return set;
            }
        }
    }
}
