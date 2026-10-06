using MyWorld.Core.Entities;
using MyWorld.Core.Enchanting;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 附魔界面：按 X 打开。选目标等级（1-3，封顶 <see cref="EnchantSystem.MaxLevel"/>）→
    /// 扣经验 + 青金石 → 写 <see cref="EnchantStore"/> 真附魔。
    /// <para>
    /// 评审 08 F25 真化：此前是占位——真扣经验与青金石却只掷 Debug.Log 提示、不写 store，
    /// 孩子花 30 级经验换一行日志。现在经验扣除与 store 写入一体走
    /// <see cref="EnchantSystem.Enchant"/>（成功才扣、失败零副作用），青金石在成功后扣。
    /// </para>
    /// <para>
    /// m10 终审修 I1 不变量保持：附魔**不改写工具栈**（不写 Metadata、不改耐久上限）——
    /// 附魔只进 <see cref="EnchantStore"/> 的槽位字典（m11 W2-2），耐久上限唯一来源
    /// 仍是 items 表 <c>maxDurability</c>。
    /// </para>
    /// </summary>
    public sealed class EnchantingUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.X;

        /// <summary>
        /// m11 W3-4：附魔完成公开事件——纯视觉订阅点（<see cref="MyWorld.Unity.FX.ParticlePool"/>
        /// 在玩家处播附魔光柱），无订阅者零开销，玩法语义零变化。
        /// DoEnchant 是 static，事件也 static（与 BlockInteraction.BlockBroken 同约定）。
        /// </summary>
        public static event System.Action Enchanted;

        private bool _open;
        private int _selectedLevel = 1;

        private void Update()
        {
            if (!Input.GetKeyDown(ToggleKey)) return;
            // m6 终审修 C1（B3-③）：自己开着时按键 = 关自己；其它模态 UI 开着时不叠开
            if (_open) SetOpen(false);
            else if (!UiCursorGate.IsOpen) SetOpen(true);
        }

        /// <summary>m6 终审修 C1：程序化开关（测试用）。所有开关路径统一经
        /// <see cref="UiCursorGate"/> 登记指针门——附魔要点等级按钮，指针不解锁实机不可用。</summary>
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
            // m6 终审修 C1（B3-②）：禁用/销毁时若还开着必须把门位还回去，否则计数泄漏
            if (_open) SetOpen(false);
        }

        private void OnGUI()
        {
            if (!_open) return;
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Items == null) return;

            float w = 300, h = 220;
            var bg = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            GUI.Box(bg, "附魔台 (X 关闭)");

            // 工具栏选中槽
            int idx = ctx.Inventory.SelectedHotbarIndex;
            var tool = ctx.Inventory.GetSlot(idx);
            string toolName = "(空)";
            if (!tool.IsEmpty && ctx.Items.TryGetByNumericId(tool.ItemId, out var def))
                toolName = def.DisplayName;

            GUI.Label(new Rect(bg.x + 20, bg.y + 40, w - 40, 20), $"目标工具：{toolName}");

            // 等级按钮（封顶 3 级：与 EnchantSystem.MaxLevel 对齐——评审 08 F25 前 UI 口径 5 级作废）
            GUI.Label(new Rect(bg.x + 20, bg.y + 70, w - 40, 20), "选择附魔等级：");
            for (int lv = 1; lv <= EnchantSystem.MaxLevel; lv++)
            {
                var btnRect = new Rect(bg.x + 20 + (lv - 1) * 50, bg.y + 95, 40, 30);
                if (GUI.Toggle(btnRect, _selectedLevel == lv, "Lv " + lv) && _selectedLevel != lv)
                {
                    _selectedLevel = lv;
                }
            }

            var cost = EnchantingTable.CostForLevel(_selectedLevel);
            int haveExp = ctx.Experience.Current + ctx.Experience.Level * Experience.ExpPerLevel;
            int haveLapis = CountItem(ctx, "lapis");

            GUI.Label(new Rect(bg.x + 20, bg.y + 140, w - 40, 20),
                $"消耗：经验 {cost.ExpCost}（有 {haveExp}），青金石 {cost.LapisCost}（有 {haveLapis}）");

            var doBtn = new Rect(bg.x + 20, bg.y + 170, w - 40, 36);
            bool canDo = !tool.IsEmpty && ctx.Items.TryGetByNumericId(tool.ItemId, out var def2) && def2.IsTool
                         && haveExp >= cost.ExpCost && haveLapis >= cost.LapisCost;
            GUI.enabled = canDo;
            if (GUI.Button(doBtn, "附魔"))
            {
                DoEnchant(ctx, tool, cost, _selectedLevel);
            }
            GUI.enabled = true;
        }

        private static int CountItem(PlayerContext ctx, string id)
        {
            if (!ctx.Items.TryGetById(id, out var def)) return 0;
            int total = 0;
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                var s = ctx.Inventory.GetSlot(i);
                if (!s.IsEmpty && s.ItemId == def.NumericId) total += s.Count;
            }
            return total;
        }

        /// <summary>EditMode 直注附魔存储（优先于 <see cref="EnchantStore.Default"/>）——
        /// 与 <see cref="Player.BlockInteraction"/> 的 ResolveEnchants 双源解析同款，
        /// 测试不碰静态单例（EnchantStore 文档纪律）。</summary>
        internal static EnchantStore EnchantsOverride;

        /// <summary>附魔存储双源解析：EditMode 直注优先，否则全局 <see cref="EnchantStore.Default"/>。</summary>
        private static EnchantStore ResolveEnchants() => EnchantsOverride ?? EnchantStore.Default;

        /// <summary>执行一次附魔（评审 08 F25 真化）：经验扣除与 <see cref="EnchantStore"/>
        /// 写入一体走 <see cref="EnchantSystem.Enchant"/>（成功才扣、任何一步不过零副作用），
        /// 青金石在其成功后扣。掷骰沿用 <see cref="EnchantingTable.Roll"/>（当前确定性给
        /// Sharpness=所选级）。等级封顶 <see cref="EnchantSystem.MaxLevel"/>。
        /// m10 终审修 I1 不变量保持：**不改写工具栈**——耐久上限唯一来源是 items 表
        /// <c>maxDurability</c>，附魔只进 store 字典。public 供 EditMode 测试直驱。</summary>
        public static void DoEnchant(PlayerContext ctx, ItemStack tool, (int ExpCost, int LapisCost) cost, int selectedLevel)
        {
            var rolled = EnchantingTable.Roll(selectedLevel);
            var store = ResolveEnchants();
            var xp = ctx.Experience;
            var result = EnchantSystem.Enchant(store, ctx.Inventory, ref xp, ctx.Items,
                ctx.Inventory.SelectedHotbarIndex, rolled.Type, rolled.Level);
            if (result != EnchantResult.Ok)
            {
                Debug.LogWarning($"[EnchantingUi] 附魔未生效：{result}（经验/目标不满足，本次零消耗）");
                return;
            }
            ctx.Experience = xp;

            // 青金石在 Enchant 成功后扣（EnchantSystem 不认识 lapis——材料成本归 UI 层；
            // canDo 已验过余额，单线程内不会失败）
            if (!ctx.Items.TryGetById("lapis", out var lapisDef)) return;
            ctx.Inventory.TryRemoveCount(lapisDef.NumericId, cost.LapisCost);

            ctx.Items.TryGetByNumericId(tool.ItemId, out var def);
            Debug.Log($"附魔 {def?.DisplayName ?? "工具"} → {rolled.Type} Lv{rolled.Level}");

            // m11 W2-4 B7：占位台路径同样算「完成一次附魔」（扣了真经验与青金石，
            // 是实机可触发的附魔动作）→ EnchantItem 任务事件。真附魔路径
            //（附魔书融合）在 BlockInteraction.TryFuseEnchantedBook 成功处另发
            MyWorld.Unity.Gameplay.QuestEventBus.Instance?.Raise(
                new MyWorld.Core.Quests.QuestEvent
                {
                    Type = MyWorld.Core.Quests.QuestEventType.EnchantItem,
                });

            // m11 W3-4：附魔光柱纯视觉挂载点（扣费成功才算完成；上方 lapis 缺失的
            // 早退路径不会到这——没扣费就没光柱，语义一致）
            Enchanted?.Invoke();
        }

        public int SelectedLevel { get => _selectedLevel; set => _selectedLevel = value; }
    }
}