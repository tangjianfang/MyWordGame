#if UNITY_EDITOR
using MyWorld.Core.Items;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m6 A2：ItemSlotDrawer 公共物品格的契约测试。
    /// 背包/工作台/口袋/熔炉/交易原先只有黑字 Label（叠深色 Box 不可读），
    /// 这里锁定图标管线的三条核心契约：缓存同实例、缺贴图品红占位、空槽不炸。
    /// </summary>
    [TestFixture]
    public class ItemSlotDrawerTests
    {
        [Test]
        public void GetTexture_SameDefinition_ReturnsCachedInstance()
        {
            // 用一个确定不存在的贴图名：缺贴图时占位纹理也会按贴图名入缓存，
            // 因此「同一物品两次取纹理」仍必须命中同一条缓存记录（SameAs）
            var def = new ItemDefinition { Id = "m6_a2_cache_test", Texture = "m6-a2-确实不存在-的贴图" };

            var first = ItemSlotDrawer.GetTextureOrPlaceholder(def);
            var second = ItemSlotDrawer.GetTextureOrPlaceholder(def);

            Assert.That(second, Is.SameAs(first),
                "同一物品重复取纹理必须命中缓存返回同一实例——否则每帧重建纹理是稳定 GC 来源");
        }

        [Test]
        public void GetTexture_MissingTexture_ReturnsMagentaPlaceholder()
        {
            var def = new ItemDefinition { Id = "m6_a2_missing_test", Texture = "m6-a2-另一个不存在贴图" };

            var tex = ItemSlotDrawer.GetTextureOrPlaceholder(def);

            Assert.That(tex, Is.Not.Null, "缺贴图必须返回占位纹理而不是 null——调用方直接 DrawTexture");
            Assert.That(tex.width, Is.EqualTo(1), "占位纹理应是 1×1 品红块");
            Assert.That(tex.height, Is.EqualTo(1), "占位纹理应是 1×1 品红块");
            Assert.That(tex.GetPixel(0, 0).r, Is.GreaterThan(0.9f), "占位纹理必须是品红（红通道满）");
            Assert.That(tex.GetPixel(0, 0).g, Is.LessThan(0.1f), "占位纹理必须是品红（绿通道空）");
            Assert.That(tex.GetPixel(0, 0).b, Is.GreaterThan(0.9f), "占位纹理必须是品红（蓝通道满）");
        }

        [Test]
        public void Draw_EmptySlot_DoesNotThrow()
        {
            // EditMode 下 OnGUI 之外 Event.current 为 null：Draw 必须静默返回，
            // 所有 GUI 调用都包在 Repaint 事件判定里，不能在非绘制事件里炸
            Assert.DoesNotThrow(() =>
                ItemSlotDrawer.Draw(new Rect(0, 0, 40, 40), ItemStack.Empty, null, false),
                "空槽 + 无 GUI 上下文时 Draw 必须静默返回不抛异常");
        }
    }
}
#endif
