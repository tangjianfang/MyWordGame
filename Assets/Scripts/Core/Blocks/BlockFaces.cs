using System;

namespace MyWorld.Core.Blocks
{
    /// <summary>
    /// 轴向与 <see cref="BlockFace"/> 的换算。贪心网格按 0/1/2 三个轴遍历，
    /// 而贴图是按面查的，两边的对应关系只在这里定义一次。
    /// </summary>
    public static class BlockFaces
    {
        /// <param name="axis">0 = X，1 = Y，2 = Z。</param>
        /// <param name="facingPositive">面的法线是否指向该轴的正方向。</param>
        public static BlockFace FromAxis(int axis, bool facingPositive)
        {
            switch (axis)
            {
                case 0:
                    return facingPositive ? BlockFace.East : BlockFace.West;
                case 1:
                    return facingPositive ? BlockFace.Top : BlockFace.Bottom;
                case 2:
                    return facingPositive ? BlockFace.North : BlockFace.South;
                default:
                    throw new ArgumentOutOfRangeException(nameof(axis), axis, "轴只能是 0、1、2。");
            }
        }
    }
}
