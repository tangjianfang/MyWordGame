#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.Player;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// Task A5：HandController jump 期间切 idle 姿势（mid-air 不挥动，落地复位）。
    /// <para>
    /// 玩家在空中时持物不再做走路挥动动画——视觉上更合理，避免「空中还在走路」的违和感。
    /// </para>
    /// </summary>
    [TestFixture]
    public class HandControllerJumpTests
    {
        [Test]
        public void Hand_StopsSwingingWhenAirborne()
        {
            var playerGo = new GameObject("Player");
            try
            {
                var pc = playerGo.AddComponent<PlayerController>();
                var handGo = new GameObject("Hand");
                handGo.transform.SetParent(playerGo.transform);
                var hc = handGo.AddComponent<HandController>();
                hc.AttachTo(pc);
                // 模拟 jump → IsGrounded = false
                pc.Jump();
                // 模拟更新
                hc.TickForTest();
                Assert.That(hc.IsSwinging, Is.False, "在空中不挥动");
            }
            finally
            {
                Object.DestroyImmediate(playerGo);
            }
        }

        [Test]
        public void Hand_ResumesSwingingWhenLanded()
        {
            var playerGo = new GameObject("Player");
            try
            {
                var pc = playerGo.AddComponent<PlayerController>();
                var handGo = new GameObject("Hand");
                handGo.transform.SetParent(playerGo.transform);
                var hc = handGo.AddComponent<HandController>();
                hc.AttachTo(pc);
                pc.Jump();
                hc.TickForTest();
                // 模拟落地（直接 IsGrounded=true）
                pc.ForceGroundedForTest();
                hc.TickForTest();
                Assert.That(hc.IsSwinging, Is.True, "落地后恢复挥动");
            }
            finally
            {
                Object.DestroyImmediate(playerGo);
            }
        }
    }
}
#endif