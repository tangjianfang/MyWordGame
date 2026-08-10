using System.Collections.Generic;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    /// <summary>脏段收集：段内、上下边界、区块水平边界、以及角上的组合情况。</summary>
    [TestFixture]
    public class DirtySectionsTests
    {
        private readonly List<SectionRef> _result = new List<SectionRef>();

        private List<SectionRef> Collect(int x, int y, int z)
        {
            DirtySections.Collect(x, y, z, _result);
            return _result;
        }

        [Test]
        public void Collect_InsideSection_ReturnsOnlyItsOwnSection()
        {
            // (8, 72, 8) 在区块 (0,0)、段 8（y ∈ [64, 80)）的正中间
            Assert.That(Collect(8, 72, 8), Is.EqualTo(new[]
            {
                new SectionRef(new ChunkPos(0, 0), 8)
            }), "段内部的方块只牵连它自己所在的段");
        }

        [Test]
        public void Collect_AtSectionBottom_AlsoMarksSectionBelow()
        {
            List<SectionRef> dirty = Collect(8, 64, 8);

            Assert.That(dirty, Has.Count.EqualTo(2));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 7)),
                "贴着段底面的方块会改变下面那段顶面的可见性");
        }

        [Test]
        public void Collect_AtChunkEdge_AlsoMarksNeighbourChunk()
        {
            List<SectionRef> dirty = Collect(0, 72, 8);

            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(-1, 0), 8)),
                "贴着区块西边界的方块会改变西侧邻区块东面的可见性");
        }

        [Test]
        public void Collect_AtChunkCorner_MarksAllTouchedNeighbours()
        {
            // (0, 64, 0)：西、北、下三个方向同时贴边
            List<SectionRef> dirty = Collect(0, 64, 0);

            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(-1, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, -1), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 7)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(-1, 0), 7)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, -1), 7)));
        }

        [Test]
        public void Collect_NeverReturnsDuplicates()
        {
            Assert.That(Collect(15, 79, 15), Is.Unique, "同一个段不应当出现两次，否则会被重建两遍");
        }

        [Test]
        public void Collect_NeverReturnsSectionOutsideWorld()
        {
            foreach (SectionRef section in Collect(0, VoxelCoords.MinY, 0))
            {
                Assert.That(section.SectionIndex, Is.InRange(0, VoxelCoords.SectionCount - 1),
                    "世界最底层的下方没有段，不应当被收进来");
            }
        }

        [Test]
        public void Collect_ClearsPreviousResult()
        {
            Collect(8, 72, 8);
            List<SectionRef> second = Collect(100, 200, 100);

            Assert.That(second, Has.Count.EqualTo(1),
                "Collect 应当先清空传入的列表，否则连续调用会越积越多");
        }
    }
}
