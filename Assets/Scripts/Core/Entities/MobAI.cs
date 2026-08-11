using MyWorld.Core.Math;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Entities
{
    /// <summary>
    /// 动物 AI tick。每帧调用，移动 + 状态切换。
    /// 不依赖 Unity——可单测。
    /// </summary>
    public static class MobAI
    {
        public const float ScareRadius = 8f;
        public const float ScareRadiusSq = ScareRadius * ScareRadius;
        public const float CalmRadius = 16f;
        public const float CalmRadiusSq = CalmRadius * CalmRadius;
        public const float HostileChaseRadius = 16f;
        public const float HostileChaseRadiusSq = HostileChaseRadius * HostileChaseRadius;
        public const float AttackRange = 1.6f;
        public const float AttackRangeSq = AttackRange * AttackRange;
        public const float WanderSpeed = 1.5f;
        public const float ChaseSpeed = 3.5f;
        public const float FleeSpeed = 5f;

        public static void Tick(Mob mob, Float3 playerPos, World world, TimeOfDay time, float dt)
        {
            if (!mob.IsAlive) return;
            if (mob.HitFlashTimer > 0) mob.HitFlashTimer -= dt;
            if (mob.AttackCooldown > 0) mob.AttackCooldown -= dt;

            float distSq = DistanceSquared(playerPos, mob.Position);
            bool isNight = time != null && time.IsNight;

            switch (mob.Kind)
            {
                case MobKind.Passive:
                    TickPassive(mob, playerPos, distSq, dt, world);
                    break;
                case MobKind.Hostile:
                    if (!isNight && mob.State != MobState.FleeingFromAttacker)
                    {
                        // 白天：和友好动物一样行为
                        TickPassive(mob, playerPos, distSq, dt, world);
                    }
                    else
                    {
                        TickHostile(mob, playerPos, distSq, dt, world);
                    }
                    break;
            }
        }

        private static void TickPassive(Mob mob, Float3 playerPos, float distSq, float dt, World world)
        {
            // 被玩家吓跑
            if (distSq < ScareRadiusSq && mob.State != MobState.FleeingFromAttacker)
            {
                mob.State = MobState.Scared;
            }
            else if (mob.State == MobState.Scared && distSq > CalmRadiusSq)
            {
                mob.State = MobState.Idle;
            }

            switch (mob.State)
            {
                case MobState.Idle:
                    mob.Velocity = default;
                    mob.WanderCooldown -= dt;
                    if (mob.WanderCooldown <= 0)
                    {
                        mob.State = MobState.Wander;
                        mob.WanderTarget = mob.Position + new Float3(
                            (RandomSigned() * 6f), 0, (RandomSigned() * 6f));
                        mob.WanderCooldown = 3f + RandomUnit() * 4f;
                    }
                    break;
                case MobState.Wander:
                {
                    var to = mob.WanderTarget - mob.Position;
                    float d = (float)System.Math.Sqrt(to.X * to.X + to.Z * to.Z);
                    if (d < 0.5f)
                    {
                        mob.State = MobState.Idle;
                        mob.WanderCooldown = 2f;
                    }
                    else
                    {
                        mob.Velocity = new Float3(to.X / d * WanderSpeed, 0, to.Z / d * WanderSpeed);
                    }
                    break;
                }
                case MobState.Scared:
                case MobState.FleeingFromAttacker:
                {
                    var away = mob.Position - playerPos;
                    float d = (float)System.Math.Sqrt(away.X * away.X + away.Z * away.Z);
                    if (d < 0.001f) away = new Float3(1, 0, 0);
                    mob.Velocity = new Float3(away.X / d * FleeSpeed, 0, away.Z / d * FleeSpeed);
                    break;
                }
            }

            mob.Position = new Float3(
                mob.Position.X + mob.Velocity.X * dt,
                mob.Position.Y,
                mob.Position.Z + mob.Velocity.Z * dt);
        }

        private static void TickHostile(Mob mob, Float3 playerPos, float distSq, float dt, World world)
        {
            if (mob.IsCreeper)
            {
                TickCreeper(mob, playerPos, distSq, dt, world);
                return;
            }

            if (distSq < HostileChaseRadiusSq)
            {
                mob.State = MobState.Chasing;
            }
            else
            {
                mob.State = MobState.Idle;
            }

            if (mob.State == MobState.Chasing)
            {
                float range = mob.AttackRange;
                float rangeSq = range * range;
                if (distSq < rangeSq)
                {
                    mob.Velocity = default;
                    // 攻击
                    if (mob.AttackCooldown <= 0)
                    {
                        mob.AttackCooldown = 1f;
                        mob.LastAttackTime = 0;
                        CombatEvents.RaiseDealt(new DamageEvent(
                            DamageSource.Melee, mob.AttackDamage,
                            attacker: mob.EntityId, victim: 0, hit: mob.Position));
                        CombatEvents.RaiseTaken(new DamageEvent(
                            DamageSource.Melee, mob.AttackDamage,
                            attacker: mob.EntityId, victim: 0, hit: playerPos));
                    }
                }
                else
                {
                    var to = playerPos - mob.Position;
                    float d = (float)System.Math.Sqrt(to.X * to.X + to.Z * to.Z);
                    mob.Velocity = new Float3(to.X / d * ChaseSpeed, 0, to.Z / d * ChaseSpeed);
                }
            }
            else
            {
                mob.Velocity = default;
            }

            mob.Position = new Float3(
                mob.Position.X + mob.Velocity.X * dt,
                mob.Position.Y,
                mob.Position.Z + mob.Velocity.Z * dt);
        }

        /// <summary>
        /// 苦力怕：进入 3 格内开始 2 秒自爆倒计时，倒计时结束自爆（Explosive 源，10 伤害）。
        /// 玩家跑出 5 格则取消引爆。倒计时 = 0 时把自己设为 Dying。
        /// </summary>
        private const float CreeperTriggerRadius = 3f;
        private const float CreeperTriggerRadiusSq = CreeperTriggerRadius * CreeperTriggerRadius;
        private const float CreeperAbortRadius = 5f;
        private const float CreeperAbortRadiusSq = CreeperAbortRadius * CreeperAbortRadius;
        private const float CreeperFuseDuration = 2f;
        private const float CreeperExplosionDamage = 10f;

        private static void TickCreeper(Mob mob, Float3 playerPos, float distSq, float dt, World world)
        {
            if (distSq < CreeperTriggerRadiusSq)
            {
                if (mob.FuseTimer <= 0)
                {
                    mob.FuseTimer = CreeperFuseDuration;
                }
            }
            else if (distSq > CreeperAbortRadiusSq && mob.FuseTimer > 0)
            {
                mob.FuseTimer = 0;
            }

            if (mob.FuseTimer > 0)
            {
                mob.FuseTimer -= dt;
                mob.Velocity = default;
                if (mob.FuseTimer <= 0)
                {
                    // 自爆：发一个 Explosion 伤害事件，把自身转 Dying
                    CombatEvents.RaiseTaken(new DamageEvent(
                        DamageSource.Environmental, CreeperExplosionDamage,
                        attacker: mob.EntityId, victim: 0, hit: mob.Position));
                    mob.Health = new Health(0);
                    mob.State = MobState.Dying;
                    mob.DeathTimer = 0.4f;
                    return;
                }
            }
            else
            {
                // 移动：朝玩家走
                var to = playerPos - mob.Position;
                float d = (float)System.Math.Sqrt(to.X * to.X + to.Z * to.Z);
                if (d > 0.001f)
                {
                    mob.Velocity = new Float3(to.X / d * ChaseSpeed, 0, to.Z / d * ChaseSpeed);
                }
            }

            mob.Position = new Float3(
                mob.Position.X + mob.Velocity.X * dt,
                mob.Position.Y,
                mob.Position.Z + mob.Velocity.Z * dt);
        }

        public static float DistanceSquared(Float3 a, Float3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        private static System.Random _rng = new System.Random(0xC0FFEE);
        private static float RandomSigned() => (float)(_rng.NextDouble() * 2 - 1);
        private static float RandomUnit() => (float)_rng.NextDouble();
    }
}
