using MyWorld.Core.Items;
using MyWorld.Core.Quests;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>对 crafting_table 按 E 打开 3×3 workbench。简化版：按 P 切换。
    /// <para>m6 C2：加 CraftForTest/SetGridForTest（与 CraftingInventoryUi 对称的产出解析点），
    /// 匹配到产出即发 CraftItem 任务事件。网格目前只有测试钩子能填充；
    /// 将来加点击合成时沿同一入口发事件即可。</para></summary>
    public sealed class CraftingWorkbenchUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.P;
        public int SlotSize = 40;

        private bool _open;
        private ItemStack[] _craft;

        /// <summary>m6 C2：最近一次 CraftForTest 的输出；null 表示未匹配 / 未绑定配方表。</summary>
        public ItemStack? LastOutput { get; private set; }

        private void Awake()
        {
            _craft = new ItemStack[9];
            for (int i = 0; i < _craft.Length; i++) _craft[i] = ItemStack.Empty;
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) _open = !_open;
        }

        /// <summary>m6 B1：程序化开关工作台（--ui-shot 截图管线用）。不影响 Update 里的按键开关。</summary>
        public void SetOpen(bool open) => _open = open;

        /// <summary>m6 C2：测试用。按 itemId 数组写入 3×3 合成网格（0 或越界 = 空）。</summary>
        public void SetGridForTest(int[] items)
        {
            int n = items != null ? items.Length : 0;
            for (int i = 0; i < _craft.Length; i++)
            {
                int id = i < n ? items[i] : 0;
                _craft[i] = id == 0 ? ItemStack.Empty : new ItemStack(id, 1);
            }
        }

        /// <summary>
        /// m6 C2：当前 3×3 网格跑 FindMatch，匹配则把输出写到 LastOutput 并发 CraftItem 事件
        /// （产出 itemId + 本次数量）。EditMode 测试与将来的点击合成共用这一入口。
        /// </summary>
        public void CraftForTest()
        {
            LastOutput = null;
            var db = PlayerContext.Instance != null ? PlayerContext.Instance.Recipes : null;
            var recipe = db != null ? db.FindMatch(_craft, 3, 3) : null;
            if (recipe == null) return;
            LastOutput = recipe.Output;

            QuestEventBus.Instance?.Raise(new QuestEvent
            {
                Type = QuestEventType.CraftItem,
                ItemId = recipe.Output.ItemId,
                Count = recipe.Output.Count,
            });
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
