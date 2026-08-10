using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>由射线命中推出放置格，并挡住三类非法放置。</summary>
    [TestFixture]
    public class BlockPlacementTests
    {
        private static VoxelRayHit HitAt(int x, int y, int z, int nx, int ny, int nz)
            => new VoxelRayHit { Hit = true, X = x, Y = y, Z = z, NormalX = nx, NormalY = ny, NormalZ = nz };

        /// <summary>玩家站在 (0.5, 64, 0.5)，包围盒占据 x/z 的 [0.2, 0.8]、y 的 [64, 65.8]。</summary>
        private static Aabb Player() => Aabb.FromBottomCenter(new Float3(0.5f, 64f, 0.5f), 0.6f, 1.8f);

        [Test]
        public void Resolve_PlacesAgainstHitFace()
        {
            bool ok = BlockPlacement.TryResolve(HitAt(5, 64, 5, 0, 1, 0), Player(), out int x, out int y, out int z);

            Assert.That(ok, Is.True);
            Assert.That(new[] { x, y, z }, Is.EqualTo(new[] { 5, 65, 5 }),
                "打在方块顶面上，新方块应当放在它正上方");
        }

        [Test]
        public void Resolve_WithoutHit_Fails()
        {
            Assert.That(BlockPlacement.TryResolve(default, Player(), out _, out _, out _), Is.False,
                "射线没打中任何方块时不应当放置");
        }

        [Test]
        public void Resolve_InsidePlayerBox_Fails()
        {
            // 打在玩家脚下那格的顶面 → 目标格正是玩家站的位置
            bool ok = BlockPlacement.TryResolve(HitAt(0, 63, 0, 0, 1, 0), Player(), out _, out _, out _);

            Assert.That(ok, Is.False, "不能把方块放到玩家身体里，否则会把自己封住");
        }

        [Test]
        public void Resolve_BesidePlayer_Succeeds()
        {
            // 玩家包围盒 x 只占 [0.2, 0.8]，x = 1 那一列是空的
            bool ok = BlockPlacement.TryResolve(HitAt(2, 64, 0, -1, 0, 0), Player(), out int x, out _, out _);

            Assert.That(ok, Is.True, "紧挨着玩家但不重叠的格子应当能放");
            Assert.That(x, Is.EqualTo(1));
        }

        [TestCase(319, 1, false)]
        [TestCase(-64, -1, false)]
        [TestCase(319, -1, true)]
        public void Resolve_ChecksWorldHeightLimits(int hitY, int normalY, bool expected)
        {
            bool ok = BlockPlacement.TryResolve(HitAt(50, hitY, 50, 0, normalY, 0), Player(), out _, out _, out _);

            Assert.That(ok, Is.EqualTo(expected),
                "超出世界高度 [-64, 320) 的格子不应当能放");
        }
    }
}