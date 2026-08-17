using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Items;
using Newtonsoft.Json.Linq;

namespace MyWorld.Core.Blocks
{
    /// <summary>
    /// 方块被挖掉落表。把 block numericId 映射到一组 <see cref="ItemStack"/>。
    /// <para>
    /// 数据源是 <c>Assets/StreamingAssets/blocks/block_drops.json</c>，写法与
    /// <see cref="BlockRegistry"/> 的方块定义同款 JSONL 数组。Core 层无 UnityEngine 依赖
    /// （asmdef <c>noEngineReferences: true</c>），<see cref="MyWorld.Unity.Player.BlockInteraction"/>
    /// 在 <c>Update</c> 命中左键时通过 <see cref="DropsFor"/> 查表后实例化
    /// <see cref="MyWorld.Core.Items.ItemDropEntity"/>。
    /// </para>
    /// <para>
    /// count 走 <c>countMin</c> / <c>countMax</c> 区间，由 <see cref="RollCount"/> 用整数
    /// 哈希 + 种子的确定性 RNG 计算（不依赖 <c>System.Random</c>，保证 replay 可复现）。
    /// </para>
    /// </summary>
    public sealed class BlockDrops
    {
        /// <summary>单条掉落：物品栈 + 触发概率（千分比，1000 = 恒掉）。
        /// m11 W2-4 E1 起支持 chance 字段（tall_grass 30% 掉麦种——农业首发种子来源）。</summary>
        private readonly struct DropEntry
        {
            public readonly ItemStack Stack;
            /// <summary>触发概率千分比 [0,1000]：1000 恒掉、0 恒不掉、300 = 30%。</summary>
            public readonly int ChancePermille;

            public DropEntry(ItemStack stack, int chancePermille)
            {
                Stack = stack;
                ChancePermille = chancePermille;
            }
        }

        private readonly Dictionary<ushort, DropEntry[]> _byNumericId =
            new Dictionary<ushort, DropEntry[]>();

        /// <summary>已配置的 block 数（调试 / 测试用）。</summary>
        public int Count => _byNumericId.Count;

        private BlockDrops() { }

        /// <summary>
        /// 解析一段或多段 JSON 文档为 BlockDrops 表。每个 JSON 文档描述一个或一组方块的掉落。
        /// 物品 id 用字符串（与 ItemDatabase 同款），通过 <paramref name="items"/> 转为 numericId。
        /// <para>
        /// 支持两种文档形态：
        /// <list type="bullet">
        /// <item>单个 <c>{ blockId, blockNumericId, drops[] }</c> 对象。</item>
        /// <item>顶层数组 <c>[ {...}, {...} ]</c>——文件级多条目（与
        /// <c>StreamingAssets/blocks/drops/block_drops.json</c> 的实际写法一致）。</item>
        /// </list>
        /// </para>
        /// <para>
        /// 失败模式（缺字段、引用未注册物品）抛 <see cref="InvalidDataException"/>，
        /// 与 <see cref="BlockRegistry"/> / <see cref="ItemDatabase"/> 同款严格解析。
        /// </para>
        /// </summary>
        public static BlockDrops FromJson(IEnumerable<string> jsonDocuments, ItemDatabase items)
        {
            if (jsonDocuments == null)
            {
                throw new ArgumentNullException(nameof(jsonDocuments));
            }
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            var table = new BlockDrops();
            foreach (string document in jsonDocuments)
            {
                table.Add(document, items);
            }
            return table;
        }

        private void Add(string document, ItemDatabase items)
        {
            JToken token;
            try
            {
                token = JToken.Parse(document);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"block_drops 不是合法 JSON：{ex.Message}", ex);
            }

            if (token is JArray array)
            {
                // 顶层数组：每个元素是一个 block entry。
                int index = 0;
                foreach (var element in array)
                {
                    if (element is JObject obj)
                    {
                        AddObject(obj, items);
                    }
                    else
                    {
                        throw new InvalidDataException(
                            $"block_drops 顶层数组的第 {index} 个元素不是对象。");
                    }
                    index++;
                }
                return;
            }
            if (token is JObject root)
            {
                AddObject(root, items);
                return;
            }
            throw new InvalidDataException("block_drops 文档必须是对象或对象数组。");
        }

        private void AddObject(JObject root, ItemDatabase items)
        {
            // blockId 字符串（如 "stone"）—— 不是 numeric；只用于错误日志与可读性。
            string blockId = (string)root["blockId"];
            int? blockNumericId = (int?)root["blockNumericId"];
            if (blockNumericId == null)
            {
                throw new InvalidDataException("block_drops 条目缺少 blockNumericId 字段。");
            }

            var dropsToken = root["drops"] as JArray;
            if (dropsToken == null)
            {
                throw new InvalidDataException(
                    $"block_drops 条目（blockId={blockId ?? "<null>"}）缺少 drops 字段。");
            }

            var entries = new List<DropEntry>(dropsToken.Count);
            foreach (var entryToken in dropsToken)
            {
                var entryObj = entryToken as JObject;
                if (entryObj == null)
                {
                    throw new InvalidDataException(
                        $"block_drops 条目（blockId={blockId}）里的 drops 项必须是对象。");
                }
                entries.Add(ParseDropEntry(blockId, entryObj, items));
            }

            _byNumericId[(ushort)blockNumericId.Value] = entries.ToArray();
        }

