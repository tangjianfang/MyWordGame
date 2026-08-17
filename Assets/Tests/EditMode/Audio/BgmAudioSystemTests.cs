#if UNITY_EDITOR
// av W1-8：BgmAudioSystem EditMode 测试。
//   - ClipPath 静态契约
//   - Menu → Day → Night → Day → Menu 状态机
//   - Tick 跨 fade 不炸、状态推进正常（无 clip 时不强断言音量数值）
//   - menuMode 下不随昼夜换曲（Menu 仅由 EnterMenu 显式进）
//
// 注意：EditMode 无 AudioClip 资源，所以 Tick 只断状态推进 + 不抛；fade 的音量曲线
// 在实机（含真 clip）才验。
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Audio
{
    [TestFixture]
    public class BgmAudioSystemTests
    {
        /// <summary>EditMode 下 AddComponent 不会自动触发 MonoBehaviour.Awake，强制反射调。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        [Test]
        public void ClipPath_LowercaseHyphen()
        {
            // Resources 路径：Audio/bgm-{state 小写}
            Assert.That(BgmAudioSystem.ClipPath(BgmAudioSystem.BgmState.Menu),
                Is.EqualTo("Audio/bgm-menu"));
            Assert.That(BgmAudioSystem.ClipPath(BgmAudioSystem.BgmState.Day),
                Is.EqualTo("Audio/bgm-day"));
            Assert.That(BgmAudioSystem.ClipPath(BgmAudioSystem.BgmState.Night),
                Is.EqualTo("Audio/bgm-night"));
        }

        [Test]
        public void StateTransitions_MenuToDayToNight()
        {
            var go = new GameObject("bgm");
            try
            {
                var bgm = go.AddComponent<BgmAudioSystem>();
                InvokeAwake(bgm);
                Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Menu),
                    "初始 Menu");

                bgm.StartGame();
                Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Day),
                    "开始游戏→Day");

                bgm.Tick(0.1f, true, false);
                Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Night),
                    "夜晚→Night");

                bgm.Tick(0.1f, false, false);
                Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Day),
                    "白天→Day");

                bgm.Tick(0.1f, false, true);
                Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Day),
                    "menuMode 下不进 Menu（Menu 只能由 EnterMenu 显式进）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EnterMenu_OnlyGoesToMenuFromOtherState()
        {
            var go = new GameObject("bgm-menu");
            try
            {
                var bgm = go.AddComponent<BgmAudioSystem>();
                InvokeAwake(bgm);
                bgm.StartGame();  // Day
                bgm.Tick(0.1f, false, false);
                Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Day));

                bgm.EnterMenu();
                Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Menu),
                    "EnterMenu 显式切回 Menu");

                // 再 EnterMenu 应 no-op
                bgm.EnterMenu();
                Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Menu));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Tick_NoClip_DoesNotThrow_StateAdvances()
        {
            // 无 clip 时不断言音量数值，只断言不炸且状态机继续推进
            var go = new GameObject("bgm-fade");
            try
            {
                var bgm = go.AddComponent<BgmAudioSystem>();
                InvokeAwake(bgm);
                MyWorld.Unity.Audio.MusicVolumeBus.Volume = 0.8f;
                bgm.StartGame();
                Assert.DoesNotThrow(() =>
                {
                    bgm.Tick(1.5f, false, false);
                    bgm.Tick(1.5f, false, false);
                    bgm.Tick(1.5f, true, false);
                    bgm.Tick(1.5f, true, false);
                }, "无 clip 时 Tick 不抛");
                Assert.That(bgm.TargetState, Is.EqualTo(BgmAudioSystem.BgmState.Night));
            }
            finally
            {
                Object.DestroyImmediate(go);
                MyWorld.Unity.Audio.MusicVolumeBus.Volume = 0.8f;
            }
        }
    }
}
#endif