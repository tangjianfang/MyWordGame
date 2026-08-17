#if UNITY_EDITOR
// av W2-12：TitleScreenUi EditMode 测试。
//   - Awake 立即显示、锁输入、开指针门
//   - StartGame 销毁遮罩、解锁、关指针门、通知 Bgm
//   - mp4 缺失 → 不抛（纯色回退）
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    [TestFixture]
    public class TitleScreenUiTests
    {
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        [Test]
        public void Awake_ShowsMenuAndLocksInput_StartGameUnlocks()
        {
            var go = new GameObject("title");
            try
            {
                var title = go.AddComponent<TitleScreenUi>();
                InvokeAwake(title);
                Assert.That(title.IsVisible, Is.True, "开局即主菜单");
                Assert.That(MyWorld.Unity.Player.BlockInteraction.InputLocked, Is.True,
                    "菜单期间锁交互");
                Assert.That(MyWorld.Unity.UI.UiCursorGate.IsOpen, Is.True,
                    "菜单期间指针解锁");

                title.StartGame();
                Assert.That(title.IsVisible, Is.False, "StartGame 后隐藏");
                Assert.That(MyWorld.Unity.Player.BlockInteraction.InputLocked, Is.False,
                    "StartGame 后解锁交互");
                Assert.That(MyWorld.Unity.UI.UiCursorGate.IsOpen, Is.False,
                    "StartGame 后锁回指针");
            }
            finally
            {
                MyWorld.Unity.Player.BlockInteraction.InputLocked = false;
                if (MyWorld.Unity.UI.UiCursorGate.IsOpen) MyWorld.Unity.UI.UiCursorGate.Close();
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Awake_MissingVideo_DoesNotThrow()
        {
            var go = new GameObject("title-novideo");
            try
            {
                var title = go.AddComponent<TitleScreenUi>();
                Assert.DoesNotThrow(() => InvokeAwake(title),
                    "EditMode 无 mp4：纯色回退不炸");
                Assert.That(title.IsVisible, Is.True, "Awake 仍把菜单显示出来");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif