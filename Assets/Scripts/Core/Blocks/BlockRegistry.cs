using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyWorld.Core.Blocks
{
    /// <summary>
    /// 方块注册表。从 JSON 定义构建，负责校验、分配数值 ID 并提供双向查找。
    /// </summary>
    public sealed class BlockRegistry
    {
        /// <summary>未显式指定 numericId 的方块从此处开始自动分配，避开内置方块的低位 ID。</summary>
        public const ushort AutoAssignStart = 1000;

        private readonly Dictionary<string, BlockDefinition> _byId = new Dictionary<string, BlockDefinition>();
        private readonly Dictionary<ushort, BlockDefinition> _byNumericId = new Dictionary<ushort, BlockDefinition>();

        private BlockRegistry()
        {
        }

        public int Count => _byId.Count;

        public static BlockRegistry FromJson(IEnumerable<string> jsonDocuments)
        {
            if (jsonDocuments == null)
            {
                throw new ArgumentNullException(nameof(jsonDocuments));
            }

            var parsed = new List<(BlockDefinition Definition, bool HasNumericId)>();

            foreach (string document in jsonDocuments)
            {
                parsed.Add(Parse(document));
            }

            var registry = new BlockRegistry();

            foreach ((BlockDefinition definition, bool hasNumericId) in parsed.Where(p => p.HasNumericId))
            {
                registry.Add(definition);
            }

            // 按字符串 id 排序后再分配，使自动 ID 与文件枚举顺序无关，跨机器结果一致
            ushort next = AutoAssignStart;
            foreach ((BlockDefinition definition, bool _) in parsed
                         .Where(p => !p.HasNumericId)
                         .OrderBy(p => p.Definition.Id, StringComparer.Ordinal))
            {
                while (registry._byNumericId.ContainsKey(next))
                {
                    next++;
                }

                definition.NumericId = next++;
                registry.Add(definition);
            }

            return registry;
        }

        public BlockDefinition GetById(string id)
        {
            if (!_byId.TryGetValue(id, out BlockDefinition definition))
            {
                throw new KeyNotFoundException($"未注册的方块 id：{id}");
            }

            return definition;
        }

        public BlockDefinition GetByNumericId(ushort numericId)
        {
            if (!_byNumericId.TryGetValue(numericId, out BlockDefinition definition))
            {
                throw new KeyNotFoundException($"未注册的方块数值 id：{numericId}");
            }

            return definition;
        }

        private void Add(BlockDefinition definition)
        {
            if (_byId.ContainsKey(definition.Id))
            {
                throw new InvalidDataException($"方块 id 重复：{definition.Id}");
            }

            if (_byNumericId.ContainsKey(definition.NumericId))
            {
                throw new InvalidDataException(
                    $"方块 numericId 重复：{definition.NumericId}（{definition.Id} 与 {_byNumericId[definition.NumericId].Id}）");
            }

            _byId[definition.Id] = definition;
            _byNumericId[definition.NumericId] = definition;
        }

        private static (BlockDefinition Definition, bool HasNumericId) Parse(string json)
        {
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"方块定义不是合法的 JSON：{exception.Message}", exception);
            }

            var id = (string)root["id"];
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new InvalidDataException("方块定义缺少必需的 id 字段。");
            }

            var lightEmission = (int?)root["lightEmission"] ?? 0;
            if (lightEmission < 0 || lightEmission > 15)
            {
                throw new InvalidDataException($"方块 {id} 的 lightEmission 为 {lightEmission}，必须在 0–15 之间。");
            }

            JToken numericIdToken = root["numericId"];

            var definition = new BlockDefinition
            {
                Id = id,
                DisplayName = (string)root["displayName"] ?? id,
                NumericId = numericIdToken != null ? (ushort)numericIdToken : (ushort)0,
                Solid = (bool?)root["solid"] ?? true,
                Opaque = (bool?)root["opaque"] ?? true,
                LightEmission = (byte)lightEmission,
                Hardness = (float?)root["hardness"] ?? 1f,
                Liquid = (bool?)root["liquid"] ?? false,
                Textures = ResolveTextures(id, root["textures"] as JObject)
            };

            return (definition, numericIdToken != null);
        }

        private static string[] ResolveTextures(string blockId, JObject textures)
        {
            // 空气没有可见面，允许完全不声明贴图
            if (blockId == "air")
            {
                return null;
            }

            if (textures == null)
            {
                throw new InvalidDataException($"方块 {blockId} 缺少 textures 字段。");
            }

            var faces = new string[6];

            var all = (string)textures["all"];
            if (!string.IsNullOrEmpty(all))
            {
                for (var i = 0; i < faces.Length; i++)
                {
                    faces[i] = all;
                }

                return faces;
            }

            var top = (string)textures["top"];
            var bottom = (string)textures["bottom"];
            var side = (string)textures["side"];

            if (string.IsNullOrEmpty(top) || string.IsNullOrEmpty(bottom) || string.IsNullOrEmpty(side))
            {
                throw new InvalidDataException(
                    $"方块 {blockId} 的 textures 不完整：需要 all，或者同时提供 top、bottom、side。");
            }

            faces[(int)BlockFace.Top] = top;
            faces[(int)BlockFace.Bottom] = bottom;
            faces[(int)BlockFace.East] = side;
            faces[(int)BlockFace.West] = side;
            faces[(int)BlockFace.North] = side;
            faces[(int)BlockFace.South] = side;

            return faces;
        }
    }
}
