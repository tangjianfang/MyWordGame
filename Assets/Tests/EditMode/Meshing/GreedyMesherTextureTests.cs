using MyWorld.Core.Blocks;
using MyWorld.Core.Meshing;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Meshing
{
    /// <summary>
    /// 验证贪心合并后每个 quad 都带上了正确的贴图索引。
    /// <see cref="PaddedBlockSource"/> 直接拿方块 ID 当贴图索引，因此断言可以直接写方块 ID。
    /// </summary>
    [TestFixture]
    public class GreedyMesherTextureTests
    {
        private const ushort Stone = 1;
        private const ushort Dirt = 2;

        [Test]
        public void QuadTextures_HasOneEntryPerQuad()
        {
            var source = new PaddedBlockSource();
            source.FillRegion(Stone);

            var mesh = new MeshBuffer();
            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadTextures.Count, Is.EqualTo(mesh.QuadCount),
                "贴图索引必须与 quad 一一对应");
        }

        [Test]
        public void SingleBlockType_AllQuadsShareOneTexture()
        {
            var source = new PaddedBlockSource();
            source.FillRegion(Stone);

            var mesh = new MeshBuffer();
            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadTextures, Is.All.EqualTo((int)Stone));
        }

        [Test]
        public void TwoBlockTypes_EachQuadCarriesItsOwnBlocksTexture()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            source.Set(2, 0, 0, Dirt);

            var mesh = new MeshBuffer();
            GreedyMesher.Build(source, mesh);

            // 两个互不相邻的孤立方块，各出 6 个面
            Assert.That(mesh.QuadCount, Is.EqualTo(12));
            Assert.That(mesh.QuadTextures.FindAll(t => t == Stone).Count, Is.EqualTo(6));
            Assert.That(mesh.QuadTextures.FindAll(t => t == Dirt).Count, Is.EqualTo(6));
        }

        [Test]
        public void Clear_AlsoResetsQuadTextures()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);

            var mesh = new MeshBuffer();
            GreedyMesher.Build(source, mesh);
            Assert.That(mesh.QuadTextures, Is.Not.Empty);

            mesh.Clear();

            Assert.That(mesh.QuadTextures, Is.Empty, "复用缓冲前必须清空，否则索引会与顶点错位");
        }

        [Test]
        public void FaceTexture_IsLookedUpWithTheOwningBlock_NotTheNeighbour()
        {
            var source = new PaddedBlockSource();
            source.FillRegion(Stone);
            // 在实心体内挖一个洞：洞的六个面属于周围的石头，而不是空气
            source.Set(8, 8, 8, ChunkSection.AirId);

            var mesh = new MeshBuffer();
            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadTextures, Is.All.EqualTo((int)Stone),
                "朝向负方向的面归属于 far 侧的方块，索引不能取成空气");
        }
    }
}
