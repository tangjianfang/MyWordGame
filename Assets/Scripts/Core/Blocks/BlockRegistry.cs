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

        /// <summary>numericId → 六个面各自的贴图索引。渲染热路径按这张表查，不再碰字符串。</summary>
        private readonly Dictionary<ushort, int[]> _faceTextures = new Dictionary<ushort, int[]>();

        private string[] _textureNames = Array.Empty<string>();

        private BlockRegistry()
        {
        }

        /// <summary>没有贴图（空气、未注册方块）时的索引。</summary>
        public const int NoTextureIndex = -1;

        public int Count => _byId.Count;

        /// <summary>全部方块定义（按字典序无意义顺序）。m10 A3 fix1：一致性守卫测试要
        /// 遍历「注册表里每一个方块」对表，不能只挑清单里的——加这个只读视图。</summary>
        public IReadOnlyCollection<BlockDefinition> Definitions => _byId.Values;

        /// <summary>全部被引用到的贴图名，去重并按序数排序。下标即贴图索引。</summary>
        public IReadOnlyList<string> TextureNames => _textureNames;

        /// <summary>热路径查询，未注册的方块返回 <see cref="NoTextureIndex"/> 而不抛异常。</summary>
        public int GetTextureIndex(ushort numericId, BlockFace face)
            => _faceTextures.TryGetValue(numericId, out int[] faces) ? faces[(int)face] : NoTextureIndex;

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

            registry.BuildTextureTable();

            return registry;
        }

        /// <summary>
        /// 在全部方块登记完之后建一次贴图索引表。索引按贴图名的序数排序决定，
        /// 与方块的枚举顺序无关，因此跨机器一致。
        /// </summary>
        private void BuildTextureTable()
        {
            _textureNames = _byId.Values
                .Where(definition => definition.Textures != null)
                .SelectMany(definition => definition.Textures)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var slots = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _textureNames.Length; i++)
            {
                slots[_textureNames[i]] = i;
            }

            foreach (BlockDefinition definition in _byId.Values)
            {
                if (definition.Textures == null)
                {
                    continue;
                }

                var faces = new int[6];
                for (var face = 0; face < faces.Length; face++)
                {
                    string textureName = definition.Textures[face];
                    faces[face] = textureName != null ? slots[textureName] : NoTextureIndex;
                }

                _faceTextures[definition.NumericId] = faces;
            }
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

        /// <summary>网格生成与碰撞的热路径用这个，遇到未注册 ID 时不抛异常。</summary>
        public bool TryGetByNumericId(ushort numericId, out BlockDefinition definition)
            => _byNumericId.TryGetValue(numericId, out definition);

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

            // m10 工具门槛：负数没有意义（0 已是「徒手可挖」），写错立刻报而不是静默当 0
            var minToolTier = (int?)root["minToolTier"] ?? 0;
            if (minToolTier < 0)
            {
                throw new InvalidDataException($"方块 {id} 的 minToolTier 为 {minToolTier}，必须 ≥ 0（0=徒手 / 1=木镐 / 2=石镐 / 3=铁镐 / 4=钻石镐）。");
            }

            var definition = new BlockDefinition
            {
                Id = id,
                DisplayName = (string)root["displayName"] ?? id,
                NumericId = numericIdToken != null ? (ushort)numericIdToken : (ushort)0,
                Solid = (bool?)root["solid"] ?? true,
                Opaque = (bool?)root["opaque"] ?? true,
                LightEmission = (byte)lightEmission,
                Hardness = (float?)root["hardness"] ?? 1f,
                MinToolTier = minToolTier,
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
