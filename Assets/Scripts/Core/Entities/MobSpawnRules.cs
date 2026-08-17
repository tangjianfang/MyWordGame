using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using MyWorld.Core.WorldGen;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 单条 Mob 刷新规则：允许的 biome 列表、最低光照阈值、相对权重。
    /// </summary>
    public class SpawnRule
    {
        public List<string> Biomes { get; set; }
        public int MinLight { get; set; }
        public int Weight { get; set; }
    }

    /// <summary>
    /// Mob 刷新判定表。Core 纯数据：从 JSON 加载，按 biome + 光照 + seed 综合判定。
    /// <para>
    /// JSON 形态（<c>Assets/StreamingAssets/mobs/spawn_rules.json</c>）：
    /// <code>
    /// {
    ///   "Pig":    { "biomes": ["Plains", "Forest"], "minLight": 9, "weight": 10 },
    ///   "Zombie": { "biomes": ["Plains", "Desert"], "minLight": 0, "weight": 5 }
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// 概率换算：weight=10 对应 50% 命中（<c>weight * 5%</c>），
    /// weight=20 → 100%（满命中）。
    /// </para>
    /// </summary>
    public class MobSpawnRules
    {
        private readonly Dictionary<MobKind, SpawnRule> _rules;

        private MobSpawnRules(Dictionary<MobKind, SpawnRule> rules)
        {
            _rules = rules;
        }

        public static MobSpawnRules Load(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException($"找不到刷新规则 JSON：{jsonPath}", jsonPath);
            }

            string json = File.ReadAllText(jsonPath);
            var raw = JsonConvert.DeserializeObject<Dictionary<string, SpawnRule>>(json);
            if (raw == null)
            {
                throw new InvalidDataException($"刷新规则 JSON 解析为空：{jsonPath}");
            }

            var dict = new Dictionary<MobKind, SpawnRule>();
            foreach (var kv in raw)
            {
                MobKind kind = ParseKind(kv.Key);
                dict[kind] = kv.Value;
            }
            return new MobSpawnRules(dict);
        }

        /// <summary>
        /// 给定 biome、光照、随机种子，判断是否应在此处刷该 mob。
        /// 未配置的 mob 一律返回 false。
        /// </summary>
        public bool ShouldSpawn(Biome biome, MobKind kind, int lightLevel, int seed)
        {
            if (!_rules.TryGetValue(kind, out var rule))
            {
                return false;
            }
            if (lightLevel < rule.MinLight)
            {
                return false;
            }
            if (rule.Biomes == null || !rule.Biomes.Contains(biome.ToString()))
            {
                return false;
            }

            // 哈希：seed × 常数 ^ biome × 常数 ^ kind × 常数
            uint h = unchecked((uint)(seed * 2654435761
                                       ^ (int)biome * 73856093
                                       ^ (int)kind * 19349663));
            // 取高 16 位做模 100，保证均匀分布（低位易受 seed 线性递增影响）
            int chance = (int)((h >> 16) % 100);
            // weight=10 → 50%；weight=0 → 0%；weight=20+ → 必中
            return chance < rule.Weight * 5;
        }

        /// <summary>
        /// 从候选列表里挑一个能在此处刷的 kind，按列表顺序检查，第一个命中即返回。
        /// 都未命中返回 null（说明当前 biome + light + seed 不允许任何候选刷怪）。
        /// <para>
        /// MobManager 调用形式：白天传 <c>{Pig, Cow, Chicken}</c>，夜晚传 <c>{Zombie}</c>。
        /// 列表顺序即优先级（白天优先刷猪）。
        /// </para>
        /// </summary>
        public MobKind? PickKind(Biome biome, int lightLevel, MobKind[] candidates, int seed)
        {
            if (candidates == null) return null;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (ShouldSpawn(biome, candidates[i], lightLevel, seed + i))
                {
                    return candidates[i];
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
                case "Villager": return MobKind.Villager;

                // m11 P0：12 新生物名字预接线。spawn_rules.json 尚未加这些名字的条目，
                // 所以解析就绪后不会真的刷出；等 W1 代理补条目 + 模型 JSON 后自然生效
                case "Sheep": return MobKind.Sheep;
                case "Rabbit": return MobKind.Rabbit;
                case "Fox": return MobKind.Fox;
                case "Deer": return MobKind.Deer;
                case "Panda": return MobKind.Panda;
                case "Penguin": return MobKind.Penguin;
                case "Goat": return MobKind.Goat;
                case "Raccoon": return MobKind.Raccoon;
                case "Hamster": return MobKind.Hamster;
                case "Skeleton": return MobKind.Skeleton;
                case "Spider": return MobKind.Spider;
                case "Creeper": return MobKind.Creeper;
                default:
                    throw new System.ArgumentException(
                        $"未知 MobKind: {name}（合法的有 Pig/Cow/Chicken/Zombie/Villager/Sheep/Rabbit/Fox/Deer/Panda/Penguin/Goat/Raccoon/Hamster/Skeleton/Spider/Creeper）");
            }
        }
    }
}
