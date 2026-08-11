using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>食物条（10 个肉排占位）。plan-3c 接饥饿系统，plan-3a 只画。</summary>
    public sealed class FoodBarUI : MonoBehaviour
    {
        public const int DrumstickSize = 14;

        private Texture2D _full;
        private Texture2D _empty;
        private Texture2D _bg;

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
            float totalWidth = 10 * (DrumstickSize + 2);
            float startX = (Screen.width - totalWidth) / 2f;
            float y = Screen.height - 64 - HotbarUI.SlotSize - 18 + HealthBarUI.HeartSize + 2;

            // plan-3a 食物永远满，UI 静态显示
            for (int i = 0; i < 10; i++)
            {
                var rect = new Rect(startX + i * (DrumstickSize + 2), y, DrumstickSize, DrumstickSize);
                GUI.DrawTexture(rect, i < 8 ? _full : _empty);
            }
        }
    }
}
