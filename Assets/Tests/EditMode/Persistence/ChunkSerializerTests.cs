using System;
using System.IO;
using MyWorld.Core.Persistence;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Persistence
{
    [TestFixture]
    public class ChunkSerializerTests
    {
        [Test]
        public void RoundTrip_EmptyColumn_YieldsEmptyColumn()
        {
            var original = new ChunkColumn();

            ChunkColumn restored = ChunkSerializer.Deserialize(ChunkSerializer.Serialize(original));

            Assert.That(restored.AllocatedSectionCount, Is.EqualTo(0));
            Assert.That(restored.GetBlock(0, 0, 0), Is.EqualTo(BlockIds.Air));
        }

        [Test]
        public void RoundTrip_PreservesEveryBlock()
        {
            var original = new ChunkColumn();
            var random = new Random(4242);
            for (var i = 0; i < 3000; i++)
            {
                original.SetBlock(random.Next(16), random.Next(VoxelCoords.MinY, VoxelCoords.MaxY), random.Next(16),
                    (ushort)random.Next(1, 12));
            }

            ChunkColumn restored = ChunkSerializer.Deserialize(ChunkSerializer.Serialize(original));

            for (int y = VoxelCoords.MinY; y < VoxelCoords.MaxY; y++)
            for (var z = 0; z < 16; z++)
            for (var x = 0; x < 16; x++)
            {
                Assert.That(restored.GetBlock(x, y, z), Is.EqualTo(original.GetBlock(x, y, z)),
                    $"位置 ({x},{y},{z}) 未能还原");
            }
        }

        [Test]
        public void RoundTrip_PreservesExtremeHeights()
        {
            var original = new ChunkColumn();
            original.SetBlock(0, VoxelCoords.MinY, 0, BlockIds.Bedrock);
            original.SetBlock(15, VoxelCoords.MaxY - 1, 15, BlockIds.Stone);

            ChunkColumn restored = ChunkSerializer.Deserialize(ChunkSerializer.Serialize(original));

            Assert.That(restored.GetBlock(0, VoxelCoords.MinY, 0), Is.EqualTo(BlockIds.Bedrock));
            Assert.That(restored.GetBlock(15, VoxelCoords.MaxY - 1, 15), Is.EqualTo(BlockIds.Stone));
        }

        [Test]
        public void Serialize_SkipsEmptySections_KeepingSparseColumnsSmall()
        {
            var sparse = new ChunkColumn();
            sparse.SetBlock(0, 0, 0, BlockIds.Stone);

            var dense = new ChunkColumn();
            for (int y = VoxelCoords.MinY; y < VoxelCoords.MaxY; y++)
            {
                dense.SetBlock(0, y, 0, BlockIds.Stone);
            }

            Assert.That(ChunkSerializer.Serialize(sparse).Length,
                Is.LessThan(ChunkSerializer.Serialize(dense).Length),
                "只含一个方块的列不应与贯穿整个高度的列体积相当");
        }

        [Test]
        public void Deserialize_RejectsUnknownFormatVersion()
        {
            byte[] data = ChunkSerializer.Serialize(new ChunkColumn());
            data[0] = 99;

            Assert.Throws<InvalidDataException>(() => ChunkSerializer.Deserialize(data));
        }

        [Test]
        public void Deserialize_RejectsTruncatedData()
        {
            var column = new ChunkColumn();
            column.SetBlock(3, 20, 4, BlockIds.Dirt);
            byte[] data = ChunkSerializer.Serialize(column);

            var truncated = new byte[data.Length / 2];
            Array.Copy(data, truncated, truncated.Length);

            Assert.Throws<InvalidDataException>(() => ChunkSerializer.Deserialize(truncated));
        }
    }
}
