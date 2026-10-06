#if UNITY_EDITOR
// 评审 08 F4：回家罗盘 HUD 的方向量化与两态文案（纯静态函数断言，双链可跑——
// 但类在 MyWorld.Unity，dotnet 链不编译本文件，整文件 #if UNITY_EDITOR 包裹）。
using MyWorld.Core.Math;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    [TestFixture]
    public class HomeCompassUiTests
    {
        [Test]
        public void 无床_提示文案()
        {
            string line = HomeCompassUi.FormatLine(null, Vector2.zero);
            StringAssert.Contains("无家", line, "无床要教孩子「放床就有方向」");
        }

        [Test]
        public void 有床_距离与方位齐全()
        {
            // 床在正北 100m（dz=+100，+Z 为北）
            string north = HomeCompassUi.FormatLine(new Float3(0f, 70f, 100f), Vector2.zero);
            StringAssert.Contains("100m", north, "水平距离（Y 不计入）");
            StringAssert.Contains("↑", north, "正北箭头");

            // 床在西南 3,4m → 5m ↙
            string southWest = HomeCompassUi.FormatLine(new Float3(-3f, 70f, -4f), Vector2.zero);
            StringAssert.Contains("5m", southWest);
            StringAssert.Contains("↙", southWest, "西南箭头");

            // 贴脸（≤2m）
            Assert.That(HomeCompassUi.FormatLine(new Float3(1f, 70f, 1f), Vector2.zero),
                Is.EqualTo("到家了"), "贴脸显示到家");
        }

        [Test]
        public void HeadingArrow_八向量化()
        {
            // 北/东北/东/东南/南/西南/西/西北（dx=东向, dz=北向）
            Assert.That(HomeCompassUi.HeadingArrow(0f, 10f), Is.EqualTo("↑"), "北");
            Assert.That(HomeCompassUi.HeadingArrow(10f, 10f), Is.EqualTo("↗"), "东北");
            Assert.That(HomeCompassUi.HeadingArrow(10f, 0f), Is.EqualTo("→"), "东");
            Assert.That(HomeCompassUi.HeadingArrow(10f, -10f), Is.EqualTo("↘"), "东南");
            Assert.That(HomeCompassUi.HeadingArrow(0f, -10f), Is.EqualTo("↓"), "南");
            Assert.That(HomeCompassUi.HeadingArrow(-10f, -10f), Is.EqualTo("↙"), "西南");
            Assert.That(HomeCompassUi.HeadingArrow(-10f, 0f), Is.EqualTo("←"), "西");
            Assert.That(HomeCompassUi.HeadingArrow(-10f, 10f), Is.EqualTo("↖"), "西北");
            Assert.That(HomeCompassUi.HeadingArrow(0f, 0f), Is.EqualTo("·"), "零向量不炸");
        }
    }
}
#endif
