using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Environment
{
    /// <summary>
    /// 玩家右键 sapling 方块（numericId=1003）时，立刻把它长成一棵树。
    /// </summary>
    [RequireComponent(typeof(MyWorld.Unity.Player.PlayerController))]
    public sealed class SaplingGrowth : MonoBehaviour
    {
        [SerializeField] private int worldSeed = 20260806;
        [SerializeField] private ushort saplingId = TreeFeature.SaplingId;

        private MyWorld.Unity.Player.PlayerController _player;
        private World _world;
        private BlockRegistry _registry;
        private ChunkViewRegistry _views;

        public void Bind(World world, BlockRegistry registry, ChunkViewRegistry views)
        {
            _player = GetComponent<MyWorld.Unity.Player.PlayerController>();
            _world = world;
            _registry = registry;
            _views = views;
        }

        private void Update()
        {
            if (_world == null || _player == null || _player.Eye == null) return;

            // 复用 BlockInteraction 的射线太耦合，这里起一条独立短射线避免循环依赖
            if (!Input.GetMouseButtonDown(1)) return;

            var source = new WorldSolidSource(_world, _registry);
            Float3 origin = new Float3(_player.Eye.position.x, _player.Eye.position.y, _player.Eye.position.z);
            Float3 direction = new Float3(_player.Eye.forward.x, _player.Eye.forward.y, _player.Eye.forward.z);
            var hit = VoxelRaycaster.Cast(source, origin, direction, _player.Settings.ReachDistance);
            if (!hit.Hit) return;

            // 命中方块必须是 sapling
            var id = _world.GetBlock(hit.X, hit.Y, hit.Z);
            if (id != saplingId) return;

            // 在 (hit.X, hit.Z) 上强制放一棵树：先清掉树苗，再直接填树干与叶
            _world.SetBlock(hit.X, hit.Y, hit.Z, BlockIds.Air);
            _views.MarkBlockChanged(hit.X, hit.Y, hit.Z);

            ForceGrowAt(hit.X, hit.Z);
        }

        /// <summary>
        /// 强制生成一棵树（不依赖 hash 命中）。逻辑与 <see cref="TreeFeature.TryGenerate"/> 同形但
        /// 跳过 skip-hash 分支，给玩家右键即长。
        /// </summary>
        private void ForceGrowAt(int worldX, int worldZ)
        {
            int chunkX = VoxelCoords.WorldToChunk(worldX);
            int chunkZ = VoxelCoords.WorldToChunk(worldZ);
            if (!_world.TryGetChunk(new ChunkPos(chunkX, chunkZ), out var column)) return;

            int localX = VoxelCoords.WorldToLocal(worldX);
            int localZ = VoxelCoords.WorldToLocal(worldZ);

            int surfaceY = -1;
            for (int y = VoxelCoords.MaxY - 1; y >= VoxelCoords.MinY; y--)
            {
                var id = column.GetBlock(localX, y, localZ);
                if (id != BlockIds.Air && id != BlockIds.Water)
                {
                    surfaceY = y;
                    break;
                }
            }
            if (surfaceY < 0) return;

            // 用 seed 决定树干高度
            int trunkHeight = 5 + System.Math.Abs(Hash2D(worldSeed ^ 0x713A, worldX, worldZ)) % 3;

            // 树干
            for (int y = surfaceY + 1; y <= surfaceY + trunkHeight; y++)
            {
                column.SetBlock(localX, y, localZ, TreeFeature.LogId);
                _views.MarkBlockChanged(worldX, y, worldZ);
            }

            // 叶冠 3 层
            for (int layer = 0; layer < TreeFeature.LeafLayers; layer++)
            {
                int y = surfaceY + trunkHeight - layer;
                int radius = (layer == 0) ? 1 : TreeFeature.LeafRadius;
                for (int dx = -radius; dx <= radius; dx++)
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (dx * dx + dz * dz > radius * radius + 1) continue;
                    int tx = localX + dx;
                    int tz = localZ + dz;
                    if (tx < 0 || tx >= 16 || tz < 0 || tz >= 16) continue;
                    int wx = chunkX * VoxelCoords.ChunkSize + tx;
                    int wz = chunkZ * VoxelCoords.ChunkSize + tz;
                    if (column.GetBlock(tx, y, tz) == BlockIds.Air)
                    {
                        column.SetBlock(tx, y, tz, TreeFeature.LeavesId);
                        _views.MarkBlockChanged(wx, y, wz);
                    }
                }
            }

            // 顶部冠
            int topY = surfaceY + trunkHeight + 1;
            if (topY <= VoxelCoords.MaxY)
            {
                if (column.GetBlock(localX, topY, localZ) == BlockIds.Air)
                {
                    column.SetBlock(localX, topY, localZ, TreeFeature.LeavesId);
                    _views.MarkBlockChanged(worldX, topY, worldZ);
                }
            }
        }

        private static int Hash2D(int seed, int x, int z)
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ x;
                h = (h * 397) ^ z;
                h ^= h >> 13;
                h *= 0x5BD1E995;
                h ^= h >> 15;
                return h & 0x7FFFFFFF;
            }
        }
    }
}