using MyWorld.Core.Entities;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 屏幕底部 10 颗红心。每颗心 = 2HP。
    /// 用 GUIStyle + 一颗纹理 9 切片绘制：满/半/空。
    /// </summary>
    public sealed class HealthBarUI : MonoBehaviour
    {
        public const int HeartSize = 18;
        public const int HeartsPerRow = 10;

        private Texture2D _heartFull;
        private Texture2D _heartHalf;
        private Texture2D _heartEmpty;
        private Texture2D _bg;

        private void EnsureTextures()
        {
            if (_heartFull != null) return;
            _heartFull = MakeHeart(new Color(0.85f, 0.1f, 0.1f));
            _heartHalf = MakeHeart(new Color(0.85f, 0.5f, 0.1f));
            _heartEmpty = MakeHeart(new Color(0.25f, 0.25f, 0.25f));
            _bg = new Texture2D(1, 1);
            _bg.SetPixel(0, 0, new Color(0, 0, 0, 0.4f));
            _bg.Apply();
        }

        private static Texture2D MakeHeart(Color c)
        {
            int s = 9;
            var t = new Texture2D(s, s);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                // 简单心形：上半圆 + 三角
                bool inside;
                if (y < 4)
                {
                    float dx1 = (x - 2.5f), dx2 = (x - 6.5f);
                    float dy = (y - 2.5f);
                    inside = (dx1 * dx1 + dy * dy <= 4f) || (dx2 * dx2 + dy * dy <= 4f);
                }
                else
                {
                    inside = (x + y) >= 6 && (x - y) <= 4;
                }
                t.SetPixel(x, y, inside ? c : new Color(0, 0, 0, 0));
            }
            t.Apply();
            t.filterMode = FilterMode.Point;
            return t;
        }

        private void OnGUI()
        {
            EnsureTextures();
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;

            float totalWidth = HeartsPerRow * (HeartSize + 2);
            float startX = (Screen.width - totalWidth) / 2f;
            float y = Screen.height - 64 - HotbarUI.SlotSize - 18;

            float hp = ctx.Health.Current;
            for (int i = 0; i < HeartsPerRow; i++)
            {
                var rect = new Rect(startX + i * (HeartSize + 2), y, HeartSize, HeartSize);
                Texture2D tex;
                if (hp >= (i + 1) * 2) tex = _heartFull;
                else if (hp >= i * 2 + 1) tex = _heartHalf;
                else tex = _heartEmpty;
                GUI.DrawTexture(rect, tex);
            }
        }
    }
}
