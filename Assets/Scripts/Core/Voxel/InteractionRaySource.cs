using MyWorld.Core.Blocks;
using MyWorld.Core.Physics;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 玩家交互射线的数据源（m11 ②）：在 <see cref="WorldSolidSource"/> 的「挡移动」判定之上，
    /// 让<b>非液体的造型方块</b>（作物 / 花草 / 火把 / 家具这类 <c>solid=false</c> 的装饰方块）
    /// 也可以被准星命中——否则骨粉催熟、成熟收获、拆花草家具全都打不中目标
    /// （<see cref="WorldSolidSource"/> 会穿过去命中脚下的耕地/地板，目标格判断整个落空）。
    /// 水仍视为虚的（Liquid 方块不命中）：不能对着水面拆/放方块的行为保持不变。
    /// <para>
    /// 与另两处「实心」语义三分，别混用：<see cref="WorldSolidSource"/> 判 Solid（挡移动，
    /// 碰撞/穿墙判定用）；<see cref="ChunkMeshSource"/> 判 Opaque（挡视线，网格剔除用）；
    /// 本源判「玩家能不能拿准星指着它」（交互拾取专用）。
    /// </para>
    /// <para>
    /// 照 <see cref="WorldSolidSource"/> 同款 readonly struct + 泛型约束写法，
    /// 让 <see cref="VoxelRaycaster.Cast{TSource}"/> 特化掉虚调用。
    /// </para>
    /// </summary>
    public readonly struct InteractionRaySource : ISolidBlockSource
    {
        private readonly World _world;
        private readonly BlockRegistry _registry;

        public InteractionRaySource(World world, BlockRegistry registry)
        {
            _world = world;
            _registry = registry;
        }

        /// <summary>命中判定：实心方块，或「不挡视线的非液体造型方块」（作物/花草/火把/家具）；空气与液体不命中。</summary>
        public bool IsSolidAt(int x, int y, int z)
        {
            ushort block = _world.GetBlock(x, y, z);
            if (block == BlockIds.Air)
            {
                return false;
            }

            if (!_registry.TryGetByNumericId(block, out BlockDefinition definition))
            {
                return true; // 未注册方块视同实心（保守：宁可多停一格，不穿模）
            }

            return definition.Solid || (!definition.Opaque && !definition.Liquid);
        }
    }
}
