#if UNITY_EDITOR
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// Task C3：锁定 HandController 挥动参数（防止有人误改默认值）。
    /// 实际动画驱动在 <see cref="HandController.Update"/>/<see cref="HandController.OnGUI"/>，
    /// 这里只断言字段默认值，避免在 EditMode 下拉起 GUI 渲染管线。
    /// </summary>
    [TestFixture]
    public class HandControllerTests
    {
        [Test]
        public void SwingDuration_Default_IsQuarterSecond()
        {
            var go = new GameObject("HandHost");
            try
            {
                var hand = go.AddComponent<HandController>();
                Assert.That(hand.SwingDuration, Is.EqualTo(0.25f),
                    "挥动时长应紧凑到 0.25 秒（C3 设计意图）");
                Assert.That(hand.SwingDownAngle, Is.EqualTo(-45f),
                    "挥砍弧度应加深到 -45 度（C3 设计意图）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif