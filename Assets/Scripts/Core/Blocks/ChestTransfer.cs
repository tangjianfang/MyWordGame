using System;
using MyWorld.Core.Items;
using MyWorld.Core.Player;

namespace MyWorld.Core.Blocks
{
    /// <summary>
    /// 箱子界面的存取纯逻辑（m11 W2-3）。<see cref="ChestSystem"/> 是行式容器（Put 并堆 / Take 整行），
    /// <see cref="PlayerInventory"/> 是格位容器，两边对「一格」的语义不同——本类把「鼠标手持栈
    /// （cursor held）+ 普通点击取放 + Shift 整组转移」的换算集中在这一个纯静态类里，
    /// <c>Unity/UI/ChestUi</c> 只负责画格子与转发点击，永不自己写搬运。
    /// <para>
    /// 放 Core 层是为了双链测试：dotnet 链只编译 Core，取放 / 合并 / 交换 / 箱满拒绝这些契约
    /// 照 <see cref="BlockPlacement"/> / <see cref="GearBonusMath"/> 的先例落成可无头断言的纯函数。
    /// </para>
    /// <para>
    /// 铁律：**任何路径都不丢物品**。箱子满 → 物品留在调用方手里（返回 false）；
    /// 关窗归还时背包与箱子都装不下 → 余量经返回值交还调用方（UI 侧 spawn 成掉落物）。
    /// </para>
    /// </summary>
    public static class ChestTransfer
    {
        /// <summary>
        /// 点击箱子侧第 <paramref name="rowIndex"/> 行（0 起，= <see cref="ChestSystem.List"/> 的下标）。
        /// <list type="bullet">
        /// <item>Shift：整行转移进背包（<see cref="ShiftTakeRow"/>），手上物品不参与</item>
        /// <item>手持空 + 行有物品：整行拿到手上（cursor）</item>
        /// <item>手持非空：<see cref="ChestSystem.Put"/> 放回箱子（自动并同 id 同 Metadata 的行、
        ///   不够再开新行；箱子 27 行全满且无处并 → 返回 false，物品留在手上）。
        ///   行式容器没有「格位」概念，点击哪一行不影响落点</item>
        /// </list>
        /// </summary>
        public static bool ClickChestSlot(
            ChestSystem chest, int x, int y, int z, int rowIndex,
            PlayerInventory inventory, ref ItemStack held, bool shift)
        {
            if (chest == null) return false;
            if (shift) return ShiftTakeRow(chest, x, y, z, rowIndex, inventory);

            if (held.IsEmpty)
            {
                ItemStack? row = chest.Take(x, y, z, rowIndex);
                if (row == null) return false; // 空行 / 越界：点了个寂寞
                held = row.Value;
                return true;
            }

            if (chest.Put(x, y, z, held))
            {
                held = ItemStack.Empty;
                return true;
            }
            return false; // 箱子满：物品留在手上
        }

        /// <summary>
        /// Shift 点击箱子行：整行倒进背包。背包装不下的余量放回箱子——刚移除过一行，
        /// 箱子必有空间，<see cref="ChestSystem.Put"/> 不会失败；即便极端情形失败也只是
        /// 该余量行留在箱子里（Put 是全有或全无，不会半途丢弃）。
        /// </summary>
        public static bool ShiftTakeRow(
            ChestSystem chest, int x, int y, int z, int rowIndex, PlayerInventory inventory)
        {
            if (chest == null || inventory == null) return false;
            ItemStack? row = chest.Take(x, y, z, rowIndex);
            if (row == null) return false;

            inventory.TryAdd(row.Value, out int leftover);
            if (leftover > 0)
            {
                chest.Put(x, y, z, row.Value.WithCount(leftover));
            }
            return true;
        }

