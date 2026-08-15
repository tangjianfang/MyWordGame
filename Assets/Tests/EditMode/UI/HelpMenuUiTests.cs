#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m6 B3：H 键帮助菜单的开关 / 按键路由 / PlayerPrefs 三键 round-trip 契约。
    /// InputLocked 静态门由 <see cref="HelpMenuUi"/> 的开关维护，
    /// <see cref="BlockInteraction.Update"/> 开头早退——这里测门的翻转，不拉起完整交互链路。
    /// </summary>
    [TestFixture]
    public class HelpMenuUiTests
    {
        /// <summary>EditMode 下 AddComponent 不会自动触发 MonoBehaviour.Awake
        /// （Unity 仅在 PlayMode / 场景加载时回调），用反射显式调用私有 Awake。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        private static HelpMenuUi NewMenu()
        {
            var go = new GameObject("HelpMenu");
            var ui = go.AddComponent<HelpMenuUi>();
            InvokeAwake(ui);
            return ui;
        }

        [SetUp]
        public void SetUp()
        {
            // 每个测试前清掉三键，避免上个测试写入的值影响「默认值」断言
            PlayerPrefs.DeleteKey(HelpMenuUi.SensitivityKey);
            PlayerPrefs.DeleteKey(HelpMenuUi.VolumeKey);
            PlayerPrefs.DeleteKey(HelpMenuUi.FovKey);
            BlockInteraction.InputLocked = false;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(HelpMenuUi.SensitivityKey);
            PlayerPrefs.DeleteKey(HelpMenuUi.VolumeKey);
            PlayerPrefs.DeleteKey(HelpMenuUi.FovKey);
            BlockInteraction.InputLocked = false;
        }

        [Test]
        public void Toggle_FlipsOpenStateAndInputLocked()
        {
            var ui = NewMenu();
            try
            {
                Assert.That(ui.IsOpen, Is.False, "初始应为关闭");
                Assert.That(BlockInteraction.InputLocked, Is.False, "关闭时不应锁挖/放输入");

                ui.Toggle();
                Assert.That(ui.IsOpen, Is.True, "Toggle 一次后打开");
                Assert.That(BlockInteraction.InputLocked, Is.True, "打开时必须锁住挖/放输入");

                ui.Toggle();
                Assert.That(ui.IsOpen, Is.False, "再 Toggle 回到关闭");
                Assert.That(BlockInteraction.InputLocked, Is.False, "关闭时解锁挖/放输入");
            }
            finally
            {
                Object.DestroyImmediate(ui.gameObject);
            }
        }

        [Test]
        public void HandleKey_H_Toggles_EscapeClosesOnlyWhenOpen()
        {
            var ui = NewMenu();
            try
            {
                ui.HandleKey(KeyCode.H);
                Assert.That(ui.IsOpen, Is.True, "H 应打开帮助菜单");

                ui.HandleKey(KeyCode.Escape);
                Assert.That(ui.IsOpen, Is.False, "打开状态下 Esc 应关闭");

                ui.HandleKey(KeyCode.Escape);
                Assert.That(ui.IsOpen, Is.False, "关闭状态下 Esc 不应打开（不误触）");

                ui.HandleKey(KeyCode.H);
                Assert.That(ui.IsOpen, Is.True, "H 再按一次重新打开");
                ui.HandleKey(KeyCode.H);
                Assert.That(ui.IsOpen, Is.False, "H 也能关闭（H 或 Esc 关）");
            }
            finally
            {
                Object.DestroyImmediate(ui.gameObject);
            }
        }

        [Test]
        public void Settings_ThreeKeys_RoundTripThroughPlayerPrefs()
        {
            // Save → Load 相等（三键同构：灵敏度 / 音量 / FOV）
            HelpMenuUi.SaveSensitivity(1.5f);
            HelpMenuUi.SaveVolume(42f);
            HelpMenuUi.SaveFov(85f);

            Assert.That(HelpMenuUi.LoadSensitivity(), Is.EqualTo(1.5f), "灵敏度应原样回读");
            Assert.That(HelpMenuUi.LoadVolume(), Is.EqualTo(42f), "音量应原样回读");
            Assert.That(HelpMenuUi.LoadFov(), Is.EqualTo(85f), "FOV 应原样回读");
        }

        [Test]
        public void Settings_Defaults_WhenKeysMissing()
        {
            // 没存过时必须返回默认值，而不是 0（0 灵敏度会让视角完全转不动）
            Assert.That(HelpMenuUi.LoadSensitivity(), Is.EqualTo(HelpMenuUi.SensitivityDefault),
                "灵敏度默认 1.0");
            Assert.That(HelpMenuUi.LoadVolume(), Is.EqualTo(HelpMenuUi.VolumeDefault),
                "音量默认 80");
            Assert.That(HelpMenuUi.LoadFov(), Is.EqualTo(HelpMenuUi.FovDefault),
                "FOV 默认 70");
        }

        [Test]
        public void Awake_RestoresCurrentSettings_FromPlayerPrefs()
        {
            HelpMenuUi.SaveSensitivity(1.75f);
            HelpMenuUi.SaveVolume(10f);
            HelpMenuUi.SaveFov(88f);

            var ui = NewMenu();
            try
            {
                Assert.That(ui.CurrentSensitivity, Is.EqualTo(1.75f), "Awake 应从 PlayerPrefs 读回灵敏度");
                Assert.That(ui.CurrentVolume, Is.EqualTo(10f), "Awake 应从 PlayerPrefs 读回音量");
                Assert.That(ui.CurrentFov, Is.EqualTo(88f), "Awake 应从 PlayerPrefs 读回 FOV");
            }
            finally
            {
                Object.DestroyImmediate(ui.gameObject);
            }
        }
    }
}
#endif
