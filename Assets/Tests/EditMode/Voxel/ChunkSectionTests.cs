using System;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    [TestFixture]
    public class ChunkSectionTests
    {
        private const ushort Air = 0;
        private const ushort Stone = 1;
        private const ushort Dirt = 2;

        [Test]
        public void NewSection_IsUniformAir()
        {
            var section = new ChunkSection();

            Assert.That(section.IsUniform, Is.True);
            Assert.That(section.IsEmpty, Is.True);
            Assert.That(section.Get(0, 0, 0), Is.EqualTo(Air));
            Assert.That(section.Get(15, 15, 15), Is.EqualTo(Air));
        }

        [Test]
        public void Set_ThenGet_ReturnsStoredBlock()
        {
            var section = new ChunkSection();

            section.Set(3, 7, 11, Stone);

            Assert.That(section.Get(3, 7, 11), Is.EqualTo(Stone));
            Assert.That(section.Get(3, 7, 10), Is.EqualTo(Air));
            Assert.That(section.IsEmpty, Is.False);
        }

        [Test]
        public void Set_SecondDistinctBlock_EndsUniformState()
        {
            var section = new ChunkSection();

            section.Set(0, 0, 0, Stone);

            Assert.That(section.IsUniform, Is.False);
            Assert.That(section.PaletteCount, Is.EqualTo(2), "空气与石头各占一个调色板槽位");
        }

        [Test]
        public void Set_SameBlockEverywhere_KeepsPaletteSmall()
        {
            var section = new ChunkSection();

            for (var y = 0; y < ChunkSection.Size; y++)
            for (var z = 0; z < ChunkSection.Size; z++)
            for (var x = 0; x < ChunkSection.Size; x++)
            {
                section.Set(x, y, z, Stone);
            }

            Assert.That(section.Get(9, 9, 9), Is.EqualTo(Stone));
            Assert.That(section.PaletteCount, Is.LessThanOrEqualTo(2));
            Assert.That(section.BitsPerEntry, Is.LessThanOrEqualTo(1));
        }

        // 期望位宽按调色板总量（含空气）推导：4→2bit, 6→4bit, 18→8bit, 301→16bit
        [TestCase(3, 2)]
        [TestCase(5, 4)]
        [TestCase(17, 8)]
        [TestCase(300, 16)]
        public void Palette_GrowsBitsPerEntryToNextSupportedWidth(int distinctBlocks, int expectedBits)
        {
            var section = new ChunkSection();

            for (var i = 0; i < distinctBlocks; i++)
            {
                section.Set(i % 16, i / 256, (i / 16) % 16, (ushort)(i + 1));
            }

            Assert.That(section.BitsPerEntry, Is.EqualTo(expectedBits));
        }

        [Test]
        public void RandomWrites_AreReadBackExactly()
        {
            var section = new ChunkSection();
            var expected = new ushort[ChunkSection.Volume];
            var random = new Random(20260806);

            for (var i = 0; i < 20000; i++)
            {
                int x = random.Next(ChunkSection.Size);
                int y = random.Next(ChunkSection.Size);
                int z = random.Next(ChunkSection.Size);
                var id = (ushort)random.Next(0, 40);

                section.Set(x, y, z, id);
                expected[ChunkSection.Index(x, y, z)] = id;
            }

            for (var y = 0; y < ChunkSection.Size; y++)
            for (var z = 0; z < ChunkSection.Size; z++)
            for (var x = 0; x < ChunkSection.Size; x++)
            {
                Assert.That(section.Get(x, y, z), Is.EqualTo(expected[ChunkSection.Index(x, y, z)]),
                    $"位置 ({x},{y},{z}) 读回值与写入值不一致");
            }
        }

        [Test]
        public void Set_OutOfRange_Throws()
        {
            var section = new ChunkSection();

            Assert.Throws<ArgumentOutOfRangeException>(() => section.Set(16, 0, 0, Dirt));
            Assert.Throws<ArgumentOutOfRangeException>(() => section.Set(0, -1, 0, Dirt));
        }
    }
}
