using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 屏幕底部的红心血条。每颗心 = 2HP。
    /// <para>
    /// m11 W2-3：心数跟随<b>有效上限</b>（<see cref="PlayerContext.EffectiveMaxHealth"/> =
    /// Health.Max + 手持装备 MaxHealthBonus）——机元装备 +2/件此前生效但不多画心，
    /// 现在上限涨过 20 就多画：≤10 心单行；&gt;10 心按 <see cref="HeartMath.HeartsPerRow"/>/行
    /// 排列，多出的行向<b>上</b>叠（行 0 紧贴食物条，与 Minecraft 同款）。半心 = 余 1 点血。
    /// 数量 / 半心 / 排布全部走 Core 纯函数 <see cref="HeartMath"/>（dotnet 双链可测），
    /// 本组件只负责把结果画出来。
    /// </para>
    /// 心形贴图复用已入库的 <c>ui/heart-{full,half,empty}.png</c>（24×24，m6 美术产物从
    /// Assets/Art/UI 挪进 StreamingAssets/ui——standalone build 读不到 Assets 目录，m6 B3 的教训）；
    /// 贴图缺失时退回程序生成心形（m5 起的旧视觉，EditMode 无盘依赖也不至于空屏）。
    /// </summary>
    public sealed class HealthBarUI : MonoBehaviour
    {
        /// <summary>单颗心绘制边长 = 心形贴图原生 24px（整数倍缩放，不糊像素）。
        /// FoodBarUI 用它推算自己贴着心行的位置——改这里两条底栏一起挪，永不重叠。</summary>
        public const int HeartSize = 24;

        /// <summary>每行心数。真源在 <see cref="HeartMath.HeartsPerRow"/>，这里别名给布局常量。</summary>
        public const int HeartsPerRow = HeartMath.HeartsPerRow;

        /// <summary>相邻心的水平间距。</summary>
        private const int HeartSpacing = 2;

        private Texture2D _heartFull;
        private Texture2D _heartHalf;
        private Texture2D _heartEmpty;

        // TickForTest 的快照（EditMode 断言用；OnGUI 每帧同样刷新）
        /// <summary>最近一次读到的总心数（随有效上限走）。</summary>
        public int CurrentHeartCount { get; private set; }
        /// <summary>最近一次读到的行数（&gt;10 心时为 2+）。</summary>
        public int CurrentRowCount { get; private set; }
        /// <summary>最近一次读到的每颗心状态（下标即心序，自左向右、自下而上）。</summary>
        public HeartMath.Fill[] CurrentFills { get; private set; } = new HeartMath.Fill[0];

        private void EnsureTextures()
        {
            if (_heartFull != null) return;
            _heartFull = LoadHeartOrFallback("heart-full.png", new Color(0.85f, 0.1f, 0.1f), HeartMath.Fill.Full);
            _heartHalf = LoadHeartOrFallback("heart-half.png", new Color(0.85f, 0.1f, 0.1f), HeartMath.Fill.Half);
            _heartEmpty = LoadHeartOrFallback("heart-empty.png", new Color(0.25f, 0.25f, 0.25f), HeartMath.Fill.Empty);
        }

        /// <summary>
        /// 从 StreamingAssets/ui 加载心形贴图（与 <see cref="HotbarUI.LoadUiTextureOrFallback"/>
        /// 同一条路径约定），文件缺失时程序生成一颗像素心兜底（M5 旧视觉）。
        /// 半心的程序兜底 = 左半红 + 右半灰（空心形打底），比旧版「整颗橙心」更贴真素材语义。
        /// </summary>
        private static Texture2D LoadHeartOrFallback(string fileName, Color heartColor, HeartMath.Fill fill)
        {
            string full = Path.Combine(Application.streamingAssetsPath, "ui", fileName);
            if (File.Exists(full))
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(full));
                tex.filterMode = FilterMode.Point;
                return tex;
            }
            return fill == HeartMath.Fill.Half
                ? MakeHeart(heartColor, halfTint: new Color(0.25f, 0.25f, 0.25f))
                : MakeHeart(heartColor, halfTint: null);
        }

        /// <summary>程序生成的像素心兜底（9×9，M5 旧视觉）。halfTint 非空时右半用它染色 = 半心。</summary>
        private static Texture2D MakeHeart(Color c, Color? halfTint)
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
                Color color = halfTint.HasValue && x >= s / 2 ? halfTint.Value : c;
                t.SetPixel(x, y, inside ? color : new Color(0, 0, 0, 0));
            }
            t.Apply();
            t.filterMode = FilterMode.Point;
            return t;
        }

        /// <summary>
        /// 从 PlayerContext 读「有效上限 + 当前血量」并刷新快照。OnGUI 每帧调；
        /// EditMode 下 <see cref="TickForTest"/> 手动驱动同一入口（照 FoodBarUI 的做法）。
        /// </summary>
        private void Refresh()
        {
            var ctx = PlayerContext.Instance;
            if (ctx == null) return;

            float effectiveMax = ctx.EffectiveMaxHealth;
            int totalHearts = HeartMath.TotalHearts(effectiveMax);
            var fills = new HeartMath.Fill[totalHearts];
            for (int i = 0; i < totalHearts; i++)
            {
                fills[i] = HeartMath.FillAt(i, ctx.Health.Current);
            }

            CurrentHeartCount = totalHearts;
            CurrentRowCount = HeartMath.RowCount(totalHearts);
            CurrentFills = fills;
        }

        /// <summary>EditMode 测试入口：手动驱动一次刷新（OnGUI 事件循环在 EditMode 不跑）。</summary>
        public void TickForTest() => Refresh();

        private void OnGUI()
        {
            EnsureTextures();
            Refresh();
            if (CurrentHeartCount <= 0) return;

            float totalWidth = HeartsPerRow * (HeartSize + HeartSpacing);
            float startX = (Screen.width - totalWidth) / 2f;
            // 行 0 的基线：hotbar 上方（旧单行位置不动，多出的行从这里向上叠）
            float baseY = Screen.height - 64 - HotbarUI.SlotSize - 18;

            for (int i = 0; i < CurrentHeartCount; i++)
            {
                (int row, int col) = HeartMath.PositionOf(i);
                var rect = new Rect(
                    startX + col * (HeartSize + HeartSpacing),
                    baseY - row * (HeartSize + HeartSpacing),
                    HeartSize, HeartSize);
                Texture2D tex = CurrentFills[i] == HeartMath.Fill.Full
                    ? _heartFull
                    : CurrentFills[i] == HeartMath.Fill.Half ? _heartHalf : _heartEmpty;
                GUI.DrawTexture(rect, tex);
            }
        }
    }
}
