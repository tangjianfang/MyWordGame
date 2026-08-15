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
            if (Input.GetKeyDown(ToggleKey)) _open = !_open;
        }

        /// <summary>m6 C2：测试用——直接写入口袋输入槽（EditMode 没法模拟鼠标点击）。</summary>
        public void SetInputForTest(ItemStack input)
        {
            _input = input;
        }

        /// <summary>
        /// 取走输出槽：按当前输入重算配方，匹配则产出进背包、清空输入并返回 true。
        /// OnGUI 的点击取料与测试都走这一条路径；产出进包后发 CraftItem 事件。
        /// </summary>
        public bool TryTakeCraftOutput()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null || ctx.Recipes == null) return false;

            var r = ctx.Recipes.FindMatch(new[] { _input }, 1, 1);
            if (r == null) return false;

            var output = r.Output;
            ctx.Inventory.TryAdd(output, out _);
            _input = ItemStack.Empty;
            _output = ItemStack.Empty;

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

            // 重新计算 output
            var slots = new[] { _input };
            var r = ctx.Recipes.FindMatch(slots, 1, 1);
            _output = r != null ? r.Output : ItemStack.Empty;
            DrawSlot(outputRect, _output);

            // 拿输出
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && outputRect.Contains(Event.current.mousePosition))
            {
                if (!_output.IsEmpty && TryTakeCraftOutput())
                {
                    Event.current.Use();
                }
            }
            // 拿输入
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && inputRect.Contains(Event.current.mousePosition))
            {
                if (!_input.IsEmpty)
                {
                    var take = _input;
                    _input = ItemStack.Empty;
                    ctx.Inventory.TryAdd(take, out _);
                    Event.current.Use();
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
