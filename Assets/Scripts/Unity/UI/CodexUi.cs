using System.Collections.Generic;
using MyWorld.Core.Codex;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 图鉴页签（m12 W2）——静态绘制类，由 <see cref="HelpMenuUi"/> 的「图鉴」页调用
    /// <see cref="DrawTab"/>。三栏（生物 / 矿石·植物）卡牌网格：已解锁 = 原色卡 + 名字，
    /// 未解锁 = 深灰块 + 「??？」（灰度化剪影留待美术细则，v1 深灰块语义等价）。
    /// 卡牌贴图从 <c>StreamingAssets/ui/codex/</c> 读；生物栏暂无卡牌美术——
    /// 深棕底块 + 名字占位（卡牌正式美术出后替换，取舍注释）。
    /// </summary>
    public static class CodexUi
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        private static Texture2D LoadCard(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Cache.TryGetValue(name, out var cached)) return cached;
            string path = System.IO.Path.Combine(
                Application.streamingAssetsPath, "ui", "codex", name + ".png");
            Texture2D tex = null;
            if (System.IO.File.Exists(path))
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(System.IO.File.ReadAllBytes(path));
                tex.filterMode = FilterMode.Point;
                tex.Apply();
            }

            Cache[name] = tex;
            return tex;
        }

        /// <summary>图鉴页主体（HelpMenuUi 的 bg 区域）。条目名自带，不需要注册表。</summary>
        public static void DrawTab(Rect bg, CodexSystem codex)
        {
            var white = ItemSlotDrawer.WhiteStyle();
            if (codex == null)
            {
                GUI.Label(new Rect(bg.x + 24, bg.y + 90, bg.width - 48, 30), "图鉴系统未加载", white);
                return;
            }

            GUI.Label(new Rect(bg.x + 24, bg.y + 78, 500, 26),
                $"图鉴（{codex.UnlockedCount}/{codex.TotalCount}）——第一次遇到 / 挖到就会点亮", white);

            float x0 = bg.x + 28;
            float y0 = bg.y + 116;
            var title = new GUIStyle(white) { fontSize = 15 };
            var nameStyle = new GUIStyle(white) { fontSize = 12, alignment = TextAnchor.MiddleCenter };

            // ── 栏 1：生物（6×3 网格，深棕底块占位卡） ──
            GUI.Label(new Rect(x0, y0, 200, 24), "生物", title);
            var mobs = CodexSystem.MobEntries;
            for (int i = 0; i < mobs.Length; i++)
            {
                int col = i % 6;
                int row = i / 6;
                var rect = new Rect(x0 + col * 74, y0 + 30 + row * 88, 64, 78);
                bool unlocked = codex.IsMobUnlocked(mobs[i].Kind);
                DrawMobCell(rect, unlocked ? mobs[i].Name : "？？？", unlocked, nameStyle);
            }

            // ── 栏 2：矿石（金/铁/合金/机元 + 钻石物品卡） ──
            float x1 = x0 + 6 * 74 + 24;
            GUI.Label(new Rect(x1, y0, 200, 24), "矿石", title);
            for (int i = 0; i < CodexSystem.BlockEntries.Length && i < 4; i++)
            {
                var e = CodexSystem.BlockEntries[i];
                var rect = new Rect(x1, y0 + 30 + i * 88, 64, 78);
                DrawCardCell(rect, e.Card, codex.IsBlockUnlocked(e.BlockId), e.Name, nameStyle);
            }

            DrawCardCell(new Rect(x1, y0 + 30 + 4 * 88, 64, 78),
                CodexSystem.DiamondCard, codex.IsDiamondUnlocked, "钻石", nameStyle);

            // ── 栏 3：植物（向日葵/蕨/樱花树） ──
            float x2 = x1 + 96;
            GUI.Label(new Rect(x2, y0, 200, 24), "植物", title);
            for (int i = 4; i < CodexSystem.BlockEntries.Length; i++)
            {
                var e = CodexSystem.BlockEntries[i];
                var rect = new Rect(x2, y0 + 30 + (i - 4) * 88, 64, 78);
                DrawCardCell(rect, e.Card, codex.IsBlockUnlocked(e.BlockId), e.Name, nameStyle);
            }
        }

        private static void DrawCardCell(Rect rect, string card, bool unlocked, string name, GUIStyle nameStyle)
        {
            var tex = LoadCard(card);
            var icon = new Rect(rect.x, rect.y, 64, 64);
            if (tex != null)
            {
                var old = GUI.color;
                GUI.color = unlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.9f);
                GUI.DrawTexture(icon, tex, ScaleMode.ScaleToFit);
                GUI.color = old;
            }
            else
            {
                DrawFlat(icon, unlocked ? new Color(0.45f, 0.4f, 0.3f) : new Color(0.18f, 0.18f, 0.2f));
            }

            GUI.Label(new Rect(rect.x - 10, rect.y + 66, rect.width + 20, 20),
                unlocked ? name : "？？？", nameStyle);
        }

        private static void DrawMobCell(Rect rect, string name, bool unlocked, GUIStyle nameStyle)
        {
            // 生物卡牌美术未出：深棕底块 + 名字占位（正式卡出后在 LoadCard 走 card-mob-*）
            DrawFlat(new Rect(rect.x, rect.y, 64, 64),
                unlocked ? new Color(0.42f, 0.33f, 0.24f) : new Color(0.16f, 0.16f, 0.19f));
            GUI.Label(new Rect(rect.x - 10, rect.y + 66, rect.width + 20, 20), name, nameStyle);
        }

        private static void DrawFlat(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = old;
        }
    }
}
