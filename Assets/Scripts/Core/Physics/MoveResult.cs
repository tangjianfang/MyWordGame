using MyWorld.Core.Math;

namespace MyWorld.Core.Physics
{
    /// <summary>碰撞解算后的移动结果。</summary>
    public struct MoveResult
    {
        public Float3 Delta;

        public bool HitX;
        public bool HitY;
        public bool HitZ;

        /// <summary>向下移动被阻挡，即踩在实体表面上。</summary>
        public bool IsGrounded;
    }
}
