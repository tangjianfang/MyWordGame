using MyWorld.Core.Physics;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Player
{
    /// <summary>由射线命中结果推出放置位置，并判定这次放置是否合法。</summary>
    public static class BlockPlacement
    {
        public static bool TryResolve(VoxelRayHit hit, Aabb playerBox, out int x, out int y, out int z)
        {
            x = 0;
            y = 0;
            z = 0;

            if (!hit.Hit)
            {
                return false;
            }

            // 沿命中面的法线往外挪一格，就是新方块该占的位置
            x = hit.X + hit.NormalX;
            y = hit.Y + hit.NormalY;
            z = hit.Z + hit.NormalZ;

            if (y < VoxelCoords.MinY || y >= VoxelCoords.MaxY)
            {
                return false;
            }

            // 这一条是必须的：不挡住的话玩家能把自己封进方块里出不来
            return !IntersectsPlayer(x, y, z, playerBox);
        }

        /// <summary>
        /// 格子与玩家包围盒是否重叠。用严格不等号——正好贴面（比如脚底那格的顶面 y = 64
        /// 对上包围盒底面 y = 64）不算重叠，否则站在地上时脚下一圈全都放不了。
        /// </summary>
        private static bool IntersectsPlayer(int x, int y, int z, Aabb box)
        {
            return box.Min.X < x + 1 && box.Max.X > x
                && box.Min.Y < y + 1 && box.Max.Y > y
                && box.Min.Z < z + 1 && box.Max.Z > z;
        }
    }
}