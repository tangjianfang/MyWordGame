using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>水平移动、疾跑、撞墙与跳跃。</summary>
    [TestFixture]
    public class PlayerMotorMoveTests
    {
        private const float Dt = 1f / 60f;
        private World _world;
        private WorldSolidSource _source;
        private PlayerMotorSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _settings = PlayerMotorSettings.Default;
            for (var x = -16; x < 16; x++)
            for (var z = -16; z < 16; z++) _world.SetBlock(x, 63, z, BlockIds.Stone);
            _source = new WorldSolidSource(_world, BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" }, ""solid"": true, ""opaque"": true }"
            }));
        }
        private PlayerState Run(PlayerState state, PlayerInput input, int frames)
        { for (var i = 0; i < frames; i++) state = PlayerMotor.Step(_source, state, input, _settings, Dt); return state; }
        private static PlayerState OnGround(float x = 0f, float z = 0f) => new PlayerState(new Float3(x, 64f, z), default, true);

        [Test] public void Step_HoldingForward_MovesAtWalkSpeed()
        { PlayerState state = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 60); Assert.That(state.Position.Z, Is.GreaterThan(_settings.WalkSpeed * 0.9f), "推满一秒应当走出接近 WalkSpeed 格"); Assert.That(state.Position.Z, Is.LessThanOrEqualTo(_settings.WalkSpeed + 0.1f), "不应当超过行走速度上限"); }
        [Test] public void Step_Sprinting_TravelsFartherThanWalking()
        { PlayerState walk = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 60); PlayerState sprint = Run(OnGround(), new PlayerInput(0f, 1f, false, true), 60); Assert.That(sprint.Position.Z, Is.GreaterThan(walk.Position.Z), "按住疾跑应当比普通行走走得更远"); }
        [Test] public void Step_ReleasingInput_DecaysHorizontalVelocity()
        { PlayerState state = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 30); state = Run(state, PlayerInput.None, 60); Assert.That(System.Math.Abs(state.Velocity.Z), Is.LessThan(0.05f), "松开方向键一秒后水平速度应当已经衰减到接近零"); }
        [Test] public void Step_IntoWall_StopsAndZeroesVelocity()
        { for (var x = -16; x < 16; x++) { _world.SetBlock(x, 64, 3, BlockIds.Stone); _world.SetBlock(x, 65, 3, BlockIds.Stone); } PlayerState state = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 120); Assert.That(state.Position.Z, Is.LessThan(3f), "玩家不应当穿过 z = 3 的墙"); Assert.That(System.Math.Abs(state.Velocity.Z), Is.LessThan(0.01f), "撞墙后水平速度应当归零，否则障碍一消失会突然弹出去"); }
        [Test] public void Step_DiagonalIntoWall_SlidesAlongIt()
        { for (var x = -16; x < 16; x++) { _world.SetBlock(x, 64, 3, BlockIds.Stone); _world.SetBlock(x, 65, 3, BlockIds.Stone); } PlayerState state = Run(OnGround(), new PlayerInput(0.707f, 0.707f, false, false), 120); Assert.That(state.Position.X, Is.GreaterThan(1f), "被墙挡住的只应当是前进方向，侧向仍应能滑动"); }
        [Test] public void Step_JumpOnGround_LeavesGround()
        { PlayerState state = PlayerMotor.Step(_source, OnGround(), new PlayerInput(0f, 0f, true, false), _settings, Dt); Assert.That(state.Velocity.Y, Is.GreaterThan(0f), "起跳后竖直速度应当向上"); Assert.That(state.IsGrounded, Is.False, "起跳后应当离地"); }
        [Test] public void Step_JumpInAir_Ignored()
        { var air = new PlayerState(new Float3(0f, 80f, 0f), default, false); PlayerState state = PlayerMotor.Step(_source, air, new PlayerInput(0f, 0f, true, false), _settings, Dt); Assert.That(state.Velocity.Y, Is.LessThan(0f), "空中按跳不应当生效，竖直速度仍应当在重力作用下向下"); }
        [Test] public void Step_JumpArc_ClearsOneBlockButNotTwo()
        { PlayerState state = OnGround(); var peak = 64f; for (var i = 0; i < 120; i++) { state = PlayerMotor.Step(_source, state, new PlayerInput(0f, 0f, i == 0, false), _settings, Dt); if (state.Position.Y > peak) peak = state.Position.Y; } float height = peak - 64f; Assert.That(height, Is.GreaterThan(1f), $"跳跃高度 {height:F2} 应当能上 1 格台阶"); Assert.That(height, Is.LessThan(2f), $"跳跃高度 {height:F2} 不应当能上 2 格"); }
    }
}
