using System.Collections.Generic;
using MyWorld.Core.Achievements;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 成就页签（m12 W1）——静态绘制类，由 <see cref="HelpMenuUi"/> 的「成就」页调用
    /// <see cref="DrawTab"/>。徽章网格亮/灰 + 进度（n/16），贴图从
    /// <c>StreamingAssets/ui/codex/</c> 读（m6 B3 教训：standalone 读不到 Assets/Art），
    /// 缺图回落深灰块不炸。达成瞬间不在这里弹——<see cref="Bind"/> 订阅
    /// <see cref="AchievementSystem.Unlocked"/> 转发 <see cref="FloatTextUi"/> 飘字
    ///（fx-badge-popup 弹窗底板美术未出，出后在此扩展）。
    /// </summary>
    public static class AchievementUi
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static AchievementSystem _bound;

        // ── 评审 04 R-9 / 02#4：样式缓存——旧实现每格 OnGUI 里 new GUIStyle（16 成就
        //    开着时每帧 ~17 个分配，违反 m5「热路径不 new」纪律），按亮/灰两态各缓存一份
        private static GUIStyle _nameStyleUnlocked;
        private static GUIStyle _nameStyleLocked;
        private static GUIStyle _footerStyle;

        private static GUIStyle NameStyle(bool unlocked, GUIStyle white)
        {
            if (unlocked)
            {
                if (_nameStyleUnlocked == null)
                {
                    _nameStyleUnlocked = new GUIStyle(white)
                    {
                        fontSize = 12,
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true,
                        normal = { textColor = Color.white },
                    };
                }
                return _nameStyleUnlocked;
            }

            if (_nameStyleLocked == null)
            {
                _nameStyleLocked = new GUIStyle(white)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f) },
                };
            }
            return _nameStyleLocked;
        }

        /// <summary>把系统接到 UI（解锁飘字）。换世界重建系统时重复调安全（先解旧）。</summary>
        public static void Bind(AchievementSystem system)
        {
            if (_bound != null)
            {
                _bound.Unlocked -= OnUnlocked;
            }

            _bound = system;
            if (_bound != null)
            {
                _bound.Unlocked += OnUnlocked;
            }
        }

        private static void OnUnlocked(Achievement a)
        {
            FloatTextUi.ShowText("成就达成：" + a.Name);
        }

        private static Texture2D LoadIcon(string name)
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

        /// <summary>成就页主体（HelpMenuUi 的 bg 区域，720×660）。</summary>
        public static void DrawTab(Rect bg, AchievementSystem system)
        {
            var white = ItemSlotDrawer.WhiteStyle();
            if (system == null)
            {
                GUI.Label(new Rect(bg.x + 24, bg.y + 90, bg.width - 48, 30),
                    "成就系统未加载（achievements.json 缺失）", white);
                return;
            }

            var all = system.All;
            GUI.Label(new Rect(bg.x + 24, bg.y + 78, 400, 26),
                $"成就（{system.UnlockedCount}/{all.Count}）——达成时右上角飘字提示", white);

            // 8×2 徽章网格（32×32 图 + 名字），未达成 = 深灰半罩 + 「?」
            const float cell = 76f;
            for (int i = 0; i < all.Count; i++)
            {
                int col = i % 8;
                int row = i / 8;
                var cellRect = new Rect(bg.x + 28 + col * (cell + 12), bg.y + 116 + row * 118, cell, 96);
                var a = all[i];
                bool unlocked = system.IsUnlocked(a);

                var tex = LoadIcon(a.Icon);
                var iconRect = new Rect(cellRect.x + (cell - 48f) / 2f, cellRect.y, 48f, 48f);
                if (tex != null)
                {
                    var old = GUI.color;
                    GUI.color = unlocked ? Color.white : new Color(0.35f, 0.35f, 0.35f, 0.9f);
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit);
                    GUI.color = old;
                }
                else
                {
                    var old = GUI.color;
                    GUI.color = new Color(0.25f, 0.25f, 0.28f);
                    GUI.DrawTexture(iconRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                    GUI.color = old;
                }

                var labelStyle = NameStyle(unlocked, white);
                GUI.Label(
                    new Rect(cellRect.x - 8, cellRect.y + 52, cell + 16, 40),
                    unlocked ? a.Name : "？", labelStyle);
            }

            // 底部一行进度说明
            int y = 116 + ((all.Count + 7) / 8) * 118;
            if (_footerStyle == null) _footerStyle = new GUIStyle(white) { fontSize = 13 };
            GUI.Label(new Rect(bg.x + 24, bg.y + y, bg.width - 48, 26),
                "在游戏里做对应的事就会点亮徽章（挖矿 / 合成 / 战斗 / 度夜…）",
                _footerStyle);
        }
    }
}
