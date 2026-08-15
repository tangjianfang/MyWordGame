using MyWorld.Core.Items;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>对 crafting_table 按 E 打开 3×3 workbench。简化版：按 P 切换。</summary>
    public sealed class CraftingWorkbenchUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.P;
        public int SlotSize = 40;

        private bool _open;
        private ItemStack[] _craft;

        private void Awake()
        {
            _craft = new ItemStack[9];
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

            // m6 A2：旧框 200×240 装不下——输出槽右缘 x=400、hotbar 行右缘 x=612、
            // 下缘 y=340，全部格子必须框在背景内，框宽改 420、高 380
            GUI.Box(new Rect(200, 100, 420, 380), GUIContent.none);
            GUI.Label(new Rect(210, 104, 200, 18), "工作台 (P 关闭)", ItemSlotDrawer.WhiteStyle());

            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                var r = new Rect(220 + x * (SlotSize + 4), 140 + y * (SlotSize + 4), SlotSize, SlotSize);
                DrawSlot(r, _craft[y * 3 + x]);
            }

            // 输出
            var outRect = new Rect(220 + 3 * (SlotSize + 4) + 8, 140 + SlotSize, SlotSize, SlotSize);
            var recipe = ctx.Recipes != null ? ctx.Recipes.FindMatch(_craft, 3, 3) : null;
            DrawSlot(outRect, recipe != null ? recipe.Output : ItemStack.Empty);

            // 主背包缩影
            for (int i = 0; i < 9; i++)
            {
                var r = new Rect(220 + i * (SlotSize + 4), 300, SlotSize, SlotSize);
                DrawSlot(r, ctx.Inventory.GetSlot(i));
            }
        }

        private void DrawSlot(Rect r, ItemStack s)
        {
            // m6 A2：底框 + 公共物品格（图标 + 数量角标）替换旧的黑字 Label
            GUI.Box(r, GUIContent.none);
            var ctx = PlayerContext.Instance;
            ItemSlotDrawer.Draw(r, s, ctx != null ? ctx.Items : null, false);
        }
    }
}
