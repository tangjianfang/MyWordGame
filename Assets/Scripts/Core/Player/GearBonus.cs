using MyWorld.Core.Items;

namespace MyWorld.Core.Player
{
    /// <summary>
    /// 手持装备带来的三属性加成（m10 C1）。简化模型：**只算选中物品一件**——
    /// 手持即生效、切走即失效（spec §3 不做穿戴栏，属性挂在选中物品上）。
    /// Unity 侧由 <c>PlayerContext.RefreshGearBonuses</c> 每帧从选中物品重建本结构。
    /// </summary>
    public readonly struct GearBonuses
    {
        /// <summary>防御点数：受伤时伤害 - 本值（下限 1，见 <see cref="GearBonusMath.MitigateDamage"/>）。</summary>
        public readonly int Defense;

        /// <summary>移速加成（比例，0.05 = +5%）：行走目标速度乘 (1 + 本值)。</summary>
        public readonly float MoveSpeedBonus;

        /// <summary>生命上限加成（点数）：有效血上限 = 基础上限 + 本值。</summary>
        public readonly int MaxHealthBonus;

        public GearBonuses(int defense, float moveSpeedBonus, int maxHealthBonus)
        {
            // 负加成没有意义（装备不该让玩家变弱）：JSON 加载层已拦，这里再兜一层，
            // 保证任何路径构造出的实例三值都 >= 0
            Defense = defense < 0 ? 0 : defense;
            MoveSpeedBonus = moveSpeedBonus < 0f ? 0f : moveSpeedBonus;
            MaxHealthBonus = maxHealthBonus < 0 ? 0 : maxHealthBonus;
        }

        /// <summary>全零加成（空手 / 无 gearBonus 物品 / 无物品表）。</summary>
        public static GearBonuses None => default;

        /// <summary>从物品定义取加成：按 <see cref="ItemDefinition.GearStat"/> 把
        /// <see cref="ItemDefinition.GearAmount"/> 放进对应槽位，其余为零。
        /// def 为 null（空手）或未声明 gearBonus 时返回 <see cref="None"/>。</summary>
        public static GearBonuses FromDefinition(ItemDefinition def)
        {
            if (def == null || def.GearAmount <= 0f)
            {
                return None;
            }

            switch (def.GearStat)
            {
                case GearStat.Defense:
                    return new GearBonuses((int)def.GearAmount, 0f, 0);
                case GearStat.MoveSpeed:
                    return new GearBonuses(0, def.GearAmount, 0);
                case GearStat.MaxHealth:
                    return new GearBonuses(0, 0f, (int)def.GearAmount);
                default:
                    return None;
            }
        }
    }

    /// <summary>
    /// 装备加成的纯计算（m10 C1）：减伤 / 有效血上限 / 血量钳制。
    /// 全部无状态无副作用，dotnet 可测；Unity 侧（TakeDamage / PlayerContext）只接线。
    /// </summary>
    public static class GearBonusMath
    {
        /// <summary>减伤后的伤害下限：防御再高每次受伤也至少掉 1 点——穿防御装不是无敌。</summary>
        public const float MinDamage = 1f;

        /// <summary>
        /// 受伤减伤：max(1, damage - defense)。
        /// <paramref name="defense"/> ≤ 0 时**原样返回**——下限只防「减穿到 0」，
        /// 不是全场取整：m10 B2 碎块 0.5 伤害在无防御时必须保持 0.5（改掉会破坏 B2 契约）。
        /// damage ≤ 0 同样原样返回（调用方早退，这里只是纯函数兜底，不能把 0 洗成 1）。
        /// </summary>
        public static float MitigateDamage(float damage, int defense)
        {
            if (damage <= 0f || defense <= 0)
            {
                return damage;
            }

            float mitigated = damage - defense;
            return mitigated < MinDamage ? MinDamage : mitigated;
        }

        /// <summary>有效血上限 = 基础上限 + 装备加成（加成恒 >= 0，不会低于基础）。</summary>
        public static float EffectiveMaxHealth(float baseMax, int maxHealthBonus)
        {
            float effective = baseMax + maxHealthBonus;
            return effective < baseMax ? baseMax : effective;
        }

        /// <summary>当前血钳到有效上限内：超上限收回（切走生命上限装备的瞬间），
        /// 未超不动——钳制只收不加，不悄悄奶玩家一口。</summary>
        public static float ClampCurrentToEffectiveMax(float current, float baseMax, int maxHealthBonus)
        {
            float max = EffectiveMaxHealth(baseMax, maxHealthBonus);
            return current > max ? max : current;
        }
    }
}
