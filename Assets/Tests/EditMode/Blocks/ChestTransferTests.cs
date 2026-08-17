using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    /// <summary>
    /// m11 W2-3：箱子界面存取纯逻辑（Core/Blocks/ChestTransfer）。
    /// 守：普通点击取放（拿整叠 / 放下 / 同物品并堆 / 异物交换）、Shift 整组转移、
    /// 箱满 / 背包满不丢物品、关窗归还兜底。纯 Core，dotnet 与 EditMode 双链同跑；
    /// Unity 侧 ChestUi 只转发点击（ChestUiTests 守接线）。
    /// </summary>
    [TestFixture]
    public class ChestTransferTests
    {
        private const int PlankItemId = 9101;
        private const int StoneItemId = 9102;
        private const int PickaxeItemId = 9103;

        private const int X = 8, Y = 70, Z = 8;

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""plank"", ""numericId"": 9101, ""maxStack"": 64 }",
                @"{ ""id"": ""stone"", ""numericId"": 9102, ""maxStack"": 64 }",
                // maxStack=1 的工具：天然永远开新行（ChestSystem 的既有语义）
                @"{ ""id"": ""pickaxe"", ""numericId"": 9103, ""maxStack"": 1 }",
            });
        }

        private static PlayerInventory BuildInventory(ItemDatabase items)
        {
            var inv = new PlayerInventory();
            inv.SetMaxStackLookup(id => items.TryGetByNumericId(id, out var d) ? d.MaxStack : 64);
            return inv;
        }

        // ─── 点击箱子行：手持空 → 整行拿上手 ────────────────────────────

        [Test]
        public void ClickChestSlot_EmptyHand_TakesWholeRowToCursor()
        {
            var chest = new ChestSystem(BuildItems());
            chest.Put(X, Y, Z, new ItemStack(PlankItemId, 12));

            var held = ItemStack.Empty;
            bool done = ChestTransfer.ClickChestSlot(chest, X, Y, Z, 0, null, ref held, shift: false);

            Assert.That(done, Is.True, "点击有内容的行应成功");
            Assert.That(held.ItemId, Is.EqualTo(PlankItemId), "整行拿到手上");
            Assert.That(held.Count, Is.EqualTo(12));
            Assert.That(chest.List(X, Y, Z).Count, Is.EqualTo(0), "箱子行已移除");
        }

        [Test]
        public void ClickChestSlot_EmptyRow_NoOp()
        {
            var chest = new ChestSystem(BuildItems());
            var held = ItemStack.Empty;

            Assert.That(ChestTransfer.ClickChestSlot(chest, X, Y, Z, 0, null, ref held, false), Is.False,
                "空行点击是 no-op");
            Assert.That(held.IsEmpty, Is.True, "手上不被污染");
        }

        // ─── 点击箱子行：手持非空 → 放回箱子（自动并堆） ─────────────────

        [Test]
        public void ClickChestSlot_HoldingStack_MergesIntoExistingRow()
        {
            var chest = new ChestSystem(BuildItems());
            chest.Put(X, Y, Z, new ItemStack(PlankItemId, 30));

            var held = new ItemStack(PlankItemId, 34);
            bool done = ChestTransfer.ClickChestSlot(chest, X, Y, Z, 0, null, ref held, false);

            Assert.That(done, Is.True);
            Assert.That(held.IsEmpty, Is.True, "放下成功手上清空");
            Assert.That(chest.List(X, Y, Z).Count, Is.EqualTo(1), "并进既有行不开新行");
            Assert.That(chest.List(X, Y, Z)[0].Count, Is.EqualTo(64), "30 + 34 = 64");
        }

        [Test]
        public void ClickChestSlot_FullChest_RejectsAndKeepsHeld()
        {
            var items = BuildItems();
            var chest = new ChestSystem(items);
            // 27 行全用互不相并的物品填满（石头 + 26 把镐）
            chest.Put(X, Y, Z, new ItemStack(StoneItemId, 64));
            for (int i = 0; i < ChestSystem.Capacity - 1; i++)
            {
                chest.Put(X, Y, Z, new ItemStack(PickaxeItemId, 1, (ushort)(i + 1)));
            }
            Assert.That(chest.List(X, Y, Z).Count, Is.EqualTo(ChestSystem.Capacity), "前置：箱子 27 行全满");

            var held = new ItemStack(PlankItemId, 5);
            bool done = ChestTransfer.ClickChestSlot(chest, X, Y, Z, 0, null, ref held, false);

            Assert.That(done, Is.False, "箱满无处并 → 拒绝");
            Assert.That(held.Count, Is.EqualTo(5), "物品留在手上，绝不丢失");
            Assert.That(chest.List(X, Y, Z).Count, Is.EqualTo(ChestSystem.Capacity), "箱子原样");
        }

        // ─── Shift：箱子行 → 背包 ───────────────────────────────────────

        [Test]
        public void ClickChestSlot_Shift_MovesWholeRowIntoInventory()
        {
            var items = BuildItems();
            var chest = new ChestSystem(items);
            chest.Put(X, Y, Z, new ItemStack(PlankItemId, 10));
            var inv = BuildInventory(items);

            var held = new ItemStack(StoneItemId, 7); // Shift 语义：手持不参与
            bool done = ChestTransfer.ClickChestSlot(chest, X, Y, Z, 0, inv, ref held, shift: true);

            Assert.That(done, Is.True);
            Assert.That(held.Count, Is.EqualTo(7), "Shift 转移不动手上的物品");
            Assert.That(inv.CountOf(PlankItemId), Is.EqualTo(10), "整行进了背包");
            Assert.That(chest.List(X, Y, Z).Count, Is.EqualTo(0), "箱子清空该行");
        }

        [Test]
        public void ClickChestSlot_Shift_PartialInventorySpace_ReturnsLeftoverToChest()
        {
            var items = BuildItems();
            var chest = new ChestSystem(items);
            chest.Put(X, Y, Z, new ItemStack(PlankItemId, 100));
            var inv = BuildInventory(items);
            // 只留一格 64 的空间：先占掉 35 格
            for (int i = 1; i < PlayerInventory.TotalSize; i++)
            {
                inv.SetSlot(i, new ItemStack(StoneItemId, 64));
            }

            var held = ItemStack.Empty;
            bool done = ChestTransfer.ClickChestSlot(chest, X, Y, Z, 0, inv, ref held, shift: true);

            Assert.That(done, Is.True);
            Assert.That(inv.CountOf(PlankItemId), Is.EqualTo(64), "装满一格 64");
            var rows = chest.List(X, Y, Z);
            Assert.That(rows.Count, Is.EqualTo(1), "装不下的 36 个回箱子（不丢物品）");
            Assert.That(rows[0].Count, Is.EqualTo(36));
        }

        // ─── 点击背包格：取放到手持 ─────────────────────────────────────

        [Test]
        public void ClickInventorySlot_EmptyHand_PicksWholeStackUp()
        {
            var items = BuildItems();
            var inv = BuildInventory(items);
            inv.SetSlot(3, new ItemStack(PlankItemId, 17));
            var held = ItemStack.Empty;

            bool done = ChestTransfer.ClickInventorySlot(null, 0, 0, 0, inv, 3, ref held, false);

            Assert.That(done, Is.True);
            Assert.That(held.Count, Is.EqualTo(17), "整格拿到手上");
            Assert.That(inv.GetSlot(3).IsEmpty, Is.True, "原格清空");
        }

        [Test]
        public void ClickInventorySlot_HeldIntoEmptySlot_PlacesDown()
        {
            var items = BuildItems();
            var inv = BuildInventory(items);
            var held = new ItemStack(PlankItemId, 9);

            bool done = ChestTransfer.ClickInventorySlot(null, 0, 0, 0, inv, 5, ref held, false);

            Assert.That(done, Is.True);
            Assert.That(held.IsEmpty, Is.True);
            Assert.That(inv.GetSlot(5).Count, Is.EqualTo(9), "手上物品放进空格");
        }

        [Test]
        public void ClickInventorySlot_SameItem_MergesUpToMaxStack()
        {
            var items = BuildItems();
            var inv = BuildInventory(items);
            inv.SetSlot(2, new ItemStack(PlankItemId, 60)); // maxStack 64，还差 4
            var held = new ItemStack(PlankItemId, 30);

            bool done = ChestTransfer.ClickInventorySlot(null, 0, 0, 0, inv, 2, ref held, false);

            Assert.That(done, Is.True);
            Assert.That(inv.GetSlot(2).Count, Is.EqualTo(64), "并到 maxStack 封顶");
            Assert.That(held.Count, Is.EqualTo(26), "手上剩 26（30 - 4）");
        }

        [Test]
        public void ClickInventorySlot_DifferentItem_Swaps()
        {
            var items = BuildItems();
            var inv = BuildInventory(items);
            inv.SetSlot(2, new ItemStack(StoneItemId, 5));
            var held = new ItemStack(PlankItemId, 8);

            bool done = ChestTransfer.ClickInventorySlot(null, 0, 0, 0, inv, 2, ref held, false);

            Assert.That(done, Is.True);
            Assert.That(inv.GetSlot(2).ItemId, Is.EqualTo(PlankItemId), "手上物品进格");
            Assert.That(inv.GetSlot(2).Count, Is.EqualTo(8));
            Assert.That(held.ItemId, Is.EqualTo(StoneItemId), "原格物品换到手上");
        }

        [Test]
        public void ClickInventorySlot_SlotFullSameItem_NoOp()
        {
            var items = BuildItems();
            var inv = BuildInventory(items);
            inv.SetSlot(2, new ItemStack(PlankItemId, 64)); // 已满
            var held = new ItemStack(PlankItemId, 1);

            bool done = ChestTransfer.ClickInventorySlot(null, 0, 0, 0, inv, 2, ref held, false);

            Assert.That(done, Is.False, "同物品格已满：并堆失败");
            Assert.That(held.Count, Is.EqualTo(1), "物品留在手上");
        }

        // ─── Shift：背包格 → 箱子 ───────────────────────────────────────

        [Test]
        public void ClickInventorySlot_Shift_MovesWholeStackIntoChest()
        {
            var items = BuildItems();
            var chest = new ChestSystem(items);
            var inv = BuildInventory(items);
            inv.SetSlot(4, new ItemStack(StoneItemId, 21));
            var held = ItemStack.Empty;

            bool done = ChestTransfer.ClickInventorySlot(chest, X, Y, Z, inv, 4, ref held, shift: true);

            Assert.That(done, Is.True);
            Assert.That(inv.GetSlot(4).IsEmpty, Is.True, "原格清空");
            Assert.That(chest.List(X, Y, Z)[0].Count, Is.EqualTo(21), "整格进了箱子");
            Assert.That(held.IsEmpty, Is.True);
        }

        [Test]
        public void ClickInventorySlot_Shift_FullChest_KeepsStackInInventory()
        {
            var items = BuildItems();
            var chest = new ChestSystem(items);
            for (int i = 0; i < ChestSystem.Capacity; i++)
            {
                chest.Put(X, Y, Z, new ItemStack(PickaxeItemId, 1, (ushort)(i + 1)));
            }
            var inv = BuildInventory(items);
            inv.SetSlot(0, new ItemStack(PlankItemId, 6));
            var held = ItemStack.Empty;

            bool done = ChestTransfer.ClickInventorySlot(chest, X, Y, Z, inv, 0, ref held, shift: true);

            Assert.That(done, Is.False, "箱满拒绝转移");
            Assert.That(inv.GetSlot(0).Count, Is.EqualTo(6), "物品留在背包，绝不丢失");
        }

        // ─── 关窗归还：背包 → 箱子 → 兜底，绝不丢 ───────────────────────

        [Test]
        public void ReturnHeld_IntoInventory_First()
        {
            var items = BuildItems();
            var inv = BuildInventory(items);
            var held = new ItemStack(PlankItemId, 7);

            ItemStack remainder = ChestTransfer.ReturnHeld(null, 0, 0, 0, inv, ref held);

            Assert.That(remainder.IsEmpty, Is.True, "背包装得下：全部归位");
            Assert.That(held.IsEmpty, Is.True, "手持清空");
            Assert.That(inv.CountOf(PlankItemId), Is.EqualTo(7));
        }

        [Test]
        public void ReturnHeld_InventoryFull_GoesToChest()
        {
            var items = BuildItems();
            var chest = new ChestSystem(items);
            var inv = BuildInventory(items);
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                inv.SetSlot(i, new ItemStack(StoneItemId, 64)); // 背包全满
            }
            var held = new ItemStack(PlankItemId, 5);

            ItemStack remainder = ChestTransfer.ReturnHeld(chest, X, Y, Z, inv, ref held);

            Assert.That(remainder.IsEmpty, Is.True, "箱子接住了余量");
            Assert.That(chest.List(X, Y, Z)[0].Count, Is.EqualTo(5), "余量进了箱子（不是掉落物）");
        }

        [Test]
        public void ReturnHeld_BothFull_ReturnsRemainderForCaller()
        {
            var items = BuildItems();
            var chest = new ChestSystem(items);
            for (int i = 0; i < ChestSystem.Capacity; i++)
            {
                chest.Put(X, Y, Z, new ItemStack(PickaxeItemId, 1, (ushort)(i + 1)));
            }
            var inv = BuildInventory(items);
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                inv.SetSlot(i, new ItemStack(StoneItemId, 64));
            }
            var held = new ItemStack(PlankItemId, 5);

            ItemStack remainder = ChestTransfer.ReturnHeld(chest, X, Y, Z, inv, ref held);

            Assert.That(remainder.Count, Is.EqualTo(5), "双栏全满：余量交还调用方 spawn 掉落物");
            Assert.That(held.IsEmpty, Is.True, "责任已移交，手持清空");
        }

        // ─── 耐久编码随行走（Metadata 无损，m11 W1-4 的存档契约在转移层同样成立） ──

        [Test]
        public void Transfers_PreserveDurabilityMetadata()
        {
            var items = BuildItems();
            var chest = new ChestSystem(items);
            var inv = BuildInventory(items);
            inv.SetSlot(0, new ItemStack(PickaxeItemId, 1, 0x5B23)); // 高 8 位耐久上限 0x5B + 低 8 位当前 0x23（m10 B1 编码）
            var held = ItemStack.Empty;

            ChestTransfer.ClickInventorySlot(chest, X, Y, Z, inv, 0, ref held, shift: true);
            var rows = chest.List(X, Y, Z);

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Metadata, Is.EqualTo((ushort)0x5B23), "Metadata 逐位无损（放箱不掉耐久编码）");
        }
    }
}
