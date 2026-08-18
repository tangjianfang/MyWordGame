using MyWorld.Core.Items;
using MyWorld.Core.Player;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 合成网格的最小点击交互（m6 C2 fix1）：三个合成 UI（口袋 1×1 / 背包 2×2 / 工作台 3×3）共用。
    /// 本里程碑不做拖拽，语义固定为：
    /// <list type="bullet">
    /// <item>左键点**空格**：从当前选中 hotbar 格放 1 个进去（hotbar 数量 -1）</item>
    /// <item>左键点**有物品的格**：取回 1 个进背包；背包满（<see cref="PlayerInventory.SpaceFor"/> 为 0）
    ///   则取不回，物品留在格子里</item>
    /// <item>m13 P0 修：SHIFT+左键点**主背包格**（9..35）→ 把该格 1 个送进合成区（不依赖 hotbar 选中）；
    ///   SHIFT+左键点**合成区有物品的格**→ 把 1 个送回指定主背包格（目标空才送，防覆盖）</item>
    /// </list>
    /// 配方匹配的刷新不用额外做——IMGUI 的 OnGUI 每次重绘都会用当前网格重跑 FindMatch 画输出格。
    /// </summary>
    internal static class CraftGridInteraction
    {
        /// <summary>本次 OnGUI 事件的鼠标左键是否落在 <paramref name="r"/> 内。</summary>
        public static bool IsLeftClickIn(Rect r)
        {
            return Event.current != null
                && Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && r.Contains(Event.current.mousePosition);
        }

        /// <summary>m13 P0：本次鼠标左键是否带 SHIFT 修饰键（主背包→合成网格路径专用）。</summary>
        public static bool IsShiftLeftClickIn(Rect r)
        {
            return Event.current != null
                && Event.current.type == EventType.MouseDown
                && Event.current.button == 0
                && Event.current.shift
                && r.Contains(Event.current.mousePosition);
        }

        /// <summary>空格点击：从选中 hotbar 格取 1 个放进 <paramref name="cell"/>。手上数量 -1。
        /// <para>m6 终审修 M1：用 <see cref="ItemStack.WithCount"/> 而不是
        /// <c>new ItemStack(id, 1)</c>——Metadata（耐久/附魔）必须随物品走，
        /// 否则工具放格再取回 = 耐久重置，是刷修复的边角。</para></summary>
        public static bool PutSelectedOne(PlayerInventory inv, ref ItemStack cell)
        {
            if (inv == null || !cell.IsEmpty) return false;
            var sel = inv.GetSelected();
            if (sel.IsEmpty) return false;

            cell = sel.WithCount(1);
            inv.TryRemoveOne(inv.SelectedHotbarIndex);
            return true;
        }

        /// <summary>有物品的格点击：取回 1 个进背包。背包零空间时失败，物品留在格子里。
        /// <para>m6 终审修 M1：取回同样用 <see cref="ItemStack.WithCount"/> 保留 Metadata。</para></summary>
        public static bool TakeBackOne(PlayerInventory inv, ref ItemStack cell)
        {
            if (inv == null || cell.IsEmpty) return false;

            inv.TryAdd(cell.WithCount(1), out int leftover);
            if (leftover > 0) return false; // 单个物品要么进包要么没有：leftover>0 = 一格都塞不下

            cell = cell.Count > 1 ? cell.WithCount(cell.Count - 1) : ItemStack.Empty;
            return true;
        }

        /// <summary>m13 P0 修：SHIFT+点击主背包格 → 把该格 1 个送进合成网格。
        /// 这是孩子"M1和MP背包和工作台里面的物品都没办法合成"的修复入口——
        /// 之前合成网格的**唯一**入料路径是 hotbar 选中格，主背包 27 格完全无路可达。
        /// 设计选择 SHIFT（而非拖拽或右键）= 与既有"无修饰键走 hotbar 路径"语义区分清楚，
        /// 用户试一下就知道按 SHIFT 是「从主背包入料」。</summary>
        public static bool PutMainSlotOne(PlayerInventory inv, ref ItemStack cell, int mainSlotIndex)
        {
            if (inv == null || !cell.IsEmpty) return false;
            if (mainSlotIndex < PlayerInventory.HotbarSize
                || mainSlotIndex >= PlayerInventory.HotbarSize + PlayerInventory.MainSize) return false;
            var s = inv.GetSlot(mainSlotIndex);
            if (s.IsEmpty) return false;

            cell = s.WithCount(1);
            inv.TryRemoveOne(mainSlotIndex);
            return true;
        }

        /// <summary>m13 P0 修：SHIFT+点击合成网格有物品的格 → 把 1 个送回指定主背包槽位。
        /// 目标格非空就拒绝（防意外覆盖既有材料——和 TakeBackOne 走背包零空间的失败模式对称）。
        /// 不调 TryAdd 而调 SetSlot 是因为目标槽已知、可控；走堆叠会让用户失去对格位布局的控制。</summary>
        public static bool TakeBackToMainSlotOne(PlayerInventory inv, ref ItemStack cell, int mainSlotIndex)
        {
            if (inv == null || cell.IsEmpty) return false;
            if (mainSlotIndex < PlayerInventory.HotbarSize
                || mainSlotIndex >= PlayerInventory.HotbarSize + PlayerInventory.MainSize) return false;
            var dest = inv.GetSlot(mainSlotIndex);
            if (!dest.IsEmpty) return false;

            inv.SetSlot(mainSlotIndex, cell.WithCount(1));
            cell = cell.Count > 1 ? cell.WithCount(cell.Count - 1) : ItemStack.Empty;
            return true;
        }
    }
}
