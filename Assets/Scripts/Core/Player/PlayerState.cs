using MyWorld.Core.Math;

namespace MyWorld.Core.Player
{
    /// <summary>
    /// 玩家的运动状态。<see cref="Position"/> 是**脚底中心**而不是几何中心——
    /// 落地判定、出生点摆放、放置方块都是按脚下算的，取脚底中心能省掉一堆 ± 半高。
    /// 饥饿/饱和度字段是状态而非运动的一部分，可变（玩家进食、饥饿衰减），
    /// 所以结构体本身去 readonly。
    /// </summary>
    public struct PlayerState
    {
        public readonly Float3 Position;
        public readonly Float3 Velocity;
        public readonly bool IsGrounded;

        /// <summary>饥饿值，0-20。默认满（20）。</summary>
        public int Hunger { get; set; }
        /// <summary>隐藏饱食度，0-20。默认 5f。</summary>
        public float Saturation { get; set; }

        public PlayerState(Float3 position, Float3 velocity, bool isGrounded)
            : this(position, velocity, isGrounded, hunger: 20, saturation: 5f)
        {
        }

        public PlayerState(Float3 position, Float3 velocity, bool isGrounded, int hunger, float saturation)
        {
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
            Hunger = hunger;
            Saturation = saturation;
        }

        /// <summary>刚放到某个位置、还没有速度的初始状态。</summary>
        public static PlayerState AtRest(Float3 position) => new PlayerState(position, default, false);
    }
}