        private static DropEntry ParseDropEntry(string blockId, JObject entryObj, ItemDatabase items)
        {
            string itemId = (string)entryObj["itemId"];
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new InvalidDataException(
                    $"block_drops 条目（blockId={blockId}）的 drops 缺 itemId 字段。");
            }
            if (!items.TryGetById(itemId, out var def))
            {
                throw new InvalidDataException(
                    $"block_drops 条目（blockId={blockId}）引用了未注册的物品：{itemId}");
            }

            int countMin = (int?)entryObj["countMin"] ?? 1;
            int countMax = (int?)entryObj["countMax"] ?? countMin;
            if (countMin < 0) countMin = 0;
            if (countMax < countMin) countMax = countMin;
            // 上限跟物品注册表对齐：ItemStack 构造只把 count 截到非负，
            // 不强制 ≤ MaxStack，所以这里显式取 def.MaxStack 作为软上限，避免生成
            // 「超过最大堆叠」但构造时不报错的 ItemStack。
            int stackCap = def.MaxStack > 0 ? def.MaxStack : 64;
            if (countMax > stackCap) countMax = stackCap;

            // m11 W2-4 E1：chance 触发概率（可选，默认 1 = 恒掉）。写严格：越界抛
            //（0 允许 = 显式声明「恒不掉」，与空 drops 等价但语义留给数据作者）
            double chance = (double?)entryObj["chance"] ?? 1.0;
            if (chance < 0.0 || chance > 1.0 || double.IsNaN(chance))
            {
                throw new InvalidDataException(
                    $"block_drops 条目（blockId={blockId}, itemId={itemId}）的 chance={chance} 不在 [0,1]。");
            }
            int chancePermille = (int)System.Math.Round(chance * 1000.0);

            int seed = ComputeSeed(blockId, itemId);
            int count = RollCount(seed, min: countMin, max: countMax);
            return new DropEntry(new ItemStack(def.NumericId, count), chancePermille);
        }

        /// <summary>
        /// 取该 block 的掉落物列表（salt=0 的确定性掷骰）。空数组 = 不掉落
        ///（空气、未注册方块、bedrock 等）。带 chance 的条目按「blockId+itemId 派生 seed」
        /// 掷骰——同一次进程内恒定；要每次挖掘不同结果请用 <see cref="DropsFor(ushort, int)"/>
        /// 传坐标等可变 salt（挖掘路径已传）。
        /// </summary>
        public ItemStack[] DropsFor(ushort blockNumericId)
        {
            return DropsFor(blockNumericId, 0);
        }

        /// <summary>
        /// m11 W2-4 E1：带 salt 的掉落查询——chance 条目按「seed ^ salt」掷骰，
        /// 不同 salt 独立掷（挖掘传方块坐标，同一方块两次挖结果可不同——与 MC 打草
        /// 得种子同语义）；无 chance 条目（恒掉）与 salt 无关，行为与旧版完全一致。
        /// 同 (block, salt) 结果恒定（整数哈希，跨机器一致）。
        /// </summary>
        public ItemStack[] DropsFor(ushort blockNumericId, int salt)
        {
            if (!_byNumericId.TryGetValue(blockNumericId, out DropEntry[] entries))
            {
                return Array.Empty<ItemStack>();
            }

            // 快路径：全部恒掉（绝大多数条目）直接复用原数组语义逐条拷出
            var result = new List<ItemStack>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                DropEntry entry = entries[i];
                if (entry.ChancePermille <= 0) continue; // 恒不掉
                if (entry.ChancePermille >= 1000)
                {
                    result.Add(entry.Stack);
                    continue;
                }
                // 概率条目：千分比掷骰（整数哈希，确定性；seed 混入方块号/条目序/调用方 salt）
                int seed = salt ^ (blockNumericId * 31) ^ (i * 101);
                if (RollCount(seed, 1, 1000) <= entry.ChancePermille)
                {
                    result.Add(entry.Stack);
                }
            }
            return result.ToArray();
        }

        /// <summary>
        /// 给定整数种子计算 [min, max] 区间内的伪随机 count（闭区间，含两端）。
        /// 用整数哈希（与 <see cref="MyWorld.Core.WorldGen.ValueNoise2D"/> 同款风格），
        /// 跨机器一致。
        /// </summary>
        public static int RollCount(int seed, int min, int max)
        {
            if (max < min) max = min;
            if (min < 0) min = 0;
            int range = max - min + 1;
            if (range <= 1) return min;

            // 把 seed 散到非负 int 上
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= h >> 13;
                h *= 2654435761u;
                h ^= h >> 16;
                return min + (int)(h % (uint)range);
            }
        }

        /// <summary>
        /// 用 blockId+itemId 派生出确定 seed：同一份 JSON 每次解析得到的 count 都一致。
        /// <see cref="string.GetHashCode"/> 在不同 .NET 实现下不一致，所以走 FNV-1a。
        /// </summary>
        private static int ComputeSeed(string blockId, string itemId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (blockId != null)
                {
                    foreach (char c in blockId)
                    {
                        hash ^= c;
                        hash *= 16777619u;
                    }
                }
                hash ^= (byte)'|';
                hash *= 16777619u;
                if (itemId != null)
                {
                    foreach (char c in itemId)
                    {
                        hash ^= c;
                        hash *= 16777619u;
                    }
                }
                return (int)hash;
            }
        }
    }
}
