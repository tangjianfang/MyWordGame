using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 单个树种的特征参数，由 <c>Assets/StreamingAssets/vegetation/trees.json</c> 填充。
    /// 数值语义与 <see cref="TreeFeature"/> 旧常量一一对应（oak 即现常量，守卫测试锁定）。
    /// </summary>
    public sealed class TreeSpecies
    {
        /// <summary>树种的字符串 id（如 "oak"），也是哈希通道派生的键。</summary>
        [JsonProperty("id")]
        public string Id;

        /// <summary>树干方块 id（oak 用内置 "log"，新树种如 "birch_log"，第 1 波注册）。</summary>
        [JsonProperty("logBlock")]
        public string LogBlock;

        /// <summary>树叶方块 id（oak 用内置 "leaves"，新树种如 "cherry_leaves"）。</summary>
        [JsonProperty("leavesBlock")]
        public string LeavesBlock;

        /// <summary>树干最低高度（含）。</summary>
        [JsonProperty("trunkMin")]
        public int TrunkMin;

        /// <summary>树干最高高度（含）。</summary>
        [JsonProperty("trunkMax")]
        public int TrunkMax;

        /// <summary>叶冠层数（自树干顶向下数）。</summary>
        [JsonProperty("leafLayers")]
        public int LeafLayers;

        /// <summary>叶冠圆盘半径（顶层固定为 1）。</summary>
        [JsonProperty("leafRadius")]
        public int LeafRadius;

        /// <summary>可投放的群系名列表（与 biomes.json 的 name 一致）。空数组 = 暂不投放（占位树种）。</summary>
        [JsonProperty("biomes")]
        public string[] Biomes;
    }

    /// <summary>
    /// 单条花草投放条目，由 <c>Assets/StreamingAssets/vegetation/flowers.json</c> 填充。
    /// 块名对应 A1 花草资源名（连字符转下划线，与 gold_ore 等既有方块命名一致），
    /// 第 1 波注册方块后由守卫测试闭环校验。
    /// </summary>
    public sealed class FlowerEntry
    {
        /// <summary>花草条目 id（如 "poppy"），查找键。</summary>
        [JsonProperty("id")]
        public string Id;

        /// <summary>投放的方块 id（如 "flower_poppy"）。</summary>
        [JsonProperty("block")]
        public string Block;

        /// <summary>可投放的群系名列表。</summary>
        [JsonProperty("biomes")]
        public string[] Biomes;

        /// <summary>每区块期望投放株数（首版 2-8）。</summary>
        [JsonProperty("densityPerChunk")]
        public int DensityPerChunk;
    }

    /// <summary>
    /// 植被特征表（m11 I2 / INFRA-B）。把树种与花草从 <see cref="TreeFeature"/> 的硬编码外置为 JSON 数据，
    /// 之后加树种/花草只改数据不动 C#。
    /// <para>
    /// 说明：任务卡原文写的是 <c>static class</c>，但 <see cref="Load"/> 必须返回可实例化的表
    /// （<see cref="TryGetTree"/>/<see cref="TryGetFlower"/> 是实例方法），static class 无法实例化——
    /// 故落地为 sealed class + 静态工厂，方法签名与卡片一字不差。
    /// </para>
    /// </summary>
    public sealed class VegetationTable
    {
        private readonly Dictionary<string, TreeSpecies> _treesById;
        private readonly Dictionary<string, FlowerEntry> _flowersById;
        private readonly List<TreeSpecies> _trees;
        private readonly List<FlowerEntry> _flowers;

        private VegetationTable(
            Dictionary<string, TreeSpecies> treesById,
            Dictionary<string, FlowerEntry> flowersById,
            List<TreeSpecies> trees,
            List<FlowerEntry> flowers)
        {
            _treesById = treesById;
            _flowersById = flowersById;
            _trees = trees;
            _flowers = flowers;
        }

        /// <summary>全部树种（保持 trees.json 书写顺序），投放器按群系过滤后遍历。</summary>
        public IReadOnlyList<TreeSpecies> Trees => _trees;

        /// <summary>全部花草（保持 flowers.json 书写顺序）。</summary>
        public IReadOnlyList<FlowerEntry> Flowers => _flowers;

        /// <summary>
        /// 从两份 JSON 文本构建植被表。任何条目参数非法、id 重复或 JSON 本身不合法都抛
        /// <see cref="InvalidDataException"/>，消息携带出错的条目 id（解析失败抛异常带 id）。
        /// </summary>
        public static VegetationTable Load(string treesJson, string flowersJson)
        {
            if (treesJson == null) throw new ArgumentNullException(nameof(treesJson));
            if (flowersJson == null) throw new ArgumentNullException(nameof(flowersJson));

            List<TreeSpecies> trees = ParseTrees(treesJson);
            List<FlowerEntry> flowers = ParseFlowers(flowersJson);

            var treesById = new Dictionary<string, TreeSpecies>(StringComparer.Ordinal);
            foreach (TreeSpecies tree in trees)
            {
                if (treesById.ContainsKey(tree.Id))
                {
                    throw new InvalidDataException($"trees.json 树种 id 重复：{tree.Id}");
                }
                treesById[tree.Id] = tree;
            }

            var flowersById = new Dictionary<string, FlowerEntry>(StringComparer.Ordinal);
            foreach (FlowerEntry flower in flowers)
            {
                if (flowersById.ContainsKey(flower.Id))
                {
                    throw new InvalidDataException($"flowers.json 花草 id 重复：{flower.Id}");
                }
                flowersById[flower.Id] = flower;
            }

            return new VegetationTable(treesById, flowersById, trees, flowers);
        }

        /// <summary>按 id 查树种，未注册返回 false 而非抛异常。</summary>
        public bool TryGetTree(string id, out TreeSpecies species)
        {
            if (id == null)
            {
                species = null;
                return false;
            }
            return _treesById.TryGetValue(id, out species);
        }

        /// <summary>按 id 查花草，未注册返回 false 而非抛异常。</summary>
        public bool TryGetFlower(string id, out FlowerEntry entry)
        {
            if (id == null)
            {
                entry = null;
                return false;
            }
            return _flowersById.TryGetValue(id, out entry);
        }

        private static List<TreeSpecies> ParseTrees(string json)
        {
            List<TreeSpecies> trees;
            try
            {
                trees = JsonConvert.DeserializeObject<List<TreeSpecies>>(json);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"trees.json 不是合法的 JSON：{exception.Message}", exception);
            }
            if (trees == null)
            {
                throw new InvalidDataException("trees.json 解析为空（null）。");
            }

            foreach (TreeSpecies tree in trees)
            {
                if (tree == null || string.IsNullOrWhiteSpace(tree.Id))
                {
                    throw new InvalidDataException("trees.json 存在缺少 id 字段的树种条目。");
                }
                if (string.IsNullOrWhiteSpace(tree.LogBlock))
                {
                    throw new InvalidDataException($"树种 {tree.Id} 缺少 logBlock。");
                }
                if (string.IsNullOrWhiteSpace(tree.LeavesBlock))
                {
                    throw new InvalidDataException($"树种 {tree.Id} 缺少 leavesBlock。");
                }
                if (tree.TrunkMin < 1)
                {
                    throw new InvalidDataException($"树种 {tree.Id} 的 trunkMin 为 {tree.TrunkMin}，必须 ≥ 1。");
                }
                if (tree.TrunkMax < tree.TrunkMin)
                {
                    throw new InvalidDataException(
                        $"树种 {tree.Id} 的 trunkMax={tree.TrunkMax} 小于 trunkMin={tree.TrunkMin}。");
                }
                if (tree.LeafLayers < 1)
                {
                    throw new InvalidDataException($"树种 {tree.Id} 的 leafLayers 为 {tree.LeafLayers}，必须 ≥ 1。");
                }
                if (tree.LeafRadius < 1)
                {
                    throw new InvalidDataException($"树种 {tree.Id} 的 leafRadius 为 {tree.LeafRadius}，必须 ≥ 1。");
                }
                if (tree.Biomes == null)
                {
                    throw new InvalidDataException($"树种 {tree.Id} 缺少 biomes 字段（空数组表示暂不投放）。");
                }
            }
            return trees;
        }

        private static List<FlowerEntry> ParseFlowers(string json)
        {
            List<FlowerEntry> flowers;
            try
            {
                flowers = JsonConvert.DeserializeObject<List<FlowerEntry>>(json);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"flowers.json 不是合法的 JSON：{exception.Message}", exception);
            }
            if (flowers == null)
            {
                throw new InvalidDataException("flowers.json 解析为空（null）。");
            }

            foreach (FlowerEntry flower in flowers)
            {
                if (flower == null || string.IsNullOrWhiteSpace(flower.Id))
                {
                    throw new InvalidDataException("flowers.json 存在缺少 id 字段的花草条目。");
                }
                if (string.IsNullOrWhiteSpace(flower.Block))
                {
                    throw new InvalidDataException($"花草 {flower.Id} 缺少 block。");
                }
                if (flower.Biomes == null)
                {
                    throw new InvalidDataException($"花草 {flower.Id} 缺少 biomes 字段。");
                }
                if (flower.DensityPerChunk < 0)
                {
                    throw new InvalidDataException(
                        $"花草 {flower.Id} 的 densityPerChunk 为 {flower.DensityPerChunk}，必须 ≥ 0。");
                }
            }
            return flowers;
        }
    }
}
