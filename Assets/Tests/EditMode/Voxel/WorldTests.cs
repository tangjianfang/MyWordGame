using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    [TestFixture]
    public class WorldTests
    {
        private const ushort Stone = 1;
        private const ushort Dirt = 2;

        [Test]
        public void ChunkPos_WithSameCoordinates_AreEqualAndShareHash()
        {
            var a = new ChunkPos(-3, 7);
            var b = new ChunkPos(-3, 7);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(new ChunkPos(7, -3)), "x 与 z 互换必须是不同区块");
        }

        [Test]
        public void NewWorld_HasNoLoadedChunks()
        {
            var world = new World();

            Assert.That(world.LoadedChunkCount, Is.EqualTo(0));
            Assert.That(world.GetBlock(0, 0, 0), Is.EqualTo(ChunkSection.AirId));
        }

        [Test]
        public void GetBlock_OnUnloadedChunk_DoesNotCreateChunk()
        {
            var world = new World();

            world.GetBlock(1000, 0, -1000);

            Assert.That(world.LoadedChunkCount, Is.EqualTo(0), "读取不应产生副作用");
        }

        [Test]
        public void SetBlock_CreatesChunkOnDemand()
        {
            var world = new World();

            world.SetBlock(0, 0, 0, Stone);

            Assert.That(world.LoadedChunkCount, Is.EqualTo(1));
            Assert.That(world.GetBlock(0, 0, 0), Is.EqualTo(Stone));
        }

        [TestCase(0, 0)]
        [TestCase(15, 15)]
        [TestCase(-1, -1)]
        [TestCase(-16, -16)]
        [TestCase(1234, -5678)]
        public void SetBlock_ThenGetBlock_RoundTripsAcrossChunkBoundaries(int worldX, int worldZ)
        {
            var world = new World();

            world.SetBlock(worldX, 64, worldZ, Dirt);

            Assert.That(world.GetBlock(worldX, 64, worldZ), Is.EqualTo(Dirt));
            Assert.That(world.GetBlock(worldX, 65, worldZ), Is.EqualTo(ChunkSection.AirId));
        }

        [Test]
        public void AdjacentBlocksAcrossChunkSeam_StayIndependent()
        {
            var world = new World();

            world.SetBlock(-1, 64, 0, Stone);
            world.SetBlock(0, 64, 0, Dirt);

            Assert.That(world.GetBlock(-1, 64, 0), Is.EqualTo(Stone));
            Assert.That(world.GetBlock(0, 64, 0), Is.EqualTo(Dirt));
            Assert.That(world.LoadedChunkCount, Is.EqualTo(2), "跨越 x=0 接缝应落在两个不同区块");
        }
    }
}
