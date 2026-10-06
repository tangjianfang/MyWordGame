using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;

namespace MyWorld.Core.Farming
{
    /// <summary>剪羊毛（评审 08 F2）：任务链 ch2_01 文案已向孩子承诺「或剪羊」——
    /// 本系统兑现内容契约。牧场闭环（喂 → 繁 → 剪）比杀羊取毛友好得多，
    /// 正合用户画像里反复出现的动物向玩法。
    /// <para>冷却语义照 <see cref="BreedingSystem"/> 的绝对时间阈值：120 秒内已剪的羊
    /// 再剪 no-op。冷却<b>不进存档</b>——重进世界羊毛"长回来"（MC 同款语义，取舍自评审 08）。</para>
    /// <para>全部纯函数 + 注入的冷却表（entityId → 剪毛时刻），双链可测、零 UnityEngine。</para>
    /// </summary>
    public static class ShearSystem
    {
        /// <summary>剪毛冷却（秒）：剪过之后羊毛要"长"这么久。</summary>
        public const float CooldownSeconds = 120f;

        /// <summary>羊毛物品 numericId。与 <c>items/wool.json</c> 手动保持一致
        ///（同 <c>BlockIds</c> 与 blocks JSON 的对应模式）。</summary>
        public const int WoolItemId = 1009;

        /// <summary>确定性羊毛产量掷骰：1-2 个（<see cref="BlockDrops.RollCount"/>
        /// 整数哈希——项目铁律：不持随机数对象）。hash 由调用方用 entityId+盐派生。</summary>
        public static int RollWoolCount(int hash) => BlockDrops.RollCount(hash, min: 1, max: 2);

        /// <summary>尝试剪 <paramref name="mob"/>：是活羊且不在冷却内 → true，
        /// 并把「本次剪毛时刻 + 冷却」记入 <paramref name="shearedUntil"/>。
        /// 非羊 / 死亡 / 冷却内一律 false（调用方 no-op——不扣剪刀耐久不消费材料）。</summary>
        public static bool TryShear(Mob mob, double now, Dictionary<long, double> shearedUntil)
        {
            if (mob == null || !mob.IsAlive || mob.Kind != MobKind.Sheep) return false;
            if (shearedUntil != null
                && shearedUntil.TryGetValue(mob.EntityId, out double until)
                && now < until)
            {
                return false; // 冷却内：羊毛还没长回来
            }

            shearedUntil?.Remove(mob.EntityId);
            shearedUntil?.Add(mob.EntityId, now + CooldownSeconds);
            return true;
        }
    }
}
