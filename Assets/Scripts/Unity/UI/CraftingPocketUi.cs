using System.Collections.Generic;
using MyWorld.Core.Items;
using MyWorld.Core.Quests;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>1×1 口袋合成。按 B 打开，输入槽 + 输出槽。
    /// <para>m6 C2：取产出的消费点发 CraftItem 任务事件（产出 itemId + 本次数量）。</para></summary>
    public sealed class CraftingPocketUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.B;
        public int SlotSize = 48;
        private bool _open;
        private ItemStack _input;
        private ItemStack _output;

        private void Update()
        {
            if (!Input.GetKeyDown(ToggleKey)) return;
            // m6 终审修 C1（B3-③）：自己开着时按键 = 关自己；其它模态 UI 开着时不叠开
            if (_open) SetOpen(false);
            else if (!UiCursorGate.IsOpen) SetOpen(true);
        }

        /// <summary>m6 终审修 C1：程序化开关口袋合成（测试用）。所有开关路径统一经
        /// <see cref="UiCursorGate"/> 登记指针门。</summary>
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

        /// <summary>m6 C2：测试用——直接写入口袋输入槽（EditMode 没法模拟鼠标点击）。</summary>
        public void SetInputForTest(ItemStack input)
        {
            _input = input;
        }

        /// <summary>
        /// m6 C2 fix1：模拟点击口袋输入格（OnGUI 的点击处理与 EditMode 测试共用这一入口）。
        /// 空格 = 从选中 hotbar 格放 1 个；有物品 = 取回 1 个进背包（背包满则留在格子里）。
        /// </summary>
        public bool ClickInputCell()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Inventory == null) return false;

            return _input.IsEmpty
                ? CraftGridInteraction.PutSelectedOne(ctx.Inventory, ref _input)
                : CraftGridInteraction.TakeBackOne(ctx.Inventory, ref _input);
        }

        /// <summary>
        /// 取走输出槽：按当前输入重算配方，匹配则产出进背包、清空输入并返回 true。
        /// OnGUI 的点击取料与测试都走这一条路径；产出进包后发 CraftItem 事件。
        /// <para>
        /// m6 C2 fix1：先做容量预检（<see cref="PlayerInventory.SpaceFor"/>）——
        /// 背包装不下时整单失败：输入不消耗、产出退回输出格，**不再**出现
        /// 「产出凭空消失还照发任务事件」的旧 bug。
        /// </para>
        /// </summary>
        public bool TryTakeCraftOutput()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Recipes == null || ctx.Inventory == null) return false;

            var r = ctx.Recipes.FindMatch(new[] { _input }, 1, 1);
            if (r == null) return false;

            var output = r.Output;
            if (ctx.Inventory.SpaceFor(output.ItemId) < output.Count)
            {
                return false; // 背包装不下：拿取失败，输入与输出格都保持原样
            }

            ctx.Inventory.TryAdd(output, out _); // 预检过，leftover 必为 0
            _input = ItemStack.Empty;
            _output = ItemStack.Empty;

            // av W3-13：合成音
            MyWorld.Unity.Audio.PlayerAudioSystem.Instance?.PlayCraft();

            // m6 C2：产出进包 = 合成落地。Count 用本次产出数量（不是背包现存量）
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
            if (ctx == null || ctx.Recipes == null) return;

            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;
            // m6 A2：旧框高 60 罩不住 48px 的槽位（槽下缘 cy+48 已出框），框高改 100 全部框住
            var bgRect = new Rect(cx - 80, cy - 30, 160, 100);
            GUI.Box(bgRect, GUIContent.none);
            GUI.Label(new Rect(cx - 70, cy - 26, 140, 18), "口袋合成 (B 关闭)", ItemSlotDrawer.WhiteStyle());

            // 输入槽
            var inputRect = new Rect(cx - 60, cy, SlotSize, SlotSize);
            DrawSlot(inputRect, _input);
            // 箭头
            GUI.Label(new Rect(cx - 10, cy + 14, 30, 20), "→", ItemSlotDrawer.WhiteStyle());
            // 输出槽
            var outputRect = new Rect(cx + 20, cy, SlotSize, SlotSize);

            // 重新计算 output（输入格变化后输出格自动刷新）
            var slots = new[] { _input };
            var r = ctx.Recipes.FindMatch(slots, 1, 1);
            _output = r != null ? r.Output : ItemStack.Empty;
            DrawSlot(outputRect, _output);

            // 拿输出
            if (CraftGridInteraction.IsLeftClickIn(outputRect) && !_output.IsEmpty && TryTakeCraftOutput())
            {
                Event.current.Use();
            }
            // 输入格：空格放 1 个 / 有物品取回 1 个（m6 C2 fix1，与其它合成网格同款）
            if (CraftGridInteraction.IsLeftClickIn(inputRect) && ClickInputCell())
            {
                Event.current.Use();
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
