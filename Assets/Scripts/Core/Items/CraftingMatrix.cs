using System.Collections.Generic;

namespace MyWorld.Core.Items
{
    /// <summary>
    /// 配方匹配 + 消耗。输入网格由调用方提供（长度 = Width * Height），
    /// <see cref="FindMatch"/> 找到第一条匹配的配方。
    /// </summary>
    public static class CraftingMatrix
    {
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
                                if (pat.ItemId != inp.ItemId || pat.Count > inp.Count) ok = false;
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
                    if (inp.IsEmpty) continue;
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
                    if (remain.TryGetValue(inp.ItemId, out int need) && need > 0)
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
                    if (pat.ItemId != inp.ItemId || pat.Count > inp.Count) return false;
                }
            }
            return hasAny;
        }
    }
}
