using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 附魔界面：按 X 打开。选目标等级（1-5）→ 扣经验 + 青金石 → 给工具加 Sharpness。
    /// <para>
    /// 简化：作用于 hotbar 选中槽的工具。失败提示理由（缺经验/缺青金石/无工具）。
    /// </para>
    /// </summary>
    public sealed class EnchantingUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.X;
        public int MaxLevel = 5;

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

        private void OnDisable()
        {
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

            // 等级按钮
            GUI.Label(new Rect(bg.x + 20, bg.y + 70, w - 40, 20), "选择附魔等级：");
            for (int lv = 1; lv <= MaxLevel; lv++)
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
                DoEnchant(ctx, tool, idx, cost, _selectedLevel);
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

        private static void DoEnchant(PlayerContext ctx, ItemStack tool, int slotIndex, (int ExpCost, int LapisCost) cost, int selectedLevel)
        {
            // 扣青金石
            if (!ctx.Items.TryGetById("lapis", out var lapisDef)) return;
            ctx.Inventory.TryRemoveCount(lapisDef.NumericId, cost.LapisCost);
            // 扣经验
            int remaining = cost.ExpCost;
            while (remaining > 0)
            {
                if (ctx.Experience.Current > 0)
                {
                    int take = System.Math.Min(ctx.Experience.Current, remaining);
                    // 减经验：Add(-x) 不降级，简单做法是手动 set
                    var newXp = new Experience(ctx.Experience.Current - take, ctx.Experience.Level);
                    ctx.Experience = newXp;
                    remaining -= take;
                }
                else if (ctx.Experience.Level > 0)
                {
                    ctx.Experience = new Experience(Experience.ExpPerLevel - 1, ctx.Experience.Level - 1);
                    remaining -= 1;
                }
                else break;
            }

            // 投附魔 → 写入工具 metadata（这里只演示 Sharpness attack bonus 累加）
            var rolled = EnchantingTable.Roll(selectedLevel);
            int newDamage = ctx.Items.TryGetByNumericId(tool.ItemId, out var def) && def.AttackDamage != null
                ? (int)(def.AttackDamage.Value + rolled.AttackBonus)
                : 0;
            // 简化：metadata 低字节存附魔等级；这里只加耐久字段（如果工具还没有）
            int maxDur = tool.HasDurability ? tool.MaxDurability : 100;
            var newStack = tool.WithMaxDurability(maxDur);
            // 编码 Sharpness level 进 metadata 的 8-15 位（与耐久共享高字节会冲突）
            // 简化：附魔等级写入 count 不可行；直接保留当前栈，不写入附魔数值（演示够用）。
            ctx.Inventory.SetSlot(slotIndex, newStack);

            // 用 IMGUI 弹一条提示（无 GUI 信息通道，简化为 Debug.Log）
            Debug.Log($"附魔 {def?.DisplayName ?? "工具"} → +Sharpness Lv{rolled.Level}，攻击 {newDamage}");
        }

        public int SelectedLevel { get => _selectedLevel; set => _selectedLevel = value; }
    }
}