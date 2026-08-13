using UnityEngine;
using MyWorld.Core.Items;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 熔炉 UI：input / fuel / output + progress bar。
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
            // 画 input / fuel / output slots + progress bar
            GUI.Box(new Rect(10, 80, 200, 30), $"Furnace: {CurrentProgress:F2}");
        }
    }
}
