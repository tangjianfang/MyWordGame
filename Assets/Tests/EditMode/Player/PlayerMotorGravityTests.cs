using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>竖直方向：重力累积、终端速度、落地、撞头。</summary>
    [TestFixture]
    public class PlayerMotorGravityTests
    {
        private const float Dt = 1f / 60f;
        private const float GroundTop = 64f;

        private World _world;
        private WorldSolidSource _source;
        private PlayerMotorSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _settings = PlayerMotorSettings.Default;

            // 一片 16×16 的石地板，顶面在 y = 64（方块占据 [63, 64)）
            for (var x = -8; x < 8; x++)
            for (var z = -8; z < 8; z++)
            {
                _world.SetBlock(x, 63, z, BlockIds.Stone);
            }

            _source = new WorldSolidSource(_world, BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" }, ""solid"": true, ""opaque"": true }"
            }));
        }

        [Test]
        public void Step_InAir_AppliesGravityForOneFrame()
        {
            var state = PlayerState.AtRest(new Float3(0f, 70f, 0f));
            PlayerState next = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);
            Assert.That(next.Velocity.Y, Is.EqualTo(_settings.Gravity * Dt).Within(1e-4f), "自由落体第一帧的竖直速度应当正好是 重力 × dt");
            Assert.That(next.IsGrounded, Is.False, "还在空中不应当判定为着地");
        }

        [Test]
        public void Step_FallingLong_ClampsToTerminalVelocity()
        {
            var state = PlayerState.AtRest(new Float3(0f, 300f, 0f));
            for (var i = 0; i < 600; i++) state = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);
            Assert.That(state.Velocity.Y, Is.GreaterThanOrEqualTo(_settings.MaxFallSpeed), "竖直速度不应当超过终端速度，否则单帧位移会大到穿透地板");
        }

        [Test]
        public void Step_Falling_LandsOnGroundSurface()
        {
            var state = PlayerState.AtRest(new Float3(0f, 70f, 0f));
            for (var i = 0; i < 120; i++) state = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);
            Assert.That(state.Position.Y, Is.EqualTo(GroundTop).Within(0.01f), "脚底应当停在地面顶面 y = 64 上");
            Assert.That(state.IsGrounded, Is.True, "停在地面上应当判定为着地");
        }

        [Test]
        public void Step_StandingOnGround_DoesNotAccumulateFallSpeed()
        {
            var state = PlayerState.AtRest(new Float3(0f, 64f, 0f));
            for (var i = 0; i < 60; i++) state = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);
            Assert.That(state.Velocity.Y, Is.EqualTo(0f).Within(1e-3f), "被地面挡住后竖直速度必须归零，否则站久了一离开地面就会瞬间高速下坠");
        }

        [Test]
        public void Step_HittingCeiling_ZeroesUpwardVelocity()
        {
            for (var x = -8; x < 8; x++)
            for (var z = -8; z < 8; z++) _world.SetBlock(x, 66, z, BlockIds.Stone);
            var state = new PlayerState(new Float3(0f, 64f, 0f), new Float3(0f, 20f, 0f), true);
            state = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);
            Assert.That(state.Velocity.Y, Is.LessThanOrEqualTo(0f), "撞到天花板后不应当还保留向上的速度");
        }
    }
}
