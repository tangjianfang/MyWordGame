using MyWorld.Core.Items;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 屏幕右下角手部方块 + 挥动动画。
    /// 简单版：每按一次左键旋转 -30° → 0°，0.3 秒。
    /// </summary>
    public sealed class HandController : MonoBehaviour
    {
        public float SwingDuration = 0.3f;
        public float SwingDownAngle = -30f;

        private float _swingTime;
        private bool _swinging;
        private Texture2D _missingTex;
        private readonly System.Collections.Generic.Dictionary<string, Texture2D> _texCache =
            new System.Collections.Generic.Dictionary<string, Texture2D>();

        private void EnsureTex()
        {
            if (_missingTex != null) return;
            _missingTex = new Texture2D(1, 1);
            _missingTex.SetPixel(0, 0, new Color(0.6f, 0.4f, 0.3f));
            _missingTex.Apply();
        }

        public void TriggerSwing()
        {
            _swinging = true;
            _swingTime = 0;
        }

        private void Update()
        {
            if (_swinging)
            {
                _swingTime += Time.deltaTime;
                if (_swingTime >= SwingDuration) _swinging = false;
            }
        }

        private Texture2D GetTex(ItemDefinition def)
        {
            if (def == null) return _missingTex;
            if (_texCache.TryGetValue(def.Texture, out var t)) return t;
            string runtime = System.IO.Path.Combine(Application.streamingAssetsPath, "items", "textures", def.Texture + ".png");
            if (System.IO.File.Exists(runtime))
            {
                var bytes = System.IO.File.ReadAllBytes(runtime);
                t = new Texture2D(2, 2);
                t.LoadImage(bytes);
            }
            else t = _missingTex;
            _texCache[def.Texture] = t;
            return t;
        }

        private void OnGUI()
        {
            EnsureTex();
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            var def = ctx.GetSelectedDefinition();
            int size = 80;
            float x = Screen.width - size - 30;
            float y = Screen.height - size - 30;
            var rect = new Rect(x, y, size, size);

            float rot = 0f;
            if (_swinging)
            {
                float t = _swingTime / SwingDuration;
                rot = Mathf.Sin(t * Mathf.PI) * SwingDownAngle;
            }
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(rot, new Vector2(rect.x + rect.width / 2, rect.y + rect.height));
            GUI.DrawTexture(rect, GetTex(def));
            GUI.matrix = matrix;
        }
    }
}
