using MyWorld.Core.Entities;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 屏幕底部绿色经验条，显示当前经验 + 等级。
    /// 简化：IMGUI 画两条矩形；不画数字（数字位置留给 hotbar）。
    /// </summary>
    public sealed class ExperienceBarUi : MonoBehaviour
    {
        public int BarWidth = 360;
        public int BarHeight = 8;
        public int BottomMargin = 12;

        private void OnGUI()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            var xp = ctx.Experience;

            int x = (Screen.width - BarWidth) / 2;
            int y = Screen.height - BottomMargin - BarHeight;
            // 背景
            GUI.Box(new Rect(x - 1, y - 1, BarWidth + 2, BarHeight + 2), GUIContent.none);
            // 绿色填充
            int fill = Mathf.RoundToInt(BarWidth * xp.Fraction);
            var fillStyle = new GUIStyle(GUI.skin.box);
            fillStyle.normal.background = MakeColorTexture(new Color(0.3f, 0.85f, 0.3f));
            GUI.Box(new Rect(x, y, fill, BarHeight), GUIContent.none, fillStyle);

            // 等级标签
            var labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            GUI.Label(new Rect(x, y - 14, BarWidth, 14), $"Lv {xp.Level}", labelStyle);
        }

        private static Texture2D _tex;
        private static Texture2D MakeColorTexture(Color c)
        {
            if (_tex != null && _tex.name == c.ToString()) return _tex;
            _tex = new Texture2D(1, 1) { name = c.ToString() };
            _tex.SetPixel(0, 0, c);
            _tex.Apply();
            return _tex;
        }
    }
}