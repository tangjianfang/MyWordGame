using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Physics
{
    [TestFixture]
    public class VoxelRaycasterTests
    {
        [Test]
        public void Ray_ThroughEmptySpace_ReportsNoHit()
        {
            var world = new TestSolidSource();

            VoxelRayHit hit = VoxelRaycaster.Cast(world, new Float3(0.5f, 0.5f, 0.5f), new Float3(1f, 0f, 0f), 10f);

            Assert.That(hit.Hit, Is.False);
        }

        [Test]
        public void Ray_AlongPositiveX_HitsFirstSolidBlock()
        {
            var world = new TestSolidSource();
            world.Add(3, 0, 0);
            world.Add(6, 0, 0);

            VoxelRayHit hit = VoxelRaycaster.Cast(world, new Float3(0.5f, 0.5f, 0.5f), new Float3(1f, 0f, 0f), 10f);

            Assert.That(hit.Hit, Is.True);
            Assert.That(new[] { hit.X, hit.Y, hit.Z }, Is.EqualTo(new[] { 3, 0, 0 }));
        }

        [Test]
        public void Hit_ReportsNormalPointingBackTowardTheRay()
        {
            var world = new TestSolidSource();
            world.Add(3, 0, 0);

            VoxelRayHit hit = VoxelRaycaster.Cast(world, new Float3(0.5f, 0.5f, 0.5f), new Float3(1f, 0f, 0f), 10f);

            Assert.That(new[] { hit.NormalX, hit.NormalY, hit.NormalZ }, Is.EqualTo(new[] { -1, 0, 0 }),
                "沿 +X 前进应命中方块的 -X 面");
        }

        [Test]
        public void PlacementPosition_IsHitBlockOffsetByNormal()
        {
            var world = new TestSolidSource();
            world.Add(3, 0, 0);

            VoxelRayHit hit = VoxelRaycaster.Cast(world, new Float3(0.5f, 0.5f, 0.5f), new Float3(1f, 0f, 0f), 10f);

            Assert.That(new[] { hit.PlacementX, hit.PlacementY, hit.PlacementZ }, Is.EqualTo(new[] { 2, 0, 0 }));
        }

        [Test]
        public void Ray_DownwardOntoGround_HitsTopFace()
        {
            var world = new TestSolidSource();
            world.Add(0, 0, 0);

            VoxelRayHit hit = VoxelRaycaster.Cast(world, new Float3(0.5f, 5f, 0.5f), new Float3(0f, -1f, 0f), 10f);

            Assert.That(hit.Hit, Is.True);
            Assert.That(new[] { hit.X, hit.Y, hit.Z }, Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(new[] { hit.NormalX, hit.NormalY, hit.NormalZ }, Is.EqualTo(new[] { 0, 1, 0 }));
        }

        [Test]
        public void Ray_IntoNegativeCoordinates_WorksAcrossZero()
        {
            var world = new TestSolidSource();
            world.Add(-4, -1, -1);

            VoxelRayHit hit = VoxelRaycaster.Cast(world, new Float3(0.5f, -0.5f, -0.5f), new Float3(-1f, 0f, 0f), 10f);

            Assert.That(hit.Hit, Is.True);
            Assert.That(new[] { hit.X, hit.Y, hit.Z }, Is.EqualTo(new[] { -4, -1, -1 }));
            Assert.That(new[] { hit.NormalX, hit.NormalY, hit.NormalZ }, Is.EqualTo(new[] { 1, 0, 0 }));
        }

        [Test]
        public void Ray_BeyondMaxDistance_DoesNotHit()
        {
            var world = new TestSolidSource();
            world.Add(8, 0, 0);

            VoxelRayHit hit = VoxelRaycaster.Cast(world, new Float3(0.5f, 0.5f, 0.5f), new Float3(1f, 0f, 0f), 5f);

            Assert.That(hit.Hit, Is.False);
        }

        [Test]
        public void Ray_StartingInsideSolidBlock_HitsThatBlockImmediately()
        {
            var world = new TestSolidSource();
            world.Add(0, 0, 0);

            VoxelRayHit hit = VoxelRaycaster.Cast(world, new Float3(0.5f, 0.5f, 0.5f), new Float3(1f, 0f, 0f), 10f);

            Assert.That(hit.Hit, Is.True);
            Assert.That(new[] { hit.X, hit.Y, hit.Z }, Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(hit.Distance, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void DiagonalRay_HitsTheBlockItActuallyPassesThrough()
        {
            var world = new TestSolidSource();
            world.Add(2, 2, 0);

            VoxelRayHit hit = VoxelRaycaster.Cast(world, new Float3(0.5f, 0.5f, 0.5f), new Float3(1f, 1f, 0f), 10f);

            Assert.That(hit.Hit, Is.True);
            Assert.That(new[] { hit.X, hit.Y, hit.Z }, Is.EqualTo(new[] { 2, 2, 0 }));
        }
    }

    internal sealed class TestSolidSource : ISolidBlockSource
    {
        private readonly System.Collections.Generic.HashSet<(int, int, int)> _solid =
            new System.Collections.Generic.HashSet<(int, int, int)>();

        public void Add(int x, int y, int z) => _solid.Add((x, y, z));

        public bool IsSolidAt(int x, int y, int z) => _solid.Contains((x, y, z));
    }
}
