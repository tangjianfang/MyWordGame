using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Items;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// m6：物品格公共绘制器。背包/工作台/口袋/熔炉/交易原先只有黑字 Label
    /// （叠深色背景不可读），本组件统一「图标 + 右下数量角标 + 选中高亮」，
    /// 图标管线与 HotbarUI 同源（streamingAssetsPath 加载 + Point + 字典缓存）。
    /// </summary>
    public static class ItemSlotDrawer
    {
        /// <summary>物品贴图基准边长：贴图是 16×16，图标缩放必须是它的整数倍
        /// （非整数倍缩放会让像素宽窄不均出锯齿，IconScalingTests 同一条契约）。</summary>
        private const int IconBaseSize = 16;

        // 数量角标几何（右下角）：距右缘 X / 距下缘 Y / 宽 / 高
        private const float CountBadgeOffsetX = 30f;
        private const float CountBadgeOffsetY = 18f;
        private const float CountBadgeWidth = 28f;
        private const float CountBadgeHeight = 16f;

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static Texture2D _missing;
        private static GUIStyle _countStyle;
        private static GUIStyle _whiteStyle;

        /// <summary>
        /// 取物品图标（带缓存），缺贴图时返回品红占位。跨 UI 共享一份静态缓存：
        /// 五个界面同屏出现时同一物品只加载一次 PNG。绝不返回 null——
        /// 调用方直接 DrawTexture，占位让「贴图没配」肉眼可辨而不是空指针。
        /// </summary>
        public static Texture2D GetTextureOrPlaceholder(ItemDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.Texture)) return Missing();
            if (Cache.TryGetValue(def.Texture, out var t)) return t;
            // 与 HotbarUI.GetItemTexture 同一条候选路径：
            // 优先 StreamingAssets/items/textures（运行时 Player 数据），退到 Assets/Art/Items（编辑器
            // fallback），再退 StreamingAssets/blocks/textures（评审 08 F0：bed/chest/glass/sand/
            // torch/wooden_door 六件共用方块贴图，修品红占位）
            string[] candidates =
            {
                Path.Combine(Application.streamingAssetsPath, "items", "textures", def.Texture + ".png"),
                Path.Combine(Application.dataPath, "Art", "Items", def.Texture + ".png"),
                Path.Combine(Application.streamingAssetsPath, "blocks", "textures", def.Texture + ".png"),
            };
            string resolved = null;
            foreach (var c in candidates) if (File.Exists(c)) { resolved = c; break; }
            t = resolved != null ? HotbarUI.LoadItemTexturePng(resolved) : Missing();
            Cache[def.Texture] = t;
            return t;
        }

        /// <summary>
        /// 纯函数判定：这一格是否需要画物品图标，需要则同时解析出贴图。
        /// 「空槽」与「贴图缺失」是两种语义（fix2 修的回归：旧版把空槽的 def==null
        /// 喂给占位逻辑，导致空格子整格画品红）——空槽返回 false 什么都不画，
        /// 品红占位只留给「槽里有物品但贴图路径解析失败」。
        /// </summary>
        public static bool TryResolveIcon(ItemStack stack, ItemDatabase items, out Texture2D tex)
        {
            tex = null;
            if (stack.IsEmpty) return false;
            ItemDefinition def = null;
            if (items != null) items.TryGetByNumericId(stack.ItemId, out def);
            tex = GetTextureOrPlaceholder(def);
            return true;
        }

        /// <summary>
        /// 画一个物品格：图标（居中，16 整数倍缩放）+ 右下数量角标 + 选中描边。
        /// 空槽只画底框/角标/描边、不画图标（见 <see cref="TryResolveIcon"/>）。
        /// 所有 GUI 调用只允许在 Repaint 事件里执行——EditMode 测试在 OnGUI 之外
        /// 调用时 <c>Event.current</c> 为 null，必须静默返回。
        /// </summary>
        public static void Draw(Rect slot, ItemStack stack, ItemDatabase items, bool selected)
        {
            var ev = Event.current;
            if (ev == null || ev.type != EventType.Repaint) return;

            if (TryResolveIcon(stack, items, out var tex))
            {
                // 图标边长取槽内最大的 16 整数倍并居中（HotbarUI IconScalingTests 同一条契约：
                // 16×16 贴图非整数倍缩放会让像素宽窄不均出锯齿）
                int icon = Mathf.Max(IconBaseSize, (int)slot.width / IconBaseSize * IconBaseSize);
                var iconRect = new Rect(
                    slot.x + (slot.width - icon) * 0.5f,
                    slot.y + (slot.height - icon) * 0.5f,
                    icon, icon);
                GUI.DrawTexture(iconRect, tex);
            }

            // 数量角标右下：只有叠了 1 个以上才显示（HotbarUI 同款语义）
            if (!stack.IsEmpty && stack.Count > 1)
            {
                var label = new Rect(
                    slot.xMax - CountBadgeOffsetX,
                    slot.yMax - CountBadgeOffsetY,
                    CountBadgeWidth, CountBadgeHeight);
                GUI.Label(label, stack.Count.ToString(), CountStyle());
            }

            // 选中高亮：默认皮肤 Box 自带描边，叠在图标上层
            if (selected) GUI.Box(slot, GUIContent.none);
        }

        /// <summary>
        /// 白字缓存样式（黑底上直接可读的粗体白字）。数量角标与各 UI 的说明文本
        /// 共用同一套「黑底白字」语义——旧的默认 Label 是黑字，叠深色 Box 不可读。
        /// 必须在 OnGUI 内首次调用（GUI.skin 只在 GUI 上下文里可用）。
        /// </summary>
        public static GUIStyle WhiteStyle()
        {
            if (_whiteStyle == null)
            {
                _whiteStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                _whiteStyle.normal.textColor = Color.white;
            }
            return _whiteStyle;
        }

        private static GUIStyle CountStyle()
        {
            if (_countStyle == null)
            {
                // 独立实例（不共享 WhiteStyle 引用）：40px 小槽用更小的字号
                _countStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                };
                _countStyle.normal.textColor = Color.white;
            }
            return _countStyle;
        }

        /// <summary>缺贴图占位：1×1 品红，肉眼一眼可辨「贴图没配」，绝不与真实贴图混淆。</summary>
        private static Texture2D Missing()
        {
            if (_missing == null)
            {
                _missing = new Texture2D(1, 1);
                _missing.SetPixel(0, 0, new Color(1f, 0f, 1f, 1f));
                _missing.Apply();
            }
            return _missing;
        }
    }
}
