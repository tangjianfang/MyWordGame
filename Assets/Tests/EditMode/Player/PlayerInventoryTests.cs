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

        // ---- m6 C2：CountOf——ObtainItem 事件的「背包现存量」取数口径 ----

        [Test]
        public void CountOf_跨槽合并计数_不含其它物品()
        {
            var inv = new PlayerInventory();
            inv.TryAdd(new ItemStack(1000, 64), out _); // 第 0 格叠满
            inv.TryAdd(new ItemStack(1000, 20), out _); // 溢到第 1 格
            inv.TryAdd(new ItemStack(1003, 5), out _);

            Assert.That(inv.CountOf(1000), Is.EqualTo(84), "两格 log 应合并计数 84");
            Assert.That(inv.CountOf(1003), Is.EqualTo(5), "其它物品只数自己的");
            Assert.That(inv.CountOf(9999), Is.EqualTo(0), "背包里没有的物品计 0");
            Assert.That(inv.CountOf(0), Is.EqualTo(0), "空气 id 恒为 0");
        }

        [Test]
        public void CountOf_TryRemoveCount后_数量同步扣减()
        {
            var inv = new PlayerInventory();
            inv.TryAdd(new ItemStack(1003, 3), out _);
            inv.TryRemoveCount(1003, 1);

            Assert.That(inv.CountOf(1003), Is.EqualTo(2), "移除后现存量同步扣减");
        }
    }
}