        /// <summary>
        /// 点击背包第 <paramref name="slotIndex"/> 格（0..35，0..8 是 hotbar）。
        /// <list type="bullet">
        /// <item>Shift：整格转移进箱子（<see cref="ShiftMoveToChest"/>），手上物品不参与</item>
        /// <item>手持空 + 格非空：整格拿到手上</item>
        /// <item>手持非空 + 格空：手上物品放进该格</item>
        /// <item>手持非空 + 同物品（id 与 Metadata 都同）：往该格并堆到 maxStack，并满为止</item>
        /// <item>手持非空 + 不同物品：整格与手上交换</item>
        /// </list>
        /// </summary>
        public static bool ClickInventorySlot(
            ChestSystem chest, int x, int y, int z,
            PlayerInventory inventory, int slotIndex, ref ItemStack held, bool shift)
        {
            if (inventory == null || slotIndex < 0 || slotIndex >= PlayerInventory.TotalSize) return false;
            if (shift) return ShiftMoveToChest(chest, x, y, z, inventory, slotIndex);

            ItemStack slot = inventory.GetSlot(slotIndex);
            if (held.IsEmpty)
            {
                if (slot.IsEmpty) return false;
                held = slot;
                inventory.SetSlot(slotIndex, ItemStack.Empty);
                return true;
            }

            if (slot.IsEmpty)
            {
                inventory.SetSlot(slotIndex, held);
                held = ItemStack.Empty;
                return true;
            }

            if (slot.ItemId == held.ItemId && slot.Metadata == held.Metadata)
            {
                // 同物品并堆：只并到 maxStack；目标格已满返回 false（物品留在手上，可换一格放）
                int max = inventory.MaxStackFor(slot.ItemId);
                int take = System.Math.Min(held.Count, max - slot.Count);
                if (take <= 0) return false;
                inventory.SetSlot(slotIndex, slot.WithCount(slot.Count + take));
                held = held.Count > take ? held.WithCount(held.Count - take) : ItemStack.Empty;
                return true;
            }

            // 不同物品：整格交换（手持永远不为空，交换后拿到的是原格物品）
            inventory.SetSlot(slotIndex, held);
            held = slot;
            return true;
        }

        /// <summary>
        /// Shift 点击背包格：整格塞进箱子。箱子装不下（27 行全满且无法并堆）→ 返回 false，
        /// 物品原样留在背包格（Put 全有或全无，不会塞一半）。
        /// </summary>
        public static bool ShiftMoveToChest(
            ChestSystem chest, int x, int y, int z, PlayerInventory inventory, int slotIndex)
        {
            if (chest == null || inventory == null) return false;
            ItemStack stack = inventory.GetSlot(slotIndex);
            if (stack.IsEmpty) return false;
            if (!chest.Put(x, y, z, stack)) return false;
            inventory.SetSlot(slotIndex, ItemStack.Empty);
            return true;
        }

        /// <summary>
        /// 关窗归还手持：先尝试进背包，余量再放回箱子；两边都装不下时把**剩余整栈**经返回值
        /// 交还调用方（UI 侧 spawn 成掉落物），绝不静默丢弃。归还后 <paramref name="held"/>
        /// 恒为空——要么归位了，要么责任已移交。
        /// </summary>
        public static ItemStack ReturnHeld(
            ChestSystem chest, int x, int y, int z, PlayerInventory inventory, ref ItemStack held)
        {
            if (held.IsEmpty) return ItemStack.Empty;

            ItemStack remainder = held;
            int leftover = held.Count;
            if (inventory != null)
            {
                inventory.TryAdd(remainder, out leftover);
            }
            if (leftover > 0)
            {
                remainder = remainder.WithCount(leftover);
                if (chest != null && chest.Put(x, y, z, remainder))
                {
                    remainder = ItemStack.Empty;
                }
                // 箱子也满：remainder 带回给调用方 spawn 掉落物
            }
            else
            {
                remainder = ItemStack.Empty;
            }

            held = ItemStack.Empty;
            return remainder;
        }
    }
}
