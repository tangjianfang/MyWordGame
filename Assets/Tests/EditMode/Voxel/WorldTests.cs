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

        [Test]
        public void AddChunk_MakesItsBlocksReadableByWorldCoordinates()
        {
            var world = new World();
            var column = new ChunkColumn();
            column.SetBlock(3, 64, 5, Stone);

            world.AddChunk(new ChunkPos(-2, 1), column);

            Assert.That(world.LoadedChunkCount, Is.EqualTo(1));
            Assert.That(world.GetBlock(-2 * 16 + 3, 64, 1 * 16 + 5), Is.EqualTo(Stone));
        }

        [Test]
        public void AddChunk_ReturnsTheSameInstanceFromTryGetChunk()
        {
            var world = new World();
            var column = new ChunkColumn();

            world.AddChunk(new ChunkPos(0, 0), column);

            Assert.That(world.TryGetChunk(new ChunkPos(0, 0), out ChunkColumn stored), Is.True);
            Assert.That(stored, Is.SameAs(column), "不应发生拷贝");
        }

        [Test]
        public void AddChunk_OnOccupiedPosition_Replaces()
        {
            var world = new World();
            var first = new ChunkColumn();
            var second = new ChunkColumn();

            world.AddChunk(new ChunkPos(0, 0), first);
            world.AddChunk(new ChunkPos(0, 0), second);

            Assert.That(world.LoadedChunkCount, Is.EqualTo(1));
            Assert.That(world.TryGetChunk(new ChunkPos(0, 0), out ChunkColumn stored), Is.True);
            Assert.That(stored, Is.SameAs(second), "重新生成/从存档载入时应当覆盖旧的区块列");
        }

        [Test]
        public void AddChunk_WithNullColumn_Throws()
        {
            var world = new World();

            Assert.Throws<System.ArgumentNullException>(() => world.AddChunk(new ChunkPos(0, 0), null));
        }
    }
}
