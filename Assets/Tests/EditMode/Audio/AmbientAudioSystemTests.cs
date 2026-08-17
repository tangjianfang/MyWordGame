#if UNITY_EDITOR
// av W1-9：AmbientAudioSystem EditMode 测试。
//   - PickTrack 纯函数：cave / night / day 三选
//   - Tick 节流 + clip 缺失静默
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Audio
{
    [TestFixture]
    public class AmbientAudioSystemTests
    {
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        [Test]
        public void PickTrack_CaveNightDay()
        {
            Assert.That(AmbientAudioSystem.PickTrack(10f, false), Is.EqualTo("amb-cave"),
                "y<40 洞穴优先");
            Assert.That(AmbientAudioSystem.PickTrack(10f, true), Is.EqualTo("amb-cave"),
                "y<40 不管昼夜都是洞穴");
            Assert.That(AmbientAudioSystem.PickTrack(70f, true), Is.EqualTo("amb-crickets"),
                "夜晚虫鸣");
            Assert.That(AmbientAudioSystem.PickTrack(70f, false), Is.EqualTo("amb-birds"),
                "白天鸟鸣");
            Assert.That(AmbientAudioSystem.PickTrack(39f, false), Is.EqualTo("amb-cave"),
                "边界值 y=39 是洞穴");
            Assert.That(AmbientAudioSystem.PickTrack(40f, false), Is.EqualTo("amb-birds"),
                "边界值 y=40 不是洞穴");
        }

        [Test]
        public void Tick_MissingClip_DoesNotThrow_TrackRecorded()
        {
            var go = new GameObject("ambient");
            try
            {
                var amb = go.AddComponent<AmbientAudioSystem>();
                InvokeAwake(amb);
                Assert.DoesNotThrow(() =>
                {
                    amb.Tick(1f, 70f, false);
                    amb.Tick(2f, 70f, false);
                    amb.Tick(2f, 70f, true);
                    amb.Tick(2f, 10f, false);
                }, "clip 缺失静默；首查立即 + 每 2s 复查");
                Assert.That(amb.CurrentTrack, Is.Not.Null,
                    "CurrentTrack 应记录最新选定的轨（即使 clip 缺失）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif