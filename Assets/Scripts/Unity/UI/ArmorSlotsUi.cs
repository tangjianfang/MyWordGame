using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 穿戴栏 UI（m11 W2-1）：背包界面右侧的头/胸/腿/脚 4 格，面板画在背包框
    /// （x=20..456）右侧。可见性**跟随背包**：绑定 <see cref="Backpack"/> 后随它同开同关
    ///（集成点在 WorldBootstrap 接线——本波禁改，先经 <see cref="SetOpen"/> 自管兜底）。
    /// 不与背包共用 E 键各自判 <see cref="UiCursorGate"/>：门是计数制，同帧先开者把门顶起来，
    /// 后开者的「无其它模态」检查通不过，两个组件会永远错拍——所以跟随后者不再碰键。
    /// <para>
    /// 交互语义（本里程碑不做拖拽，与合成格同款最小点击交互）：
    /// 左键点**空槽** = 自动穿上背包里第一件对应部位的盔甲（扫描 0..35 槽，
    /// 部位不匹配的跳过）；左键点**已穿槽** = 脱下回背包（背包满则保持穿戴）。
    /// 换装 = 脱下后再点一次空槽。穿脱都在 UI 内完成，不经 BlockInteraction 右键。
    /// </para>
    /// <para>
    /// 测试与 OnGUI 共用 <see cref="ClickSlot"/> 入口（CraftingInventoryUi.ClickGridCell 同款）；
    /// EditMode 下 AddComponent 不回调 Awake，PlayerContext.Instance 是 null，
    /// 测试经 <see cref="Bind"/> 显式注入。
    /// </para>
    /// </summary>
    public sealed class ArmorSlotsUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.E;
        public int SlotSize = 40;

        /// <summary>可选：跟随的背包界面（集成点接线）。绑定后穿戴栏随背包同开同关，
        /// 自身的按键/门逻辑停用（门由背包登记，不重复计数）；null = 自管开关（默认，
        /// 截图管线与未接线时可用）。</summary>
        public CraftingInventoryUi Backpack;

        // 背包框右缘 = 20 + 436 = 456，穿戴栏贴它右边（背包开着时视觉上是一体两栏）
        private const float PanelX = 464f;
        private const float PanelY = 20f;
        private const float PanelWidth = 160f;
        private const float PanelHeight = 278f;

        /// <summary>部位行标签：下标即穿戴槽号（0 头 / 1 胸 / 2 腿 / 3 脚）。</summary>
        private static readonly string[] PartLabels = { "头盔", "胸甲", "护腿", "靴子" };

        private bool _open;
        // 显式 Bind 注入的上下文；null 时回退 PlayerContext.Instance（运行时装配路径）
        private PlayerContext _bound;

        /// <summary>面板当前是否可见：绑了背包跟背包，否则看自己的开关。</summary>
        private bool IsVisible => Backpack != null ? Backpack.IsOpen : _open;

        /// <summary>当前生效的玩家上下文：Bind 注入优先，否则全局单例。</summary>
        private PlayerContext Ctx => _bound != null ? _bound : PlayerContext.Instance;

        /// <summary>
        /// m13 W4 模态 UI 点外关闭（孩子原话第 9 行）——本面板背景矩形。
        /// OnGUI 重算、EditMode 测试断言用。穿戴栏跟随背包开合——点外关闭**仅**
        /// 在自管模式生效（背包未绑定时）；跟随模式由背包的点击外判定接管。
        /// </summary>
        public Rect BackgroundBounds { get; private set; }

        /// <summary>
        /// raw 版"点外关闭"谓词（EditMode 测试直调入口）——与 <see cref="WeaponPanelUi"/>
        /// 同款语义：左键 MouseDown button=0、鼠标位置不在本面板矩形内。穿戴栏没有
        /// SHIFT 修饰路径（m11 W2-1 至今未引入），所以不挡 SHIFT。
        /// </summary>
        public bool ShouldCloseOnMouseDown(EventType type, int button, bool shift, Vector2 mousePosition)
        {
            if (type != EventType.MouseDown || button != 0) return false;
            if (shift) return false; // 预留：未来若引入 SHIFT 修饰（如批量卸甲），不能误关
            if (!IsVisible) return false;
            // 跟随背包时不独立关闭——背包面板自己点外关闭会顺带关穿戴栏
            if (Backpack != null) return false;
            return !BackgroundBounds.Contains(mousePosition);
        }

        /// <summary>B6 同款：显式绑定上下文（测试 / 截图管线用）。null = 解绑回退单例。</summary>
        public void Bind(PlayerContext ctx)
        {
            _bound = ctx;
        }

        private void Update()
        {
            // 跟随背包（集成点已接线）时不碰键也不碰门——背包自己管
            if (Backpack != null) return;
            if (!Input.GetKeyDown(ToggleKey)) return;
            if (_open) SetOpen(false);
            else if (!UiCursorGate.IsOpen) SetOpen(true);
        }

        /// <summary>程序化开关（--ui-shot 截图管线 / 测试用）。统一经
        /// <see cref="UiCursorGate"/> 登记指针门——穿戴栏是模态 UI，点击靠解锁指针。</summary>
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
            // 禁用/销毁时若还开着必须把门位还回去（计数泄漏会让指针永久解锁）。
            // 跟随背包时 _open 恒 false（门由背包登记），这里天然 no-op。
            if (_open) SetOpen(false);
        }

        /// <summary>该槽是否有盔甲穿着（= 画格时高亮）。穿戴栏有物品就是「正在生效」。</summary>
        public bool IsSlotWorn(int armorIndex)
        {
            var ctx = Ctx;
            return ctx != null && ctx.ArmorSlots != null && !ctx.ArmorSlots.GetSlot(armorIndex).IsEmpty;
        }

        /// <summary>
        /// 模拟点击穿戴槽（OnGUI 的点击处理与 EditMode 测试共用这一入口）。
        /// 空槽 = 穿上背包里第一件对应部位盔甲；已穿槽 = 脱下回背包（满则拒绝）。
        /// 穿脱成功后当场 <see cref="PlayerContext.RefreshGearBonuses"/>——
        /// EditMode 没有 Update，属性与汇总提示必须即时可见。
        /// </summary>
        public bool ClickSlot(int armorIndex)
        {
            var ctx = Ctx;
            if (ctx == null || ctx.Inventory == null || ctx.ArmorSlots == null) return false;
            if (armorIndex < 0 || armorIndex >= ArmorInventory.SlotCount) return false;

            bool changed = ctx.ArmorSlots.GetSlot(armorIndex).IsEmpty
                ? TryEquipFirstFound(ctx, armorIndex)
                : ctx.ArmorSlots.TryUnequipTo(ctx.Inventory, armorIndex);

            if (changed) ctx.RefreshGearBonuses();
            return changed;
        }

        /// <summary>扫描背包 0..35 槽，把第一件部位对应的盔甲穿进 armorIndex 槽。</summary>
        private static bool TryEquipFirstFound(PlayerContext ctx, int armorIndex)
        {
            var items = ctx.Items;
            if (items == null) return false;
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                ItemStack stack = ctx.Inventory.GetSlot(i);
                if (stack.IsEmpty) continue;
                if (!items.TryGetByNumericId(stack.ItemId, out var def)) continue;
                if (ArmorInventory.SlotIndexOf(def.ArmorPart) != armorIndex) continue;
                return ctx.ArmorSlots.TryEquipFrom(ctx.Inventory, i, items);
            }
            return false;
        }

        /// <summary>穿戴生效提示：三属性当前汇总（双源——穿戴 + 手持），只列非零项。
        /// 纯读当前值（刷新由每帧 Update / ClickSlot 负责），测试断言用。</summary>
        public string BonusSummaryText()
        {
            var ctx = Ctx;
            if (ctx == null) return "无装备加成";

            string text = "";
            if (ctx.Defense > 0) text += $"防御 +{ctx.Defense}  ";
            if (ctx.MoveSpeedBonus > 0f) text += $"移速 +{Mathf.RoundToInt(ctx.MoveSpeedBonus * 100f)}%  ";
            if (ctx.MaxHealthBonus > 0) text += $"血上限 +{ctx.MaxHealthBonus}";
            return text.Length > 0 ? text.TrimEnd() : "无装备加成";
        }

        private void OnGUI()
        {
            if (!IsVisible) return;
            var ctx = Ctx;
            if (ctx == null || ctx.Inventory == null || ctx.ArmorSlots == null) return;

            var bg = new Rect(PanelX, PanelY, PanelWidth, PanelHeight);
            BackgroundBounds = bg; // m13 W4：点外关闭断言用

            // m13 W4 模态 UI 点外关闭（孩子原话第 9 行）。跟随背包（Backpack != null）
            // 时由 ShouldCloseOnMouseDown 拦截——背包点外关闭会顺带关穿戴栏，行为不重复。
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
            GUI.Label(new Rect(PanelX + 8, PanelY + 4, 140, 18), "穿戴 (E 关闭)",
                ItemSlotDrawer.WhiteStyle());

            for (int i = 0; i < ArmorInventory.SlotCount; i++)
            {
                var slot = new Rect(PanelX + 8, PanelY + 30 + i * (SlotSize + 4), SlotSize, SlotSize);
                GUI.Box(slot, GUIContent.none);
                ItemStack stack = ctx.ArmorSlots.GetSlot(i);
                // 穿戴中的槽位描边高亮（selected 传 IsSlotWorn 语义）：这 4 槽有物品 = 加成正在生效
                ItemSlotDrawer.Draw(slot, stack, ctx.Items, selected: !stack.IsEmpty);
                GUI.Label(new Rect(slot.xMax + 4, slot.y + 11, 96, 18), PartLabels[i],
                    ItemSlotDrawer.WhiteStyle());
                if (CraftGridInteraction.IsLeftClickIn(slot) && ClickSlot(i))
                {
                    Event.current.Use();
                }
            }

            // 底部三属性汇总提示（双源合计，与血条/移速实际生效值同源）
            GUI.Label(
                new Rect(PanelX + 8, PanelY + 30 + ArmorInventory.SlotCount * (SlotSize + 4) + 2, 148, 64),
                BonusSummaryText(), ItemSlotDrawer.WhiteStyle());
        }
    }
}
