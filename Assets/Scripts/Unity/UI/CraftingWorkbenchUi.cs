using MyWorld.Core.Items;
using MyWorld.Core.Quests;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>对 crafting_table 按 E 打开 3×3 workbench。简化版：按 P 切换。
    /// <para>m6 C2 fix1：合成网格有点击交互了——左键空格放 1 个（选中 hotbar 格 -1）、
    /// 左键有物品的格取回 1 个（背包满则留在格子里）；点输出格拿走产出
    /// （<see cref="TryTakeCraftOutput"/>：按配方精确消耗网格、发 CraftItem 事件）。</para></summary>
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
            EnsureGrid();
        }

        private void Update()
        {
            if (!Input.GetKeyDown(ToggleKey)) return;
            // m6 终审修 C1（B3-③）：自己开着时按键 = 关自己；其它模态 UI 开着时不叠开
            if (_open) SetOpen(false);
            else if (!UiCursorGate.IsOpen) SetOpen(true);
        }

        /// <summary>m6 B1：程序化开关工作台（--ui-shot 截图管线用）。不影响 Update 里的按键开关。
        /// <para>m6 终审修 C1：所有开关路径统一经 <see cref="UiCursorGate"/> 登记指针门。</para></summary>
        public void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            if (open) UiCursorGate.Open();
            else UiCursorGate.Close();
        }

        private void OnDisable()
        {
            // m6 终审修 C1（B3-②）：禁用/销毁时若还开着必须把门位还回去，否则计数泄漏
            if (_open) SetOpen(false);
        }

        /// <summary>EditMode 下 AddComponent 不触发 Awake，网格数组可能还是 null——所有入口先补齐。</summary>
        private void EnsureGrid()
        {
            if (_craft == null)
            {
                _craft = new ItemStack[9];
                for (int i = 0; i < _craft.Length; i++) _craft[i] = ItemStack.Empty;
            }
        }

        /// <summary>m6 C2：测试用。按 itemId 数组写入 3×3 合成网格（0 或越界 = 空）。</summary>
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

        /// <summary>m6 C2 fix1：测试读网格格（断言点击交互的中间状态）。</summary>
        internal ItemStack GetCellForTest(int cellIndex)
        {
            EnsureGrid();
            return cellIndex >= 0 && cellIndex < _craft.Length ? _craft[cellIndex] : ItemStack.Empty;
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

        /// <summary>m6 C2：当前 3×3 网格跑 FindMatch，匹配则把输出写到 LastOutput。纯解析，不消耗、不发事件。</summary>
        public void CraftForTest()
        {
            EnsureGrid();
            LastOutput = null;
            var db = PlayerContext.Instance != null ? PlayerContext.Instance.Recipes : null;
            var recipe = db != null ? db.FindMatch(_craft, 3, 3) : null;
            if (recipe == null) return;
            LastOutput = recipe.Output;
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

            var db = ctx.Recipes;
            var recipe = db != null ? db.FindMatch(_craft, 3, 3) : null;
            if (recipe == null) return false;

            var output = recipe.Output;
            if (ctx.Inventory.SpaceFor(output.ItemId) < output.Count)
            {
                return false; // 背包装不下：整单失败，产出留在输出格、网格不消耗
            }

            ctx.Inventory.TryAdd(output, out _); // 预检过，leftover 必为 0
            int[] consumed = CraftingMatrix.Consume(recipe, _craft, 3);
            for (int i = 0; i < _craft.Length; i++)
            {
                if (consumed[i] <= 0) continue;
                var s = _craft[i];
                _craft[i] = s.Count > consumed[i] ? s.WithCount(s.Count - consumed[i]) : ItemStack.Empty;
            }
            LastOutput = output;

            // av W3-13：合成音
            MyWorld.Unity.Audio.PlayerAudioSystem.Instance?.PlayCraft();

            QuestEventBus.Instance?.Raise(new QuestEvent
            {
                Type = QuestEventType.CraftItem,
                ItemId = output.ItemId,
                Count = output.Count,
            });
            return true;
        }

        private void OnGUI()
        {
            if (!_open) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            EnsureGrid();

            // m6 A2：旧框 200×240 装不下——输出槽右缘 x=400、hotbar 行右缘 x=612、
            // 下缘 y=340，全部格子必须框在背景内，框宽改 420、高 380
            GUI.Box(new Rect(200, 100, 420, 380), GUIContent.none);
            GUI.Label(new Rect(210, 104, 200, 18), "工作台 (P 关闭)", ItemSlotDrawer.WhiteStyle());

            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                var r = new Rect(220 + x * (SlotSize + 4), 140 + y * (SlotSize + 4), SlotSize, SlotSize);
                int idx = y * 3 + x;
                DrawSlot(r, _craft[idx]);
                if (CraftGridInteraction.IsLeftClickIn(r) && ClickGridCell(idx))
                {
                    Event.current.Use();
                }
                // m13 P0 修：SHIFT+click 合成格（有物品）→ 把 1 个送回主背包空格位
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
            var outRect = new Rect(220 + 3 * (SlotSize + 4) + 8, 140 + SlotSize, SlotSize, SlotSize);
            var recipe = ctx.Recipes != null ? ctx.Recipes.FindMatch(_craft, 3, 3) : null;
            DrawSlot(outRect, recipe != null ? recipe.Output : ItemStack.Empty);
            if (CraftGridInteraction.IsLeftClickIn(outRect) && TryTakeCraftOutput())
            {
                Event.current.Use();
            }

            // m10 C2 fix1（I3）：残血装备不能当合成材料（升级不是免费维修机）——
            // 匹配层已拒合（输出格空着），这里给孩子一句为什么，不然只会干瞪眼
            if (recipe == null && CraftingMatrix.HasDamagedMaterial(_craft))
            {
                GUI.Label(new Rect(220, 274, 400, 18),
                    "装备耐久不满，不能合成（升级要两件完好的同款装备）", ItemSlotDrawer.WhiteStyle());
            }

            // m13 P0 修：主背包缩影（替换原 9 格 hotbar-only 视图为 9 hotbar + 18 main）
            // — 27 主背包格对应 PlayerInventory.HotbarSize..35（=0..8 hotbar + 9..35 main）
            // — 但工作台尺寸约束紧凑，分两行：上行 9 格 hotbar，下行 9+9 主背包前 18 格
            // — 排版：上行 220..612 在主框内（420 宽），下行 220..612（两排 9 格×68px + 间隔）
            for (int i = 0; i < 9; i++)
            {
                var r = new Rect(220 + i * (SlotSize + 4), 300, SlotSize, SlotSize);
                int slotIndex = i;  // hotbar 0..8
                DrawSlot(r, ctx.Inventory.GetSlot(slotIndex));
                // SHIFT+click hotbar 格 → 找第一个空格合成位塞入（hotbar 也走同一入口）
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
            // 主背包 27 格（位置：紧贴 hotbar 下方，占 3 行×9 列）
            for (int i = 0; i < PlayerInventory.MainSize; i++)
            {
                int row = i / 9;
                int col = i % 9;
                var r = new Rect(220 + col * (SlotSize + 4), 340 + row * (SlotSize + 4), SlotSize, SlotSize);
                int slotIndex = PlayerInventory.HotbarSize + i;  // 9..35
                DrawSlot(r, ctx.Inventory.GetSlot(slotIndex));
                // SHIFT+click 主背包格 → 把 1 个送进合成网格
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
