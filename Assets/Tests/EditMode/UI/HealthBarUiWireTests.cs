#if UNITY_EDITOR
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m11 W2-3：HealthBarUI 与 PlayerContext 的接线（EditMode IMGUI 测试照
    /// FoodBarUiWireTests 模式——OnGUI 不跑，TickForTest 驱动同一刷新入口）。
    /// 守：心数读的是<b>有效上限</b>（Health.Max + MaxHealthBonus，经 RefreshGearBonuses），
    /// &gt;10 心两行、半心可见。纯排布契约在 HeartMathTests（dotnet 双链同跑）。
    /// </summary>
    [TestFixture]
    public class HealthBarUiWireTests
    {
        private GameObject _host;
        private PlayerContext _ctx;
        private HealthBarUI _ui;

        /// <summary>EditMode 下 AddComponent 不会跑 Awake，用反射补一脚（BlockBreakDropTests 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(mb, null);
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("HealthBarUiWire");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Health = new Health(20);
            _ctx.Items = ItemDatabase.FromJson(new[]
            {
                // gearBonus maxHealth +2/件（机元装备的 m10 数值，schema 照 machine_essence 头盔）。
                // 12 心的两条路：手持一件（+2）或穿戴两件（+4）——本夹具测两行排布，
                // 用「穿戴头+胸两件」的布置（20 + 2×2 = 24 = 12 心，与 HeartMathTests
                // 「m10 机元装备 +2/件：2 件 +4 = 24（12 心、2 行）」同一口径）
                @"{ ""id"": ""machine_core_gear"", ""numericId"": 9401, ""gearBonus"": { ""stat"": ""maxHealth"", ""amount"": 2 } }",
                @"{ ""id"": ""machine_helm_gear"", ""numericId"": 9402, ""armorPart"": ""helmet"", ""gearBonus"": { ""stat"": ""maxHealth"", ""amount"": 2 } }",
                @"{ ""id"": ""machine_chest_gear"", ""numericId"": 9403, ""armorPart"": ""chest"", ""gearBonus"": { ""stat"": ""maxHealth"", ""amount"": 2 } }",
            });
            _ui = _host.AddComponent<HealthBarUI>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void TenHearts_SingleRow_NoBonus()
        {
            _ctx.Inventory.SelectedHotbarIndex = 0; // 空手：无加成
            _ctx.RefreshGearBonuses();

            _ui.TickForTest();

            Assert.That(_ui.CurrentHeartCount, Is.EqualTo(10), "基础 20 血 = 10 心");
            Assert.That(_ui.CurrentRowCount, Is.EqualTo(1), "≤10 心单行（旧观感不变）");
        }

        /// <summary>穿上两件 +2/件机元装备（头 + 胸），选中槽挪空——脱下时物品回背包不经手持源接力。</summary>
        private void WearTwoMaxHealthPieces()
        {
            _ctx.Inventory.SelectedHotbarIndex = 7; // 空槽：手持源恒空
            _ctx.Inventory.SetSlot(0, new ItemStack(9402, 1)); // 头盔
            _ctx.Inventory.SetSlot(1, new ItemStack(9403, 1)); // 胸甲
            Assert.That(_ctx.ArmorSlots.TryEquipFrom(_ctx.Inventory, 0, _ctx.Items), Is.True, "前置：头盔穿上");
            Assert.That(_ctx.ArmorSlots.TryEquipFrom(_ctx.Inventory, 1, _ctx.Items), Is.True, "前置：胸甲穿上");
        }

        [Test]
        public void TwelveHearts_TwoRows_WithMaxHealthBonus()
        {
            WearTwoMaxHealthPieces();
            _ctx.RefreshGearBonuses();
            Assert.That(_ctx.EffectiveMaxHealth, Is.EqualTo(24f).Within(0.01f),
                "前置：穿戴 +2/件两件生效，有效上限 24");

            _ui.TickForTest();

            Assert.That(_ui.CurrentHeartCount, Is.EqualTo(12), "有效上限 24 → 12 颗心（m10 生效但不多画心的欠账）");
            Assert.That(_ui.CurrentRowCount, Is.EqualTo(2), ">10 心两行排列");
        }

        [Test]
        public void HalfHeart_VisibleAtOddHealth()
        {
            WearTwoMaxHealthPieces();
            _ctx.RefreshGearBonuses();
            _ctx.Health.Current = 11f; // 5 心满 + 第 6 心半（余 1 点）

            _ui.TickForTest();

            Assert.That(_ui.CurrentFills.Length, Is.EqualTo(12));
            Assert.That(_ui.CurrentFills[5], Is.EqualTo(HeartMath.Fill.Half), "血量 11：第 6 颗心半心");
            Assert.That(_ui.CurrentFills[6], Is.EqualTo(HeartMath.Fill.Empty), "第 7 颗心空");
            int full = 0, half = 0, empty = 0;
            foreach (var f in _ui.CurrentFills)
            {
                if (f == HeartMath.Fill.Full) full++;
                else if (f == HeartMath.Fill.Half) half++;
                else empty++;
            }
            Assert.That((full, half, empty), Is.EqualTo((5, 1, 6)), "5 满 + 1 半 + 6 空");
        }

        [Test]
        public void BonusRemoved_HeartsShrinkBack_SameFrame()
        {
            // 两件全脱：有效上限回落 20，心数立刻缩回 10（RefreshGearBonuses 同帧钳血）。
            // 选中槽已在 WearTwoMaxHealthPieces 里挪空——脱下的装备回背包不经手持源接力
            WearTwoMaxHealthPieces();
            _ctx.RefreshGearBonuses();
            _ui.TickForTest();
            Assert.That(_ui.CurrentHeartCount, Is.EqualTo(12), "前置：戴上有 12 心");

            Assert.That(_ctx.ArmorSlots.TryUnequipTo(_ctx.Inventory, 0), Is.True, "前置：脱下头盔");
            Assert.That(_ctx.ArmorSlots.TryUnequipTo(_ctx.Inventory, 1), Is.True, "前置：脱下胸甲");
            _ctx.RefreshGearBonuses();
            _ui.TickForTest();

            Assert.That(_ui.CurrentHeartCount, Is.EqualTo(10), "切走装备心数缩回 10");
            Assert.That(_ui.CurrentRowCount, Is.EqualTo(1), "回到单行");
        }
    }
}
#endif
