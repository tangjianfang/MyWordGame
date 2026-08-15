using UnityEngine;
using MyWorld.Core.Items;
using MyWorld.Unity.Gameplay;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 熔炉 UI：input / fuel / output 三槽 + progress bar。
    /// m6 A2：原先只有一行黑字进度文本，现在三槽走 ItemSlotDrawer（图标 + 数量角标），
    /// 文本换白字缓存样式，深色 Box 上直接可读。
    /// </summary>
    public class CraftingFurnaceUi : MonoBehaviour
    {
        public float CurrentProgress { get; private set; }
        private FurnaceSystem _furnace;

        public void Bind(FurnaceSystem f) { _furnace = f; }

        public void TickForTest()
        {
            if (_furnace != null) CurrentProgress = _furnace.Progress;
        }

        private void OnGUI()
        {
            if (_furnace == null) return;
            CurrentProgress = _furnace.Progress;
            var items = PlayerContext.Instance != null ? PlayerContext.Instance.Items : null;

            // 背景：160(左) 宽 200、高 170，三槽 + 进度条全部框在内
            GUI.Box(new Rect(10, 80, 200, 170), GUIContent.none);
            GUI.Label(new Rect(20, 84, 180, 18), $"熔炉 {CurrentProgress:F2}", ItemSlotDrawer.WhiteStyle());

            const int size = 40;
            // 左列：上=输入、下=燃料；右列：输出
            ItemSlotDrawer.Draw(new Rect(24, 110, size, size), _furnace.Input ?? ItemStack.Empty, items, false);
            ItemSlotDrawer.Draw(new Rect(24, 160, size, size), _furnace.Fuel ?? ItemStack.Empty, items, false);
            ItemSlotDrawer.Draw(new Rect(110, 135, size, size), _furnace.Output ?? ItemStack.Empty, items, false);

            // 烧炼进度条：输出槽下方，宽度按进度填充（Progress 达到烧炼时长即重置，clamp 防瞬时越界）
            var bar = new Rect(110, 190, 84, 10);
            GUI.Box(bar, GUIContent.none);
            float fillW = bar.width * Mathf.Clamp01(CurrentProgress);
            if (fillW > 0.5f) GUI.DrawTexture(new Rect(bar.x, bar.y, fillW, bar.height), Texture2D.whiteTexture);
        }
    }
}
