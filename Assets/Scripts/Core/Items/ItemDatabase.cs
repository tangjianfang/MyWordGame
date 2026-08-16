using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyWorld.Core.Items
{
    /// <summary>
    /// 物品注册表：解析 <c>StreamingAssets/items/*.json</c>、分配 numericId、提供双向查找。
    /// 与 <see cref="MyWorld.Core.Blocks.BlockRegistry"/> 同构但独立——两者数值 ID 空间互不干扰。
    /// </summary>
    public sealed class ItemDatabase
    {
        /// <summary>未显式指定 numericId 的物品从此处开始自动分配，避开物品低位 ID（0 留给空气）。</summary>
        public const int AutoAssignStart = 1000;

        private readonly Dictionary<string, ItemDefinition> _byId = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<int, ItemDefinition> _byNumericId = new Dictionary<int, ItemDefinition>();

        public IReadOnlyDictionary<string, ItemDefinition> ById => _byId;
        public IReadOnlyDictionary<int, ItemDefinition> ByNumericId => _byNumericId;

        public int Count => _byId.Count;

        public static ItemDatabase FromJson(IEnumerable<string> jsonDocuments)
        {
            if (jsonDocuments == null)
            {
                throw new ArgumentNullException(nameof(jsonDocuments));
            }

            var parsed = new List<(ItemDefinition Definition, bool HasNumericId)>();

            foreach (string document in jsonDocuments)
            {
                parsed.Add(Parse(document));
            }

            var db = new ItemDatabase();

            foreach ((ItemDefinition definition, bool hasNumericId) in parsed.Where(p => p.HasNumericId))
            {
                db.Add(definition);
            }

            int next = AutoAssignStart;
            foreach ((ItemDefinition definition, bool _) in parsed
                         .Where(p => !p.HasNumericId)
                         .OrderBy(p => p.Definition.Id, StringComparer.Ordinal))
            {
                while (db._byNumericId.ContainsKey(next))
                {
                    next++;
                }

                definition.NumericId = next++;
                db.Add(definition);
            }

            return db;
        }

        public ItemDefinition GetById(string id)
        {
            if (!_byId.TryGetValue(id, out ItemDefinition def))
            {
                throw new KeyNotFoundException($"未注册的物品 id：{id}");
            }
            return def;
        }

        public ItemDefinition GetByNumericId(int numericId)
        {
            if (!_byNumericId.TryGetValue(numericId, out ItemDefinition def))
            {
                throw new KeyNotFoundException($"未注册的物品数值 id：{numericId}");
            }
            return def;
        }

        public bool TryGetByNumericId(int numericId, out ItemDefinition def)
            => _byNumericId.TryGetValue(numericId, out def);

        public bool TryGetById(string id, out ItemDefinition def)
            => _byId.TryGetValue(id, out def);

        private void Add(ItemDefinition def)
        {
            if (_byId.ContainsKey(def.Id))
            {
                throw new InvalidDataException($"物品 id 重复：{def.Id}");
            }

            if (_byNumericId.ContainsKey(def.NumericId))
            {
                throw new InvalidDataException(
                    $"物品 numericId 重复：{def.NumericId}（{def.Id} 与 {_byNumericId[def.NumericId].Id}）");
            }

            _byId[def.Id] = def;
            _byNumericId[def.NumericId] = def;
        }

        private static (ItemDefinition Definition, bool HasNumericId) Parse(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"物品定义不是合法的 JSON：{ex.Message}", ex);
            }

            var id = (string)root["id"];
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidDataException("物品定义缺少必需的 id 字段。");
            }

            JToken numericIdToken = root["numericId"];

            // m10 A3 镐门槛：负数没有意义（0 已是「视同徒手」），写错立刻报而不是静默当 0——
            // 与 BlockRegistry 对 minToolTier 的校验态度一致
            var toolTier = (int?)root["toolTier"] ?? 0;
            if (toolTier < 0)
            {
                throw new InvalidDataException(
                    $"物品 {id} 的 toolTier 为 {toolTier}，必须 ≥ 0（0=徒手 / 1=木镐 / 2=石镐 / 3=铁镐 / 4=钻石镐，且只有镐类物品该写字段）。");
            }

            var def = new ItemDefinition
            {
                Id = id,
                DisplayName = (string)root["displayName"] ?? id,
                NumericId = numericIdToken != null ? (int)numericIdToken : 0,
                MaxStack = (int?)root["maxStack"] ?? 64,
                Texture = (string)root["texture"] ?? "missing",
                AttackDamage = root["attackDamage"] != null ? (float?)(float)root["attackDamage"] : null,
                HealAmount = root["healAmount"] != null ? (float?)(float)root["healAmount"] : null,
                IsTool = (bool?)root["isTool"] ?? false,
                MiningLevel = (int?)root["miningLevel"] ?? 0,
                ToolTier = toolTier,
            };

            return (def, numericIdToken != null);
        }
    }
}
