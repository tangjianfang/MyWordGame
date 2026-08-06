using MyWorld.Core.Blocks;
using MyWorld.Core.Physics;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 把 <see cref="World"/> 包装成射线拾取与碰撞所需的实心判定源，按世界坐标寻址。
    /// </summary>
    public readonly struct WorldSolidSource : ISolidBlockSource
    {
        private readonly World _world;
        private readonly BlockRegistry _registry;

        public WorldSolidSource(World world, BlockRegistry registry)
        {
            _world = world;
            _registry = registry;
        }

        /// <summary>此处的"实心"指是否阻挡移动：水可穿过，与网格生成用的不透明度判定不同。</summary>
        public bool IsSolidAt(int x, int y, int z)
        {
            ushort block = _world.GetBlock(x, y, z);
            return _registry.TryGetByNumericId(block, out BlockDefinition definition) && definition.Solid;
        }
    }
}
