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
        // B6：显式 Bind 注入的配方表；为 null 时回退到 PlayerContext.Instance.Recipes，
        // 这样 OnGUI 既能被 WorldBootstrap 主动驱动，也能保持原先「只靠 PlayerContext」的写法。
        private RecipeDatabase _recipes;

        /// <summary>B6：最近一次 CraftForTest 得到的输出；null 表示未匹配、未绑定或配方表为空。</summary>
        public ItemStack? LastOutput { get; private set; }

        private void Awake()
        {
            _craft = new ItemStack[CraftWidth * CraftHeight];
            for (int i = 0; i < _craft.Length; i++) _craft[i] = ItemStack.Empty;
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) _open = !_open;
        }

        /// <summary>m6 B1：程序化开关背包（--ui-shot 截图管线用）。不影响 Update 里的按键开关。</summary>
        public void SetOpen(bool open) => _open = open;

        /// <summary>B6：显式绑定 RecipeDatabase；null = 解绑，回退到 PlayerContext。</summary>
        public void Bind(RecipeDatabase db)
        {
            _recipes = db;
        }

        /// <summary>B6：测试用。按 itemId 数组写入合成网格（0 或越界 = 空）。</summary>
        public void SetGridForTest(int[] items)
        {
            EnsureGrid();
            int n = items != null ? items.Length : 0;
            for (int i = 0; i < _craft.Length; i++)
            {
                int id = i < n ? items[i] : 0;
                _craft[i] = id == 0 ? ItemStack.Empty : new ItemStack(id, 1);
            }
        }

        /// <summary>B6：测试用。当前网格跑 FindMatch，匹配则把输出 ItemStack 写到 LastOutput。</summary>
        public void CraftForTest()
        {
            EnsureGrid();
            LastOutput = null;
            var db = _recipes != null ? _recipes : PlayerContext.Instance?.Recipes;
            if (db == null) return;
            var recipe = db.FindMatch(_craft, CraftWidth, CraftHeight);
            if (recipe == null) return;
            LastOutput = recipe.Output;
        }

        private void EnsureGrid()
        {
            int target = CraftWidth * CraftHeight;
            if (_craft == null || _craft.Length != target)
            {
                _craft = new ItemStack[target];
                for (int i = 0; i < _craft.Length; i++) _craft[i] = ItemStack.Empty;
            }
        }

        private void OnGUI()
        {
            if (!_open) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;

            float bgW = (CraftWidth + 1) * (SlotSize + 4) + 40;
            // m6 A2：240 装不下实际内容——27 格主背包占 y=180..312，hotbar 行再 +8 到 y=360，
            // 背景框必须 ≥380 才能把全部格子框住（此前底部两行画在框外）
            const float bgH = 380;
            var bg = new Rect(20, 20, bgW, bgH);
            GUI.Box(bg, GUIContent.none);
            GUI.Label(new Rect(30, 24, 200, 18), "背包 (E 关闭)", ItemSlotDrawer.WhiteStyle());

            // 2x2 crafting 区
            for (int y = 0; y < CraftHeight; y++)
            for (int x = 0; x < CraftWidth; x++)
            {
                var r = new Rect(40 + x * (SlotSize + 4), 60 + y * (SlotSize + 4), SlotSize, SlotSize);
                DrawSlot(r, _craft[y * CraftWidth + x]);
            }
            // 输出
            var outRect = new Rect(40 + (CraftWidth + 1) * (SlotSize + 4), 60 + SlotSize / 2, SlotSize, SlotSize);
            var db = _recipes != null ? _recipes : ctx.Recipes;
            var recipe = db != null ? db.FindMatch(_craft, CraftWidth, CraftHeight) : null;
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
            // m6 A2：底框 + 公共物品格（图标 + 数量角标）替换旧的黑字 Label
            GUI.Box(r, GUIContent.none);
            var ctx = PlayerContext.Instance;
            ItemSlotDrawer.Draw(r, s, ctx != null ? ctx.Items : null, false);

            // 悬停 tooltip：在格子上方显示物品中文名（旧实现只有一格黑字，认不出是什么）
            if (!s.IsEmpty && Event.current != null
                && r.Contains(Event.current.mousePosition)
                && ctx != null && ctx.Items != null
                && ctx.Items.TryGetByNumericId(s.ItemId, out var def))
            {
                GUI.Label(new Rect(r.x, r.y - 20, 140, 18), def.DisplayName, ItemSlotDrawer.WhiteStyle());
            }
        }
    }
}