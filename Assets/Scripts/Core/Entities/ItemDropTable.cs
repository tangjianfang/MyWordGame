using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using MyWorld.Core.Items;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 单条掉落定义：物品 ID、堆叠数、命中概率（0..1）。
    /// </summary>
    public class DropEntry
    {
        public int ItemId { get; set; }
        public int Count { get; set; }
        public float Chance { get; set; }
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
    ///   "Pig":     [{ "itemId": 1008, "count": 1, "chance": 1.0 }],
    ///   "Zombie":  [{ "itemId": 1010, "count": 1, "chance": 0.5 }]
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
                dict[kind] = kv.Value;
            }
            return new ItemDropTable(dict);
        }

        /// <summary>
        /// 按 seed 掷骰，返回该 mob 死亡时应掉落的物品栈；多条 entry 按 chance 累计概率命中。
        /// 未配置的 mob 或累计 chance 之外的余量返回 null。
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
            foreach (var e in entries)
            {
                cumulative += e.Chance;
                if (roll < cumulative)
                {
                    return new ItemStack(e.ItemId, e.Count);
                }
            }
            return null;
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
