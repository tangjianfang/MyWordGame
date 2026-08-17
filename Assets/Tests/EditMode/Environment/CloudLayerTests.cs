#if UNITY_EDITOR
// m11 W3-4：云层——4 片 y≈150 半透面片、确定性尺寸/偏移、漂移 wrap 在玩家周围。
//   - Bind 建 4 片 quad（去碰撞体）且高度恒 CloudY
//   - 材质走 UrpMaterialFactory.CreateOverlay（白色，着色器色 Alpha=CloudAlpha，两态材质约定）
//   - 漂移累加 + 玩家走远时云 wrap 回 FollowRadius 包裹半径内（永远在头顶可见）
//   - 同参数两次装配尺寸/偏移一致（确定性哈希，不持随机数对象）
// 依赖 UnityEngine，dotnet 链跑不动，整文件 #if UNITY_EDITOR 包裹（与 WeatherSystemTests 同款）。
using MyWorld.Unity.Environment;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Environment
{
    [TestFixture]
    public class CloudLayerTests
    {
        private GameObject _go;
        private GameObject _playerGo;
        private CloudLayer _clouds;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("CloudLayer(测试)");
            _playerGo = new GameObject("CloudPlayer(测试)");
            _clouds = _go.AddComponent<CloudLayer>();
            _clouds.Bind(_playerGo.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_playerGo != null) Object.DestroyImmediate(_playerGo);
        }

        [Test]
        public void 装配_四片面片_高度恒150_无碰撞体()
        {
            Assert.That(_go.transform.childCount, Is.EqualTo(CloudLayer.CloudCount), "Bind 建 4 片云面片");
            for (int i = 0; i < CloudLayer.CloudCount; i++)
            {
                Assert.That(_clouds.CloudPosition(i).y, Is.EqualTo(CloudLayer.CloudY),
                    $"第 {i} 片云固定在 y≈150");
                Assert.That(_clouds.CloudSize(i), Is.InRange(60f, 110f), "云边长 60-110 格");
                var cloud = _go.transform.GetChild(i);
                Assert.That(cloud.GetComponent<Collider>(), Is.Null, "云不挂碰撞体（不挡移动/射线）");
                var renderer = cloud.GetComponent<Renderer>();
                Assert.That(renderer, Is.Not.Null, "面片有渲染器");
                Assert.That(renderer.sharedMaterial, Is.Not.Null, "材质经 UrpMaterialFactory 建立");
                Assert.That(renderer.sharedMaterial.color.a, Is.EqualTo(CloudLayer.CloudAlpha).Within(0.001f),
                    "半透明走材质色 Alpha（0.30），与水同款配方");
            }
        }

        [Test]
        public void 漂移_缓慢平移_玩家走远云wrap回头顶()
        {
            _clouds.TickClouds(10f); // 10 秒漂 8 格
            Assert.That(_clouds.Drift, Is.EqualTo(CloudLayer.DriftUnitsPerSecond * 10f).Within(0.001f),
                "漂移量按速度累加");

            // 玩家瞬移 500 格：云必须 wrap 回玩家周围 FollowRadius 内（头顶永远有云）
            _playerGo.transform.position = new Vector3(500f, 70f, 500f);
            _clouds.TickClouds(1f);
            for (int i = 0; i < CloudLayer.CloudCount; i++)
            {
                var pos = _clouds.CloudPosition(i);
                Assert.That(pos.y, Is.EqualTo(CloudLayer.CloudY), "wrap 不改高度");
                Assert.That(Mathf.Abs(pos.x - 500f), Is.LessThan(CloudLayer.FollowRadius),
                    $"第 {i} 片云 X 在玩家 ±{CloudLayer.FollowRadius} 内");
                Assert.That(Mathf.Abs(pos.z - 500f), Is.LessThan(CloudLayer.FollowRadius),
                    $"第 {i} 片云 Z 在玩家 ±{CloudLayer.FollowRadius} 内");
            }

            // 长时间漂移（等效 10 分钟）也不出界
            _clouds.TickClouds(600f);
            for (int i = 0; i < CloudLayer.CloudCount; i++)
            {
                var pos = _clouds.CloudPosition(i);
                Assert.That(Mathf.Abs(pos.x - 500f), Is.LessThan(CloudLayer.FollowRadius),
                    "超长漂移后仍在包裹半径内（wrap 循环）");
            }
        }

        [Test]
        public void 确定性_两次装配尺寸一致_wrap数学双向正确()
        {
            var clouds2Go = new GameObject("CloudLayer2(测试)");
            try
            {
                var clouds2 = clouds2Go.AddComponent<CloudLayer>();
                clouds2.Bind(_playerGo.transform);
                for (int i = 0; i < CloudLayer.CloudCount; i++)
                {
                    Assert.That(clouds2.CloudSize(i), Is.EqualTo(_clouds.CloudSize(i)),
                        $"第 {i} 片尺寸由哈希派生，两次装配一致");
                    Assert.That(clouds2.CloudPosition(i), Is.EqualTo(_clouds.CloudPosition(i)),
                        $"第 {i} 片初始位置一致（确定性）");
                }
            }
            finally
            {
                Object.DestroyImmediate(clouds2Go);
            }

            // wrap 数学：正负值都折回 [-range, range)
            Assert.That(CloudLayer.WrapRange(0f, 240f), Is.EqualTo(0f), "0 在区间中心");
            Assert.That(CloudLayer.WrapRange(241f, 240f), Is.EqualTo(-239f).Within(0.001f),
                "越过 +range 从 -侧绕回");
            Assert.That(CloudLayer.WrapRange(-241f, 240f), Is.EqualTo(239f).Within(0.001f),
                "越过 -range 从 +侧绕回（负数取模正确折正）");
            Assert.That(CloudLayer.WrapRange(480f, 240f), Is.EqualTo(0f).Within(0.001f),
                "整周期回原点");
        }
    }
}
#endif
