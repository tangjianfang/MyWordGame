using MyWorld.Core.Math;

namespace MyWorld.Core.Player
{
    /// <summary>
    /// 玩家的运动状态。<see cref="Position"/> 是**脚底中心**而不是几何中心——
    /// 落地判定、出生点摆放、放置方块都是按脚下算的，取脚底中心能省掉一堆 ± 半高。
    /// </summary>
    public readonly struct PlayerState
    {
        public readonly Float3 Position;
        public readonly Float3 Velocity;
        public readonly bool IsGrounded;

        public PlayerState(Float3 position, Float3 velocity, bool isGrounded)
        {
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
        }

        /// <summary>刚放到某个位置、还没有速度的初始状态。</summary>
        public static PlayerState AtRest(Float3 position) => new PlayerState(position, default, false);
    }
}
