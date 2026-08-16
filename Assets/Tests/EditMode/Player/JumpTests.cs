#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Player;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// Task A4：PlayerController 公开 jump / grounded / dt 钳位 API 的契约测试。
    /// <para>
    /// 这些属性/方法的目的是为 HandController（A5）提供「玩家是否在空中」信号，
    /// 同时为无头驱动链路（PlayHarness / ScreenshotCapture）提供一个数值稳定的步进入口。
    /// </para>
    /// </summary>
    [TestFixture]
    public class JumpTests
    {
        [Test]
        public void Jump_SetsVerticalVelocity()
        {
            var go = new GameObject("JumpTest");
            try
            {
                var pc = go.AddComponent<PlayerController>();
                pc.Jump();
                Assert.That(pc.VerticalVelocity, Is.GreaterThan(0f), "jump 后 VerticalVelocity > 0");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Jump_OnlyWhenGrounded()
        {
            var go = new GameObject("JumpTest");
            try
            {
                var pc = go.AddComponent<PlayerController>();
                pc.Jump();
                float v1 = pc.VerticalVelocity;
                pc.Jump();  // 已在空中
                Assert.That(pc.VerticalVelocity, Is.EqualTo(v1), "二次 jump 无效（已在空中）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WalkPhase_DtIsClamped()
        {
            var go = new GameObject("JumpTest");
            try
            {
                var pc = go.AddComponent<PlayerController>();
                // 模拟极小 dt
                pc.ApplyMovementTick(1f, 0.0001f);  // speed=1, dt=0.0001
                // WalkPhase 累加不应爆炸
                float phase = pc.WalkPhase;
                Assert.That(phase, Is.LessThan(10f), "WalkPhase 累加受钳位");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    /// <summary>
    /// m8 B2 fix1（I1）：暂停恢复的输入残留。两处 Core 语义决定了「暂停帧照跑运动步进」
    /// 一定会毒化状态——<see cref="PlayerMotor"/> 的跳跃初速不乘 dt（dt=0 也照样把
    /// Velocity.Y 顶成 JumpSpeed，位置 ×dt=0 没动、恢复后照样起飞），且
    /// <c>VoxelCollision.Move</c> 零位移步进会把 IsGrounded 判成 false（Delta.Y &lt; 0
    /// 才算着地）。修法在 <see cref="PlayerController"/>：真暂停（timeScale=0）帧整帧
    /// 跳过运动步进（<see cref="PlayerController.StepInputKind.Skip"/>，Core 状态一个
    /// 字节不动），恢复首帧喂一帧 <see cref="PlayerInput.None"/>（Blank）清按住键残留，
    /// 第二帧起恢复真实输入（Live）。这里按 Update 实际会做的决策驱动
    /// <see cref="PlayerMotor.Step"/>，把「暂停→按住跳→恢复→该帧不起跳」断言成确定行为。
    /// </summary>
    [TestFixture]
    public class PauseResumeJumpTests
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

        private static PlayerState OnGround() => new PlayerState(new Float3(0f, 64f, 0f), default, true);

        [Test]
        public void 暂停中按住跳_恢复首帧不起跳_第二帧真实按跳才起跳()
        {
            PlayerState state = OnGround();

            // 暂停帧（timeScale=0）：整帧跳过运动步进，Core 状态不动
            Assert.That(PlayerController.InputDecision(timeScaleZero: true, wasPausedLastFrame: false),
                Is.EqualTo(PlayerController.StepInputKind.Skip),
                "暂停帧应整帧跳过——零 dt 步进也在改 Core 状态（跳跃初速不乘 dt）");

            // 反例（I1 病灶）：暂停帧若照喂按住的 Space——位置没动（×dt=0）但速度已起飞，
            // 恢复后第一帧照样上升。这正是暂停帧必须 Skip 的原因
            PlayerState poisoned = PlayerMotor.Step(_source, OnGround(),
                new PlayerInput(0f, 0f, true, false), _settings, 0f);
            Assert.That(poisoned.Position.Y, Is.EqualTo(64f), "反例前置：dt=0 位置确实没动");
            Assert.That(poisoned.Velocity.Y, Is.GreaterThan(0f),
                "反例：dt=0 挡不住跳跃初速——按住 Space 直接把 Velocity.Y 顶成 JumpSpeed");

            // 恢复首帧（timeScale 回 1、上一帧是暂停）：喂一帧 None，按住的 Space 不生效
            Assert.That(PlayerController.InputDecision(timeScaleZero: false, wasPausedLastFrame: true),
                Is.EqualTo(PlayerController.StepInputKind.Blank),
                "恢复首帧应喂 PlayerInput.None——按住的键不该在解冻瞬间生效");
            state = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);
            Assert.That(state.Velocity.Y, Is.LessThanOrEqualTo(0.01f),
                "恢复首帧不得起跳（重力步进或被地面钳回，都不该向上）");

            // 恢复第二帧起：Live——真实按跳照常起跳
            Assert.That(PlayerController.InputDecision(timeScaleZero: false, wasPausedLastFrame: false),
                Is.EqualTo(PlayerController.StepInputKind.Live),
                "第二帧起恢复真实输入");
            state = PlayerMotor.Step(_source, state, new PlayerInput(0f, 0f, true, false), _settings, Dt);
            Assert.That(state.Velocity.Y, Is.GreaterThan(0f), "恢复后真实按跳照常起跳");
        }

        [Test]
        public void 帧序_连续多帧暂停_恢复时Blank只有一帧()
        {
            // 帧序走一遍「正常 → 暂停 3 帧 → 恢复」：Skip 覆盖整个暂停段，
            // Blank 只出现在恢复后的第一帧，之后全是 Live
            Assert.That(PlayerController.InputDecision(false, false), Is.EqualTo(PlayerController.StepInputKind.Live),
                "正常帧 Live");

            Assert.That(PlayerController.InputDecision(true, false), Is.EqualTo(PlayerController.StepInputKind.Skip),
                "暂停第 1 帧 Skip");
            Assert.That(PlayerController.InputDecision(true, true), Is.EqualTo(PlayerController.StepInputKind.Skip),
                "暂停第 2 帧仍 Skip");
            Assert.That(PlayerController.InputDecision(true, true), Is.EqualTo(PlayerController.StepInputKind.Skip),
                "暂停第 3 帧仍 Skip");

            Assert.That(PlayerController.InputDecision(false, true), Is.EqualTo(PlayerController.StepInputKind.Blank),
                "恢复首帧 Blank（清残留，仅此一帧）");
            Assert.That(PlayerController.InputDecision(false, false), Is.EqualTo(PlayerController.StepInputKind.Live),
                "恢复第二帧起 Live——不能一直 Blank 否则按键丢输入");
        }
    }
}
#endif
