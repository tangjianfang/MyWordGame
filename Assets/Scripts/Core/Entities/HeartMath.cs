using System;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 血条心形布局的纯数学（m11 W2-3）。每颗心 = 2 HP（<see cref="Health"/> 的既有约定），
    /// 有效上限 = <c>Health.Max + MaxHealthBonus</c>（Unity 侧 <c>PlayerContext.EffectiveMaxHealth</c>），
    /// 上限涨过 20（10 心）后 UI 按行排列——每行最多 <see cref="HeartsPerRow"/> 颗，多出的行往**上**叠
    /// （行 0 最靠下、紧贴食物条，与 Minecraft 同款观感）。
    /// <para>
    /// 放 Core 层（而非 HealthBarUI 里做私有静态）是为了双链测试：dotnet 链只编译 Core，
    /// 血心数量 / 半心 / 两行排布这些契约照 <see cref="GearBonusMath"/> 的先例落成纯函数，
    /// UI 只负责把结果画出来，永不自己算。
    /// </para>
    /// </summary>
    public static class HeartMath
    {
        /// <summary>每行心数上限：10 心以内单行，超过按行向上叠（Minecraft 同款）。</summary>
        public const int HeartsPerRow = 10;

        /// <summary>每颗心代表的血量点数。</summary>
        public const float HealthPerHeart = 2f;

        /// <summary>一颗心的填充状态：满 / 半（余 1 点）/ 空。</summary>
        public enum Fill
        {
            Empty = 0,
            Half = 1,
            Full = 2,
        }

        /// <summary>
        /// 有效上限对应的心数（向上取整——上限 21 = 10.5 心画 11 颗，最后一颗永远只能半满，
        /// 让玩家看见「多出来的 1 点血」真实存在，与 MC 一致）。上限 ≤0 返回 0（一颗都不画）。
        /// </summary>
        public static int TotalHearts(float effectiveMaxHealth)
        {
            if (effectiveMaxHealth <= 0f) return 0;
            return (int)System.Math.Ceiling(effectiveMaxHealth / HealthPerHeart);
        }

        /// <summary>
        /// 第 <paramref name="heartIndex"/> 颗心（0 起，自左向右、自下而上）在当前血量下的状态：
        /// 血量 ≥ (i+1)×2 → 满；≥ i×2+1 → 半；否则空。
        /// </summary>
        public static Fill FillAt(int heartIndex, float currentHealth)
        {
            if (currentHealth >= (heartIndex + 1) * HealthPerHeart) return Fill.Full;
            if (currentHealth >= heartIndex * HealthPerHeart + 1f) return Fill.Half;
            return Fill.Empty;
        }

        /// <summary>需要几行才能排下 <paramref name="totalHearts"/> 颗心（每行 <see cref="HeartsPerRow"/> 颗）。</summary>
        public static int RowCount(int totalHearts)
        {
            if (totalHearts <= 0) return 0;
            return (totalHearts + HeartsPerRow - 1) / HeartsPerRow;
        }

        /// <summary>
        /// 自下往上数第 <paramref name="rowFromBottom"/> 行（0 = 最底下那行）的心数：
        /// 底下各行排满 <see cref="HeartsPerRow"/> 颗，最上面一行摆余数（1..10）。
        /// 行号越界 / 总数为 0 返回 0。
        /// </summary>
        public static int HeartsInRow(int rowFromBottom, int totalHearts)
        {
            int remaining = totalHearts - rowFromBottom * HeartsPerRow;
            if (remaining <= 0) return 0;
            return System.Math.Min(remaining, HeartsPerRow);
        }

        /// <summary>第 <paramref name="heartIndex"/> 颗心的排布坐标（行 = 自下往上，列 = 自左向右）。</summary>
        public static (int Row, int Column) PositionOf(int heartIndex)
        {
            if (heartIndex < 0) heartIndex = 0;
            return (heartIndex / HeartsPerRow, heartIndex % HeartsPerRow);
        }
    }
}
