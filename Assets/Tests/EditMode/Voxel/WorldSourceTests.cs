using System.Linq;
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

        /// <summary>
        /// m11 W1-5：九件家具方块 Solid=false——玩家必须能径直穿过摆进屋里的家具（不堵路）。
        /// 加载真实 blocks/*.json 而不是内联副本：谁把某个家具 JSON 的 solid 改成 true，
        /// 这里立刻红（与本文件内联注册表的正反向用例互不掩护）。
        /// </summary>
        [Test]
        public void SolidSource_RealFurnitureBlocks_DoNotBlockMovement()
        {
            var registry = BlockRegistry.FromJson(
                System.IO.Directory.GetFiles(LocateRealBlocksDirectory(), "*.json")
                    .Select(System.IO.File.ReadAllText));

            string[] furnitureIds =
            {
                "chair_block", "table_block", "office_desk_block", "laptop_block", "keyboard_block",
                "mouse_block", "notebook_block", "hacker_pc_block", "globe_block",
            };

            var world = new World();
            var source = new WorldSolidSource(world, registry);

            int x = -40;
            foreach (string id in furnitureIds)
            {
                ushort numericId = registry.GetById(id).NumericId;
                world.SetBlock(x, 70, 128, numericId);
                Assert.That(source.IsSolidAt(x, 70, 128), Is.False,
                    $"{id} 不应阻挡玩家移动（装饰性家具 Solid=false）");
                x++;
            }

            // 对照组：同一坐标系里石头仍挡路——证明上面不是「未加载区块恒 false」式的假绿
            world.SetBlock(x, 70, 128, BlockIds.Stone);
            Assert.That(source.IsSolidAt(x, 70, 128), Is.True, "对照组石头应照常阻挡移动");
        }

        /// <summary>dotnet 从测试输出目录向上爬找仓库；Unity 走 streamingAssetsPath。</summary>
        private static string LocateRealBlocksDirectory()
        {
#if UNITY_EDITOR
            return System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "blocks");
#else
            var directory = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = System.IO.Path.Combine(
                    directory.FullName, "Assets", "StreamingAssets", "blocks");
                if (System.IO.Directory.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new System.IO.DirectoryNotFoundException(
                "未能从测试输出目录向上找到 Assets/StreamingAssets/blocks。");
#endif
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
