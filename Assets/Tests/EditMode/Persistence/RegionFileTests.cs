using System;
using System.IO;
using MyWorld.Core.Persistence;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Persistence
{
    [TestFixture]
    public class RegionFileTests
    {
        [Test]
        public void NewRegion_ContainsNoChunks()
        {
            var region = new RegionFile(new ChunkPos(0, 0));

            Assert.That(region.ChunkCount, Is.EqualTo(0));
            Assert.That(region.TryGetChunk(new ChunkPos(0, 0), out _), Is.False);
        }

        [Test]
        public void StoredChunk_CanBeReadBack()
        {
            var region = new RegionFile(new ChunkPos(0, 0));
            var column = new ChunkColumn();
            column.SetBlock(5, 70, 9, BlockIds.Grass);

            region.StoreChunk(new ChunkPos(3, 7), column);

            Assert.That(region.TryGetChunk(new ChunkPos(3, 7), out ChunkColumn restored), Is.True);
            Assert.That(restored.GetBlock(5, 70, 9), Is.EqualTo(BlockIds.Grass));
        }

        [Test]
        public void SaveAndLoad_PreservesAllStoredChunks()
        {
            var region = new RegionFile(new ChunkPos(0, 0));
            var a = new ChunkColumn();
            a.SetBlock(1, 65, 2, BlockIds.Stone);
            var b = new ChunkColumn();
            b.SetBlock(14, 200, 13, BlockIds.Dirt);

            region.StoreChunk(new ChunkPos(0, 0), a);
            region.StoreChunk(new ChunkPos(31, 31), b);

            using var stream = new MemoryStream();
            region.Save(stream);
            stream.Position = 0;
            RegionFile loaded = RegionFile.Load(stream);

            Assert.That(loaded.RegionPos, Is.EqualTo(new ChunkPos(0, 0)));
            Assert.That(loaded.ChunkCount, Is.EqualTo(2));
            Assert.That(loaded.TryGetChunk(new ChunkPos(0, 0), out ChunkColumn ra), Is.True);
            Assert.That(ra.GetBlock(1, 65, 2), Is.EqualTo(BlockIds.Stone));
            Assert.That(loaded.TryGetChunk(new ChunkPos(31, 31), out ChunkColumn rb), Is.True);
            Assert.That(rb.GetBlock(14, 200, 13), Is.EqualTo(BlockIds.Dirt));
        }

        [Test]
        public void SaveAndLoad_PreservesNegativeRegionCoordinates()
        {
            var region = new RegionFile(new ChunkPos(-2, -3));
            var column = new ChunkColumn();
            column.SetBlock(7, 70, 7, BlockIds.Sand);
            region.StoreChunk(new ChunkPos(-64, -96), column);

            using var stream = new MemoryStream();
            region.Save(stream);
            stream.Position = 0;
            RegionFile loaded = RegionFile.Load(stream);

            Assert.That(loaded.RegionPos, Is.EqualTo(new ChunkPos(-2, -3)));
            Assert.That(loaded.TryGetChunk(new ChunkPos(-64, -96), out ChunkColumn restored), Is.True);
            Assert.That(restored.GetBlock(7, 70, 7), Is.EqualTo(BlockIds.Sand));
        }

        [Test]
        public void StoringSameChunkTwice_KeepsTheLatestVersion()
        {
            var region = new RegionFile(new ChunkPos(0, 0));
            var pos = new ChunkPos(2, 2);

            var first = new ChunkColumn();
            first.SetBlock(0, 70, 0, BlockIds.Stone);
            region.StoreChunk(pos, first);

            var second = new ChunkColumn();
            second.SetBlock(0, 70, 0, BlockIds.Sand);
            region.StoreChunk(pos, second);

            Assert.That(region.ChunkCount, Is.EqualTo(1));
            Assert.That(region.TryGetChunk(pos, out ChunkColumn restored), Is.True);
            Assert.That(restored.GetBlock(0, 70, 0), Is.EqualTo(BlockIds.Sand));
        }

        [Test]
        public void RegionFor_GroupsChunksIntoThirtyTwoByThirtyTwoBlocks()
        {
            Assert.That(RegionFile.RegionFor(new ChunkPos(0, 0)), Is.EqualTo(new ChunkPos(0, 0)));
            Assert.That(RegionFile.RegionFor(new ChunkPos(31, 31)), Is.EqualTo(new ChunkPos(0, 0)));
            Assert.That(RegionFile.RegionFor(new ChunkPos(32, 0)), Is.EqualTo(new ChunkPos(1, 0)));
            Assert.That(RegionFile.RegionFor(new ChunkPos(-1, -1)), Is.EqualTo(new ChunkPos(-1, -1)),
                "负坐标应向下取整而非向零取整");
        }

        [Test]
        public void Load_RejectsStreamWithWrongMagic()
        {
            using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });

            Assert.Throws<InvalidDataException>(() => RegionFile.Load(stream));
        }

        [Test]
        public void Load_RejectsTruncatedStream()
        {
            var region = new RegionFile(new ChunkPos(0, 0));
            var column = new ChunkColumn();
            column.SetBlock(0, 70, 0, BlockIds.Stone);
            region.StoreChunk(new ChunkPos(0, 0), column);

            using var full = new MemoryStream();
            region.Save(full);
            byte[] bytes = full.ToArray();

            using var truncated = new MemoryStream(bytes, 0, bytes.Length - 10);

            Assert.Throws<InvalidDataException>(() => RegionFile.Load(truncated));
        }

        [Test]
        public void StoreChunk_BelongingToAnotherRegion_Throws()
        {
            var region = new RegionFile(new ChunkPos(0, 0));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => region.StoreChunk(new ChunkPos(32, 0), new ChunkColumn()));
        }

        [Test]
        public void StoreChunk_WithNullColumn_Throws()
        {
            var region = new RegionFile(new ChunkPos(0, 0));

            Assert.Throws<ArgumentNullException>(() => region.StoreChunk(new ChunkPos(0, 0), null));
        }
    }
}
