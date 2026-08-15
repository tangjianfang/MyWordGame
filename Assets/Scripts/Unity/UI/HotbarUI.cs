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

        /// <summary>物品图标绘制边长。物品贴图是 16×16，绘制边长必须是 16 的整数倍
        /// （这里定格 48 = 16×3），否则非整数倍缩放会让像素宽窄不均、边界出锯齿。</summary>
        public const int IconDrawSize = 48;

        private Texture2D _slotBg;
        private Texture2D _countBorder;
        private Texture2D _countBg;
        private Texture2D _missingTex;
        private readonly System.Collections.Generic.Dictionary<string, Texture2D> _texCache =
            new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>m5 C1：数量角标样式只构造一次缓存复用——原先在 OnGUI 里每槽每帧
        /// new GUIStyle，9 个槽连续分配是 IMGUI 侧稳定的 GC 来源。</summary>
        private GUIStyle _countStyle;

        private void EnsureTextures()
        {
            if (_slotBg != null) return;
            _slotBg = LoadTextureOrFallback("Assets/Art/UI/hotbar-slot.png",
                new Color(0, 0, 0, 0.6f));
            _countBorder = LoadTextureOrFallback("Assets/Art/UI/hotbar-slot-selected.png",
                Color.white);
            _countBg = new Texture2D(1, 1);
            _countBg.SetPixel(0, 0, new Color(0, 0, 0, 0.85f));
            _countBg.Apply();
            _missingTex = new Texture2D(1, 1);
            _missingTex.SetPixel(0, 0, new Color(0.6f, 0.2f, 0.9f, 1f));
            _missingTex.Apply();
        }

        /// <summary>
        /// 从项目内相对路径加载 PNG，文件不存在时返回 1×1 占位纹理（保持形状大小合理，Point 采样）。
        /// 应用运行时 <c>Application.dataPath</c> 指向 <c>Assets/</c>，所以用 <c>Path.Combine</c> 拼成绝对路径读盘。
        /// </summary>
        private static Texture2D LoadTextureOrFallback(string projectRelativePath, Color fallbackColor)
        {
            string full = System.IO.Path.Combine(Application.dataPath, "..", projectRelativePath);
            if (System.IO.File.Exists(full))
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(System.IO.File.ReadAllBytes(full));
                tex.filterMode = FilterMode.Point;
                return tex;
            }
            var fb = new Texture2D(1, 1);
            fb.SetPixel(0, 0, fallbackColor);
            fb.Apply();
            return fb;
        }

        /// <summary>
        /// 加载物品 PNG 并应用像素风采样设置。公开静态：HandController 等 UI 复用同一条
        /// 加载路径（<see cref="Player.HandController"/>），EditMode 测试也直接走这里断言
        /// filterMode 契约。必须在 LoadImage 之后设置 filterMode——LoadImage 会按 PNG
        /// 重建纹理，之前的采样设置会丢；默认双线性会把 16×16 像素边界糊出锯齿。
        /// </summary>
        public static Texture2D LoadItemTexturePng(string path)
        {
            var t = new Texture2D(2, 2);
            t.LoadImage(System.IO.File.ReadAllBytes(path));
            t.filterMode = FilterMode.Point;
            return t;
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
                t = LoadItemTexturePng(resolved);
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
                    // 图标在 64 槽内居中：48 图标 + 两侧各 8 空白，布局不变仅图标改为整数倍缩放
                    var texRect = new Rect(
                        rect.x + (SlotSize - IconDrawSize) / 2f,
                        rect.y + (SlotSize - IconDrawSize) / 2f,
                        IconDrawSize, IconDrawSize);
                    GUI.DrawTexture(texRect, GetItemTexture(def));
                    if (stack.Count > 1)
                    {
                        // 黑底白字：1×1 独立黑底（85% 不透明）+ 上方 16 号粗体白字。
                        var bgRect = new Rect(rect.x + SlotSize - 22, rect.y + SlotSize - 20, 20, 18);
                        GUI.DrawTexture(bgRect, _countBg);
                        if (_countStyle == null)
                        {
                            // 首帧构造一次（GUI.skin 只在 OnGUI 内可用），之后逐帧复用
                            _countStyle = new GUIStyle(GUI.skin.label)
                            {
                                fontSize = 16,
                                fontStyle = FontStyle.Bold,
                            };
                            _countStyle.normal.textColor = Color.white;
                        }
                        GUI.Label(new Rect(rect.x + SlotSize - 20, rect.y + SlotSize - 19, 18, 16),
                            stack.Count.ToString(), _countStyle);
                    }
                }

                if (i == ctx.Inventory.SelectedHotbarIndex)
                {
                    GUI.DrawTexture(rect, _countBorder);
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
