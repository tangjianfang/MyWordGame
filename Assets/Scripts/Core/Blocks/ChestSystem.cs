using System;
using System.Collections.Generic;
using MyWorld.Core.Items;
using MyWorld.Core.Persistence;

namespace MyWorld.Core.Blocks
{
    /// <summary>
    /// 箱子内容存储（m11 W1-4）。每个箱子按坐标键 <c>"x,y,z"</c>（<see cref="Key"/>，负坐标原样进字符串）
    /// 持有一列内容行，行结构复用 <see cref="DropSnapshot"/>（ItemId/Count/Metadata 有效，X/Y/Z 不用）——
    /// 与 <see cref="LevelData.ChestContents"/>（m11 I3 预留字段）的存档契约一致。
    /// <para>
    /// 行为约定：Put 优先并入「同物品 id + 同 Metadata 且行容量装得下」的既有行，装不下开新行，
    /// 满 <see cref="Capacity"/> 行拒绝（不拆分不硬塞，调用方拿回 false 自行处理）；
    /// 工具类（maxStack=1）天然永远开新行，两把同款镐不会堆成 count=2。
    /// Core 不感知 UI/渲染；开箱界面与放置接线在 Unity 侧（集成点②）。
    /// </para>
    /// <para>
    /// 存档走 <see cref="SaveTo"/>/<see cref="LoadFrom"/>（全量替换），读档后 Metadata 无损——
    /// 带耐久编码的工具放进箱子再取出不能掉耐久。
    /// </para>
    /// </summary>
    public sealed class ChestSystem
    {
        /// <summary>每箱容量（行数）。MC 同款 27 格——孩子游戏也不宜无限仓库。</summary>
        public const int Capacity = 27;

        /// <summary>未提供 <see cref="ItemDatabase"/> 时的行堆叠上限兜底。</summary>
        public const int DefaultRowStack = 64;

        private readonly ItemDatabase _items;
        private readonly Dictionary<string, List<DropSnapshot>> _contents =
            new Dictionary<string, List<DropSnapshot>>(StringComparer.Ordinal);

        /// <param name="items">
        /// 物品表（可选）：提供时按物品 maxStack 决定行堆叠上限与可否并堆；
        /// 缺省一律 <see cref="DefaultRowStack"/>（测试与占位场景）。
        /// </param>
        public ChestSystem(ItemDatabase items = null)
        {
            _items = items;
        }

        /// <summary>坐标 → 存档键。与 <see cref="LevelData.ChestContents"/> 的键格式逐字一致（含负坐标）。</summary>
        public static string Key(int x, int y, int z) => $"{x},{y},{z}";

        /// <summary>当前有内容（或被登记过）的箱子数。</summary>
        public int ChestCount => _contents.Count;

        /// <summary>
        /// 存入一件物品栈。空栈拒绝；能并入既有行或还有空行返回 true；
        /// 满 <see cref="Capacity"/> 行返回 false（物品留在调用方手里，不静默丢）。
        /// </summary>
        public bool Put(int x, int y, int z, ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return false;
            }

            string key = Key(x, y, z);
            if (!_contents.TryGetValue(key, out List<DropSnapshot> rows))
            {
                rows = new List<DropSnapshot>();
                _contents[key] = rows;
            }

            int rowCap = RowStackLimit(stack.ItemId);
            if (rowCap > 1)
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    DropSnapshot row = rows[i];
                    if (row.ItemId == stack.ItemId && row.Metadata == stack.Metadata
                        && row.Count + stack.Count <= rowCap)
                    {
                        rows[i] = new DropSnapshot { ItemId = row.ItemId, Count = row.Count + stack.Count, Metadata = row.Metadata };
                        return true;
                    }
                }
            }

            if (rows.Count >= Capacity)
            {
                return false;
            }

            rows.Add(new DropSnapshot { ItemId = stack.ItemId, Count = stack.Count, Metadata = stack.Metadata });
            return true;
        }

        /// <summary>
        /// 取走一整行（不部分取——部分取由 UI 层拿走后再 Put 回余量实现）。
        /// 空箱、箱子未登记或下标越界返回 null，不抛异常（读容忍）。
        /// </summary>
        public ItemStack? Take(int x, int y, int z, int rowIndex)
        {
            if (!_contents.TryGetValue(Key(x, y, z), out List<DropSnapshot> rows)
                || rowIndex < 0 || rowIndex >= rows.Count)
            {
                return null;
            }

            DropSnapshot row = rows[rowIndex];
            rows.RemoveAt(rowIndex);
            if (rows.Count == 0)
            {
                _contents.Remove(Key(x, y, z));
            }

            return new ItemStack(row.ItemId, row.Count, row.Metadata);
        }

        /// <summary>只读内容。未登记的坐标返回空列表（不建行、不产生副作用）。</summary>
        public IReadOnlyList<DropSnapshot> List(int x, int y, int z)
            => _contents.TryGetValue(Key(x, y, z), out List<DropSnapshot> rows)
                ? rows
                : (IReadOnlyList<DropSnapshot>)Array.Empty<DropSnapshot>();

        /// <summary>
        /// 拆箱：移除该坐标的全部内容并倒出（调用方 spawn 成掉落物或塞回玩家背包）。
        /// 该坐标没有内容返回 null。
        /// </summary>
        public List<DropSnapshot> RemoveChest(int x, int y, int z)
        {
            string key = Key(x, y, z);
            if (!_contents.TryGetValue(key, out List<DropSnapshot> rows))
            {
                return null;
            }

            _contents.Remove(key);
            return rows;
        }

        /// <summary>全量写入 level.dat（替换该字段，不合并——箱子状态以内存为单一真源）。</summary>
        public void SaveTo(LevelData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var copy = new Dictionary<string, List<DropSnapshot>>(_contents.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<DropSnapshot>> pair in _contents)
            {
                copy[pair.Key] = new List<DropSnapshot>(pair.Value);
            }

            data.ChestContents = copy;
        }

        /// <summary>
        /// 从 level.dat 恢复（全量替换内存状态）。旧档无 <c>ChestContents</c> 键（null，
        /// <see cref="LevelDataCodec"/> 会归一为空字典，这里再兜一层）= 全新开始。
        /// </summary>
        public void LoadFrom(LevelData data)
        {
            _contents.Clear();
            if (data?.ChestContents == null)
            {
                return;
            }

            foreach (KeyValuePair<string, List<DropSnapshot>> pair in data.ChestContents)
            {
                _contents[pair.Key] = new List<DropSnapshot>(pair.Value);
            }
        }

        /// <summary>该物品的行堆叠上限：查物品表 maxStack；表缺（未注册物品）兜底 64。</summary>
        private int RowStackLimit(int itemId)
            => _items != null && _items.TryGetByNumericId(itemId, out ItemDefinition def) && def.MaxStack > 0
                ? def.MaxStack
                : DefaultRowStack;
    }
}
