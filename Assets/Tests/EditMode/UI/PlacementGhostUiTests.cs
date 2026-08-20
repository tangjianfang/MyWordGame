#if UNITY_EDITOR
// m12 P0-b：放置落点幽灵框（EditMode）——两态材质 / 定位 / 显隐。
// 照 SelectionBoxTests 的做法断言渲染器结构，另加 valid/blocked 材质切换。
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    [TestFixture]
    public class PlacementGhostUiTests
    {
        private GameObject _host;
        private PlacementGhostUi _ghost;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("GhostHost");
            _ghost = PlacementGhostUi.Create(_host.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void Create_StartsHidden()
        {
            Assert.That(_ghost.IsShown, Is.False);
            Assert.That(_ghost.CurrentValid, Is.False);
        }

        [Test]
        public void ShowAt_Valid_UsesValidMaterialAndPosition()
        {
            _ghost.ShowAt(10, 64, 20, valid: true);

            Assert.That(_ghost.IsShown, Is.True);
            Assert.That(_ghost.CurrentValid, Is.True);
            Assert.That(_ghost.transform.position,
                Is.EqualTo(new Vector3(10.5f, 64.5f, 20.5f)), "摆到放置格中心（格坐标 + 0.5）");
            AssertMaterialNear(PlacementGhostUi.ValidColor, "合法态 = 水蓝半透");
        }

        [Test]
        public void ShowAt_Blocked_UsesBlockedMaterial()
        {
            _ghost.ShowAt(10, 64, 20, valid: true);
            _ghost.ShowAt(10, 64, 20, valid: false);

            Assert.That(_ghost.CurrentValid, Is.False);
            AssertMaterialNear(PlacementGhostUi.BlockedColor, "被挡态 = 红半透");
        }

        private void AssertMaterialNear(Color expected, string message)
        {
            var renderer = _ghost.GetComponent<MeshRenderer>();
            Color actual = renderer.sharedMaterial.color;
            Assert.That(Mathf.Abs(actual.r - expected.r), Is.LessThan(0.01f), message + " R");
            Assert.That(Mathf.Abs(actual.g - expected.g), Is.LessThan(0.01f), message + " G");
            Assert.That(Mathf.Abs(actual.b - expected.b), Is.LessThan(0.01f), message + " B");
            Assert.That(Mathf.Abs(actual.a - expected.a), Is.LessThan(0.01f), message + " A");
        }

        [Test]
        public void Hide_ResetsState()
        {
            _ghost.ShowAt(1, 2, 3, valid: true);
            _ghost.Hide();

            Assert.That(_ghost.IsShown, Is.False);
            Assert.That(_ghost.CurrentValid, Is.False);
        }

        [Test]
        public void Renderer_DoesNotParticipateInLighting()
        {
            var renderer = _ghost.GetComponent<MeshRenderer>();
            Assert.That(renderer.receiveShadows, Is.False, "叠加层不接影（照 SelectionBox）");
            Assert.That(renderer.shadowCastingMode,
                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
        }
    }

    [TestFixture]
    public class DigCrackOverlayTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("CrackHost");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void Create_EditorMode_DoesNotThrow()
        {
            // EditMode 下 streamingAssetsPath 存在，break-*.png 已入库 → 大概率加载成功；
            // 无论加载与否都不能抛（缺图 = 功能降级，蓄力照常）
            DigCrackOverlay crack = DigCrackOverlay.Create(_host.transform);
            Assert.That(crack, Is.Not.Null);
            Assert.That(crack.CurrentStage, Is.EqualTo(-1), "创建后隐藏");
        }

        [Test]
        public void ShowAt_ClampsStage()
        {
            DigCrackOverlay crack = DigCrackOverlay.Create(_host.transform);
            crack.ShowAt(8, 70, 8, 99); // 越顶档位
            Assert.That(crack.CurrentStage, Is.EqualTo(DigCrackOverlay.StageCount - 1),
                "越顶钳到最后一档");
            crack.ShowAt(8, 70, 8, -5);
            Assert.That(crack.CurrentStage, Is.EqualTo(0), "负档钳到 0");
        }
    }
}
#endif
