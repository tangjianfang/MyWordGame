#if UNITY_EDITOR
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m11 W2-3：箱子界面接线（Unity/UI/ChestUi）。守：OpenAt 懒挂 + 指针门开关、
    /// 点击转发到 Core ChestTransfer（取放语义的纯逻辑守卫在 ChestTransferTests，双链同跑）、
    /// 关窗归还手持、存档链往返（ChestSystem → LevelData.ChestContents → 回读）。
    /// OnGUI 不跑，直调公共入口（照 CraftingFurnaceUiTests 模式）。
    /// </summary>
    [TestFixture]
    public class ChestUiTests
    {
        private const int PlankItemId = 9201;
        private const int StoneItemId = 9202;

        private GameObject _host;
        private PlayerContext _ctx;

        /// <summary>EditMode 下 AddComponent 不会跑 Awake，用反射补一脚（BlockBreakDropTests 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(mb, null);
        }

        [SetUp]
        public void SetUp()
        {
            UiCursorGate.Reset();
            _host = new GameObject("ChestUiCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Items = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""plank"", ""numericId"": 9201, ""maxStack"": 64 }",
                @"{ ""id"": ""stone"", ""numericId"": 9202, ""maxStack"": 64 }",
            });
            _ctx.Inventory.SetMaxStackLookup(
                id => _ctx.Items.TryGetByNumericId(id, out var d) ? d.MaxStack : 64);
            _ctx.ChestSystem = new ChestSystem(_ctx.Items);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            UiCursorGate.Reset();
        }

        // ─── 打开 / 关闭：懒挂 + 指针门 ─────────────────────────────────

        [Test]
        public void OpenAt_LazilyMounts_OpensGate_SnapshotsChestCoords()
        {
            var ui = ChestUi.OpenAt(_host, _ctx, 8, 70, -3);

            Assert.That(ui, Is.Not.Null, "宿主上懒挂成功（WorldBootstrap 零装配）");
            Assert.That(ui.IsOpen, Is.True);
            Assert.That((ui.ChestX, ui.ChestY, ui.ChestZ), Is.EqualTo((8, 70, -3)), "坐标快照（负 z 原样进键）");
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(1), "开箱登记指针门（IMGUI 要解锁指针才能点格子）");
            Assert.That(_host.GetComponent<ChestUi>(), Is.EqualTo(ui), "同一宿主复用同一组件");
        }

        [Test]
        public void OpenAt_WithoutChestSystem_ReturnsNull_NoGateLeak()
        {
            _ctx.ChestSystem = null;

            var ui = ChestUi.OpenAt(_host, _ctx, 1, 2, 3);

            Assert.That(ui, Is.Null, "ChestSystem 降级时不开（BlockInteraction 分支保持消费右键）");
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(0), "不开门不泄漏");
        }

        [Test]
        public void Close_ReleasesGate_AndReturnsHeldStackToInventory()
        {
            var ui = ChestUi.OpenAt(_host, _ctx, 8, 70, -3);
            _ctx.ChestSystem.Put(8, 70, -3, new ItemStack(PlankItemId, 5));
            ui.ClickChestSlot(0, shift: false);
            Assert.That(ui.Held.Count, Is.EqualTo(5), "前置：手上拿着 5 个木板");

            ui.Close();

            Assert.That(ui.IsOpen, Is.False);
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(0), "门位还回（门计数归零）");
            Assert.That(_ctx.Inventory.CountOf(PlankItemId), Is.EqualTo(5), "关窗手持归还背包，绝不丢");
        }

        // ─── 点击转发：箱子 ↔ 手持 ↔ 背包 ──────────────────────────────

        [Test]
        public void ClickChestSlot_TakesRowToHeld_ThenPlacesBackIntoChest()
        {
            var ui = ChestUi.OpenAt(_host, _ctx, 8, 70, -3);
            _ctx.ChestSystem.Put(8, 70, -3, new ItemStack(PlankItemId, 7));

            Assert.That(ui.ClickChestSlot(0), Is.True, "点箱子行：整行拿到手上");
            Assert.That(ui.Held.Count, Is.EqualTo(7));
            Assert.That(ui.ChestRows().Count, Is.EqualTo(0));

            Assert.That(ui.ClickChestSlot(0), Is.True, "再点箱子区（空行）：手上的放回箱子");
            Assert.That(ui.Held.IsEmpty, Is.True);
            Assert.That(ui.ChestRows()[0].Count, Is.EqualTo(7), "内容回到箱子（真源 ChestSystem，存档链已通）");
        }

        [Test]
        public void ClickInventorySlot_PickAndPlace_RoundTrip()
        {
            var ui = ChestUi.OpenAt(_host, _ctx, 8, 70, -3);
            _ctx.Inventory.SetSlot(4, new ItemStack(StoneItemId, 9));

            Assert.That(ui.ClickInventorySlot(4), Is.True, "点背包格：拿到手上");
            Assert.That(ui.Held.ItemId, Is.EqualTo(StoneItemId));
            Assert.That(_ctx.Inventory.GetSlot(4).IsEmpty, Is.True);

            Assert.That(ui.ClickInventorySlot(10), Is.True, "点另一空格：放下");
            Assert.That(_ctx.Inventory.GetSlot(10).Count, Is.EqualTo(9));
        }

        [Test]
        public void ClickSlots_ShiftTransfer_BothDirections()
        {
            var ui = ChestUi.OpenAt(_host, _ctx, 8, 70, -3);
            _ctx.ChestSystem.Put(8, 70, -3, new ItemStack(PlankItemId, 6));
            _ctx.Inventory.SetSlot(0, new ItemStack(StoneItemId, 4));

            Assert.That(ui.ClickChestSlot(0, shift: true), Is.True, "Shift 点箱子行 → 背包");
            Assert.That(_ctx.Inventory.CountOf(PlankItemId), Is.EqualTo(6));
            Assert.That(ui.ChestRows().Count, Is.EqualTo(0));

            Assert.That(ui.ClickInventorySlot(0, shift: true), Is.True, "Shift 点背包格 → 箱子");
            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.True);
            Assert.That(ui.ChestRows()[0].Count, Is.EqualTo(4));
        }

        // ─── 存档链往返：UI 操作后经 LevelData 全量往返 ─────────────────

        [Test]
        public void UiOperations_SurviveSaveLoadRoundtrip()
        {
            var ui = ChestUi.OpenAt(_host, _ctx, 8, 70, -3);
            _ctx.Inventory.SetSlot(0, new ItemStack(PlankItemId, 30));
            ui.ClickInventorySlot(0, shift: true); // 背包 → 箱子
            _ctx.Inventory.SetSlot(1, new ItemStack(StoneItemId, 12));
            ui.ClickInventorySlot(1, shift: true);
            ui.Close();

            // 存：SaveLoadService 同款路径（ChestSystem.SaveTo → LevelData.ChestContents）
            var data = new LevelData();
            _ctx.ChestSystem.SaveTo(data);
            Assert.That(data.ChestContents.ContainsKey(ChestSystem.Key(8, 70, -3)), Is.True,
                "箱子坐标进了 level.dat（键格式 x,y,z）");

            // 读：新 ChestSystem 全量恢复（读档 = 新世界重建）
            var restored = new ChestSystem(_ctx.Items);
            restored.LoadFrom(data);
            var rows = restored.List(8, 70, -3);

            Assert.That(rows.Count, Is.EqualTo(2), "两行都活着");
            Assert.That(rows[0].ItemId, Is.EqualTo(PlankItemId) | Is.EqualTo(StoneItemId), "行内容无损");
            Assert.That(rows[0].Count + rows[1].Count, Is.EqualTo(42), "30 + 12 一个不少");
        }

        // ─── 换箱子：手持不跟着带进另一个箱子 ───────────────────────────

        [Test]
        public void OpenAt_SwitchChestWhileHolding_ReturnsStackFirst()
        {
            var ui = ChestUi.OpenAt(_host, _ctx, 1, 70, 1);
            _ctx.ChestSystem.Put(1, 70, 1, new ItemStack(PlankItemId, 5));
            ui.ClickChestSlot(0);
            Assert.That(ui.Held.Count, Is.EqualTo(5), "前置：手上拿着箱子 A 的物品");

            ui.Open(2, 70, 2); // 不关面板直接开箱子 B（对着另一格右键）

            Assert.That((ui.ChestX, ui.ChestZ), Is.EqualTo((2, 2)), "目标切到箱子 B");
            Assert.That(ui.Held.IsEmpty, Is.True, "换箱前手持先归还（不静默带进 B）");
            Assert.That(_ctx.Inventory.CountOf(PlankItemId), Is.EqualTo(5), "归还进背包");
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(1), "面板没关过：门位不抖动");
        }
    }
}
#endif
