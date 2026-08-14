using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using MyWorld.Core.Items;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 单条掉落定义：物品 ID、堆叠数区间、命中概率（0..1）。
    /// <para>
    /// 数量走 <c>countMin</c> / <c>countMax</c> 区间，由 <see cref="RollCount"/> 用
    /// 整数哈希派生的确定性 RNG 选出（不依赖 <c>System.Random</c>，保证 replay 可复现）。
    /// 若 JSON 写的是 <c>count: 1</c>（旧 schema），同时填 <c>countMin = countMax = 1</c>。
    /// </para>
    /// </summary>
    public class DropEntry
    {
        public int ItemId { get; set; }
        // 单值旧 schema 兼容：仅当 countMin/countMax 缺省时使用。Load 后统一归并到 CountMin/Max。
        public int Count { get; set; }
        public int CountMin { get; set; }
        public int CountMax { get; set; }
        public float Chance { get; set; }

        /// <summary>规范化后写入的最终 [min, max] 区间。</summary>
        public void Normalize()
        {
            // 旧单值 schema 兼容：若缺 countMin/countMax（默认 0）则用 count 兜底。
            // 用 Newtonsoft 默认 int = 0；新 schema 显式给 countMin/countMax 时这两个值
            // 至少 1 个 > 0，不会走进这个分支。
            if (CountMin == 0 && CountMax == 0)
            {
                int legacy = Count > 0 ? Count : 1;
                CountMin = legacy;
                CountMax = legacy;
            }
            if (CountMax < CountMin) CountMax = CountMin;
            if (CountMin < 0) CountMin = 0;
        }
    }

    /// <summary>
    /// Mob 死亡掉落表。Core 纯数据：从 JSON 加载，按 <see cref="Roll"/> 概率掷骰。
    /// <para>
    /// 与 <c>MyWorld.Core.Items.ItemDropTable</c>（静态、按 mobKind 全量返回 <see cref="ItemStack"/>[]）
    /// 职责不同——后者是 MobAI 死亡时拿到的确定性列表，本类面向概率驱动的生成场景。
    /// 两者命名空间不同，互不冲突。
    /// </para>
    /// <para>
    /// JSON 形态（<c>Assets/StreamingAssets/mobs/drop_tables.json</c>）：
    /// <code>
    /// {
    ///   "Pig":     [{ "itemId": 1008, "countMin": 1, "countMax": 3, "chance": 1.0 }],
    ///   "Zombie":  [
    ///     { "itemId": 1010, "countMin": 0, "countMax": 2, "chance": 0.5 },
    ///     { "itemId": 1004, "countMin": 1, "countMax": 1, "chance": 0.05 }
    ///   ]
    /// }
    /// </code>
    /// 同一 mob 多条 entry 时按顺序累计 chance；总和 &lt; 1 时未命中返回 null。
    /// </para>
    /// </summary>
    public class ItemDropTable
    {
        private readonly Dictionary<MobKind, List<DropEntry>> _table;

        private ItemDropTable(Dictionary<MobKind, List<DropEntry>> table)
        {
            _table = table;
        }

        /// <summary>从 JSON 文件加载。文件不存在或格式错误抛 <see cref="InvalidDataException"/>。</summary>
        public static ItemDropTable Load(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"找不到掉落表 JSON：{jsonPath}", jsonPath);
            }

            string json = File.ReadAllText(jsonPath);
            var raw = JsonConvert.DeserializeObject<Dictionary<string, List<DropEntry>>>(json);
            if (raw == null)
            {
                throw new InvalidDataException($"掉落表 JSON 解析为空：{jsonPath}");
            }

            var dict = new Dictionary<MobKind, List<DropEntry>>();
            foreach (var kv in raw)
            {
                MobKind kind = ParseKind(kv.Key);
                if (kv.Value != null)
                {
                    foreach (var entry in kv.Value)
                    {
                        entry.Normalize();
                    }
                }
                dict[kind] = kv.Value;
            }
            return new ItemDropTable(dict);
        }

        /// <summary>
        /// 按 seed 掷骰，返回该 mob 死亡时应掉落的物品栈；多条 entry 按 chance 累计概率命中。
        /// 未配置的 mob 或累计 chance 之外的余量返回 null。
        /// <para>
        /// 命中后的 count = <see cref="RollCount"/> 派生的 [countMin, countMax] 区间值，
        /// 与 itemId 联合 JITAO：itemId 决定 JSON 哪条 entry，seed 决定 chance 命中与 count。
        /// </para>
        /// </summary>
        public ItemStack? Roll(MobKind kind, int seed)
        {
            if (!_table.TryGetValue(kind, out var entries) || entries == null || entries.Count == 0)
            {
                return null;
            }

            // 简单整数哈希：2654435761 是 Knuth 黄金比例乘倒数，保证不同 seed 散列均匀
            uint h = unchecked((uint)(seed * 2654435761));
            // 取高 16 位当 [0,1) 浮点
            float roll = (h & 0xFFFF) / 65535f;

            float cumulative = 0f;
            int entryIndex = 0;
            foreach (var e in entries)
            {
                cumulative += e.Chance;
                if (roll < cumulative)
                {
                    // 用 seed × entryIndex 区分 count 与 chance 命中，避免完全同步
                    int countSeed = unchecked(seed * 31 + entryIndex * 17 + 1);
                    int count = RollCount(countSeed, e.CountMin, e.CountMax);
                    return new ItemStack(e.ItemId, count);
                }
                entryIndex++;
            }
            return null;
        }

        /// <summary>
        /// 给定整数种子计算 [min, max] 区间内的伪随机 count（闭区间，含两端）。
        /// 用整数哈希，与 <see cref="MyWorld.Core.Blocks.BlockDrops"/> 同款风格，跨机器一致。
        /// </summary>
        public static int RollCount(int seed, int min, int max)
        {
            if (max < min) max = min;
            if (min < 0) min = 0;
            int range = max - min + 1;
            if (range <= 1) return min;

            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= h >> 13;
                h *= 2654435761u;
                h ^= h >> 16;
                return min + (int)(h % (uint)range);
            }
        }

        private static MobKind ParseKind(string name)
        {
            switch (name)
            {
                case "Pig": return MobKind.Pig;
                case "Cow": return MobKind.Cow;
                case "Chicken": return MobKind.Chicken;
                case "Zombie": return MobKind.Zombie;
                default:
                    throw new System.ArgumentException(
                        $"未知 MobKind: {name}（合法的有 Pig/Cow/Chicken/Zombie）");
            }
        }
    }
}
