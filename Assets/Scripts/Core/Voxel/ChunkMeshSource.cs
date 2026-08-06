using MyWorld.Core.Blocks;
using MyWorld.Core.Meshing;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 把 <see cref="World"/> 的某一段区块包装成网格生成所需的数据源。
    /// 坐标越出 [0,16) 时自动落到相邻区块或相邻段，因此接缝处的面能被正确剔除。
    /// </summary>
    public readonly struct ChunkMeshSource : IBlockSource
    {
        private readonly World _world;
        private readonly BlockRegistry _registry;
        private readonly int _originX;
        private readonly int _originZ;
        private readonly int _originY;

        public ChunkMeshSource(World world, BlockRegistry registry, ChunkPos chunk, int sectionBaseY)
        {
            _world = world;
            _registry = registry;
            _originX = chunk.X * VoxelCoords.ChunkSize;
            _originZ = chunk.Z * VoxelCoords.ChunkSize;
            _originY = sectionBaseY;
        }

        public ushort GetBlock(int x, int y, int z)
            => _world.GetBlock(_originX + x, _originY + y, _originZ + z);

        /// <summary>此处的"实心"指是否遮挡视线：水不透明会挡住水下地形，与碰撞用的判定不同。</summary>
        public bool IsSolid(ushort blockId)
            => _registry.TryGetByNumericId(blockId, out BlockDefinition definition) && definition.Opaque;
    }
}
