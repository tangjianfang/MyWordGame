using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyWorld.Core.Items
{
    /// <summary>
    /// 配方表：从 <c>StreamingAssets/recipes/*.json</c> 解析。
    /// 字段解析比较宽松（与方块系统保持一致），缺字段直接抛 InvalidDataException。
    /// </summary>
    public sealed class RecipeDatabase
    {
        private readonly List<Recipe> _all = new List<Recipe>();

        public IReadOnlyList<Recipe> All => _all;

        public int Count => _all.Count;

        public static RecipeDatabase FromJson(IEnumerable<string> jsonDocuments, ItemDatabase items)
        {
            if (jsonDocuments == null) throw new System.ArgumentNullException(nameof(jsonDocuments));
            if (items == null) throw new System.ArgumentNullException(nameof(items));

            var db = new RecipeDatabase();

            foreach (string doc in jsonDocuments)
            {
                Recipe r = Parse(doc, items);
                db._all.Add(r);
            }
            return db;
        }

        public Recipe FindMatch(IReadOnlyList<ItemStack> input, int width, int height)
        {
            // 优先匹配最严格的档位，避免 2x2 配方被 3x3 网格误中
            foreach (var tier in new[] { CraftingTier.Workbench3x3, CraftingTier.Inventory2x2, CraftingTier.Pocket1x1 })
            {
                // m11 ②（B 批守卫修复）：同档位内先稳定排序再试匹配——
                //   ① shaped 优先于 shapeless；② 材料格多者优先（Recipe.MaterialCount）。
                // 否则「6 板摆两行合门」（shaped）会被「4 板合工作台」（shapeless）抢匹配：
                // shapeless 只数材料总数，6 ≥ 4 恒真。修复前靠配方文件名恰好排在前面侥幸不抢
                // （文件枚举顺序是文件系统实现细节，跨机器不可靠）。
                // 并列时按 id 序数决胜，彻底摆脱枚举顺序。
                Recipe r = CraftingMatrix.FindMatch(
                    input, width, height,
                    _all.Where(x => x.Tier == tier)
                        .OrderByDescending(x => x.Shaped)
                        .ThenByDescending(x => x.MaterialCount)
                        .ThenBy(x => x.Id, StringComparer.Ordinal));
                if (r != null) return r;
            }
            return null;
        }

        private static Recipe Parse(string json, ItemDatabase items)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (JsonException ex) { throw new InvalidDataException($"配方不是合法 JSON：{ex.Message}", ex); }

            var id = (string)root["id"];
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidDataException("配方缺少 id 字段。");

            var tierStr = (string)root["tier"] ?? "pocket";
            CraftingTier tier = tierStr switch
            {
                "pocket" => CraftingTier.Pocket1x1,
                "inventory" => CraftingTier.Inventory2x2,
                "workbench" => CraftingTier.Workbench3x3,
                _ => throw new InvalidDataException($"配方 {id} 的 tier 未知：{tierStr}"),
            };

            int width = (int?)root["width"] ?? (tier == CraftingTier.Pocket1x1 ? 1 : tier == CraftingTier.Inventory2x2 ? 2 : 3);
            int height = (int?)root["height"] ?? width;

            var patternToken = root["pattern"] as JArray;
            if (patternToken == null) throw new InvalidDataException($"配方 {id} 缺少 pattern 字段。");
            if (patternToken.Count != width * height)
                throw new InvalidDataException($"配方 {id} 的 pattern 长度 {patternToken.Count} ≠ {width}*{height}。");

            var pattern = new ItemStack[width * height];
            for (var i = 0; i < pattern.Length; i++)
            {
                var sym = (string)patternToken[i];
                pattern[i] = ResolveSymbol(sym, items);
            }

            var outputToken = root["output"] as JObject;
            if (outputToken == null) throw new InvalidDataException($"配方 {id} 缺少 output。");
            var outputItemId = (string)outputToken["item"];
            if (string.IsNullOrWhiteSpace(outputItemId)) throw new InvalidDataException($"配方 {id} 的 output 缺 item 字段。");
            if (!items.TryGetById(outputItemId, out var outDef))
                throw new InvalidDataException($"配方 {id} 的输出物品未注册：{outputItemId}");
            int outputCount = (int?)outputToken["count"] ?? 1;

            return new Recipe
            {
                Id = id,
                Tier = tier,
                Width = width,
                Height = height,
                Pattern = pattern,
                Shaped = (bool?)root["shaped"] ?? true,
                Output = new ItemStack(outDef.NumericId, outputCount),
            };
        }

        private static ItemStack ResolveSymbol(string sym, ItemDatabase items)
        {
            if (string.IsNullOrEmpty(sym) || sym == "_" || sym == "0")
                return ItemStack.Empty;
            if (!items.TryGetById(sym, out var def))
                throw new InvalidDataException($"配方引用了未注册的物品：{sym}");
            return new ItemStack(def.NumericId, 1);
        }
    }
}
