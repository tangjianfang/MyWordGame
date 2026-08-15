#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.UI;
using MyWorld.Core.Items;

namespace MyWorld.Core.Tests.UI
{
    public class CraftingFurnaceUiTests
    {
        [Test]
        public void Bind_ShowsProgress()
        {
            var go = new GameObject("FurnaceUI");
            var ui = go.AddComponent<CraftingFurnaceUi>();
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            f.AddInput(new ItemStack(1, 1));
            f.AddFuel(new ItemStack(10, 1));
            ui.Bind(f);
            f.Tick(0.5f);
            ui.TickForTest();
            Assert.That(ui.CurrentProgress, Is.EqualTo(0.5f).Within(0.01f));
            Object.DestroyImmediate(go);
        }

        // ---- fix2（B1 评审 F1）：熔炉 UI 老 bug——自创建起无开关常驻左上角 ----

        [Test]
        public void Default_IsClosed_AndToggleKeyIsF()
        {
            var go = new GameObject("FurnaceUI");
            var ui = go.AddComponent<CraftingFurnaceUi>();
            ui.Bind(new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f));

            Assert.That(ui.IsOpen, Is.False,
                "熔炉 UI 默认必须关闭——旧实现 Bind 之后无条件常驻屏幕左上角");
            Assert.That(ui.ToggleKey, Is.EqualTo(KeyCode.F),
                "熔炉开关键定格 F（E/P/B/V/X 已被背包/工作台/口袋/交易/附魔占用）");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void SetOpen_TogglesOpenState()
        {
            var go = new GameObject("FurnaceUI");
            var ui = go.AddComponent<CraftingFurnaceUi>();
            ui.Bind(new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f));

            ui.SetOpen(true);
            Assert.That(ui.IsOpen, Is.True, "SetOpen(true) 后应处于打开状态");
            ui.SetOpen(false);
            Assert.That(ui.IsOpen, Is.False, "SetOpen(false) 应回到关闭状态");
            Object.DestroyImmediate(go);
        }
    }
}
#endif
