using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Combat
{
    /// <summary>一次爆炸的产物：被破坏的方块清单、按 <see cref="BlockDrops"/> 表滚出的掉落、玩家伤害。</summary>
    public sealed class ExplosionResult
    {
        /// <summary>被破坏的方块坐标（世界坐标，破坏前的方块 id 由调用方在 Detonate 内先行查过）。</summary>
        public List<(int X, int Y, int Z)> DestroyedBlocks { get; } = new List<(int, int, int)>();

        /// <summary>被破坏方块的掉落物（BlockDrops 表逐块查询累计；表为 null 时为空——方块直接消失）。</summary>
        public List<ItemStack> Drops { get; } = new List<ItemStack>();

        /// <summary>玩家在爆心处应受的距离衰减伤害（半径外为 0）。</summary>
        public float PlayerDamage;
    }

    /// <summary>
    /// 爆炸的核心计算（m11 W1-1）：中心 / 半径 → 破坏方块清单 + 实体伤害衰减。
    /// 苦力怕自爆（<see cref="Entities.MobAI"/> 新苦力怕分支）委托这里，Core 纯函数可双链单测。
    /// <para>
    /// 确定性铁律：整数范围枚举 + 方块中心欧氏距离判定，全程不持随机数对象——
    /// 同一份世界与参数跑两次，破坏清单（含顺序）一字不差（ExplosionTests 守卫）。
    /// 不破坏 <c>y&lt;0</c>（不炸穿世界底）与不可破坏方块（基岩等 hardness&lt;0）。
    /// </para>
    /// <para>
    /// 注册表 / 掉落表通过 <see cref="BoundRegistry"/> / <see cref="BoundDrops"/> 静态注入
    /// （模式照 <see cref="Entities.MobAI"/>.DropTable 的可绑定静态）：Unity 侧由
    /// WorldBootstrap 在集成点②接线；null 时退化为「只按内置 BlockIds.Bedrock 拦截 +
    /// 不产生掉落」，Core 单测与未接线的早期场景照常工作。
    /// </para>
    /// </summary>
    public static class Explosion
    {
        /// <summary>苦力怕爆炸半径（格）。卡片数值：3 格方块破坏。</summary>
        public const float DefaultRadius = 3f;

        /// <summary>爆心伤害上限（点）。卡片数值：距离衰减伤害最高 6。</summary>
        public const float MaxDamage = 6f;

        /// <summary>
        /// 硬度 / 液体判定用的方块注册表（集成点②注入）。null 时只按内置
        /// <see cref="BlockIds.Bedrock"/> 拦截不可破坏方块。
        /// </summary>
        public static BlockRegistry BoundRegistry { get; set; }

        /// <summary>破坏方块的掉落表（集成点②注入）。null 时被炸掉的方块直接消失、不掉物品。</summary>
        public static BlockDrops BoundDrops { get; set; }

        /// <summary>最近一次 Detonate 的产物（含掉落清单）——Unity 侧据此实例化 ItemDropEntity。</summary>
        public static ExplosionResult LastResult { get; private set; }

        /// <summary>
        /// m11 W3-4：起爆完成事件（爆心、破坏半径）——纯视觉订阅点（Unity 侧
        /// ParticlePool 播 fx-explosion 三帧特效）。Core 不持 Unity 类型，坐标用
        /// <see cref="Float3"/>；无订阅者时 <c>?.Invoke</c> 零开销，结算逻辑与
        /// 本事件完全解耦（既有 ExplosionTests 的确定性断言不受影响）。
        /// </summary>
        public static event System.Action<Float3, float> AfterDetonate;

        /// <summary>
        /// 某格方块能否被本次爆炸破坏：空气不算、<c>y&lt;0</c> 不算、不可破坏方块
        /// （注册表 hardness&lt;0，或无注册表时的内置基岩）不算、液体不算（水不该被「炸开」）。
        /// </summary>
        public static bool CanDestroy(World world, int x, int y, int z, BlockRegistry registry)
        {
            if (world == null || y < 0)
            {
                return false;
            }

            ushort blockId = world.GetBlock(x, y, z);
            if (blockId == BlockIds.Air)
            {
                return false;
            }

            if (registry != null && registry.TryGetByNumericId(blockId, out var definition))
            {
                if (definition.IsUnbreakable)
                {
                    return false;
                }

                if (definition.Liquid)
                {
                    return false;
                }
            }
            else if (blockId == BlockIds.Bedrock)
            {
                // 无注册表兜底：至少保住内置基岩（确定性不破坏 bedrock 是卡片硬约束）
                return false;
            }

            return true;
        }

        /// <summary>
        /// 收集爆心 <paramref name="radius"/> 格球内可破坏的方块坐标（不含边界外、
        /// 基岩、y&lt;0）。方块中心（<c>x+0.5</c>）到爆心的欧氏距离 ≤ 半径即入球。
        /// 只查询不改世界——Detonate 才落笔。
        /// </summary>
        public static List<(int X, int Y, int Z)> CollectBlocks(
            World world, Float3 center, float radius, BlockRegistry registry)
        {
            var blocks = new List<(int, int, int)>();
            if (world == null || radius <= 0f)
            {
                return blocks;
            }

            int minX = (int)System.MathF.Floor(center.X - radius);
            int maxX = (int)System.MathF.Floor(center.X + radius);
            int minY = (int)System.MathF.Floor(center.Y - radius);
            int maxY = (int)System.MathF.Floor(center.Y + radius);
            int minZ = (int)System.MathF.Floor(center.Z - radius);
            int maxZ = (int)System.MathF.Floor(center.Z + radius);

            float radiusSq = radius * radius;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        float dx = x + 0.5f - center.X;
                        float dy = y + 0.5f - center.Y;
                        float dz = z + 0.5f - center.Z;
                        bool inSphere = dx * dx + dy * dy + dz * dz <= radiusSq;
                        if (inSphere && CanDestroy(world, x, y, z, registry))
                        {
                            blocks.Add((x, y, z));
                        }
                    }
                }
            }

            return blocks;
        }

        /// <summary>
        /// 距离衰减伤害：爆心处 = <paramref name="maxDamage"/>，随距离线性衰减，
        /// 半径处及以外为 0。卡片数值：苦力怕最高 6 点。
        /// </summary>
        public static float DamageAt(Float3 center, Float3 target,
            float radius = DefaultRadius, float maxDamage = MaxDamage)
        {
            if (radius <= 0f || maxDamage <= 0f)
            {
                return 0f;
            }

            float dx = target.X - center.X;
            float dy = target.Y - center.Y;
            float dz = target.Z - center.Z;
            float distSq = dx * dx + dy * dy + dz * dz;
            if (distSq >= radius * radius)
            {
                return 0f;
            }

            float dist = (float)System.Math.Sqrt(distSq);
            return maxDamage * (1f - dist / radius);
        }

        /// <summary>
        /// 起爆：球内可破坏方块置空气（<see cref="World.SetBlock"/>，正常编辑路径——
        /// 脏区块由流式层重网格），逐块查 <paramref name="drops"/> 表累计掉落，
        /// 玩家伤害按 <see cref="DamageAt"/> 衰减并以 Environmental 源发
        /// <see cref="CombatEvents.RaiseTaken"/>（MobManager 转发给 PlayerController.TakeDamage，
        /// 与僵尸近战同一条伤害入口）。
        /// </summary>
        /// <param name="world">世界（null 时只结算伤害，不破坏方块）。</param>
        /// <param name="center">爆心（世界坐标）。</param>
        /// <param name="playerPos">玩家位置（伤害衰减基准）。</param>
        /// <param name="attackerEntityId">起爆者（苦力怕）的 EntityId，伤害事件归属。</param>
        /// <param name="radius">破坏半径。</param>
        /// <param name="registry">方块注册表；null 用 <see cref="BoundRegistry"/>。</param>
        /// <param name="drops">掉落表；null 用 <see cref="BoundDrops"/>。</param>
        public static ExplosionResult Detonate(World world, Float3 center, Float3 playerPos,
            int attackerEntityId, float radius = DefaultRadius,
            BlockRegistry registry = null, BlockDrops drops = null)
        {
            if (registry == null) registry = BoundRegistry;
            if (drops == null) drops = BoundDrops;

            var result = new ExplosionResult();
            foreach (var (x, y, z) in CollectBlocks(world, center, radius, registry))
            {
                ushort blockId = world.GetBlock(x, y, z);
                world.SetBlock(x, y, z, BlockIds.Air);
                result.DestroyedBlocks.Add((x, y, z));
                if (drops != null)
                {
                    result.Drops.AddRange(drops.DropsFor(blockId));
                }
            }

            result.PlayerDamage = DamageAt(center, playerPos, radius);
            if (result.PlayerDamage > 0f)
            {
                CombatEvents.RaiseTaken(new DamageEvent(
                    DamageSource.Environmental, result.PlayerDamage,
                    attacker: attackerEntityId, victim: 0, hit: center));
            }

            LastResult = result;
            AfterDetonate?.Invoke(center, radius); // m11 W3-4：纯视觉挂载点（结算完成后广播）
            return result;
        }
    }
}
