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
            // m6 C2 起用真实物品 id：圆石(1003)入料、煤(1007)燃料（占位 1/10 已修）
            f.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 1));
            f.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 1));
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

        // ---- m10 C2 fix1（I2）：进度条分母用当前输入的真实烧炼时长 ----

        [Test]
        public void ProgressFillFraction_RawOre_HalfwayAtFiveSeconds()
        {
            var go = new GameObject("FurnaceUI");
            var ui = go.AddComponent<CraftingFurnaceUi>();
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            f.AddInput(new ItemStack(FurnaceSystem.RawGoldItemId, 1));  // 粗金 10s
            f.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 2));
            ui.Bind(f);

            f.Tick(5f);
            ui.TickForTest();

            Assert.That(ui.CurrentSmeltDuration, Is.EqualTo(10f),
                "UI 应取到当前输入（粗金）的 10s 烧炼时长");
            Assert.That(ui.ProgressFillFraction, Is.EqualTo(0.5f).Within(0.01f),
                "粗矿 10s：5s 处进度条应半满——旧实现分母写死 1s，1s 就假满格后空等 9s");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ProgressFillFraction_Cobblestone_KeepsOldBasis()
        {
            var go = new GameObject("FurnaceUI");
            var ui = go.AddComponent<CraftingFurnaceUi>();
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            f.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 1));  // 圆石 1s
            f.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 1));
            ui.Bind(f);

            f.Tick(0.5f);
            ui.TickForTest();

            Assert.That(ui.ProgressFillFraction, Is.EqualTo(0.5f).Within(0.01f),
                "圆石（1s）半程处照旧半满——修复不得改变旧映射的进度观感");
            Object.DestroyImmediate(go);
        }
    }
}
#endif
