#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.Bootstrap;

namespace MyWorld.Core.Tests.Bootstrap
{
    /// <summary>
    /// m5 B3：启动自适应物理屏全屏的计算逻辑契约测试。
    /// <para>
    /// ProjectSettings 固定 1920×1080 + FullScreenWindow，非 16:9 物理屏（如 2560×1600、
    /// 3440×1440）两侧会出现 pillarbox 黑边。启动时按物理屏原生分辨率 SetResolution 即可消除。
    /// 这里只测纯函数 <see cref="WorldBootstrap.ComputeAdaptiveResolution"/>——
    /// EditMode 下不能真改分辨率。
    /// </para>
    /// </summary>
    [TestFixture]
    public class AdaptiveFullscreenTests
    {
        [Test]
        public void Compute_16x9Screen_ReturnsNativeResolution()
        {
            var r = WorldBootstrap.ComputeAdaptiveResolution(1920, 1080);
            Assert.That(r, Is.Not.Null, "16:9 物理屏应返回原生分辨率（不引入黑边）");
            Assert.That(r.Value.Width, Is.EqualTo(1920), "宽度 = 物理屏系统宽");
            Assert.That(r.Value.Height, Is.EqualTo(1080), "高度 = 物理屏系统高");
            Assert.That(r.Value.Mode, Is.EqualTo(FullScreenMode.FullScreenWindow), "保持全屏窗口模式");
        }

        [Test]
        public void Compute_16x10Screen_ReturnsNativeResolution()
        {
            var r = WorldBootstrap.ComputeAdaptiveResolution(2560, 1600);
            Assert.That(r, Is.Not.Null, "16:10 物理屏应返回原生分辨率（否则两侧 pillarbox 黑边）");
            Assert.That(r.Value.Width, Is.EqualTo(2560), "宽度 = 物理屏系统宽");
            Assert.That(r.Value.Height, Is.EqualTo(1600), "高度 = 物理屏系统高");
            Assert.That(r.Value.Mode, Is.EqualTo(FullScreenMode.FullScreenWindow), "保持全屏窗口模式");
        }

        [Test]
        public void Compute_UltrawideScreen_ReturnsNativeResolution()
        {
            var r = WorldBootstrap.ComputeAdaptiveResolution(3440, 1440);
            Assert.That(r, Is.Not.Null, "超宽屏应返回原生分辨率（否则两侧 pillarbox 黑边）");
            Assert.That(r.Value.Width, Is.EqualTo(3440), "宽度 = 物理屏系统宽");
            Assert.That(r.Value.Height, Is.EqualTo(1440), "高度 = 物理屏系统高");
            Assert.That(r.Value.Mode, Is.EqualTo(FullScreenMode.FullScreenWindow), "保持全屏窗口模式");
        }

        [Test]
        public void Compute_InvalidDimensions_ReturnsNull()
        {
            Assert.That(WorldBootstrap.ComputeAdaptiveResolution(0, 1080), Is.Null, "宽为 0 → 返回 null，不调用 SetResolution");
            Assert.That(WorldBootstrap.ComputeAdaptiveResolution(1920, 0), Is.Null, "高为 0 → 返回 null，不调用 SetResolution");
            Assert.That(WorldBootstrap.ComputeAdaptiveResolution(-1, -1), Is.Null, "负值 → 返回 null，不调用 SetResolution");
        }
    }
}
#endif
