using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 箱子界面（m11 W2-3）：上 27 格箱子（9×3）+ 下 36 格背包（主 27 + hotbar 9）双栏，
    /// 物品格一律走 <see cref="ItemSlotDrawer"/>。交互语义（Minecraft 同款简化）：
    /// <list type="bullet">
    /// <item>普通点击：物品在「鼠标手持栈」与格子之间取放（拿整叠 / 放下 / 同物品并堆 / 异物交换）</item>
    /// <item>Shift 点击：整叠直接转移到另一栏（箱子→背包 / 背包→箱子）</item>
    /// </list>
    /// 所有搬运逻辑都在 Core 纯静态 <see cref="ChestTransfer"/>（双链可测），本组件只画格子、
    /// 转发点击、托管手持栈；箱子内容真源是 <see cref="PlayerContext.ChestSystem"/>（坐标键），
    /// 存档链（SaveLoadService → LevelData.ChestContents）已通，关窗无需额外落盘动作。
    /// <para>
    /// <b>打开</b>：<see cref="Player.BlockInteraction"/> 的箱子右键分支（本卡独占）经
    /// <see cref="OpenAt"/> 触发——组件懒挂在玩家宿主上，不新增 WorldBootstrap 装配步骤；
    /// <b>关闭</b>：面板上的「关闭」按钮（E/Esc 不接：E 会与背包同帧双触发、Esc 会与暂停菜单
    /// 双触发，按键路由不在本卡范围）。开关走 <see cref="UiCursorGate"/> 指针门。
    /// </para>
    /// </summary>
    public sealed class ChestUi : MonoBehaviour
    {
        /// <summary>格子边长（与 CraftingInventoryUi 同款 40px，ItemSlotDrawer 角标几何按它调过）。</summary>
        public const int SlotSize = 40;

        /// <summary>格子间距。</summary>
        private const int SlotSpacing = 4;

        /// <summary>箱子格列数（27 格 = 9×3，与 <see cref="ChestSystem.Capacity"/> 一致）。</summary>
        private const int Columns = 9;

        private bool _open;
        private int _cx, _cy, _cz;
        private ItemStack _held = ItemStack.Empty;

        /// <summary>当前是否打开（EditMode 断言路由用）。</summary>
        public bool IsOpen => _open;

        /// <summary>当前打开的箱子坐标 X（打开时快照，测试断言开对了箱子）。</summary>
        public int ChestX => _cx;
        /// <summary>当前打开的箱子坐标 Y。</summary>
        public int ChestY => _cy;
        /// <summary>当前打开的箱子坐标 Z。</summary>
        public int ChestZ => _cz;

        /// <summary>鼠标手持栈（只读视图；测试断言取放中间态）。</summary>
        public ItemStack Held => _held;

        /// <summary>
        /// m13 W4 模态 UI 点外关闭（孩子原话第 9 行）——本面板背景矩形。
        /// OnGUI 重算、EditMode 测试断言用。箱子面板的关闭按钮路径独立（背景右上角
        /// "关闭"按钮），与点外关闭并存。
        /// </summary>
        public Rect BackgroundBounds { get; private set; }

        /// <summary>
        /// raw 版"点外关闭"谓词（EditMode 测试直调入口）——与 <see cref="WeaponPanelUi"/>
        /// 同款语义：左键 MouseDown button=0、鼠标位置不在本面板矩形内、且**不带 SHIFT**
        /// （箱子面板 SHIFT 是「整叠转移」语义——SHIFT+click 背包格 = 整叠进箱子，不能误关）。
        /// </summary>
        public bool ShouldCloseOnMouseDown(EventType type, int button, bool shift, Vector2 mousePosition)
        {
            if (type != EventType.MouseDown || button != 0) return false;
            if (shift) return false; // SHIFT 优先：箱子的整叠转移路径不能误关
            if (!_open) return false;
            return !BackgroundBounds.Contains(mousePosition);
        }

        /// <summary>
        /// 打开指定坐标的箱子（BlockInteraction 箱子分支入口）。组件懒挂：宿主上已有就复用，
        /// 没有就 AddComponent——WorldBootstrap 禁改，不新增装配步骤。ChestSystem 未就绪
        /// （数据表缺失降级）时返回 null，调用方保持右键 no-op 不放方块。
        /// </summary>
        public static ChestUi OpenAt(GameObject host, PlayerContext ctx, int x, int y, int z)
        {
            if (host == null || ctx == null || ctx.ChestSystem == null) return null;
            var ui = host.GetComponent<ChestUi>() ?? host.AddComponent<ChestUi>();
            ui.Open(x, y, z);
            return ui;
        }

        /// <summary>打开（换箱子 = 复用面板只换坐标）。开关统一走 <see cref="UiCursorGate"/>。</summary>
        public void Open(int x, int y, int z)
        {
            if (_open)
            {
                // 已开着：切换目标箱子前先把手上物品归位（不能带进另一个箱子）
                CloseHeldOnly();
                _cx = x; _cy = y; _cz = z;
                return;
            }
            _cx = x; _cy = y; _cz = z;
            _open = true;
            UiCursorGate.Open();
        }

        /// <summary>关闭：手持栈归还（背包→箱子→兜底掉落物，绝不丢），指针门还位。</summary>
        public void Close()
        {
            if (!_open) return;
            CloseHeldOnly();
            _open = false;
            UiCursorGate.Close();
        }

        /// <summary>归位手持栈（关窗 / 换箱共用）。背包与箱子都装不下时在箱子位置 spawn 掉落物。</summary>
        private void CloseHeldOnly()
        {
            var ctx = PlayerContext.Instance;
            var inventory = ctx != null ? ctx.Inventory : null;
            var chest = ctx != null ? ctx.ChestSystem : null;
            var held = _held;
            ItemStack remainder = ChestTransfer.ReturnHeld(chest, _cx, _cy, _cz, inventory, ref held);
            _held = held;

            if (!remainder.IsEmpty && ctx != null)
            {
                // 双栏全满的极端兜底：掉回箱子方块上（ItemDropEntity 走既有拾取管线，绝不静默丢弃）
                var drop = new ItemDropEntity(remainder, new Float3(_cx + 0.5f, _cy + 1.2f, _cz + 0.5f));
                drop.SpawnTime = Time.time;
                ctx.ItemDrops.Add(drop);
            }
        }

        /// <summary>评审 04 R-3：保存/退出前手持栈归位（复用关窗兜底链：背包→箱子→掉落）。</summary>
        private void ReturnHeldForSave(MyWorld.Unity.Gameplay.PlayerContext ctx) => CloseHeldOnly();

        private void OnEnable() => CraftGridInteraction.RegisterReturnHandler(ReturnHeldForSave);

        private void OnDisable()
        {
            // m6 终审修 C1（B3-②）同款：禁用/销毁时若还开着必须把门位还回去，否则计数泄漏
            CraftGridInteraction.UnregisterReturnHandler(ReturnHeldForSave);
            if (_open) Close();
        }

        // ─── 点击交互（OnGUI 与 EditMode 测试共用；逻辑全在 Core ChestTransfer） ───

        /// <summary>点击箱子侧第 rowIndex 行（0 起 = <see cref="ChestSystem.List"/> 下标）。
        /// shift=true 时整行转移进背包。</summary>
        public bool ClickChestSlot(int rowIndex, bool shift = false)
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.ChestSystem == null || ctx.Inventory == null) return false;
            var held = _held;
            bool done = ChestTransfer.ClickChestSlot(
                ctx.ChestSystem, _cx, _cy, _cz, rowIndex, ctx.Inventory, ref held, shift);
            _held = held;
            return done;
        }

        /// <summary>点击背包第 slotIndex 格（0..35）。shift=true 时整格转移进箱子。</summary>
        public bool ClickInventorySlot(int slotIndex, bool shift = false)
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.ChestSystem == null || ctx.Inventory == null) return false;
            var held = _held;
            bool done = ChestTransfer.ClickInventorySlot(
                ctx.ChestSystem, _cx, _cy, _cz, ctx.Inventory, slotIndex, ref held, shift);
            _held = held;
            return done;
        }

        /// <summary>当前箱子内容（OnGUI 画格与测试断言共用，只读）。</summary>
        public System.Collections.Generic.IReadOnlyList<DropSnapshot> ChestRows()
        {
            var ctx = PlayerContext.Instance;
            return ctx != null && ctx.ChestSystem != null
                ? ctx.ChestSystem.List(_cx, _cy, _cz)
                : new DropSnapshot[0];
        }

        private void OnGUI()
        {
            if (!_open) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return;

            // 9 列 × (40+4) - 4 + 左右边距 40 ≈ 436（与背包 UI 同宽，同屏切换不跳）
            float w = Columns * (SlotSize + SlotSpacing) - SlotSpacing + 40f;
            // 标题 24 + 箱子 3 行 144 + 间隔 16 + 主背包 3 行 144 + 间隔 12 + hotbar 44 + 底距 20
            float h = 24f + 3 * (SlotSize + SlotSpacing) + 16f + 3 * (SlotSize + SlotSpacing) + 12f
                      + (SlotSize + SlotSpacing) + 20f;
            var bg = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            BackgroundBounds = bg; // m13 W4：点外关闭断言用

            // m13 W4 模态 UI 点外关闭（孩子原话第 9 行）。SHIFT 优先——箱子面板
            // SHIFT+click = 整叠转移（箱子↔背包），不能被本分支误关。关闭按钮（右上角）
            // 是独立路径，命中按钮时 Event.current 还在按钮上 → 落在 bg 内 → 不触发本分支。
            if (Event.current != null
                && ShouldCloseOnMouseDown(
                    Event.current.type, Event.current.button,
                    Event.current.shift, Event.current.mousePosition))
            {
                Close();
                Event.current.Use();
                return;
            }

            GUI.Box(bg, GUIContent.none);
            GUI.Label(new Rect(bg.x + 20, bg.y + 6, 200, 18), "箱子", ItemSlotDrawer.WhiteStyle());

            // 关闭按钮（右上角）：唯一关闭入口（E/Esc 双触发风险见类注释）
            var closeRect = new Rect(bg.xMax - 90, bg.y + 4, 70, 26);
            if (GUI.Button(closeRect, "关闭"))
            {
                Close();
                return;
            }

            var rows = ChestRows();
            float left = bg.x + 20f;

            // ── 箱子 27 格：行式容器按序铺进 9×3 格（行 i → 第 i 格；行数之后的格画空） ──
            for (int i = 0; i < ChestSystem.Capacity; i++)
            {
                int row = i / Columns, col = i % Columns;
                var r = new Rect(
                    left + col * (SlotSize + SlotSpacing),
                    bg.y + 28f + row * (SlotSize + SlotSpacing),
                    SlotSize, SlotSize);
                var stack = i < rows.Count
                    ? new ItemStack(rows[i].ItemId, rows[i].Count, rows[i].Metadata)
                    : ItemStack.Empty;
                DrawSlot(r, stack);
                if (CraftGridInteraction.IsLeftClickIn(r) && ClickChestSlot(i, Event.current.shift))
                {
                    Event.current.Use();
                }
            }

            // ── 主背包 27 格（9..35）+ hotbar 9 格（0..8），布局照 CraftingInventoryUi ──
            float invY = bg.y + 28f + 3 * (SlotSize + SlotSpacing) + 16f;
            for (int i = 0; i < PlayerInventory.MainSize; i++)
            {
                int row = i / Columns, col = i % Columns;
                var r = new Rect(
                    left + col * (SlotSize + SlotSpacing),
                    invY + row * (SlotSize + SlotSpacing),
                    SlotSize, SlotSize);
                int slotIndex = PlayerInventory.HotbarSize + i;
                DrawSlot(r, ctx.Inventory.GetSlot(slotIndex));
                if (CraftGridInteraction.IsLeftClickIn(r) && ClickInventorySlot(slotIndex, Event.current.shift))
                {
                    Event.current.Use();
                }
            }

            float hotbarY = invY + 3 * (SlotSize + SlotSpacing) + 12f;
            for (int i = 0; i < PlayerInventory.HotbarSize; i++)
            {
                var r = new Rect(left + i * (SlotSize + SlotSpacing), hotbarY, SlotSize, SlotSize);
                DrawSlot(r, ctx.Inventory.GetSlot(i));
                if (CraftGridInteraction.IsLeftClickIn(r) && ClickInventorySlot(i, Event.current.shift))
                {
                    Event.current.Use();
                }
            }

            // ── 手持栈：贴着鼠标画（所有格子之后，永远最上层） ──
            if (!_held.IsEmpty && Event.current != null)
            {
                var heldRect = new Rect(
                    Event.current.mousePosition.x - SlotSize / 2f,
                    Event.current.mousePosition.y - SlotSize / 2f,
                    SlotSize, SlotSize);
                ItemSlotDrawer.Draw(heldRect, _held, ctx.Items, false);
            }
        }

        /// <summary>画一格：底框 + 公共物品格（图标 + 数量角标）+ 悬停物品名（CraftingInventoryUi 同款）。</summary>
        private void DrawSlot(Rect r, ItemStack s)
        {
            GUI.Box(r, GUIContent.none);
            var ctx = PlayerContext.Instance;
            ItemSlotDrawer.Draw(r, s, ctx != null ? ctx.Items : null, false);

            if (!s.IsEmpty && Event.current != null
                && Event.current.type == EventType.Repaint
                && r.Contains(Event.current.mousePosition)
                && ctx != null && ctx.Items != null
                && ctx.Items.TryGetByNumericId(s.ItemId, out var def))
            {
                GUI.Label(new Rect(r.x, r.y - 20, 140, 18), def.DisplayName, ItemSlotDrawer.WhiteStyle());
            }
        }
    }
}
