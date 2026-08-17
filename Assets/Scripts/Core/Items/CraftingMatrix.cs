using System.Collections.Generic;

namespace MyWorld.Core.Items
{
    /// <summary>
    /// 配方匹配 + 消耗。输入网格由调用方提供（长度 = Width * Height），
    /// <see cref="FindMatch"/> 找到第一条匹配的配方。
    /// <para>
    /// m10 C2 fix1（I3）材料门槛：**带耐久编码的物品必须满耐久**才能作为合成输入——
    /// 残血装备 ×2 合成出满耐久新品 = 无限洗耐久漏洞（升级配方的产物是新物品、
    /// 耐久从零写满，材料端必须把关）。Metadata 未启用耐久编码（HasDurability=false）
    /// 按满耐久放行，与 <see cref="ItemStack"/> 的存量兼容语义一致。
    /// </para>
    /// </summary>
    public static class CraftingMatrix
    {
        /// <summary>材料栈是否可用于合成：无耐久概念或耐久满（cur ≥ max）。
        /// shaped 的 <see cref="MatchesAt"/>、shapeless 的计数与扣减三处共用，
        /// 保证「能匹配的网格一定扣得动、扣的位置一定通过过判定」。</summary>
        private static bool UsableAsMaterial(ItemStack stack)
            => !stack.HasDurability || stack.CurrentDurability >= stack.MaxDurability;

        /// <summary>网格里是否有「带耐久且不满耐久」的物品（合成不可用材料）。
        /// 匹配层已拒合，本方法供 UI 显示「装备耐久不满，不能合成」的提示——
        /// 否则输出格空着，孩子不知道为什么合不出来。</summary>
        public static bool HasDamagedMaterial(IReadOnlyList<ItemStack> input)
        {
            foreach (ItemStack s in input)
            {
                if (!s.IsEmpty && !UsableAsMaterial(s)) return true;
            }
            return false;
        }

        public static Recipe FindMatch(IReadOnlyList<ItemStack> input, int width, int height, IEnumerable<Recipe> recipes)
        {
            foreach (var recipe in recipes)
            {
                if (recipe.Tier == CraftingTier.Pocket1x1 && (width != 1 || height != 1)) continue;
                if (recipe.Tier == CraftingTier.Inventory2x2 && (width < 2 || height < 2)) continue;
                if (recipe.Tier == CraftingTier.Workbench3x3 && (width < 3 || height < 3)) continue;

                if (Matches(recipe, input, width))
                {
                    return recipe;
                }
            }
            return null;
        }

        public static bool Matches(Recipe recipe, IReadOnlyList<ItemStack> input, int inputWidth)
        {
            int rw = recipe.Width;
            int rh = recipe.Height;
            int inputH = input.Count / inputWidth;

            if (recipe.Shaped)
            {
                if (inputWidth < rw || inputH < rh) return false;
                // 在 input 里滑窗找与 pattern 完全一致的子矩阵
                for (var oy = 0; oy <= inputH - rh; oy++)
                {
                    for (var ox = 0; ox <= inputWidth - rw; ox++)
                    {
                        bool ok = true;
                        bool hasAny = false;
                        for (var py = 0; py < rh && ok; py++)
                        {
                            for (var px = 0; px < rw && ok; px++)
                            {
                                var pat = recipe.Pattern[py * rw + px];
                                var inp = input[(oy + py) * inputWidth + (ox + px)];
                                if (!pat.IsEmpty) hasAny = true;
                                if (pat.ItemId != inp.ItemId || pat.Count > inp.Count
                                    || !UsableAsMaterial(inp)) ok = false;
                            }
                        }
                        if (ok && hasAny) return true;
                    }
                }
                return false;
            }
            else
            {
                // 无序：检查每个非空 pattern 物品的总数是否能在 input 中找到
                var need = new Dictionary<int, int>();
                foreach (var p in recipe.Pattern)
                {
                    if (p.IsEmpty) continue;
                    if (!need.TryGetValue(p.ItemId, out int c)) c = 0;
                    need[p.ItemId] = c + p.Count;
                }
                if (need.Count == 0) return false;

                var have = new Dictionary<int, int>();
                foreach (var inp in input)
                {
                    // 残血材料不计入可用量（m10 C2 fix1 I3）——满耐久的同款物品凑数才匹配
                    if (inp.IsEmpty || !UsableAsMaterial(inp)) continue;
                    if (!have.TryGetValue(inp.ItemId, out int c)) c = 0;
                    have[inp.ItemId] = c + inp.Count;
                }
                foreach (var kv in need)
                {
                    if (!have.TryGetValue(kv.Key, out int got) || got < kv.Value) return false;
                }
                return true;
            }
        }

        /// <summary>从 input 扣掉 recipe.Pattern 所需的各物品，返回每格被扣的数量（与 input 同长）。</summary>
        public static int[] Consume(Recipe recipe, IReadOnlyList<ItemStack> input, int inputWidth)
        {
            var consumed = new int[input.Count];
            if (recipe.Shaped)
            {
                int rw = recipe.Width, rh = recipe.Height;
                int inputH = input.Count / inputWidth;
                for (var oy = 0; oy <= inputH - rh; oy++)
                {
                    for (var ox = 0; ox <= inputWidth - rw; ox++)
                    {
                        if (MatchesAt(recipe, input, inputWidth, ox, oy))
                        {
                            for (var py = 0; py < rh; py++)
                            {
                                for (var px = 0; px < rw; px++)
                                {
                                    var pat = recipe.Pattern[py * rw + px];
                                    if (!pat.IsEmpty)
                                    {
                                        consumed[(oy + py) * inputWidth + (ox + px)] = pat.Count;
                                    }
                                }
                            }
                            return consumed;
                        }
                    }
                }
            }
            else
            {
                // 扣到刚好够
                var remain = new Dictionary<int, int>();
                foreach (var p in recipe.Pattern)
                {
                    if (p.IsEmpty) continue;
                    if (!remain.TryGetValue(p.ItemId, out int c)) c = 0;
                    remain[p.ItemId] = c + p.Count;
                }
                for (var i = 0; i < input.Count; i++)
                {
                    var inp = input[i];
                    if (inp.IsEmpty) continue;
                    // 只从满耐久的格子里扣（与 shapeless Matches 的计数口径一致，m10 C2 fix1 I3）
                    if (remain.TryGetValue(inp.ItemId, out int need) && need > 0 && UsableAsMaterial(inp))
                    {
                        int take = System.Math.Min(need, inp.Count);
                        consumed[i] = take;
                        remain[inp.ItemId] = need - take;
                    }
                }
            }
            return consumed;
        }

        private static bool MatchesAt(Recipe recipe, IReadOnlyList<ItemStack> input, int inputWidth, int ox, int oy)
        {
            int rw = recipe.Width, rh = recipe.Height;
            bool hasAny = false;
            for (var py = 0; py < rh; py++)
            {
                for (var px = 0; px < rw; px++)
                {
                    var pat = recipe.Pattern[py * rw + px];
                    var inp = input[(oy + py) * inputWidth + (ox + px)];
                    if (!pat.IsEmpty) hasAny = true;
                    if (pat.ItemId != inp.ItemId || pat.Count > inp.Count
                        || !UsableAsMaterial(inp)) return false;
                }
            }
            return hasAny;
        }
    }
}
