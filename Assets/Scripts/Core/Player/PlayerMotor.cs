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
            float velocityY = StepVertical(state, input, settings, dt);

            var velocity = new Float3(0f, velocityY, 0f);
            Aabb box = Aabb.FromBottomCenter(state.Position, settings.Width, settings.Height);
            MoveResult result = VoxelCollision.Move(source, box, new Float3(
                velocity.X * dt, velocity.Y * dt, velocity.Z * dt));

            var position = new Float3(
                state.Position.X + result.Delta.X,
                state.Position.Y + result.Delta.Y,
                state.Position.Z + result.Delta.Z);

            // 被挡的轴速度必须归零：继续攒速度的话，一旦障碍消失会瞬间弹出去
            if (result.HitY)
            {
                velocity = new Float3(velocity.X, 0f, velocity.Z);
            }

            return new PlayerState(position, velocity, result.IsGrounded);
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
