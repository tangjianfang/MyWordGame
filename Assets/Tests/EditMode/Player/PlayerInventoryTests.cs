using MyWorld.Core.Items;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class PlayerInventoryTests
    {
        [Test]
        public void New_Inventory_IsAllEmpty()
        {
            var inv = new PlayerInventory();
            for (var i = 0; i < PlayerInventory.TotalSize; i++)
                Assert.That(inv.GetSlot(i).IsEmpty, Is.True);
        }

        [Test]
        public void TryAdd_FillsEmptySlots()
        {
            var inv = new PlayerInventory();
            Assert.That(inv.TryAdd(new ItemStack(7, 32), out int left), Is.True);
            Assert.That(left, Is.EqualTo(0));
        }

        [Test]
        public void TryAdd_StacksWithExisting()
        {
            var inv = new PlayerInventory();
            inv.TryAdd(new ItemStack(7, 30), out _);
            inv.TryAdd(new ItemStack(7, 20), out int left);
            Assert.That(left, Is.EqualTo(0));
            int total = 0;
            for (var i = 0; i < PlayerInventory.TotalSize; i++)
                total += inv.GetSlot(i).Count;
            Assert.That(total, Is.EqualTo(50));
        }

        [Test]
        public void TryAdd_Overflow_ReturnsLeftover()
        {
            var inv = new PlayerInventory();
            // 36 槽 × 64 = 2304 容量上限
            bool ok = inv.TryAdd(new ItemStack(7, 2400), out int left);
            Assert.That(ok, Is.False);
            Assert.That(left, Is.GreaterThan(0));
        }

        [Test]
        public void TryRemoveOne_DecrementsCount()
        {
            var inv = new PlayerInventory();
            inv.TryAdd(new ItemStack(7, 5), out _);
            Assert.That(inv.TryRemoveOne(0), Is.True);
            Assert.That(inv.GetSlot(0).Count, Is.EqualTo(4));
        }

        [Test]
        public void TryRemoveOne_OnEmpty_Fails()
        {
            var inv = new PlayerInventory();
            Assert.That(inv.TryRemoveOne(0), Is.False);
        }

        [Test]
        public void SelectedHotbar_ReturnsCorrectSlot()
        {
            var inv = new PlayerInventory();
            inv.SetSlot(3, new ItemStack(99, 1));
            inv.SelectedHotbarIndex = 3;
            Assert.That(inv.GetSelected().ItemId, Is.EqualTo(99));
        }

        [Test]
        public void MoveSlot_SwapsDisjointItems()
        {
            var inv = new PlayerInventory();
            inv.SetSlot(0, new ItemStack(1, 1));
            inv.SetSlot(5, new ItemStack(2, 1));
            inv.MoveSlot(0, 5);
            Assert.That(inv.GetSlot(0).ItemId, Is.EqualTo(2));
            Assert.That(inv.GetSlot(5).ItemId, Is.EqualTo(1));
        }

        [Test]
        public void MoveSlot_StacksSameItem()
        {
            var inv = new PlayerInventory();
            inv.SetSlot(0, new ItemStack(1, 10));
            inv.SetSlot(5, new ItemStack(1, 5));
            inv.MoveSlot(0, 5);
            Assert.That(inv.GetSlot(5).Count, Is.EqualTo(15));
            Assert.That(inv.GetSlot(0).IsEmpty, Is.True);
        }
    }
}
