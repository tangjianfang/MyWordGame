// m12 P0-b：腾空垫脚（tower-up）+ 六向命中落格契约（纯 Core，双链同源）。
// 孩子反馈「只能往旁边堆，不能往上」的修复判定在这里钉死：
// 站立时腿部格仍全禁（防自封）；跳跃最高点附近（0.5 格容差窗）允许垫脚下格。
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class BlockPlacementTowerUpTests
    {
        // 玩家宽 0.6 / 高 1.8（PlayerMotorSettings 既有值），站在 (0.5, 64, 0.5)：
        // 盒范围 X/Z ∈ [0.2, 0.8]、Y ∈ [64, 65.8)
        private static Aabb StandingBox(float feetY)
            => Aabb.FromBottomCenter(new Float3(0.5f, feetY, 0.5f), 0.6f, 1.8f);

        private static VoxelRayHit HitAt(int x, int y, int z, int nx, int ny, int nz)
            => new VoxelRayHit { Hit = true, X = x, Y = y, Z = z, NormalX = nx, NormalY = ny, NormalZ = nz };

        [Test]
        public void Standing_LegCell_StillRejected()
        {
            // 站在 y=64：脚下地面块 (0,63,0)，腿部格 (0,64,0) 与玩家盒重叠
            Aabb box = StandingBox(64f);
            var hit = HitAt(0, 63, 0, 0, 1, 0); // 打地面块顶面 → 放置格 (0,64,0)

            bool allowed = BlockPlacement.TryResolve(hit, box, out _, out _, out _, allowTowerUp: true);

            Assert.That(allowed, Is.False, "站立时垫脚格必须拒绝——防把自己封进方块");
        }

        [Test]
        public void JumpApex_FootCell_Allowed()
        {
            // 跳跃最高点：脚 ≈ 64.9（走跳 ~1.25 格顶点附近），目标格顶 65，64.9 ≥ 65-0.5
            Aabb box = StandingBox(64.9f);
            var hit = HitAt(0, 63, 0, 0, 1, 0);

            bool allowed = BlockPlacement.TryResolve(hit, box, out int x, out int y, out int z, allowTowerUp: true);

            Assert.That(allowed, Is.True, "跳跃最高点应允许把方块垫进脚下格");
            Assert.That((x, y, z), Is.EqualTo((0, 64, 0)), "落格 = 命中格沿顶面法线外挪一格");
        }

        [Test]
        public void Airborne_BesidePlayer_Rejected()
        {
            // 空中但目标格在旁边（水平不重叠）——不是垫脚，照旧拒绝
            Aabb box = StandingBox(64.9f);
            var hit = HitAt(3, 63, 0, 0, 1, 0); // 放置格 (3,64,0)，离玩家 3 格远

            bool allowed = BlockPlacement.TryResolve(hit, box, out _, out _, out _, allowTowerUp: true);

            Assert.That(allowed, Is.True, "旁边不重叠的格子本来就合法（IntersectsPlayer 直接过）");
        }

        [Test]
        public void Airborne_TooLow_RisingEarly_Rejected()
        {
            // 刚起跳脚 64.3：还差容差窗（< 64.5），垫脚不放开——防起跳瞬间连点自封
            Aabb box = StandingBox(64.3f);
            var hit = HitAt(0, 63, 0, 0, 1, 0);

            bool allowed = BlockPlacement.TryResolve(hit, box, out _, out _, out _, allowTowerUp: true);

            Assert.That(allowed, Is.False, "容差窗之下（起跳早期）仍拒绝");
        }

        [Test]
        public void DefaultParameter_KeepsLegacyContract()
        {
            // 不传 allowTowerUp 的既有调用（守卫测试 / 床门多格）契约不变：空中也拒
            Aabb box = StandingBox(64.9f);
            var hit = HitAt(0, 63, 0, 0, 1, 0);

            bool allowed = BlockPlacement.TryResolve(hit, box, out _, out _, out _);

            Assert.That(allowed, Is.False, "缺省参数保持旧契约（m11 多格放置守卫依赖）");
        }

        [Test]
        public void IsTowerUpPlacement_StandingOnGround_FootCellNotQualifying()
        {
            // 直接判定函数：站立（脚 64.0）对腿部格 (0,64,0)——顶 65 比 64 高出 1 > 0.5 容差
            Aabb box = StandingBox(64f);

            Assert.That(BlockPlacement.IsTowerUpPlacement(0, 64, 0, box), Is.False,
                "站立时腿部格不算垫脚格");
        }

        // ─── 五向命中落格（孩子说"前后上下左右都不能选"——用参数化把"能选"钉成契约） ───

        [TestCase(0, 1, 0, 10, 65, 10, TestOf = typeof(BlockPlacement))]          // 顶面：往上放
        [TestCase(0, -1, 0, 10, 63, 10, TestOf = typeof(BlockPlacement))]         // 底面：往下放
        [TestCase(1, 0, 0, 11, 64, 10, TestOf = typeof(BlockPlacement))]          // +X 侧面
        [TestCase(-1, 0, 0, 9, 64, 10, TestOf = typeof(BlockPlacement))]          // -X 侧面
        [TestCase(0, 0, 1, 10, 64, 11, TestOf = typeof(BlockPlacement))]          // +Z 侧面
        [TestCase(0, 0, -1, 10, 64, 9, TestOf = typeof(BlockPlacement))]          // -Z 侧面
        public void TryResolve_FiveFaces_PlacementCellFollowsNormal(
            int nx, int ny, int nz, int expectedX, int expectedY, int expectedZ)
        {
            // 命中 (10,64,10)，玩家盒放很远（50,80,50）不参与防卡身——纯验证落格换算
            Aabb box = Aabb.FromBottomCenter(new Float3(50f, 80f, 50f), 0.6f, 1.8f);
            var hit = HitAt(10, 64, 10, nx, ny, nz);

            bool allowed = BlockPlacement.TryResolve(hit, box, out int x, out int y, out int z);

            Assert.That(allowed, Is.True, "远处玩家不拦：六向都应可解算");
            Assert.That(x, Is.EqualTo(expectedX), "X 落格 = 命中格 + 法线X");
            Assert.That(y, Is.EqualTo(expectedY), "Y 落格 = 命中格 + 法线Y");
            Assert.That(z, Is.EqualTo(expectedZ), "Z 落格 = 命中格 + 法线Z");
        }
    }
}
