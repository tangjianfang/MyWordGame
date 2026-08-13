using MyWorld.Unity.Gameplay;
using MyWorld.Core.Player;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>食物条（10 个肉排占位）。每帧从 <see cref="HungerSystem"/> 读 <see cref="HungerSystem.Hunger"/>。</summary>
    public sealed class FoodBarUI : MonoBehaviour
    {
        public const int DrumstickSize = 14;

        /// <summary>最近一次从 <see cref="HungerSystem"/> 读到的 Hunger 值。OnGUI / TickForTest 后才更新。</summary>
        public int CurrentHunger { get; private set; } = 20;

        private HungerSystem _hunger;
        private Texture2D _full;
        private Texture2D _empty;
        private Texture2D _bg;

        /// <summary>绑定饥饿系统。允许为空，<c>null</c> 时 OnGUI 保持占位显示。</summary>
        public void Bind(HungerSystem hs) { _hunger = hs; }

        /// <summary>EditMode 测试入口：手动驱动一次刷新（避免依赖 OnGUI 事件循环）。</summary>
        public void TickForTest()
        {
            if (_hunger != null) CurrentHunger = _hunger.Hunger;
        }

        private void EnsureTextures()
        {
            if (_full != null) return;
            int s = 7;
            _full = new Texture2D(s, s);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool meat = (y >= 1 && y <= 4 && x >= 1 && x <= 5);
                bool bone = (y >= 5 && (x == 2 || x == 3));
                _full.SetPixel(x, y, meat ? new Color(0.75f, 0.4f, 0.3f) :
                                  bone ? new Color(0.95f, 0.9f, 0.7f) : new Color(0, 0, 0, 0));
            }
            _full.Apply();
            _full.filterMode = FilterMode.Point;
            _empty = new Texture2D(1, 1);
            _empty.SetPixel(0, 0, new Color(0.25f, 0.25f, 0.25f, 0.5f));
            _empty.Apply();
            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, new Color(0, 0, 0, 0.3f));
            _bg.Apply();
        }

        private void OnGUI()
        {
            EnsureTextures();
            if (_hunger != null) CurrentHunger = _hunger.Hunger;
            float totalWidth = 10 * (DrumstickSize + 2);
            float startX = (Screen.width - totalWidth) / 2f;
            float y = Screen.height - 64 - HotbarUI.SlotSize - 18 + HealthBarUI.HeartSize + 2;

            // 当前 Hunger 决定已填槽数（每 2 点 = 1 块肉排，10 槽满 = 20）。
            int filled = Mathf.Clamp(CurrentHunger / 2, 0, 10);
            for (int i = 0; i < 10; i++)
            {
                var rect = new Rect(startX + i * (DrumstickSize + 2), y, DrumstickSize, DrumstickSize);
                GUI.DrawTexture(rect, i < filled ? _full : _empty);
            }
        }
    }
}
