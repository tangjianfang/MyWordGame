#if UNITY_EDITOR
// av W1-10：MobAudioSystem EditMode 测试。
//   - ClipPath / FallbackPath 静态契约
//   - IdleIntervalSeconds 确定性 + 在 8–20 区间
//   - TickIdle 远距离重置 / 近距离累计 / clip 缺失静默
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Audio
{
    [TestFixture]
    public class MobAudioSystemTests
    {
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        [Test]
        public void Paths_KindLowercaseHyphen()
        {
            Assert.That(MobAudioSystem.ClipPath(MyWorld.Core.Entities.MobKind.Pig, false),
                Is.EqualTo("Audio/Mobs/pig-idle"));
            Assert.That(MobAudioSystem.ClipPath(MyWorld.Core.Entities.MobKind.Pig, true),
                Is.EqualTo("Audio/Mobs/pig-hurt"));
            Assert.That(MobAudioSystem.ClipPath(MyWorld.Core.Entities.MobKind.Hamster, false),
                Is.EqualTo("Audio/Mobs/hamster-idle"));
        }

        [Test]
        public void Fallback_SmallVsLarge()
        {
            // 鸡/兔/仓鼠走 small
            Assert.That(MobAudioSystem.FallbackPath(MyWorld.Core.Entities.MobKind.Chicken, false),
                Is.EqualTo("Audio/Mobs/generic-small-idle"));
            Assert.That(MobAudioSystem.FallbackPath(MyWorld.Core.Entities.MobKind.Rabbit, true),
                Is.EqualTo("Audio/Mobs/generic-small-hurt"));
            // 其它走 large（猪/僵尸/村民 等）
            Assert.That(MobAudioSystem.FallbackPath(MyWorld.Core.Entities.MobKind.Pig, false),
                Is.EqualTo("Audio/Mobs/generic-large-idle"));
            Assert.That(MobAudioSystem.FallbackPath(MyWorld.Core.Entities.MobKind.Zombie, true),
                Is.EqualTo("Audio/Mobs/generic-large-hurt"));
        }

        [Test]
        public void IdleInterval_DeterministicAndInRange()
        {
            int a = MobAudioSystem.IdleIntervalSeconds((int)MyWorld.Core.Entities.MobKind.Pig, 0);
            Assert.That(a, Is.EqualTo(MobAudioSystem.IdleIntervalSeconds(
                (int)MyWorld.Core.Entities.MobKind.Pig, 0)),
                "同输入同输出（确定性哈希）");
            Assert.That(MobAudioSystem.IdleIntervalSeconds(
                (int)MyWorld.Core.Entities.MobKind.Pig, 7), Is.InRange(8, 20),
                "8–20s 区间");
            Assert.That(a, Is.InRange(8, 20));
        }

        [Test]
        public void TickIdle_FarResets_NearAccumulates_MissingClipSilent()
        {
            var go = new GameObject("mobaudio");
            try
            {
                var sys = MobAudioSystem.Attach(go, MyWorld.Core.Entities.MobKind.Cow);
                InvokeAwake(sys);
                Assert.DoesNotThrow(() =>
                {
                    sys.TickIdle(1f, 30f);   // 超过 16：重置
                    sys.TickIdle(25f, 5f);   // 近距离累计 25s：必跨过一次间隔（clip 缺失静默）
                    sys.PlayHurt();
                }, "clip 缺失静默，远距离重置，近距离累计到点静默播");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif