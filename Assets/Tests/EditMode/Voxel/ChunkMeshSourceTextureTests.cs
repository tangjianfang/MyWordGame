using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    [TestFixture]
    public class ChunkMeshSourceTextureTests
    {
        private const string AirJson = @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }";

        private const string StoneJson =
            @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }";

        private const string GrassJson =
            @"{ ""id"": ""grass"", ""numericId"": 2,
                ""textures"": { ""top"": ""grass-top"", ""bottom"": ""dirt"", ""side"": ""grass-side"" } }";

        private static ChunkMeshSource CreateSource(out BlockRegistry registry)
        {
            registry = BlockRegistry.FromJson(new[] { AirJson, StoneJson, GrassJson });
            return new ChunkMeshSource(new World(), registry, new ChunkPos(0, 0), 0);
        }

        [Test]
        public void GetTextureIndex_MatchesRegistry()
        {
            ChunkMeshSource source = CreateSource(out BlockRegistry registry);

            foreach (BlockFace face in new[] { BlockFace.Top, BlockFace.Bottom, BlockFace.East })
            {
                Assert.That(source.GetTextureIndex(2, face), Is.EqualTo(registry.GetTextureIndex(2, face)),
                    $"{face} 面的贴图索引应当直接透传注册表的结果");
            }
        }

        [Test]
        public void GetTextureIndex_ForAir_IsNoTextureIndex()
        {
            ChunkMeshSource source = CreateSource(out BlockRegistry _);

            Assert.That(source.GetTextureIndex(0, BlockFace.Top), Is.EqualTo(BlockRegistry.NoTextureIndex));
        }
    }
}
