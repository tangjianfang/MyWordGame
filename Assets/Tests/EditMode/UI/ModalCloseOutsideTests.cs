#if UNITY_EDITOR
// m13 W4 模态 UI 点外关闭（孩子原话第 9 行："当我按开背包和工作台之后，鼠标点击其他地方，
// 它可以自动消失"）。六个 UI 逐一断言：
//   - 面板未开 → 任何点击不触发关闭
//   - 鼠标在面板矩形内 → 不关闭
//   - 鼠标在面板矩形外 + 左键 MouseDown → 关闭
//   - 非左键 / 非 MouseDown → 不关闭
//   - SHIFT+click 优先级保留（CraftingInventoryUi / CraftingWorkbenchUi）：
//     SHIFT+click 主背包格是「入合成网格」语义，不应被点外关闭分支吞掉
//
// 测试走 raw 值版 ShouldCloseOnMouseDown（与 DeathScreenUi 同款 EditMode 直调入口），
// 避免构造 Event.current。
using System.Reflection;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    [TestFixture]
    public class ModalCloseOutsideTests
    {
        /// <summary>EditMode 下 AddComponent 不回调 Awake——统一反射补 Awake（CraftingInventoryUi 等用）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var m = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            m?.Invoke(mb, null);
        }

        /// <summary>设置 { get; private set; } 自动属性的 backing field（点外测试需要预填 BackgroundBounds）。</summary>
        private static void SetBackingField(object instance, string propName, object value)
        {
            var prop = instance.GetType().GetProperty(propName);
            Assert.That(prop, Is.Not.Null, instance.GetType().Name + "." + propName + " 存在");
            var field = prop.DeclaringType.GetField(
                $"<{prop.Name}>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, instance.GetType().Name + "." + propName + " 是 auto-prop");
            field.SetValue(instance, value);
        }

        // ─── CraftingInventoryUi（m13 P0 已加 SHIFT+click —— 必须保留） ─────

        [Test]
        public void CraftingInventoryUi_点外左键_关闭()
        {
            var go = new GameObject("背包");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<CraftingInventoryUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(20, 20, 436, 380));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(500, 500)),
                Is.True, "面板外左键 = 关闭");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CraftingInventoryUi_点内左键_不关闭()
        {
            var go = new GameObject("背包");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<CraftingInventoryUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(20, 20, 436, 380));

            // 鼠标在 bg 内（矩形含右边下边——Rect.Contains 是闭区间）
            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(200, 200)),
                Is.False, "面板内点击不关闭");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CraftingInventoryUi_ShiftClick主背包格_不关闭_优先级保留()
        {
            // m13 P0 修复路径：SHIFT+click 主背包格 → PutMainSlotOne 入合成网格
            // （孩子"没办法合成"的修复入口）。SHIFT 必须优先于点外关闭——
            // 否则开背包时按 SHIFT+click 主背包的木板会被本分支先关掉，路径彻底失效。
            var go = new GameObject("背包");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<CraftingInventoryUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(20, 20, 436, 380));

            // 鼠标在面板外但带 SHIFT → 走 PutMainSlotOne 路径，不该被关
            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, true, new Vector2(500, 500)),
                Is.False, "SHIFT 优先：合成 UI 的入料路径不能误关");

            // 鼠标在面板内 + SHIFT → 仍走 PutMainSlotOne，更不该误关
            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, true, new Vector2(200, 200)),
                Is.False, "面板内 SHIFT+click 走合成入料，无关点外判定");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CraftingInventoryUi_未开时点外_不关闭()
        {
            var go = new GameObject("背包");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<CraftingInventoryUi>();
            // SetOpen(false) —— BackgroundBounds 默认 Rect.zero 也会包含 (0,0)
            // _open=false 必须在 BackgroundBounds.Contains 之前先拦，否则点 (0,0) 会被误关

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(0, 0)),
                Is.False, "未开时不构成关闭");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CraftingInventoryUi_非左键_不关闭()
        {
            var go = new GameObject("背包");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<CraftingInventoryUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(20, 20, 436, 380));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 1, false, new Vector2(500, 500)),
                Is.False, "右键不是关闭谓词");
            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseUp, 0, false, new Vector2(500, 500)),
                Is.False, "MouseUp 不是关闭谓词");

            Object.DestroyImmediate(go);
        }

        // ─── CraftingWorkbenchUi（同款断言，SHIFT 路径与 CraftingInventoryUi 对称） ─

        [Test]
        public void CraftingWorkbenchUi_点外左键_关闭()
        {
            var go = new GameObject("工作台");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<CraftingWorkbenchUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(200, 100, 420, 380));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(50, 50)),
                Is.True);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CraftingWorkbenchUi_点内左键_不关闭()
        {
            var go = new GameObject("工作台");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<CraftingWorkbenchUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(200, 100, 420, 380));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(400, 300)),
                Is.False);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CraftingWorkbenchUi_ShiftClick主背包格_不关闭_优先级保留()
        {
            var go = new GameObject("工作台");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<CraftingWorkbenchUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(200, 100, 420, 380));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, true, new Vector2(50, 50)),
                Is.False, "工作台 SHIFT+click 主背包格入合成网格，路径必须保留");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CraftingWorkbenchUi_未开_不关闭()
        {
            var go = new GameObject("工作台");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<CraftingWorkbenchUi>();

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(0, 0)),
                Is.False);

            Object.DestroyImmediate(go);
        }

        // ─── ChestUi ─────────────────────────────────────────────────────

        [Test]
        public void ChestUi_点外左键_关闭()
        {
            var go = new GameObject("箱子");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            ctx.Items = ItemDatabase.FromJson(new[] { @"{ ""id"": ""x"", ""numericId"": 1, ""maxStack"": 64 }" });
            var ui = go.AddComponent<ChestUi>();
            ui.Open(0, 0, 0);
            SetBackingField(ui, "BackgroundBounds", new Rect(100, 100, 400, 300));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(50, 50)),
                Is.True, "箱子面板外左键 = 关闭");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ChestUi_点内左键_不关闭()
        {
            var go = new GameObject("箱子");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            ctx.Items = ItemDatabase.FromJson(new[] { @"{ ""id"": ""x"", ""numericId"": 1, ""maxStack"": 64 }" });
            var ui = go.AddComponent<ChestUi>();
            ui.Open(0, 0, 0);
            SetBackingField(ui, "BackgroundBounds", new Rect(100, 100, 400, 300));

            // 关闭按钮（bg.xMax - 90, bg.y + 4, 70, 26 = (410, 104)..(480, 130)）—— 必须在面板内
            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(440, 117)),
                Is.False, "点关闭按钮不构成「点外」——按按钮走独立关闭路径");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ChestUi_ShiftClick背包格_不关闭_优先级保留()
        {
            // 箱子面板 SHIFT+click 背包格 = 整叠转移进箱子（m11 W2-3 既有路径）
            var go = new GameObject("箱子");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            ctx.Items = ItemDatabase.FromJson(new[] { @"{ ""id"": ""x"", ""numericId"": 1, ""maxStack"": 64 }" });
            var ui = go.AddComponent<ChestUi>();
            ui.Open(0, 0, 0);
            SetBackingField(ui, "BackgroundBounds", new Rect(100, 100, 400, 300));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, true, new Vector2(50, 50)),
                Is.False, "SHIFT 优先：箱子面板的整叠转移路径不能误关");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ChestUi_未开_不关闭()
        {
            var go = new GameObject("箱子");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<ChestUi>();
            // 没 Open：IsOpen = false

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(50, 50)),
                Is.False);

            Object.DestroyImmediate(go);
        }

        // ─── ArmorSlotsUi ────────────────────────────────────────────────

        [Test]
        public void ArmorSlotsUi_自管模式_点外左键_关闭()
        {
            var go = new GameObject("穿戴栏");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<ArmorSlotsUi>();
            // Backpack = null（自管模式）
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(464, 20, 160, 278));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(50, 50)),
                Is.True, "自管模式下点外 = 关闭");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ArmorSlotsUi_跟随模式_点外不独立关闭()
        {
            // Backpack != null：跟随背包开关——自身点外关闭被拦截，由背包面板接管
            var backpackGo = new GameObject("背包跟随");
            var backpackCtx = backpackGo.AddComponent<PlayerContext>();
            InvokeAwake(backpackCtx);
            backpackCtx.Inventory = new PlayerInventory();
            var backpackUi = backpackGo.AddComponent<CraftingInventoryUi>();

            var go = new GameObject("穿戴栏跟随");
            var ctx = go.AddComponent<PlayerContext>();
            // 注：不调 InvokeAwake(ctx) —— backpackCtx 已经占据 PlayerContext.Instance，
            // 再 Awake 会进 `Instance != null && Instance != this` 分支调 Destroy(this)，
            // EditMode 下 Destroy 抛 error 把后续断言全打断。本测试只验 ShouldCloseOnMouseDown，
            // 不读 Instance，所以这步可省。
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<ArmorSlotsUi>();
            ui.Backpack = backpackUi; // 绑定 = 跟随模式
            backpackUi.SetOpen(true); // 背包开 → 穿戴栏 IsVisible = true
            SetBackingField(ui, "BackgroundBounds", new Rect(464, 20, 160, 278));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(50, 50)),
                Is.False, "跟随背包时不独立关闭——背包面板点外关闭会顺带关穿戴栏");

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(backpackGo);
        }

        [Test]
        public void ArmorSlotsUi_点内左键_不关闭()
        {
            var go = new GameObject("穿戴栏");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<ArmorSlotsUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(464, 20, 160, 278));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(500, 100)),
                Is.False);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ArmorSlotsUi_未开_不关闭()
        {
            var go = new GameObject("穿戴栏");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<ArmorSlotsUi>();
            // SetOpen(false)：跟随 Backpack = null → IsVisible = false

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(50, 50)),
                Is.False, "未开时点外不构成关闭");

            Object.DestroyImmediate(go);
        }

        // ─── WeaponPanelUi（W4 新建，统一行为） ─────────────────────────────

        [Test]
        public void WeaponPanelUi_点外左键_关闭()
        {
            var go = new GameObject("武器面板");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            ctx.Items = ItemDatabase.FromJson(new[] { @"{ ""id"": ""x"", ""numericId"": 1, ""maxStack"": 64 }" });
            var ui = go.AddComponent<WeaponPanelUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(100, 100, 360, 200));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(50, 50)),
                Is.True);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void WeaponPanelUi_点内左键_不关闭()
        {
            var go = new GameObject("武器面板");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            ctx.Items = ItemDatabase.FromJson(new[] { @"{ ""id"": ""x"", ""numericId"": 1, ""maxStack"": 64 }" });
            var ui = go.AddComponent<WeaponPanelUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(100, 100, 360, 200));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(250, 200)),
                Is.False);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void WeaponPanelUi_ShiftClick_不关闭_为合成UI留路()
        {
            // R 面板开着时按 SHIFT+click 背包格 = 合成 UI 入料路径（背包面板的 SHIFT 优先分支）
            // ——R 面板自己不应误关
            var go = new GameObject("武器面板");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            ctx.Items = ItemDatabase.FromJson(new[] { @"{ ""id"": ""x"", ""numericId"": 1, ""maxStack"": 64 }" });
            var ui = go.AddComponent<WeaponPanelUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(100, 100, 360, 200));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, true, new Vector2(50, 50)),
                Is.False);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void WeaponPanelUi_未开_不关闭()
        {
            var go = new GameObject("武器面板");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            var ui = go.AddComponent<WeaponPanelUi>();

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 0, false, new Vector2(50, 50)),
                Is.False);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void WeaponPanelUi_非左键_不关闭()
        {
            var go = new GameObject("武器面板");
            var ctx = go.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            ctx.Inventory = new PlayerInventory();
            ctx.Items = ItemDatabase.FromJson(new[] { @"{ ""id"": ""x"", ""numericId"": 1, ""maxStack"": 64 }" });
            var ui = go.AddComponent<WeaponPanelUi>();
            ui.SetOpen(true);
            SetBackingField(ui, "BackgroundBounds", new Rect(100, 100, 360, 200));

            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseDown, 1, false, new Vector2(50, 50)),
                Is.False, "右键不算");
            Assert.That(ui.ShouldCloseOnMouseDown(EventType.MouseUp, 0, false, new Vector2(50, 50)),
                Is.False, "MouseUp 不算");

            Object.DestroyImmediate(go);
        }
    }
}
#endif