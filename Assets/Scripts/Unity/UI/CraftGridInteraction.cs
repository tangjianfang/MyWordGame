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

        /// <summary>空格点击：从选中 hotbar 格取 1 个放进 <paramref name="cell"/>。手上数量 -1。</summary>
        public static bool PutSelectedOne(PlayerInventory inv, ref ItemStack cell)
        {
            if (inv == null || !cell.IsEmpty) return false;
            var sel = inv.GetSelected();
            if (sel.IsEmpty) return false;

            cell = new ItemStack(sel.ItemId, 1);
            inv.TryRemoveOne(inv.SelectedHotbarIndex);
            return true;
        }

        /// <summary>有物品的格点击：取回 1 个进背包。背包零空间时失败，物品留在格子里。</summary>
        public static bool TakeBackOne(PlayerInventory inv, ref ItemStack cell)
        {
            if (inv == null || cell.IsEmpty) return false;

            inv.TryAdd(new ItemStack(cell.ItemId, 1), out int leftover);
            if (leftover > 0) return false; // 单个物品要么进包要么没有：leftover>0 = 一格都塞不下

            cell = cell.Count > 1 ? cell.WithCount(cell.Count - 1) : ItemStack.Empty;
            return true;
        }
    }
}
