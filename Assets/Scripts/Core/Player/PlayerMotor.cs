using MyWorld.Core.Math;
using MyWorld.Core.Physics;

namespace MyWorld.Core.Player
{
    /// <summary>
    /// 玩家运动解算。纯函数：同样的输入必然得到同样的输出，不持有任何状态，
    /// 因此可完整单测，将来做回放或联机预测也不需要改。
    /// </summary>
    public static class PlayerMotor
    {
        public static PlayerState Step<TSource>(TSource source, PlayerState state, PlayerInput input,
            PlayerMotorSettings settings, float dt) where TSource : ISolidBlockSource
        {
            Float3 velocity = StepHorizontal(state, input, settings, dt);
            velocity = new Float3(velocity.X, StepVertical(state, input, settings, dt), velocity.Z);

            Aabb box = Aabb.FromBottomCenter(state.Position, settings.Width, settings.Height);
            MoveResult result = VoxelCollision.Move(source, box,
                new Float3(velocity.X * dt, velocity.Y * dt, velocity.Z * dt));

            var position = new Float3(
                state.Position.X + result.Delta.X,
                state.Position.Y + result.Delta.Y,
                state.Position.Z + result.Delta.Z);

            // 被挡的轴速度归零。逐轴判断而不是整体归零——沿墙滑动靠的就是“只死一个轴”
            velocity = new Float3(
                result.HitX ? 0f : velocity.X,
                result.HitY ? 0f : velocity.Y,
                result.HitZ ? 0f : velocity.Z);

            return new PlayerState(position, velocity, result.IsGrounded);
        }

        /// <summary>
        /// 水平速度朝目标速度收敛。地面上响应快、空中打折，没有输入时按摩擦衰减到零。
        /// 用“朝目标线性逼近”而不是直接赋值，起步和松手才不会是硬切换。
        /// m10 C1：目标速度再乘 (1 + <see cref="PlayerMotorSettings.MoveSpeedBonus"/>)——
        /// 手持夏季合金装备的移速加成（比例乘在速度上限上，疾跑倍率照旧先乘后乘无差）。
        /// </summary>
        private static Float3 StepHorizontal(PlayerState state, PlayerInput input,
            PlayerMotorSettings settings, float dt)
        {
            float speedLimit = settings.WalkSpeed
                               * (input.Sprint ? settings.SprintMultiplier : 1f)
                               * (1f + settings.MoveSpeedBonus);
            float targetX = input.MoveX * speedLimit;
            float targetZ = input.MoveZ * speedLimit;

            bool hasInput = input.MoveX != 0f || input.MoveZ != 0f;
            float rate = hasInput
                ? (state.IsGrounded ? settings.GroundFriction : settings.GroundFriction * settings.AirControl)
                : (state.IsGrounded ? settings.GroundFriction : settings.AirFriction);

            float t = rate * dt;
            if (t > 1f)
            {
                t = 1f;
            }

            return new Float3(
                state.Velocity.X + (targetX - state.Velocity.X) * t,
                state.Velocity.Y,
                state.Velocity.Z + (targetZ - state.Velocity.Z) * t);
        }

        private static float StepVertical(PlayerState state, PlayerInput input,
            PlayerMotorSettings settings, float dt)
        {
            if (state.IsGrounded && input.Jump)
            {
                return settings.JumpSpeed;
            }

            float velocityY = state.Velocity.Y + settings.Gravity * dt;
            return velocityY < settings.MaxFallSpeed ? settings.MaxFallSpeed : velocityY;
        }
    }
}
