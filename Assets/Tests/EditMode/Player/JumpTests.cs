#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
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
}
#endif
