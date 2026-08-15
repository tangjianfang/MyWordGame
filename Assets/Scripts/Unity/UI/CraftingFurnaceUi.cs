using UnityEngine;
using MyWorld.Core.Items;
using MyWorld.Unity.Gameplay;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 熔炉 UI：input / fuel / output 三槽 + progress bar。
    /// m6 A2：原先只有一行黑字进度文本，改为三槽走 ItemSlotDrawer（图标 + 数量角标），
    /// 文本换白字缓存样式，深色 Box 上直接可读。
    /// m6 A2 fix2：老 bug——本类自创建（7051b08）起就没有开关、Bind 后常驻左上角。
    /// 现在与背包 E / 工作台 P 同款：F 键开关（E/P/B/V/X 已被其它 UI 占用）。
    /// </summary>
    public class CraftingFurnaceUi : MonoBehaviour
    {
        public KeyCode ToggleKey = KeyCode.F;
        public float CurrentProgress { get; private set; }

        /// <summary>是否打开显示。默认关闭（fix2 起）。</summary>
        public bool IsOpen => _open;

        private bool _open;
        private FurnaceSystem _furnace;

        public void Bind(FurnaceSystem f) { _furnace = f; }

        /// <summary>程序化开关（B1 截图管线将来加 ui-furnace.png 用，也供测试）。</summary>
        public void SetOpen(bool open) { _open = open; }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) _open = !_open;
        }

        public void TickForTest()
        {
            if (_furnace != null) CurrentProgress = _furnace.Progress;
        }

        private void OnGUI()
        {
            if (!_open || _furnace == null) return;
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
