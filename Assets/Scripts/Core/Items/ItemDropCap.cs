using System.Collections.Generic;

namespace MyWorld.Core.Items
{
    /// <summary>评审 03 B-9：掉落物数量上限与过期淘汰。用户存档画像 237 个散落掉落物
    /// 占 level.dat 字节 52%——无上限无过期的堆积既撑爆存档也持续喂 GC。
    /// 策略：超限移除最旧（Attracting 吸附中的豁免）；过期（5 分钟）由 Unity 侧
    /// tick 判 <see cref="IsExpired"/> 清除。</summary>
    public static class ItemDropCap
    {
        /// <summary>同屏掉落物上限。超限移除最旧的（列表序≈时间序，新掉的更重要——
        /// 玩家刚挖的更可能要捡）。</summary>
        public const int MaxDrops = 128;

        /// <summary>过期时间（秒）。</summary>
        public const float ExpireSeconds = 300f;

        /// <summary>把列表裁到 <see cref="MaxDrops"/> 以内：移除最旧的非吸附实体。
        /// 全部都在吸附（玩家正在捡）的极端时刻放弃本轮裁剪——裁飞行中的掉落物
        /// 观感是 BUG，下一轮自然恢复。返回移除数量。</summary>
        public static int Prune(List<ItemDropEntity> drops)
        {
            if (drops == null) return 0;
            int excess = drops.Count - MaxDrops;
            if (excess <= 0) return 0;

            int removed = 0;
            int i = 0;
            while (i < drops.Count && removed < excess)
            {
                if (!drops[i].Attracting)
                {
                    drops.RemoveAt(i);
                    removed++;
                }
                else
                {
                    i++; // 吸附中的跳过（本轮豁免）
                }
            }

            return removed;
        }

        /// <summary>是否已过期。<see cref="ItemDropEntity.SpawnTime"/> 为 0 的实体
        /// （读档恢复 / Core 单元测试默认）不过期——保守：宁可多留不误删。</summary>
        public static bool IsExpired(ItemDropEntity drop, float currentTime)
            => drop != null && drop.SpawnTime > 0f && currentTime - drop.SpawnTime >= ExpireSeconds;
    }
}
