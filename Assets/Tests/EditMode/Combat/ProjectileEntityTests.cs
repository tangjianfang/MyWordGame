// m11 W1-1（战斗）：Core/Entities/ProjectileEntity（箭实体）的离散步进 / 抛物线 /
// 命中玩家 / 命中方块转掉落 守卫。半隐式欧拉积分（先积分速度再积分位置），
// 与 ItemDropEntity.TickGravity 同款离散语义——测试断言按同一公式计算期望值。
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Combat
{
    [TestFixture]
    public class ProjectileEntityTests
    {
        /// <summary>远处一个不可能被命中的玩家位置（各测试按需覆盖）。</summary>
        private static readonly Float3 FarAwayPlayer = new Float3(0f, -100f, 0f);

        [TearDown]
        public void TearDown() => CombatEvents.Reset();

        // ─── 离散步进：水平匀速 + 竖直抛物线 ────────────────────────────────

        [Test]
        public void Tick_离散步进_水平匀速竖直按半隐式欧拉下坠()
        {
            // 初速水平 10、竖直 0：重力只作用在 Y，X 每步恒推进 vx*dt
            var arrow = new ProjectileEntity(new Float3(0f, 64f, 0f), new Float3(10f, 0f, 0f), ownerEntityId: 7);

            const float dt = 0.05f;
            const int steps = 10;
            for (int i = 0; i < steps; i++)
            {
                arrow.Tick(world: null, playerPos: FarAwayPlayer, dt: dt);
            }

            // X：vx 恒定 → 10 × 0.05 × 10 = 5
            Assert.That(arrow.Position.X, Is.EqualTo(5f).Within(1e-4f), "水平方向无阻力，恒速推进");
            // Y：半隐式欧拉 y_n = -g·dt²·n(n+1)/2 = -12×0.0025×55 = -1.65
            Assert.That(arrow.Position.Y, Is.EqualTo(64f - 1.65f).Within(1e-3f),
                "竖直方向按半隐式欧拉累计重力（10 步 0.05s 应下坠 1.65 格）");
            Assert.That(arrow.State, Is.EqualTo(ProjectileState.Flying), "没有命中任何东西，仍在飞");
        }

        [Test]
        public void Tick_超时最长寿命_标记死亡()
        {
            var arrow = new ProjectileEntity(new Float3(0f, 64f, 0f), new Float3(1f, 0f, 0f), ownerEntityId: 7);
            // 0.5s 一步走 21 步 = 10.5s > MaxLifetime 10
            for (int i = 0; i < 21; i++)
            {
                arrow.Tick(world: null, playerPos: FarAwayPlayer, dt: 0.5f);
            }
            Assert.That(arrow.State, Is.EqualTo(ProjectileState.Dead), "超时应标记 Dead（不再飞行/不掉落）");
            Assert.That(arrow.ToPickup().HasValue, Is.False, "超时消失的箭不可拾取");
        }

        // ─── 命中玩家：2 点远程伤害事件 ─────────────────────────────────────

        [Test]
        public void Tick_飞近玩家_发两点远程伤害事件_箭标记死亡()
        {
            var arrow = new ProjectileEntity(new Float3(0f, 64f, 0f), new Float3(10f, 0f, 0f), ownerEntityId: 7);

            int taken = 0;
            DamageEvent got = default;
            CombatEvents.OnDamageTaken += ev => { got = ev; taken++; };

            // 玩家在飞行路径上 (2, 64, 0)：某一步进入 0.6 半径
            bool hit = false;
            for (int i = 0; i < 60 && !hit; i++)
            {
                hit = arrow.Tick(world: null, playerPos: new Float3(2f, 64f, 0f), dt: 0.02f);
            }

            Assert.That(hit, Is.True, "箭穿过玩家位置应判定命中");
            Assert.That(taken, Is.EqualTo(1), "命中只发一次伤害事件");
            Assert.That(got.Source, Is.EqualTo(DamageSource.Projectile), "伤害源 = 远程");
            Assert.That(got.Amount, Is.EqualTo(2f).Within(1e-4f), "箭伤固定 2 点（卡片数值）");
            Assert.That(got.AttackerEntityId, Is.EqualTo(7), "伤害归属发射者（骷髅）");
            Assert.That(got.VictimEntityId, Is.EqualTo(0), "victim=0 表示玩家");
            Assert.That(arrow.State, Is.EqualTo(ProjectileState.Dead), "命中玩家后箭消失");
        }

        [Test]
        public void Tick_玩家在命中半径外_不判命中()
        {
            // 玩家偏离飞行路径 2 格（> 0.6 半径）：飞完也不应发伤害事件
            var arrow = new ProjectileEntity(new Float3(0f, 64f, 0f), new Float3(10f, 0f, 0f), ownerEntityId: 7);
            int taken = 0;
            CombatEvents.OnDamageTaken += _ => taken++;

            for (int i = 0; i < 30; i++)
            {
                arrow.Tick(world: null, playerPos: new Float3(2f, 66f, 0f), dt: 0.05f);
            }
            Assert.That(taken, Is.EqualTo(0), "玩家在命中半径外不应受伤");
        }

        // ─── 命中方块：停住并转可拾取掉落 ───────────────────────────────────

        [Test]
        public void Tick_命中方块_停在命中前一步_转可拾取掉落()
        {
            var world = new World();
            for (int y = 60; y < 70; y++)
            {
                world.SetBlock(5, y, 0, BlockIds.Stone); // x=5 立一堵墙
            }

            var arrow = new ProjectileEntity(new Float3(0f, 64f, 0f), new Float3(20f, 0f, 0f), ownerEntityId: 7);
            bool stopped = false;
            for (int i = 0; i < 100 && !stopped; i++)
            {
                stopped = arrow.Tick(world, playerPos: FarAwayPlayer, dt: 0.05f);
            }

            Assert.That(stopped, Is.True, "朝墙飞应停下");
            Assert.That(arrow.State, Is.EqualTo(ProjectileState.Stuck), "命中方块转 Stuck");
            Assert.That(arrow.Position.X, Is.GreaterThanOrEqualTo(4f), "停在墙前的最后一步");
            Assert.That(arrow.Position.X, Is.LessThan(5f), "不进入方块内部");

            var pickup = arrow.ToPickup();
            Assert.That(pickup.HasValue, Is.True, "Stuck 的箭可转掉落");
            Assert.That(pickup.Value.ItemId, Is.EqualTo(ProjectileEntity.ArrowItemId), "掉落物 = 箭");
            Assert.That(pickup.Value.Count, Is.EqualTo(1));

            // Stuck 后不再移动（掉落转换由宿主接管，箭本体冻结）
            float frozenX = arrow.Position.X;
            arrow.Tick(world, playerPos: FarAwayPlayer, dt: 0.05f);
            Assert.That(arrow.Position.X, Is.EqualTo(frozenX), "Stuck 的箭不再步进");
        }

        // ─── 弹道辅助：重力补偿瞄准 ─────────────────────────────────────────

        [Test]
        public void ComputeLaunchVelocity_重力补偿_十米外箭落在目标附近()
        {
            // 骷髅的实战弹道：抬枪补偿抛物线下坠，10 格外的目标应能被打进命中半径
            var origin = new Float3(0f, 65.4f, 0f); // 骷髅持弓高度（脚底 + 1.4）
            var target = new Float3(10f, 64.9f, 0f); // 玩家胸口（脚底 + 0.9）
            var velocity = ProjectileEntity.ComputeLaunchVelocity(
                origin, target, ProjectileEntity.DefaultSpeed, ProjectileEntity.Gravity);
            Assert.That(Length(velocity), Is.EqualTo(ProjectileEntity.DefaultSpeed).Within(1e-3f),
                "出膛初速 = DefaultSpeed");

            var arrow = new ProjectileEntity(origin, velocity, ownerEntityId: 24);
            bool hit = false;
            for (int i = 0; i < 120 && !hit; i++)
            {
                hit = arrow.Tick(world: null, playerPos: target, dt: 1f / 60f);
            }
            Assert.That(hit, Is.True,
                "重力补偿瞄准下，10 格外的目标应被命中（实际末位置 " + arrow.Position + "）");
        }

        [Test]
        public void ArrowItemId_与物品表arrow一致()
        {
            // 1300 是 items/arrow.json 显式声明的 numericId——掉落物 id 对不上背包里就是「未知物品」
            Assert.That(ProjectileEntity.ArrowItemId, Is.EqualTo(1300));
        }

        private static float Length(Float3 v)
        {
            return (float)System.Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        }
    }
}
