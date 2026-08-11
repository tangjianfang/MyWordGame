using System.Collections.Generic;
using MyWorld.Core.Math;

namespace MyWorld.Core.Entities
{
    /// <summary>村民职业（简化版）。</summary>
    public enum VillagerProfession { Farmer, Librarian, Blacksmith, None }

    /// <summary>
    /// 单条交易："给 X 物品 → 收 Y 物品"。
    /// <see cref="Uses"/> 限制交易次数：0 表示无限。
    /// </summary>
    public readonly struct TradeOffer
    {
        public readonly string BuyItem;
        public readonly int BuyCount;
        public readonly string SellItem;
        public readonly int SellCount;
        public readonly int Uses;
        public readonly int MaxUses;

        public TradeOffer(string buyItem, int buyCount, string sellItem, int sellCount, int uses = 0, int maxUses = 0)
        {
            BuyItem = buyItem;
            BuyCount = buyCount;
            SellItem = sellItem;
            SellCount = sellCount;
            Uses = uses;
            MaxUses = maxUses == 0 ? int.MaxValue : maxUses;
        }

        public bool CanTrade => Uses < MaxUses;
    }

    /// <summary>
    /// 村民实体。MobTypeId=6。白天在岗、晚上回家（原地站着，简化版不移动）。
    /// </summary>
    public sealed class Villager
    {
        public int EntityId;
        public string Name;
        public VillagerProfession Profession;
        public Float3 Position;
        public Health Health;
        public List<TradeOffer> Offers = new List<TradeOffer>();
        public float HitFlashTimer;
        public float WanderCooldown;

        public bool IsAlive => Health.Current > 0;

        public static Villager Create(int entityId, VillagerProfession profession, Float3 position, IEnumerable<TradeOffer> offers = null)
        {
            var v = new Villager
            {
                EntityId = entityId,
                Name = profession.ToString() + "_" + entityId,
                Profession = profession,
                Position = position,
                Health = new Health(20),
                WanderCooldown = 2f,
            };
            if (offers != null)
            {
                foreach (var o in offers) v.Offers.Add(o);
            }
            return v;
        }
    }

    /// <summary>
    /// 村民 AI tick。简化：白天 Idle 不动、晚上找最近表面位置躲避。
    /// </summary>
    public static class VillagerAI
    {
        public const float WanderRadius = 4f;

        public static void Tick(Villager v, Float3 playerPos, Time.TimeOfDay time, float dt)
        {
            if (!v.IsAlive) return;
            if (v.HitFlashTimer > 0) v.HitFlashTimer -= dt;

            bool isNight = time != null && time.IsNight;
            // 晚上原地不动；白天偶尔小晃
            if (!isNight)
            {
                v.WanderCooldown -= dt;
                if (v.WanderCooldown <= 0)
                {
                    v.WanderCooldown = 5f + (System.Math.Abs(Hash(v.EntityId)) % 50) / 10f;
                    // 简化：不做实际位移（演示够用）
                }
            }
        }

        private static int Hash(int x)
        {
            unchecked
            {
                int h = x;
                h ^= h >> 13;
                h *= 0x5BD1E995;
                h ^= h >> 15;
                return h;
            }
        }
    }
}