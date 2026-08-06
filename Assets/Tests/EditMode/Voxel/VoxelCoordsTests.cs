using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    [TestFixture]
    public class VoxelCoordsTests
    {
        [TestCase(0, 0)]
        [TestCase(15, 0)]
        [TestCase(16, 1)]
        [TestCase(31, 1)]
        [TestCase(-1, -1)]
        [TestCase(-16, -1)]
        [TestCase(-17, -2)]
        public void WorldToChunk_FloorsTowardNegativeInfinity(int world, int expectedChunk)
        {
            Assert.That(VoxelCoords.WorldToChunk(world), Is.EqualTo(expectedChunk));
        }

        [TestCase(0, 0)]
        [TestCase(15, 15)]
        [TestCase(16, 0)]
        [TestCase(-1, 15)]
        [TestCase(-16, 0)]
        [TestCase(-17, 15)]
        public void WorldToLocal_AlwaysReturnsNonNegativeOffset(int world, int expectedLocal)
        {
            Assert.That(VoxelCoords.WorldToLocal(world), Is.EqualTo(expectedLocal));
        }
    }
}
