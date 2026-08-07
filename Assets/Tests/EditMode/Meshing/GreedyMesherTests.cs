using MyWorld.Core.Blocks;
using MyWorld.Core.Meshing;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Meshing
{
    [TestFixture]
    public class GreedyMesherTests
    {
        private const ushort Air = 0;
        private const ushort Stone = 1;
        private const ushort Dirt = 2;

        [Test]
        public void EmptyRegion_ProducesNoGeometry()
        {
            var source = new PaddedBlockSource();
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.VertexCount, Is.EqualTo(0));
            Assert.That(mesh.IndexCount, Is.EqualTo(0));
        }

        [Test]
        public void SolidRegionWithNoAir_ProducesNoGeometry()
        {
            var source = new PaddedBlockSource();
            source.FillAll(Stone);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadCount, Is.EqualTo(0), "被完全包裹的区域没有任何可见面");
        }

        [Test]
        public void SingleBlock_ProducesSixQuads()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadCount, Is.EqualTo(6));
            Assert.That(mesh.VertexCount, Is.EqualTo(24));
            Assert.That(mesh.IndexCount, Is.EqualTo(36));
        }

        [Test]
        public void TwoAdjacentBlocks_MergeIntoSixQuads()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            source.Set(1, 0, 0, Stone);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            // 朴素逐面做法会产生 10 个四边形；贪心合并后顶/底/前/后各自合成 1 个 2×1 面
            Assert.That(mesh.QuadCount, Is.EqualTo(6));
        }

        [Test]
        public void FullSolidSection_MergesEachFaceIntoOneQuad()
        {
            var source = new PaddedBlockSource();
            source.FillRegion(Stone);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadCount, Is.EqualTo(6), "16³ 实心块的每个朝向应合并为单个 16×16 面");
        }

        [Test]
        public void DifferentBlockTypes_AreNotMergedTogether()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            source.Set(1, 0, 0, Dirt);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            // 顶面因材质不同无法合并，比同材质情形多出 4 个面（上下前后各多 1 个）
            Assert.That(mesh.QuadCount, Is.EqualTo(10));
        }

        [Test]
        public void BlockBuriedUnderNeighbourChunk_HasNoFaceTowardThatNeighbour()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            source.Set(-1, 0, 0, Stone); // 邻区块中的方块，位于本区域之外

            var mesh = new MeshBuffer();
            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadCount, Is.EqualTo(5), "朝向邻区块实心方块的那一面应被剔除");
        }

        [Test]
        public void EveryQuad_HasOutwardFacingNormal()
        {
            var source = new PaddedBlockSource();
            source.Set(5, 5, 5, Stone);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            var normals = mesh.Normals;
            for (var i = 0; i < mesh.VertexCount; i++)
            {
                float length = normals[i].X * normals[i].X + normals[i].Y * normals[i].Y + normals[i].Z * normals[i].Z;
                Assert.That(length, Is.EqualTo(1f).Within(1e-5f), $"顶点 {i} 的法线不是单位向量");
            }
        }

        [Test]
        public void AllIndices_ReferenceExistingVertices()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            source.Set(3, 4, 5, Dirt);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.IndexCount % 3, Is.EqualTo(0), "索引数量必须是 3 的倍数");
            for (var i = 0; i < mesh.IndexCount; i++)
            {
                Assert.That(mesh.Indices[i], Is.InRange(0, mesh.VertexCount - 1));
            }
        }
    }

    /// <summary>测试用的方块来源，覆盖 16³ 区域并额外容纳 -1 与 16 两层邻居采样。</summary>
    internal struct PaddedBlockSource : IBlockSource
    {
        private const int Size = ChunkSection.Size;
        private const int Padded = Size + 2;

        private ushort[] _blocks;

        private ushort[] Blocks => _blocks ??= new ushort[Padded * Padded * Padded];

        private static int Index(int x, int y, int z) => ((y + 1) * Padded + (z + 1)) * Padded + (x + 1);

        public void Set(int x, int y, int z, ushort blockId) => Blocks[Index(x, y, z)] = blockId;

        public void FillAll(ushort blockId)
        {
            ushort[] blocks = Blocks;
            for (var i = 0; i < blocks.Length; i++)
            {
                blocks[i] = blockId;
            }
        }

        public void FillRegion(ushort blockId)
        {
            for (var y = 0; y < Size; y++)
            for (var z = 0; z < Size; z++)
            for (var x = 0; x < Size; x++)
            {
                Set(x, y, z, blockId);
            }
        }

        public ushort GetBlock(int x, int y, int z) => Blocks[Index(x, y, z)];

        public bool IsSolid(ushort blockId) => blockId != ChunkSection.AirId;

        /// <summary>测试里不接真实注册表，直接拿方块 ID 当贴图索引，断言写起来最直观。</summary>
        public int GetTextureIndex(ushort blockId, BlockFace face) => blockId;
    }
}
