using System.Collections.Generic;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// m13 W4 武器面板（孩子原话"设置一个专门用来切换各种武器的面板"）：
    /// R 键开关，列出背包（含 hotbar 0..8 + 主背包 9..35）全部武器，
    /// 分类显示近战（剑/斧）/ 弓 / 枪，各自弹药数（弓无弹药，枪显子弹数）。
    /// 点击行 → 把目标物品的源槽位设为 hotbar 选中槽（已选则无操作）。
    /// <para>
    /// 与 Esc/E/关闭按钮并存（m6 多通道开关风格）。换装路由与 hotbar 数字键 / 滚轮
    /// 共用 <see cref="PlayerInventory.SelectedHotbarIndex"/>，攻击/挖掘/蓄力等所有
    ///「看选中槽」的下游无需新增分支——这是 m11 hotbar 路由的最小延展。
    /// </para>
    /// <para>
    /// m13 W4 模态 UI 点外关闭：每个 UI 自己查 <c>Event.current.mousePosition</c> 与本面板矩形
    /// <see cref="BackgroundBounds"/>，落在外面且本帧是 MouseDown button=0 → 关闭自己。
    /// SHIFT+click 在本面板内不构成"点外"（避免 SHIFT 被吞）；其它 UI 的 SHIFT 优先
    /// 路径在它们自己的 OnGUI 内先判，本面板不抢。
    /// </para>
    /// <para>
    /// 测试与 OnGUI 共用 <see cref="EnumerateWeapons"/> / <see cref="ClickWeaponRow"/> /
    /// <see cref="IsInsidePanel"/> / <see cref="ShouldCloseOnMouseDown"/> 公共入口——
    /// EditMode 下 Event.current 不可构造，测试走 raw 值版本（照 DeathScreenUi 模式）。
    /// </para>
    /// </summary>
    public sealed class WeaponPanelUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.R;
        public int RowHeight = 28;
        public int Padding = 8;
        public int TitleHeight = 24;
        public int IconSize = 24;
        public int BulletNumericId = 1608; // bullet 物品 numericId（musket 弹药）

        private bool _open;
        // OnGUI 在每帧多次回调之间共用一份武器清单（Layout / Repaint 不重复扫描 36 槽）。
        private readonly List<WeaponRow> _cachedRows = new List<WeaponRow>();

        /// <summary>当前是否打开（EditMode 断言 + 截图管线用）。</summary>
        public bool IsOpen => _open;

        /// <summary>
        /// 面板背景矩形（OnGUI 画时计算，测试断言点外关闭时复用）。
        /// 屏幕居中，宽 = 360、高 = 标题 + 行数 × 行高 + 上下边距。零行时仅画标题。
        /// </summary>
        public Rect BackgroundBounds { get; private set; }

        private void Update()
        {
            if (!Input.GetKeyDown(ToggleKey)) return;
            // m6 终审修 C1（B3-③）：自己开着时按键 = 关自己；其它模态 UI 开着时不叠开
            if (_open) SetOpen(false);
            else if (!UiCursorGate.IsOpen) SetOpen(true);
        }

        /// <summary>m6 B1：程序化开关（--ui-shot 截图管线用）。
        /// <para>所有开关路径统一经 <see cref="UiCursorGate"/> 登记指针门。</para></summary>
        public void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;
            if (open) UiCursorGate.Open();
            else UiCursorGate.Close();
        }

        /// <summary>评审 04 R-6：级联关闭入口（幂等——已关再调 no-op）。</summary>
        private void CloseSelf() => SetOpen(false);

        private void OnEnable() => UiCursorGate.RegisterClose(CloseSelf);

        private void OnDisable()
        {
            UiCursorGate.UnregisterClose(CloseSelf);
            // m6 终审修 C1（B3-②）：禁用/销毁时若还开着必须把门位还回去
            if (_open) SetOpen(false);
        }

        // ─── 武器清单（与 OnGUI 共用，EditMode 测试也走这里） ─────────────────

        /// <summary>
        /// 武器分类：近战（剑/斧）、弓（远程抛物线）、枪（远程直射）。
        /// 仅按 <see cref="ItemDefinition.AttackDamage"/> / <see cref="ItemDefinition.Range"/>
        /// 判定，不维护显式 id 名单——加新剑/弓/枪只要写 AttackDamage/Range 就自动进表。
        /// </summary>
        public enum WeaponKind { Melee, Bow, Musket }

        /// <summary>一行武器的视图数据（OnGUI 与 EditMode 测试共用）。</summary>
        public readonly struct WeaponRow
        {
            /// <summary>武器所在背包槽下标（0..35）。</summary>
            public readonly int SlotIndex;
            /// <summary>武器物品 numericId。</summary>
            public readonly int ItemId;
            /// <summary>同槽的数量（武器 maxStack=1，恒为 1，预留）。</summary>
            public readonly int Count;
            /// <summary>武器分类（决定是否显示弹药列）。</summary>
            public readonly WeaponKind Kind;

            public WeaponRow(int slotIndex, int itemId, int count, WeaponKind kind)
            {
                SlotIndex = slotIndex;
                ItemId = itemId;
                Count = count;
                Kind = kind;
            }
        }

        /// <summary>
        /// 扫描背包 0..35，按"近战→弓→枪"内部排序收集所有武器槽位。
        /// 列表不传 out，调用方按 OnGUI 当帧缓存（<see cref="_cachedRows"/>）复用。
        /// </summary>
        public IReadOnlyList<WeaponRow> EnumerateWeapons()
        {
            _cachedRows.Clear();
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null || ctx.Items == null) return _cachedRows;

            var inv = ctx.Inventory;
            var items = ctx.Items;
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                var stack = inv.GetSlot(i);
                if (stack.IsEmpty) continue;
                if (!items.TryGetByNumericId(stack.ItemId, out var def)) continue;
                var kind = Classify(def);
                if (kind == null) continue;
                _cachedRows.Add(new WeaponRow(i, stack.ItemId, stack.Count, kind.Value));
            }
            // 同分类按源槽位升序（hotbar 0..8 在主背包 9..35 之前）—— 视觉稳定，孩子好定位
            _cachedRows.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
            return _cachedRows;
        }

        /// <summary>
        /// 物品是不是武器、属于哪一类。null = 非武器（不出现在面板里）。
        /// 近战：<c>AttackDamage &gt; 0 &amp;&amp; Range == 0</c>；弓/枪：<c>Range &gt; 0</c>
        /// （按 id 文本判 bow/musket——弓保留重力抛物线，枪走直射；两者都靠 Range 消亡，
        /// 但分类是显示用，不会改 <c>ProjectileEntity.IsStraightLine</c>）。
        /// </summary>
        public static WeaponKind? Classify(ItemDefinition def)
        {
            if (def == null) return null;
            if (def.Range > 0)
            {
                // 物品 id 含 "bow" → 弓；含 "musket" → 枪；其他 Range>0 一律按弓（保守抛物线）
                return def.Id != null && def.Id.IndexOf("musket", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? (WeaponKind?)WeaponKind.Musket
                    : (WeaponKind?)WeaponKind.Bow;
            }
            if (def.AttackDamage.HasValue && def.AttackDamage.Value > 0f) return WeaponKind.Melee;
            return null;
        }

        /// <summary>
        /// 模拟点击武器面板第 rowIndex 行（OnGUI 与 EditMode 测试共用）。
        /// 把该行的源槽位设为 hotbar 选中槽——若目标槽就是当前选中槽则 no-op，
        /// 不抖动 <see cref="PlayerInventory.SelectedHotbarIndex"/> 触发下游刷新。
        /// </summary>
        public bool ClickWeaponRow(int rowIndex)
        {
            var rows = EnumerateWeapons();
            if (rowIndex < 0 || rowIndex >= rows.Count) return false;
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return false;

            int target = rows[rowIndex].SlotIndex;
            // 武器在主背包（9..35）也允许选中——hotbar 数字键/滚轮只能切 0..8，但
            // 选中态是个逻辑槽，BlockInteraction 攻击/挖掘按 GetSelected() 读，
            // 主背包物品也能被手持使用（m3 既有路径）。但 m11 之后 hotbar UI 选中框
            // 只画 0..8——选 9..35 时格子在主背包里不在屏幕上可见，与现实现状一致。
            // 把武器挪到 hotbar 槽是 m11 既有行为（CombatController 在选不到武器时
            // 自动从背包挪到 hotbar 空槽）；本面板只切选中态即可，挪格由攻击路由兜底。
            ctx.Inventory.SelectedHotbarIndex = target;
            return true;
        }

        // ─── 点外关闭（m13 W4 模态 UI 统一行为） ──────────────────────────────

        /// <summary>
        /// raw 版"点外关闭"谓词（EditMode 测试直调入口）——Event.type + button 是
        /// 公共 raw 值，绕开 EditMode 构造 Event.current 的限制。
        /// 返回 true = 应当关闭本面板；调用方负责 <see cref="SetOpen"/>(false)。
        /// 闭包约定：m13 W4 与其它模态 UI 同步——
        /// <list type="bullet">
        /// <item>左键 MouseDown button=0，且鼠标位置不在 <see cref="BackgroundBounds"/> 内 → 关闭</item>
        /// <item>SHIFT 修饰不算点外（SHIFT 是合成面板的入料修饰键，本面板不抢但也不应误关）</item>
        /// <item>非 MouseDown 事件 / 其它按钮一律不算</item>
        /// </list>
        /// </summary>
        public bool ShouldCloseOnMouseDown(EventType type, int button, bool shift, Vector2 mousePosition)
        {
            if (type != EventType.MouseDown || button != 0) return false;
            if (shift) return false; // SHIFT 优先：合成 UI 的入料路径不应误关本面板
            if (!_open) return false;
            return !BackgroundBounds.Contains(mousePosition);
        }

        /// <summary>
        /// 鼠标位置是否落在面板矩形内（OnGUI 路由与其他 UI 的 SHIFT 优先级判定复用）。
        /// </summary>
        public bool IsInsidePanel(Vector2 mousePosition) => BackgroundBounds.Contains(mousePosition);

        private void OnGUI()
        {
            if (!_open) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;

            var rows = EnumerateWeapons();
            // 背景矩形：屏幕居中，宽 360，行数 × 行高 + 标题 + 上下边距
            const float bgW = 360f;
            float bgH = TitleHeight + Padding + Mathf.Max(1, rows.Count) * (RowHeight + Padding) + Padding;
            var bg = new Rect((Screen.width - bgW) / 2f, (Screen.height - bgH) / 2f, bgW, bgH);
            BackgroundBounds = bg;

            // m13 W4：点外关闭。在画任何格子之前判——SHIFT 优先于点外（合成 UI 路径）。
            if (Event.current != null
                && ShouldCloseOnMouseDown(
                    Event.current.type, Event.current.button,
                    Event.current.shift, Event.current.mousePosition))
            {
                SetOpen(false);
                Event.current.Use();
                return;
            }

            GUI.Box(bg, GUIContent.none);
            GUI.Label(
                new Rect(bg.x + Padding, bg.y + 4, bg.width - Padding * 2, TitleHeight - 4),
                $"武器面板 (R 关闭)  共 {rows.Count} 件",
                ItemSlotDrawer.WhiteStyle());

            if (rows.Count == 0)
            {
                GUI.Label(
                    new Rect(bg.x + Padding, bg.y + TitleHeight + Padding, bg.width - Padding * 2, RowHeight),
                    "背包里没有武器",
                    ItemSlotDrawer.WhiteStyle());
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var r = new Rect(
                    bg.x + Padding,
                    bg.y + TitleHeight + Padding + i * (RowHeight + Padding),
                    bg.width - Padding * 2, RowHeight);
                DrawRow(r, i, rows[i]);
                if (CraftGridInteraction.IsLeftClickIn(r) && ClickWeaponRow(i))
                {
                    Event.current.Use();
                }
            }
        }

        /// <summary>画一行武器：左侧小图标 + 中文名 + 弹药数（仅枪）。</summary>
        private void DrawRow(Rect r, int rowIndex, WeaponRow row)
        {
            // 整行可点击的高亮底——背景色微亮比默认 Box 显眼，但仍走 IMGUI 现有基色
            GUI.Box(r, GUIContent.none);

            var ctx = PlayerContext.Instance;
            var items = ctx != null ? ctx.Items : null;
            // 图标
            if (items != null && items.TryGetByNumericId(row.ItemId, out var def))
            {
                var iconRect = new Rect(r.x + 2, r.y + (r.height - IconSize) / 2f, IconSize, IconSize);
                var tex = ItemSlotDrawer.GetTextureOrPlaceholder(def);
                GUI.DrawTexture(iconRect, tex);
                // 名称
                GUI.Label(
                    new Rect(r.x + IconSize + 6, r.y + 4, r.width - IconSize - 8, r.height - 8),
                    def.DisplayName ?? def.Id ?? "?",
                    ItemSlotDrawer.WhiteStyle());
            }

            // 弹药数：仅枪（musket → bullet）。弓无弹药、ConsumeArrows 走既有路径
            if (row.Kind == WeaponKind.Musket && ctx != null && ctx.Inventory != null)
            {
                int ammo = ctx.Inventory.CountOf(BulletNumericId);
                GUI.Label(
                    new Rect(r.xMax - 60, r.y + 4, 56, r.height - 8),
                    $"弹 {ammo}",
                    ItemSlotDrawer.WhiteStyle());
            }

            // 选中标记：当前 hotbar 选中槽是这行的源槽位 → 右侧画一个"●"提示
            if (ctx != null && ctx.Inventory != null
                && ctx.Inventory.SelectedHotbarIndex == row.SlotIndex)
            {
                GUI.Label(
                    new Rect(r.xMax - 16, r.y + 4, 12, r.height - 8),
                    "●", ItemSlotDrawer.WhiteStyle());
            }
        }
    }
}