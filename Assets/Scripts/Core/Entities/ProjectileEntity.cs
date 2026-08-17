using System;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Entities
{
    /// <summary>箭实体的飞行阶段。</summary>
    public enum ProjectileState
    {
        /// <summary>飞行中（每帧步进积分）。</summary>
        Flying,

        /// <summary>命中方块停住——可转可拾取掉落（<see cref="ProjectileEntity.ToPickup"/>）。</summary>
        Stuck,

        /// <summary>已消失（命中玩家 / 超时），不可拾取。</summary>
        Dead,
    }

    /// <summary>
    /// 远程弹道实体（m11 W1-1）：骷髅射的箭。抛物线重力 <see cref="Gravity"/>，
    /// 命中玩家伤 <see cref="PlayerHitDamage"/>（走 <see cref="CombatEvents"/> 的
    /// Projectile 源事件，MobManager 转发 PlayerController.TakeDamage），
    /// 命中方块停在命中前一步并转可拾取掉落。
    /// <para>
    /// 积分是半隐式欧拉（先积分速度再积分位置），与
    /// <see cref="MyWorld.Core.Items.ItemDropEntity.TickGravity"/> 同款离散语义；
    /// 帧率改变只影响误差阶，不影响确定性断言（ProjectileEntityTests 按同一公式对账）。
    /// </para>
    /// <para>
    /// 归属：骷髅开火时经 <see cref="MobAI.OnProjectileFired"/> 抛出箭实体，
    /// Unity 侧（集成点②接线）订阅后接管 tick 列表与视觉；Core 单测订阅捕获做弹道断言。
    /// </para>
    /// </summary>
    public sealed class ProjectileEntity
    {
        /// <summary>抛物线重力（格/s²）。比玩家 -28 温和——8-12m 射程下的弧线肉眼可读。</summary>
        public const float Gravity = 12f;

        /// <summary>出膛默认初速（格/s）。骷髅实战弹道用这个值。</summary>
        public const float DefaultSpeed = 18f;

        /// <summary>命中玩家的伤害（点）。卡片数值：2。</summary>
        public const float PlayerHitDamage = 2f;

        /// <summary>玩家命中半径（米）：箭进入玩家该半径内判命中。</summary>
        public const float PlayerHitRadius = 0.6f;

        /// <summary>最长飞行时间（秒）：超时标记 Dead（防永远悬空的幽灵箭）。</summary>
        public const float MaxLifetime = 10f;

        /// <summary>
        /// 箭物品的 numericId。与 <c>StreamingAssets/items/arrow.json</c> 显式声明的
        /// 1300 手动保持一致（模式照 <see cref="MyWorld.Core.Items.ItemDropTable.PorkchopItemId"/>）——
        /// 对不上背包里就是「未知物品」。
        /// </summary>
        public const int ArrowItemId = 1300;

        /// <summary>发射者（骷髅）的 EntityId，伤害事件归属。</summary>
        public int OwnerEntityId;

        /// <summary>当前位置（世界坐标，米）。</summary>
        public Float3 Position;

        /// <summary>当前速度（格/s）。</summary>
        public Float3 Velocity;

        /// <summary>累计飞行时间（秒）。</summary>
        public float Age;

        /// <summary>飞行阶段。</summary>
        public ProjectileState State;

        public ProjectileEntity(Float3 position, Float3 velocity, int ownerEntityId)
        {
            Position = position;
            Velocity = velocity;
            OwnerEntityId = ownerEntityId;
            State = ProjectileState.Flying;
        }

        /// <summary>
        /// 步进一帧：半隐式欧拉（Y 先减 g·dt，位置再加 v·dt），随后按
        /// 玩家命中 → 方块命中的顺序判定。
        /// 返回 true = 本帧发生了终局事件（命中玩家 / 命中方块）；Stuck / Dead 后是 no-op。
        /// </summary>
        /// <param name="world">世界（null 时不判方块命中——纯弹道测试用）。</param>
        /// <param name="playerPos">玩家位置（命中判定基准）。</param>
        /// <param name="dt">步长（秒）。</param>
        public bool Tick(World world, Float3 playerPos, float dt)
        {
            if (State != ProjectileState.Flying)
            {
                return false;
            }

            Age += dt;
            if (Age > MaxLifetime)
            {
                State = ProjectileState.Dead;
                return false;
            }

            Float3 previous = Position;
            Velocity = new Float3(Velocity.X, Velocity.Y - Gravity * dt, Velocity.Z);
            Position = Position + Velocity * dt;

            // 1) 玩家命中优先（贴墙的玩家仍会被打到）
            float dx = Position.X - playerPos.X;
            float dy = Position.Y - playerPos.Y;
            float dz = Position.Z - playerPos.Z;
            if (dx * dx + dy * dy + dz * dz < PlayerHitRadius * PlayerHitRadius)
            {
                CombatEvents.RaiseTaken(new DamageEvent(
                    DamageSource.Projectile, PlayerHitDamage,
                    attacker: OwnerEntityId, victim: 0, hit: Position));
                State = ProjectileState.Dead;
                return true;
            }

            // 2) 方块命中：退回命中前一步（不进方块内部）并停住
            if (world != null && BlockSolidAt(world, Position))
            {
                Position = previous;
                State = ProjectileState.Stuck;
                return true;
            }

            return false;
        }

        /// <summary>Stuck 的箭转可拾取掉落（箭 ×1）；Flying / Dead 返回 null。</summary>
        public ItemStack? ToPickup()
        {
            return State == ProjectileState.Stuck
                ? new ItemStack(ArrowItemId, 1)
                : (ItemStack?)null;
        }

        /// <summary>
        /// 重力补偿瞄准：把出膛速度向量指向「目标抬高 0.5·g·t²」的位置
        /// （t = 水平距离 / speed 的近似飞行时间），让抛物线下坠恰好落在目标身上。
        /// 近似误差在 8-12m 射程内 < 0.1 格（ProjectileEntityTests 用整条弹道仿真断言命中）。
        /// </summary>
        public static Float3 ComputeLaunchVelocity(Float3 origin, Float3 target, float speed, float gravity)
        {
            float dx = target.X - origin.X;
            float dz = target.Z - origin.Z;
            float horizontal = (float)System.Math.Sqrt(dx * dx + dz * dz);
            float flightTime = horizontal / speed;

            // 抬高瞄准点补偿整段抛物线下坠
            var aim = new Float3(target.X, target.Y + 0.5f * gravity * flightTime * flightTime, target.Z);
            var dir = aim - origin;
            float length = (float)System.Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z);
            if (length < 0.0001f)
            {
                return new Float3(0f, speed, 0f); // 与目标重合：竖直上抛，避免除零
            }

            return new Float3(dir.X / length * speed, dir.Y / length * speed, dir.Z / length * speed);
        }

        private static bool BlockSolidAt(World world, Float3 p)
        {
            int x = (int)MathF.Floor(p.X);
            int y = (int)MathF.Floor(p.Y);
            int z = (int)MathF.Floor(p.Z);
            return world.GetBlock(x, y, z) != BlockIds.Air;
        }
    }
}
