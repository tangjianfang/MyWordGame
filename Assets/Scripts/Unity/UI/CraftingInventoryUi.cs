using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Core.Quests;
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
            if (!Input.GetKeyDown(ToggleKey)) return;
            // m6 终审修 C1（B3-③）：自己开着时按键 = 关自己；其它模态 UI 开着时不叠开
            if (_open) SetOpen(false);
            else if (!UiCursorGate.IsOpen) SetOpen(true);
        }

        /// <summary>背包当前是否开着（m11 W2-1）。只读投影——穿戴栏
        /// <see cref="ArmorSlotsUi"/> 绑定背包后随它同开同关，避免两个组件
        /// 各自按 E 判 <see cref="UiCursorGate"/> 的同帧竞态（先开者把门顶起来，
        /// 后开者的「无其它模态」检查就通不过，两边永远错拍）。</summary>
        public bool IsOpen => _open;

        /// <summary>m6 B1：程序化开关背包（--ui-shot 截图管线用）。不影响 Update 里的按键开关。
        /// <para>m6 终审修 C1：所有开关路径统一经 <see cref="UiCursorGate"/> 登记指针门——
        /// 打开时解锁指针，否则实机上点击只命中屏幕中心，格子点不到。</para></summary>
        public void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            if (open) UiCursorGate.Open();
            else UiCursorGate.Close();
        }

        private void OnDisable()
        {
            // m6 终审修 C1（B3-②）：禁用/销毁时若还开着必须把门位还回去，
            // 否则计数泄漏会让指针永远解锁、挖/放永久抑制
            if (_open) SetOpen(false);
        }

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

        /// <summary>B6：测试用。当前网格跑 FindMatch，匹配则把输出 ItemStack 写到 LastOutput。
        /// <para>m6 C2 fix1：纯解析——不消耗网格、不发事件（发事件的是
        /// <see cref="TryTakeCraftOutput"/>，那是真正的合成落地路径）。</para></summary>
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

        /// <summary>
        /// m6 C2 fix1：模拟点击合成格（OnGUI 的点击处理与 EditMode 测试共用这一入口）。
        /// 空格 = 从选中 hotbar 格放 1 个；有物品 = 取回 1 个进背包（背包满则留在格子里）。
        /// </summary>
        public bool ClickGridCell(int cellIndex)
        {
            EnsureGrid();
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return false;
            if (cellIndex < 0 || cellIndex >= _craft.Length) return false;

            return _craft[cellIndex].IsEmpty
                ? CraftGridInteraction.PutSelectedOne(ctx.Inventory, ref _craft[cellIndex])
                : CraftGridInteraction.TakeBackOne(ctx.Inventory, ref _craft[cellIndex]);
        }

        /// <summary>m6 C2 fix1：测试读网格格（断言点击交互的中间状态）。</summary>
        internal ItemStack GetCellForTest(int cellIndex)
        {
            EnsureGrid();
            return cellIndex >= 0 && cellIndex < _craft.Length ? _craft[cellIndex] : ItemStack.Empty;
        }

        /// <summary>
        /// m6 C2 fix1：取走输出格的合成产出——容量预检（<see cref="PlayerInventory.SpaceFor"/>）
        /// 通过才拿：产出进背包、按 <see cref="CraftingMatrix.Consume"/> 精确扣掉网格里配方
        /// 消耗的物品，并发 CraftItem 任务事件。装不下 / 无匹配配方返回 false，网格与背包都不动。
        /// </summary>
        public bool TryTakeCraftOutput()
        {
            EnsureGrid();
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return false;

            var db = _recipes != null ? _recipes : ctx.Recipes;
            var recipe = db != null ? db.FindMatch(_craft, CraftWidth, CraftHeight) : null;
            if (recipe == null) return false;

            var output = recipe.Output;
            if (ctx.Inventory.SpaceFor(output.ItemId) < output.Count)
            {
                return false; // 背包装不下：整单失败，产出留在输出格、网格不消耗
            }

            ctx.Inventory.TryAdd(output, out _); // 预检过，leftover 必为 0
            int[] consumed = CraftingMatrix.Consume(recipe, _craft, CraftWidth);
            for (int i = 0; i < _craft.Length; i++)
            {
                if (consumed[i] <= 0) continue;
                var s = _craft[i];
                _craft[i] = s.Count > consumed[i] ? s.WithCount(s.Count - consumed[i]) : ItemStack.Empty;
            }
            LastOutput = output;

            QuestEventBus.Instance?.Raise(new QuestEvent
            {
                Type = QuestEventType.CraftItem,
                ItemId = output.ItemId,
                Count = output.Count,
            });
            return true;
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

            // m6 终审修 I1：背景框宽取「合成区（含输出格）」与「主背包 9 列」的较大者——
            // 旧值 (2+1)*(40+4)+40=172 只罩住 2×2 合成区，主背包 9 列（x=40..432）与
            // hotbar 行大半画在框外（spec「背包所有格子在框内」）。9 列时 = 9*44+40 = 436
            float bgW = Mathf.Max((CraftWidth + 1) * (SlotSize + 4) + 40, 9 * (SlotSize + 4) + 40);
            // m6 A2：240 装不下实际内容——27 格主背包占 y=180..312，hotbar 行再 +8 到 y=360，
            // 背景框必须 ≥380 才能把全部格子框住（此前底部两行画在框外）
            const float bgH = 380;
            var bg = new Rect(20, 20, bgW, bgH);
            GUI.Box(bg, GUIContent.none);
            GUI.Label(new Rect(30, 24, 200, 18), "背包 (E 关闭)", ItemSlotDrawer.WhiteStyle());

            // 2x2 crafting 区（m6 C2 fix1：点击放/取，见 CraftGridInteraction）
            for (int y = 0; y < CraftHeight; y++)
            for (int x = 0; x < CraftWidth; x++)
            {
                var r = new Rect(40 + x * (SlotSize + 4), 60 + y * (SlotSize + 4), SlotSize, SlotSize);
                int idx = y * CraftWidth + x;
                DrawSlot(r, _craft[idx]);
                if (CraftGridInteraction.IsLeftClickIn(r) && ClickGridCell(idx))
                {
                    Event.current.Use();
                }
                // m13 P0 修：SHIFT+click 合成格（有物品）→ 把 1 个送回最近的主背包空格位
                else if (CraftGridInteraction.IsShiftLeftClickIn(r)
                    && !_craft[idx].IsEmpty)
                {
                    int sent = -1;
                    for (int mi = 0; mi < PlayerInventory.MainSize; mi++)
                    {
                        int slotIndex = PlayerInventory.HotbarSize + mi;
                        if (CraftGridInteraction.TakeBackToMainSlotOne(
                            ctx.Inventory, ref _craft[idx], slotIndex))
                        {
                            sent = slotIndex;
                            break;
                        }
                    }
                    if (sent >= 0) Event.current.Use();
                }
            }
            // 输出（每次重绘用当前网格重跑 FindMatch——网格变化后输出格自动刷新）
            var outRect = new Rect(40 + (CraftWidth + 1) * (SlotSize + 4), 60 + SlotSize / 2, SlotSize, SlotSize);
            var db = _recipes != null ? _recipes : ctx.Recipes;
            var recipe = db != null ? db.FindMatch(_craft, CraftWidth, CraftHeight) : null;
            DrawSlot(outRect, recipe != null ? recipe.Output : ItemStack.Empty);
            if (CraftGridInteraction.IsLeftClickIn(outRect) && TryTakeCraftOutput())
            {
                Event.current.Use();
            }

            // 主背包（m13 P0 修：SHIFT+click 主背包格 → 把 1 个送进合成网格）
            for (int i = 0; i < PlayerInventory.MainSize; i++)
            {
                int row = i / 9;
                int col = i % 9;
                var r = new Rect(40 + col * (SlotSize + 4), 180 + row * (SlotSize + 4), SlotSize, SlotSize);
                int slotIndex = PlayerInventory.HotbarSize + i;
                DrawSlot(r, ctx.Inventory.GetSlot(slotIndex));
                // SHIFT+click 主背包格 → 找第一个空格合成位塞入
                if (CraftGridInteraction.IsShiftLeftClickIn(r))
                {
                    for (int cellIdx = 0; cellIdx < _craft.Length; cellIdx++)
                    {
                        if (_craft[cellIdx].IsEmpty
                            && CraftGridInteraction.PutMainSlotOne(ctx.Inventory, ref _craft[cellIdx], slotIndex))
                        {
                            Event.current.Use();
                            break;
                        }
                    }
                }
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