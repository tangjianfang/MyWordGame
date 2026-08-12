using MyWorld.Core.Items;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 屏幕底部 9 槽 hotbar。IMGUI 简单版：每槽 64×64，左侧贴图，右侧数量。
    /// 选中槽画白色边框。
    /// </summary>
    public sealed class HotbarUI : MonoBehaviour
    {
        public const int SlotSize = 64;
        public const int Padding = 4;

        private Texture2D _slotBg;
        private Texture2D _selectBorder;
        private Texture2D _missingTex;
        private readonly System.Collections.Generic.Dictionary<string, Texture2D> _texCache =
            new System.Collections.Generic.Dictionary<string, Texture2D>();

        private void EnsureTextures()
        {
            if (_slotBg != null) return;
            _slotBg = new Texture2D(1, 1);
            _slotBg.SetPixel(0, 0, new Color(0, 0, 0, 0.6f));
            _slotBg.Apply();
            _selectBorder = new Texture2D(1, 1);
            _selectBorder.SetPixel(0, 0, Color.white);
            _selectBorder.Apply();
            _missingTex = new Texture2D(1, 1);
            _missingTex.SetPixel(0, 0, new Color(0.6f, 0.2f, 0.9f, 1f));
            _missingTex.Apply();
        }

        private Texture2D GetItemTexture(ItemDefinition def)
        {
            if (def == null) return _missingTex;
            if (_texCache.TryGetValue(def.Texture, out var t)) return t;
            // 优先查 StreamingAssets/items/textures（运行时 Player 数据），
            // 退到 Assets/Art/Items（编辑器/打包时 fallback）。
            string[] candidates = {
                System.IO.Path.Combine(Application.streamingAssetsPath, "items", "textures", def.Texture + ".png"),
                System.IO.Path.Combine(Application.dataPath, "Art", "Items", def.Texture + ".png"),
            };
            string resolved = null;
            foreach (var c in candidates) if (System.IO.File.Exists(c)) { resolved = c; break; }
            if (resolved != null)
            {
                var bytes = System.IO.File.ReadAllBytes(resolved);
                t = new Texture2D(2, 2);
                t.LoadImage(bytes);
            }
            else
            {
                t = _missingTex;
            }
            _texCache[def.Texture] = t;
            return t;
        }

        private void OnGUI()
        {
            EnsureTextures();
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;

            float totalWidth = (SlotSize + Padding) * 9;
            float startX = (Screen.width - totalWidth) / 2f;
            float y = Screen.height - SlotSize - 16;

            for (int i = 0; i < 9; i++)
            {
                var rect = new Rect(startX + i * (SlotSize + Padding), y, SlotSize, SlotSize);
                GUI.DrawTexture(rect, _slotBg);

                var stack = ctx.Inventory.GetSlot(i);
                if (!stack.IsEmpty && ctx.Items != null && ctx.Items.TryGetByNumericId(stack.ItemId, out var def))
                {
                    var texRect = new Rect(rect.x + 4, rect.y + 4, SlotSize - 8, SlotSize - 8);
                    GUI.DrawTexture(texRect, GetItemTexture(def));
                    if (stack.Count > 1)
                    {
                        GUI.Label(new Rect(rect.x + SlotSize - 18, rect.y + SlotSize - 18, 16, 16),
                            stack.Count.ToString());
                    }
                }

                if (i == ctx.Inventory.SelectedHotbarIndex)
                {
                    GUI.DrawTexture(rect, _selectBorder);
                }
            }
        }

        private void Update()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    ctx.Inventory.SelectedHotbarIndex = i;
                }
            }
            // 滚轮切换
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f) ctx.Inventory.SelectedHotbarIndex = (ctx.Inventory.SelectedHotbarIndex + 8) % 9;
            else if (scroll < 0f) ctx.Inventory.SelectedHotbarIndex = (ctx.Inventory.SelectedHotbarIndex + 1) % 9;
        }
    }
}
