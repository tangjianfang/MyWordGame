using System;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    [TestFixture]
    public class ChunkColumnTests
    {
        private const ushort Stone = 1;

        [Test]
        public void HeightModel_CoversMinusSixtyFourToThreeTwenty()
        {
            Assert.That(VoxelCoords.MinY, Is.EqualTo(-64));
            Assert.That(VoxelCoords.MaxY, Is.EqualTo(320));
            Assert.That(VoxelCoords.WorldHeight, Is.EqualTo(384));
            Assert.That(VoxelCoords.SectionCount, Is.EqualTo(24));
        }

        [TestCase(-64, 0)]
        [TestCase(-49, 0)]
        [TestCase(-48, 1)]
        [TestCase(0, 4)]
        [TestCase(319, 23)]
        public void SectionIndexForY_MapsHeightOntoSections(int worldY, int expectedSection)
        {
            Assert.That(VoxelCoords.SectionIndexForY(worldY), Is.EqualTo(expectedSection));
        }

        [Test]
        public void NewColumn_ReadsAsAirAcrossFullHeight()
        {
            var column = new ChunkColumn();

            Assert.That(column.GetBlock(0, VoxelCoords.MinY, 0), Is.EqualTo(ChunkSection.AirId));
            Assert.That(column.GetBlock(15, VoxelCoords.MaxY - 1, 15), Is.EqualTo(ChunkSection.AirId));
            Assert.That(column.AllocatedSectionCount, Is.EqualTo(0), "全空气列不应分配任何 section");
        }

        [Test]
        public void SetBlock_AllocatesOnlyTheTouchedSection()
        {
            var column = new ChunkColumn();

            column.SetBlock(2, 5, 9, Stone);

            Assert.That(column.GetBlock(2, 5, 9), Is.EqualTo(Stone));
            Assert.That(column.AllocatedSectionCount, Is.EqualTo(1));
        }

        [Test]
        public void SetBlock_AtBothHeightExtremes_RoundTrips()
        {
            var column = new ChunkColumn();

            column.SetBlock(0, VoxelCoords.MinY, 0, Stone);
            column.SetBlock(15, VoxelCoords.MaxY - 1, 15, Stone);

            Assert.That(column.GetBlock(0, VoxelCoords.MinY, 0), Is.EqualTo(Stone));
            Assert.That(column.GetBlock(15, VoxelCoords.MaxY - 1, 15), Is.EqualTo(Stone));
            Assert.That(column.AllocatedSectionCount, Is.EqualTo(2));
        }

        [TestCase(-65)]
        [TestCase(320)]
        public void SetBlock_OutsideHeightRange_Throws(int worldY)
        {
            var column = new ChunkColumn();

            Assert.Throws<ArgumentOutOfRangeException>(() => column.SetBlock(0, worldY, 0, Stone));
        }

        [Test]
        public void GetBlock_OutsideHeightRange_ReturnsAirInsteadOfThrowing()
        {
            var column = new ChunkColumn();

            Assert.That(column.GetBlock(0, -65, 0), Is.EqualTo(ChunkSection.AirId));
            Assert.That(column.GetBlock(0, 320, 0), Is.EqualTo(ChunkSection.AirId));
        }
    }
}
