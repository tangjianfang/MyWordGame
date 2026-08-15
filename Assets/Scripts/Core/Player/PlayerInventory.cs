using System;
using System.Collections.Generic;
using MyWorld.Core.Items;

namespace MyWorld.Core.Player
{
    /// <summary>
    /// 玩家背包。布局：槽 0..8 是 hotbar，9..35 是主背包，共 36 槽。
    /// 选中槽由 <see cref="SelectedHotbarIndex"/> 决定（0..8）。
    /// </summary>
    public sealed class PlayerInventory
    {
        public const int HotbarSize = 9;
        public const int MainSize = 27;
        public const int TotalSize = 36;

        private readonly ItemStack[] _slots;
        private System.Func<int, int> _maxStackLookup;

        public int SelectedHotbarIndex { get; set; }

        public PlayerInventory()
        {
            _slots = new ItemStack[TotalSize];
            for (var i = 0; i < _slots.Length; i++) _slots[i] = ItemStack.Empty;
            _maxStackLookup = id => 64;
        }

        public IReadOnlyList<ItemStack> Slots => _slots;

        public ItemStack GetSlot(int index) => InRange(index) ? _slots[index] : ItemStack.Empty;

        public ItemStack GetSelected() => GetSlot(SelectedHotbarIndex);

        public void SetSlot(int index, ItemStack stack)
        {
            if (!InRange(index)) throw new System.ArgumentOutOfRangeException(nameof(index));
            _slots[index] = stack.IsEmpty ? ItemStack.Empty : stack;
        }

        /// <summary>尝试加入。满 / 装不下则把剩余数量写回 leftover 并返 false。</summary>
        public bool TryAdd(ItemStack stack, out int leftover)
        {
            leftover = stack.Count;
            if (stack.IsEmpty) return true;

            for (var i = 0; i < _slots.Length && leftover > 0; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty || s.ItemId != stack.ItemId) continue;
                int max = MaxStackFor(stack.ItemId);
                if (s.Count >= max) continue;
                int space = max - s.Count;
                int take = System.Math.Min(space, leftover);
                _slots[i] = s.WithCount(s.Count + take);
                leftover -= take;
            }
            for (var i = 0; i < _slots.Length && leftover > 0; i++)
            {
                if (!_slots[i].IsEmpty) continue;
                int max = MaxStackFor(stack.ItemId);
                int take = System.Math.Min(max, leftover);
                _slots[i] = new ItemStack(stack.ItemId, take, stack.Metadata);
                leftover -= take;
            }
            return leftover == 0;
        }

        public bool TryRemoveOne(int index)
        {
            if (!InRange(index) || _slots[index].IsEmpty) return false;
            var s = _slots[index];
            if (s.Count <= 1) _slots[index] = ItemStack.Empty;
            else _slots[index] = s.WithCount(s.Count - 1);
            return true;
        }

        /// <summary>
        /// 统计背包里指定物品的总数（跨槽合并计数）。m6 C2 的任务系统用它取
        ///「背包现存量」——ObtainItem 事件的 Count 语义，见 <c>QuestCondition</c>。
        /// </summary>
        public int CountOf(int itemId)
        {
            if (itemId == 0) return 0;
            int total = 0;
            for (int i = 0; i < TotalSize; i++)
            {
                if (_slots[i].ItemId == itemId) total += _slots[i].Count;
            }
            return total;
        }

        /// <summary>从任意槽扣指定物品数量（合并计数）。不够返回 false。</summary>
        public bool TryRemoveCount(int itemId, int count)
        {
            if (itemId == 0 || count <= 0) return false;
            int have = 0;
            for (int i = 0; i < TotalSize; i++)
            {
                if (_slots[i].ItemId == itemId) have += _slots[i].Count;
            }
            if (have < count) return false;

            int remaining = count;
            for (int i = 0; i < TotalSize && remaining > 0; i++)
            {
                if (_slots[i].ItemId != itemId) continue;
                int take = System.Math.Min(_slots[i].Count, remaining);
                var s = _slots[i];
                _slots[i] = take >= s.Count ? ItemStack.Empty : s.WithCount(s.Count - take);
                remaining -= take;
            }
            return true;
        }

        /// <summary>从一格移整个 stack 到另一格（合并同类）。</summary>
        public void MoveSlot(int from, int to)
        {
            if (!InRange(from) || !InRange(to) || from == to) return;
            var a = _slots[from];
            var b = _slots[to];
            if (a.IsEmpty) return;
            if (!b.IsEmpty && a.ItemId == b.ItemId)
            {
                int max = MaxStackFor(a.ItemId);
                int total = System.Math.Min(a.Count + b.Count, max);
                int taken = total - b.Count;
                int fromCount = a.Count - taken;
                _slots[to] = b.WithCount(total);
                _slots[from] = fromCount > 0 ? a.WithCount(fromCount) : ItemStack.Empty;
            }
            else
            {
                _slots[to] = a;
                _slots[from] = b;
            }
        }

        public int MaxStackFor(int itemId) => _maxStackLookup == null ? 64 : _maxStackLookup(itemId);

        public void SetMaxStackLookup(System.Func<int, int> lookup) => _maxStackLookup = lookup ?? (id => 64);

        private bool InRange(int index) => index >= 0 && index < TotalSize;
    }
}
