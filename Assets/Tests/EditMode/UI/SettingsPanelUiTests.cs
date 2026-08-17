#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.UI;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m8 B1：公共设置面板的 PlayerPrefs 三键契约（从 HelpMenuUiTests 平移，
    /// 断言原样、只是宿主换成 <see cref="SettingsPanelUi"/>）。
    /// 键名 / 量程 / 默认值 / 即时生效语义是全局硬约束——平移重构不得动任何一个数字。
    /// </summary>
    [TestFixture]
    public class SettingsPanelUiTests
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

        /// <summary>建一个面板并显式走 Awake（读 PlayerPrefs + 应用设置）。</summary>
        private static SettingsPanelUi NewPanel()
        {
            var go = new GameObject("SettingsPanel");
            var panel = go.AddComponent<SettingsPanelUi>();
            InvokeAwake(panel);
            return panel;
        }

        [SetUp]
        public void SetUp()
        {
            // 每个测试前清掉四键，避免上个测试写入的值影响「默认值」断言
            PlayerPrefs.DeleteKey(SettingsPanelUi.SensitivityKey);
            PlayerPrefs.DeleteKey(SettingsPanelUi.VolumeKey);
            PlayerPrefs.DeleteKey(SettingsPanelUi.FovKey);
            PlayerPrefs.DeleteKey(SettingsPanelUi.MusicVolumeKey);  // av W1-7
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(SettingsPanelUi.SensitivityKey);
            PlayerPrefs.DeleteKey(SettingsPanelUi.VolumeKey);
            PlayerPrefs.DeleteKey(SettingsPanelUi.FovKey);
            PlayerPrefs.DeleteKey(SettingsPanelUi.MusicVolumeKey);  // av W1-7
        }

        [Test]
        public void 三滑条_Save后Load_三键RoundTrip原样回读()
        {
            // Save → Load 相等（三键同构：灵敏度 / 音量 / FOV）
            SettingsPanelUi.SaveSensitivity(1.5f);
            SettingsPanelUi.SaveVolume(42f);
            SettingsPanelUi.SaveFov(85f);

            Assert.That(SettingsPanelUi.LoadSensitivity(), Is.EqualTo(1.5f), "灵敏度应原样回读");
            Assert.That(SettingsPanelUi.LoadVolume(), Is.EqualTo(42f), "音量应原样回读");
            Assert.That(SettingsPanelUi.LoadFov(), Is.EqualTo(85f), "FOV 应原样回读");
        }

        [Test]
        public void 默认值_未存键_返回三默认而不是0()
        {
            // 没存过时必须返回默认值，而不是 0（0 灵敏度会让视角完全转不动）
            Assert.That(SettingsPanelUi.LoadSensitivity(), Is.EqualTo(SettingsPanelUi.SensitivityDefault),
                "灵敏度默认 1.0");
            Assert.That(SettingsPanelUi.LoadVolume(), Is.EqualTo(SettingsPanelUi.VolumeDefault),
                "音量默认 80");
            Assert.That(SettingsPanelUi.LoadFov(), Is.EqualTo(SettingsPanelUi.FovDefault),
                "FOV 默认 70");
        }

        [Test]
        public void 默认值_音乐音量_未存键返回80()
        {
            // av W1-7：音乐音量默认 80，与全局音量同步
            Assert.That(SettingsPanelUi.LoadMusicVolume(), Is.EqualTo(SettingsPanelUi.MusicDefault),
                "音乐音量默认 80");
        }

        [Test]
        public void Save_越界写入_钳到量程两端()
        {
            // 写严格：Save 钳到量程是「范围原样」硬约束的守门——旧档被外部工具写坏也不许超范围进系统
            SettingsPanelUi.SaveSensitivity(99f);
            SettingsPanelUi.SaveVolume(-5f);
            SettingsPanelUi.SaveFov(400f);

            Assert.That(SettingsPanelUi.LoadSensitivity(), Is.EqualTo(SettingsPanelUi.SensitivityMax),
                "灵敏度超上限应钳到 2.0");
            Assert.That(SettingsPanelUi.LoadVolume(), Is.EqualTo(SettingsPanelUi.VolumeMin),
                "音量低于 0 应钳到 0");
            Assert.That(SettingsPanelUi.LoadFov(), Is.EqualTo(SettingsPanelUi.FovMax),
                "FOV 超上限应钳到 90");
        }

        [Test]
        public void MusicVolume_Save_越界写入_钳到量程两端()
        {
            // av W1-7：音乐音量复用 VolumeMin/Max，钳端同款
            SettingsPanelUi.SaveMusicVolume(150f);
            Assert.That(SettingsPanelUi.LoadMusicVolume(), Is.EqualTo(SettingsPanelUi.VolumeMax),
                "音乐音量超上限应钳到 100");
            SettingsPanelUi.SaveMusicVolume(-3f);
            Assert.That(SettingsPanelUi.LoadMusicVolume(), Is.EqualTo(SettingsPanelUi.VolumeMin),
                "音乐音量低于 0 应钳到 0");
        }

        [Test]
        public void Awake_从PlayerPrefs读回当前三设置()
        {
            SettingsPanelUi.SaveSensitivity(1.75f);
            SettingsPanelUi.SaveVolume(10f);
            SettingsPanelUi.SaveFov(88f);

            var panel = NewPanel();
            try
            {
                Assert.That(panel.CurrentSensitivity, Is.EqualTo(1.75f), "Awake 应从 PlayerPrefs 读回灵敏度");
                Assert.That(panel.CurrentVolume, Is.EqualTo(10f), "Awake 应从 PlayerPrefs 读回音量");
                Assert.That(panel.CurrentFov, Is.EqualTo(88f), "Awake 应从 PlayerPrefs 读回 FOV");
            }
            finally
            {
                Object.DestroyImmediate(panel.gameObject);
            }
        }

        [Test]
        public void Awake_从PlayerPrefs读回音乐音量()
        {
            // av W1-7：四键都应读回
            SettingsPanelUi.SaveMusicVolume(40f);

            var panel = NewPanel();
            try
            {
                Assert.That(panel.CurrentMusicVolume, Is.EqualTo(40f),
                    "Awake 应从 PlayerPrefs 读回音乐音量");
            }
            finally
            {
                Object.DestroyImmediate(panel.gameObject);
            }
        }

        [Test]
        public void Awake_应用音乐音量到MusicVolumeBus_归一0到1()
        {
            // av W1-7：音乐音量 0–100 应除以 VolumeMax 归一后喂 MusicVolumeBus.Volume
            SettingsPanelUi.SaveMusicVolume(40f);

            var panel = NewPanel();
            try
            {
                Assert.That(MyWorld.Unity.Audio.MusicVolumeBus.Volume, Is.EqualTo(0.4f).Within(0.001f),
                    "音乐音量 40 应归一成 0.4 写进 MusicVolumeBus");
            }
            finally
            {
                Object.DestroyImmediate(panel.gameObject);
                MyWorld.Unity.Audio.MusicVolumeBus.Volume = 0.8f; // 还原，不污染后续夹具
            }
        }

        [Test]
        public void Awake_应用音量到AudioListener_归一0到1()
        {
            // 平移语义守卫：音量 0–100 存储值要除以 VolumeMax 归一后喂 AudioListener.volume
            SettingsPanelUi.SaveVolume(50f);

            var panel = NewPanel();
            try
            {
                Assert.That(AudioListener.volume, Is.EqualTo(0.5f).Within(0.0001f),
                    "音量 50 应归一成 0.5 写进 AudioListener");
            }
            finally
            {
                Object.DestroyImmediate(panel.gameObject);
                AudioListener.volume = 1f; // 还原全局，不污染后续夹具
            }
        }

        [Test]
        public void 面板高度_常量与内容行匹配()
        {
            // 宿主按 PanelHeight 预留区域并在其下排列后续内容（如「保存并退出」按钮）——
            // av W1-7：四行滑条各 70 + 提示行 20 = 300；旧三滑条是 220，改布局时这个常量要跟着动
            Assert.That(SettingsPanelUi.PanelHeight, Is.EqualTo(300f),
                "四滑条 70×4 + 提示行 20 = 300");
        }
    }
}
#endif
