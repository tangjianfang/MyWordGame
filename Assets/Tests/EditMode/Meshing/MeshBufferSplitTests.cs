using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Meshing;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Meshing
{
    [TestFixture]
    public class MeshBufferSplitTests
    {
        private const ushort Stone = 1;
        private const ushort Dirt = 2;

        private static MeshBuffer BuildTwoBlocks()
        {
            var source = new PaddedBlockSource();
            source.Set(0, 0, 0, Stone);
            source.Set(2, 0, 0, Dirt);

            var mesh = new MeshBuffer();
            GreedyMesher.Build(source, mesh);
            return mesh;
        }

        [Test]
        public void SplitByTexture_OnEmptyBuffer_ProducesNoSubmesh()
        {
            var mesh = new MeshBuffer();
            var submeshes = new List<Submesh>();

            mesh.SplitByTexture(submeshes);

            Assert.That(submeshes, Is.Empty);
        }

        [Test]
        public void SplitByTexture_ProducesOneSubmeshPerDistinctTexture()
        {
            MeshBuffer mesh = BuildTwoBlocks();
            var submeshes = new List<Submesh>();

            mesh.SplitByTexture(submeshes);

            Assert.That(submeshes.Count, Is.EqualTo(2));
            Assert.That(submeshes[0].TextureIndex, Is.EqualTo((int)Stone));
            Assert.That(submeshes[1].TextureIndex, Is.EqualTo((int)Dirt), "段按贴图索引升序排列");
        }

        [Test]
        public void SplitByTexture_SubmeshesCoverTheWholeIndexBufferWithoutOverlap()
        {
            MeshBuffer mesh = BuildTwoBlocks();
            var submeshes = new List<Submesh>();

            mesh.SplitByTexture(submeshes);

            var cursor = 0;
            foreach (Submesh submesh in submeshes)
            {
                Assert.That(submesh.IndexStart, Is.EqualTo(cursor), "各段必须首尾相接，不留空隙");
                cursor += submesh.IndexCount;
            }

            Assert.That(cursor, Is.EqualTo(mesh.IndexCount));
        }

        [Test]
        public void SplitByTexture_EveryIndexInASubmeshBelongsToThatTexture()
        {
            MeshBuffer mesh = BuildTwoBlocks();
            var submeshes = new List<Submesh>();

            mesh.SplitByTexture(submeshes);

            foreach (Submesh submesh in submeshes)
            {
                for (int i = submesh.IndexStart; i < submesh.IndexStart + submesh.IndexCount; i++)
                {
                    int quad = mesh.Indices[i] / 4;
                    Assert.That(mesh.QuadTextures[quad], Is.EqualTo(submesh.TextureIndex),
                        $"第 {i} 个索引指向的 quad 贴图与所在段不符");
                }
            }
        }

        [Test]
        public void SplitByTexture_KeepsTheSameSetOfTriangles()
        {
            MeshBuffer mesh = BuildTwoBlocks();
            var before = new List<int>(mesh.Indices);

            mesh.SplitByTexture(new List<Submesh>());

            Assert.That(mesh.Indices, Is.EquivalentTo(before), "重排只能改顺序，不能增删索引");
            Assert.That(mesh.VertexCount, Is.EqualTo(mesh.Positions.Count), "顶点缓冲不参与重排");
        }

        [Test]
        public void SplitByTexture_IsIdempotent()
        {
            MeshBuffer mesh = BuildTwoBlocks();
            var first = new List<Submesh>();
            var second = new List<Submesh>();

            mesh.SplitByTexture(first);
            var afterFirst = new List<int>(mesh.Indices);
            mesh.SplitByTexture(second);

            Assert.That(mesh.Indices, Is.EqualTo(afterFirst), "已经排好的缓冲再拆一次结果不变");
            Assert.That(second.Count, Is.EqualTo(first.Count));
        }
    }
}
