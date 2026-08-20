#if UNITY_EDITOR
// m12 P1：主菜单世界管理三态（EditMode）——「继续上次」种子解析 + 换世界跳过菜单路径。
// OnGUI 按钮点击驱动不了，测可直调的纯逻辑与 Awake 分支（TitleScreenUiTests 同款反射 Awake）。
using System.IO;
using MyWorld.Core.Persistence;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    [TestFixture]
    public class TitleScreenUiWorldMenuTests
    {
        private string _root;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), $"title-world-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            PlayerPrefs.DeleteKey(WorldBootstrap.PrefKeyLastWorldSeed);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            if (_host != null) Object.DestroyImmediate(_host);
            PlayerPrefs.DeleteKey(WorldBootstrap.PrefKeyLastWorldSeed);
            TitleScreenUi.SkipMenuNextLoad = false;
            BlockInteraction.InputLocked = false;
            UiCursorGate.Reset();
        }

        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        private void MakeWorldDir(long seed)
        {
            string dir = Path.Combine(_root, seed.ToString());
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "level.dat"), "{}");
        }

        [Test]
        public void ResolveContinueSeed_NoWorlds_ReturnsNull()
        {
            Assert.That(TitleScreenUi.ResolveContinueSeed(_root), Is.Null,
                "没有任何世界 → 「继续上次」按钮灰");
        }

        [Test]
        public void ResolveContinueSeed_PrefWithLiveDir_ReturnsPref()
        {
            MakeWorldDir(777);
            MakeWorldDir(123); // 干扰项：mtime 更新也没用，pref 优先
            PlayerPrefs.SetString(WorldBootstrap.PrefKeyLastWorldSeed, "777");

            Assert.That(TitleScreenUi.ResolveContinueSeed(_root), Is.EqualTo(777L),
                "PlayerPrefs 记的种子目录还在 → 优先用它");
        }

        [Test]
        public void ResolveContinueSeed_PrefDirDeleted_FallsBackToCatalog()
        {
            MakeWorldDir(123); // pref 指向的世界已被删
            PlayerPrefs.SetString(WorldBootstrap.PrefKeyLastWorldSeed, "777");

            Assert.That(TitleScreenUi.ResolveContinueSeed(_root), Is.EqualTo(123L),
                "pref 失效回落目录 mtime 最新者");
        }

        [Test]
        public void Awake_SkipMenuNextLoad_AutoStartsWithoutMenu()
        {
            TitleScreenUi.SkipMenuNextLoad = true;
            _host = new GameObject("TitleSkip");
            var title = _host.AddComponent<TitleScreenUi>();
            InvokeAwake(title);

            Assert.That(title.IsVisible, Is.False, "换世界重载后不弹菜单");
            Assert.That(BlockInteraction.InputLocked, Is.False, "输入直接解锁");
            Assert.That(UiCursorGate.IsOpen, Is.False, "指针门不开");
        }

        [Test]
        public void Awake_NormalPath_LocksInput()
        {
            _host = new GameObject("TitleNormal");
            var title = _host.AddComponent<TitleScreenUi>();
            InvokeAwake(title);

            Assert.That(title.IsVisible, Is.True);
            Assert.That(BlockInteraction.InputLocked, Is.True);
            Assert.That(UiCursorGate.IsOpen, Is.True);
        }
    }
}
#endif
