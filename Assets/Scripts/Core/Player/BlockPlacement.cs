using MyWorld.Core.Physics;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Player
{
    /// <summary>由射线命中结果推出放置位置，并判定这次放置是否合法。</summary>
    public static class BlockPlacement
    {
        /// <summary>
        /// 由射线命中结果推出放置位置，并判定这次放置是否合法。
        /// <para>
        /// m12 P0-b 起带 <paramref name="allowTowerUp"/>：腾空垫脚（跳跃中把方块垫进脚下格，
        /// 解决"只能平铺、不能向上搭塔"）。真实放置路由传 true；守卫测试默认 false 保持
        /// 旧契约逐值不变。
        /// </para>
        /// </summary>
        public static bool TryResolve(VoxelRayHit hit, Aabb playerBox, out int x, out int y, out int z,
            bool allowTowerUp = false)
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
            if (!IntersectsPlayer(x, y, z, playerBox))
            {
                return true;
            }

            // m12 P0-b：腾空垫脚放宽——命中格与玩家盒重叠，但玩家正跳跃在它上方
            //（0.5 格容差 ≈ 最高点附近）且格子水平上在脚下，允许垫进去。
            // 放置成功后 Unity 侧一次性把玩家抬到新块顶（BlockInteraction.UseAt），
            // 0.5 容差允许脚低于块顶时就放，靠物理解算会把人卡进方块里。
            return allowTowerUp && IsTowerUpPlacement(x, y, z, playerBox);
        }

        /// <summary>腾空垫脚的脚底容差（格）：玩家盒底高于目标格顶 - 本值即视作"在其上方"。</summary>
        public const float TowerUpFootTolerance = 0.5f;

        /// <summary>
        /// 腾空垫脚判定（m12 P0-b）：目标格水平上与玩家盒重叠（在脚下而不是旁边），
        /// 且玩家 AABB 底 ≥ 目标格顶 - <see cref="TowerUpFootTolerance"/>。
        /// 站立时双脚贴地（盒底 == 地表），腿部格顶远在盒底 +0.5 之上 → 不满足，
        /// 仍然全禁（防自封）；跳跃最高点（盒底 ≈ 地表 +1.25）恰好落进容差窗 → 允许。
        /// </summary>
        public static bool IsTowerUpPlacement(int x, int y, int z, Aabb box)
        {
            bool horizontalOverlap = box.Min.X < x + 1 && box.Max.X > x
                && box.Min.Z < z + 1 && box.Max.Z > z;
            bool footAboveTargetTop = box.Min.Y >= y + 1f - TowerUpFootTolerance;
            return horizontalOverlap && footAboveTargetTop;
        }

        /// <summary>
        /// 格子与玩家包围盒是否重叠。用严格不等号——正好贴面（比如脚底那格的顶面 y = 64
        /// 对上包围盒底面 y = 64）不算重叠，否则站在地上时脚下一圈全都放不了。
        /// <para>
        /// m11 W3-1 起公开：放置路由对床/门这类<b>多格方块</b>的额外占据格
        /// （床头格 / 门上格）也要过同一条防卡身判定——放固体方块进玩家身体所在格
        /// 会把人封死在里面，多格放置的每一格都复用这一份判定，别各写一份漂移。
        /// </para>
        /// </summary>
        public static bool IntersectsPlayer(int x, int y, int z, Aabb box)
        {
            return box.Min.X < x + 1 && box.Max.X > x
                && box.Min.Y < y + 1 && box.Max.Y > y
                && box.Min.Z < z + 1 && box.Max.Z > z;
        }
    }
}
