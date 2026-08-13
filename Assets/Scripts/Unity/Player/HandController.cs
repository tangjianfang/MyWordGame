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
        public float SwingDuration = 0.25f;
        public float SwingDownAngle = -45f;

        // ─── 公开 API（A5：HandController jump 姿势） ─────────────────────────────
        /// <summary>当前是否处于挥动状态——绑定玩家时由 airborne 信号驱动：
        /// 未绑定 / 在空中 = false（持物但不挥），着地 = true（允许挥动）。</summary>
        public bool IsSwinging { get; private set; } = false;

        private PlayerController _player;

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

        /// <summary>绑定到玩家身上——后续每帧 Update 会读 <see cref="PlayerController.IsGrounded"/>
        /// 决定是否挥动。不绑定则保持原行为（每帧只推进挥动计时器）。</summary>
        public void AttachTo(PlayerController player)
        {
            _player = player;
        }

        /// <summary>测试入口——同 Update 逻辑但可在 EditMode 下显式触发。
        /// 公开出来是因为 EditMode 不跑 MonoBehaviour.Update，必须手动步进。</summary>
        public void TickForTest()
        {
            TickInternal();
        }

        private void Update()
        {
            TickInternal();
        }

        private void TickInternal()
        {
            // 玩家在空中：停止挥动、复位姿势到 idle，避免「空中还在走路挥剑」的违和感
            if (_player != null && !_player.IsGrounded)
            {
                IsSwinging = false;
                _swinging = false;
                transform.localRotation = Quaternion.identity;
                return;
            }

            IsSwinging = true;
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
