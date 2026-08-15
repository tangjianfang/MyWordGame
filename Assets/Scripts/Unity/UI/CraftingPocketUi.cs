using System.Collections.Generic;
using MyWorld.Core.Items;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>1×1 口袋合成。按 B 打开，输入槽 + 输出槽。</summary>
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
            DrawSlot(outputRect, _output);

            // 重新计算 output
            var slots = new[] { _input };
            var r = ctx.Recipes.FindMatch(slots, 1, 1);
            _output = r != null ? r.Output : ItemStack.Empty;

            // 拿输出
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0
                && outputRect.Contains(Event.current.mousePosition))
            {
                if (!_output.IsEmpty)
                {
                    ctx.Inventory.TryAdd(_output, out _);
                    _input = ItemStack.Empty;
                    _output = ItemStack.Empty;
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
