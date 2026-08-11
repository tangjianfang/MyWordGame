using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>按 E 打开 inventory（3 行 9 列 + 2×2 crafting + 1 输出）。</summary>
    public sealed class CraftingInventoryUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.E;
        public int SlotSize = 40;
        public int CraftWidth = 2;
        public int CraftHeight = 2;

        private bool _open;
        private ItemStack[] _craft;

        private void Awake()
        {
            _craft = new ItemStack[CraftWidth * CraftHeight];
            for (int i = 0; i < _craft.Length; i++) _craft[i] = ItemStack.Empty;
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) _open = !_open;
        }

        private void OnGUI()
        {
            if (!_open) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;

            float bgW = (CraftWidth + 1) * (SlotSize + 4) + 40;
            float bgH = 240;
            var bg = new Rect(20, 20, bgW, bgH);
            GUI.Box(bg, "背包 (E 关闭)");

            // 2x2 crafting 区
            for (int y = 0; y < CraftHeight; y++)
            for (int x = 0; x < CraftWidth; x++)
            {
                var r = new Rect(40 + x * (SlotSize + 4), 60 + y * (SlotSize + 4), SlotSize, SlotSize);
                DrawSlot(r, _craft[y * CraftWidth + x]);
            }
            // 输出
            var outRect = new Rect(40 + (CraftWidth + 1) * (SlotSize + 4), 60 + SlotSize / 2, SlotSize, SlotSize);
            var recipe = ctx.Recipes != null ? ctx.Recipes.FindMatch(_craft, CraftWidth, CraftHeight) : null;
            DrawSlot(outRect, recipe != null ? recipe.Output : ItemStack.Empty);

            // 主背包
            for (int i = 0; i < PlayerInventory.MainSize; i++)
            {
                int row = i / 9;
                int col = i % 9;
                var r = new Rect(40 + col * (SlotSize + 4), 180 + row * (SlotSize + 4), SlotSize, SlotSize);
                DrawSlot(r, ctx.Inventory.GetSlot(PlayerInventory.HotbarSize + i));
            }
            // hotbar 缩影在最下方
            for (int i = 0; i < 9; i++)
            {
                var r = new Rect(40 + i * (SlotSize + 4), 180 + 3 * (SlotSize + 4) + 8, SlotSize, SlotSize);
                DrawSlot(r, ctx.Inventory.GetSlot(i));
            }
        }

        private void DrawSlot(Rect r, ItemStack s)
        {
            GUI.Box(r, GUIContent.none);
            if (s.IsEmpty) return;
            var ctx = PlayerContext.Instance;
            if (ctx != null && ctx.Items != null && ctx.Items.TryGetByNumericId(s.ItemId, out var def))
            {
                GUI.Label(r, def.DisplayName + " ×" + s.Count);
            }
        }
    }
}
