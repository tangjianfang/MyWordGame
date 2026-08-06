using MyWorld.Core.Blocks;
using MyWorld.Core.Meshing;
using MyWorld.Core.Physics;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    [TestFixture]
    public class WorldSourceTests
    {
        private BlockRegistry _registry;

        [SetUp]
        public void BuildRegistry()
        {
            _registry = BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" }, ""solid"": true, ""opaque"": true }",
                @"{ ""id"": ""water"", ""numericId"": 5, ""textures"": { ""all"": ""water"" }, ""solid"": false, ""opaque"": false }"
            });
        }

        [Test]
        public void TryGetByNumericId_ReportsWhetherBlockIsRegistered()
        {
            Assert.That(_registry.TryGetByNumericId(1, out BlockDefinition stone), Is.True);
            Assert.That(stone.Id, Is.EqualTo("stone"));
            Assert.That(_registry.TryGetByNumericId(999, out _), Is.False);
        }

        // ---- ChunkMeshSource ----

        [Test]
        public void MeshSource_ReadsBlocksInsideItsOwnSection()
        {
            var world = new World();
            world.SetBlock(3, 64, 5, BlockIds.Stone);

            var source = new ChunkMeshSource(world, _registry, new ChunkPos(0, 0), sectionBaseY: 64);

            Assert.That(source.GetBlock(3, 0, 5), Is.EqualTo(BlockIds.Stone));
            Assert.That(source.GetBlock(3, 1, 5), Is.EqualTo(BlockIds.Air));
        }

        [Test]
        public void MeshSource_SamplesAcrossChunkBoundary()
        {
            var world = new World();
            world.SetBlock(-1, 64, 0, BlockIds.Stone); // 属于区块 (-1, 0)

            var source = new ChunkMeshSource(world, _registry, new ChunkPos(0, 0), sectionBaseY: 64);

            Assert.That(source.GetBlock(-1, 0, 0), Is.EqualTo(BlockIds.Stone),
                "x=-1 必须读到相邻区块，否则区块接缝处会多出一层面");
        }

        [Test]
        public void MeshSource_SamplesAcrossSectionBoundaryVertically()
        {
            var world = new World();
            world.SetBlock(0, 63, 0, BlockIds.Stone); // 下一段
            world.SetBlock(0, 80, 0, BlockIds.Stone); // 上一段

            var source = new ChunkMeshSource(world, _registry, new ChunkPos(0, 0), sectionBaseY: 64);

            Assert.That(source.GetBlock(0, -1, 0), Is.EqualTo(BlockIds.Stone));
            Assert.That(source.GetBlock(0, 16, 0), Is.EqualTo(BlockIds.Stone));
        }

        [Test]
        public void MeshSource_BeyondWorldHeight_ReadsAsAir()
        {
            var world = new World();
            var bottom = new ChunkMeshSource(world, _registry, new ChunkPos(0, 0), VoxelCoords.MinY);

            Assert.That(bottom.GetBlock(0, -1, 0), Is.EqualTo(BlockIds.Air),
                "世界底部之下应视为空气，避免网格生成时越界");
        }

        [Test]
        public void MeshSource_UsesOpacityFromRegistryRatherThanHardcodingAir()
        {
            var world = new World();
            var source = new ChunkMeshSource(world, _registry, new ChunkPos(0, 0), sectionBaseY: 64);

            Assert.That(source.IsSolid(BlockIds.Stone), Is.True);
            Assert.That(source.IsSolid(BlockIds.Air), Is.False);
            Assert.That(source.IsSolid(BlockIds.Water), Is.False,
                "水不透明会挡住水下的地形，不透明度必须来自注册表");
        }

        [Test]
        public void MeshSource_UnknownBlockId_IsTreatedAsTransparent()
        {
            var world = new World();
            var source = new ChunkMeshSource(world, _registry, new ChunkPos(0, 0), sectionBaseY: 64);

            Assert.That(source.IsSolid(999), Is.False, "未注册方块不应让网格生成崩溃");
        }

        [Test]
        public void MeshSource_FeedsGreedyMesherAcrossChunkSeamCorrectly()
        {
            var world = new World();
            world.SetBlock(0, 64, 0, BlockIds.Stone);
            world.SetBlock(-1, 64, 0, BlockIds.Stone); // 邻区块，应遮住 -X 面

            var source = new ChunkMeshSource(world, _registry, new ChunkPos(0, 0), sectionBaseY: 64);
            var mesh = new MeshBuffer();

            GreedyMesher.Build(source, mesh);

            Assert.That(mesh.QuadCount, Is.EqualTo(5), "朝向邻区块实心方块的面应被剔除");
        }

        // ---- WorldSolidSource ----

        [Test]
        public void SolidSource_UsesWorldCoordinatesDirectly()
        {
            var world = new World();
            world.SetBlock(-40, 70, 128, BlockIds.Stone);

            var source = new WorldSolidSource(world, _registry);

            Assert.That(source.IsSolidAt(-40, 70, 128), Is.True);
            Assert.That(source.IsSolidAt(-40, 71, 128), Is.False);
        }

        [Test]
        public void SolidSource_TreatsWaterAsPassable()
        {
            var world = new World();
            world.SetBlock(0, 62, 0, BlockIds.Water);

            var source = new WorldSolidSource(world, _registry);

            Assert.That(source.IsSolidAt(0, 62, 0), Is.False, "玩家应能走进水里而不是站在水面上");
        }

        [Test]
        public void SolidSource_UnloadedChunk_IsNotSolid()
        {
            var source = new WorldSolidSource(new World(), _registry);

            Assert.That(source.IsSolidAt(99999, 70, -99999), Is.False);
        }

        [Test]
        public void SolidSource_DrivesRaycasterAgainstRealWorld()
        {
            var world = new World();
            world.SetBlock(5, 70, 0, BlockIds.Stone);

            var source = new WorldSolidSource(world, _registry);
            VoxelRayHit hit = VoxelRaycaster.Cast(source,
                new Math.Float3(0.5f, 70.5f, 0.5f), new Math.Float3(1f, 0f, 0f), 20f);

            Assert.That(hit.Hit, Is.True);
            Assert.That(new[] { hit.X, hit.Y, hit.Z }, Is.EqualTo(new[] { 5, 70, 0 }));
            Assert.That(new[] { hit.PlacementX, hit.PlacementY, hit.PlacementZ }, Is.EqualTo(new[] { 4, 70, 0 }));
        }

        [Test]
        public void SolidSource_DrivesCollisionAgainstRealWorld()
        {
            var world = new World();
            for (var x = -2; x <= 2; x++)
            for (var z = -2; z <= 2; z++)
            {
                world.SetBlock(x, 70, z, BlockIds.Stone);
            }

            var source = new WorldSolidSource(world, _registry);
            Aabb box = Aabb.FromBottomCenter(new Math.Float3(0.5f, 75f, 0.5f), 0.6f, 1.8f);

            MoveResult result = VoxelCollision.Move(source, box, new Math.Float3(0f, -10f, 0f));

            Assert.That(result.IsGrounded, Is.True);
            Assert.That(result.Delta.Y, Is.EqualTo(-4f).Within(0.01f), "应停在方块顶面 y=71");
        }
    }
}
