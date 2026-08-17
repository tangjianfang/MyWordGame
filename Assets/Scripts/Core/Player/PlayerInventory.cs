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

        /// <summary>
        /// 背包还能装下多少个指定物品：同类格的剩余叠放空间 + 空格的整格容量。
        /// m6 C2 fix1 起作为拿合成 / 烧炼产出前的容量预检——不够就整单失败，
        /// 避免 <see cref="TryAdd"/> 半途写入一部分后难以回滚的窘境。
        /// </summary>
        public int SpaceFor(int itemId)
        {
            if (itemId == 0) return 0;
            int max = MaxStackFor(itemId);
            int space = 0;
            for (int i = 0; i < TotalSize; i++)
            {
                var s = _slots[i];
                if (s.IsEmpty) space += max;
                else if (s.ItemId == itemId) space += max - s.Count;
            }
            return space;
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

    /// <summary>
    /// 玩家穿戴栏（m11 W2-1）：头/胸/腿/脚 4 槽，只收对应部位的盔甲——
    /// 部位来自 <see cref="MyWorld.Core.Items.ItemDefinition.ArmorPart"/>
    ///（items/*.json 的 armorPart 字段），穿入时按部位定槽，不存在「把头盔塞进靴槽」的路径。
    /// 与 <see cref="PlayerInventory"/> 分离：穿戴栏不参与合成/熔炉/掉落等物品流，
    /// 自动拾取（<see cref="PlayerInventory.TryAdd"/>）也永远只进背包。
    /// 属性汇总（防御/移速/血上限）由 Unity 侧 PlayerContext 每帧从
    /// 「穿戴 4 槽 + 手持选中格」双源重建，本类只管存取与穿脱交换。
    /// </summary>
    public sealed class ArmorInventory
    {
        /// <summary>头 / 胸 / 腿 / 脚，恒 4 槽。</summary>
        public const int SlotCount = 4;

        private readonly ItemStack[] _slots;

        public ArmorInventory()
        {
            _slots = new ItemStack[SlotCount];
            for (var i = 0; i < _slots.Length; i++) _slots[i] = ItemStack.Empty;
        }

        /// <summary>部位 → 穿戴槽下标：头 0 / 胸 1 / 腿 2 / 脚 3。
        /// <see cref="MyWorld.Core.Items.ArmorPart.None"/> 返回 -1（非盔甲无槽可放）。</summary>
        public static int SlotIndexOf(MyWorld.Core.Items.ArmorPart part) =>
            part == MyWorld.Core.Items.ArmorPart.None ? -1 : (int)part - 1;

        /// <summary>穿戴槽下标 → 部位（与 <see cref="SlotIndexOf"/> 互逆）。越界返回 None（读容忍）。</summary>
        public static MyWorld.Core.Items.ArmorPart PartOf(int slotIndex) =>
            slotIndex >= 0 && slotIndex < SlotCount
                ? (MyWorld.Core.Items.ArmorPart)(slotIndex + 1)
                : MyWorld.Core.Items.ArmorPart.None;

        /// <summary>读槽。越界返回空（读容忍，与 <see cref="PlayerInventory.GetSlot"/> 同款）。</summary>
        public ItemStack GetSlot(int index) => index >= 0 && index < SlotCount ? _slots[index] : ItemStack.Empty;

        /// <summary>直接写槽，不校验部位——**存档恢复专用**（<see cref="MyWorld.Core.Persistence.SnapshotMappers.RestoreArmor"/>）。
        /// 部位约束守的是穿脱交互路径（<see cref="TryEquipFrom"/>）；档里是什么照单全收，
        /// 与 <see cref="PlayerInventory.SetSlot"/> 不查物品存在性的态度一致。越界抛（写严格）。</summary>
        public void SetSlotRaw(int index, ItemStack stack)
        {
            if (index < 0 || index >= SlotCount)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }
            _slots[index] = stack.IsEmpty ? ItemStack.Empty : stack;
        }

        /// <summary>
        /// 穿入：把背包 <paramref name="fromIndex"/> 格的盔甲**整格**穿进对应部位槽。
        /// 非盔甲（无部位）/ 空格 / 越界 / 无物品表一律返回 false 且两边不动；
        /// 目标槽已占用则交换——旧盔甲回到 <paramref name="fromIndex"/> 背包格（Metadata 随行），
        /// 换装不经过背包满判定（一出一进，格数不变）。
        /// </summary>
        public bool TryEquipFrom(PlayerInventory inventory, int fromIndex, ItemDatabase items)
        {
            if (inventory == null || items == null) return false;
            ItemStack stack = inventory.GetSlot(fromIndex); // 越界读容忍：返回空 → 下面拦掉
            if (stack.IsEmpty) return false;
            if (!items.TryGetByNumericId(stack.ItemId, out var def)) return false;
            int slot = SlotIndexOf(def.ArmorPart);
            if (slot < 0) return false; // 不是盔甲：没有可穿的部位

            ItemStack previous = _slots[slot];
            _slots[slot] = stack;
            inventory.SetSlot(fromIndex, previous); // 空栈写 Empty，SetSlot 内部已归一
            return true;
        }

        /// <summary>
        /// 脱下：<paramref name="armorIndex"/> 槽的盔甲放回背包。背包零空间
        ///（<see cref="PlayerInventory.SpaceFor"/> 不够）返回 false 且保持穿戴——
        /// 盔甲绝不悬空丢失。空槽 / 越界返回 false。
        /// </summary>
        public bool TryUnequipTo(PlayerInventory inventory, int armorIndex)
        {
            if (inventory == null) return false;
            ItemStack stack = GetSlot(armorIndex);
            if (stack.IsEmpty) return false;
            if (inventory.SpaceFor(stack.ItemId) < stack.Count) return false;

            inventory.TryAdd(stack, out int leftover); // 预检过，leftover 必为 0
            _slots[armorIndex] = leftover > 0 ? stack : ItemStack.Empty;
            return leftover == 0;
        }
    }
}
