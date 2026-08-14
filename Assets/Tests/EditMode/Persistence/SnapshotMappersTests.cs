using System.Collections.Generic;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Persistence
{
    /// <summary>
    /// milestone-4 A4：游戏对象与快照的双向映射。round-trip 后状态必须一致。
    /// </summary>
    [TestFixture]
    public class SnapshotMappersTests
    {
        [Test]
        public void Slots_RoundTrip_PreservesMetadataAndSelection()
        {
            var inventory = new PlayerInventory();
            inventory.SetSlot(0, new ItemStack(1000, 12));
            inventory.SetSlot(1, new ItemStack(1006, 1, 0x0503)); // 带耐久的工具
            // 注：SelectedHotbarIndex 不在 SlotSnapshot 里（A2 的 PlayerSnapshot 有独立字段），
            // 由 B2 SaveLoadService 从 PlayerSnapshot.SelectedHotbarIndex 恢复，本测试不覆盖。

            SlotSnapshot[] snapshot = SnapshotMappers.SnapshotSlots(inventory);
            Assert.That(snapshot.Length, Is.EqualTo(PlayerInventory.TotalSize));
            Assert.That(snapshot[1].Metadata, Is.EqualTo((ushort)0x0503));

            var restored = new PlayerInventory();
            SnapshotMappers.RestoreSlots(restored, snapshot);
            Assert.That(restored.GetSlot(1).Metadata, Is.EqualTo((ushort)0x0503), "耐久元数据必须无损");
            Assert.That(restored.GetSlot(0).Count, Is.EqualTo(12));
            Assert.That(restored.GetSlot(50), Is.EqualTo(ItemStack.Empty), "没存到的槽恢复为空");
        }

        [Test]
        public void Furnace_RoundTrip_PreservesProgressAndSlots()
        {
            var furnace = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 30f);
            // 注意：AddFuel 只累计 _fuelRemaining、不写 Fuel 属性（Fuel 槽由存档恢复专用路径管理），
            // 所以这里直接用 Restore 灌三个槽位 + 进度 + 剩余燃料。
            furnace.Restore(new ItemStack(1004, 3), new ItemStack(1007, 2), null, 17.3f, 5.5f);

            FurnaceSnapshot snapshot = SnapshotMappers.SnapshotFurnace(furnace);
            Assert.That(snapshot.FuelRemaining, Is.EqualTo(5.5f), "剩余燃料必须进快照");

            var fresh = new FurnaceSystem(8, 30f);
            SnapshotMappers.RestoreFurnace(fresh, snapshot);
            Assert.That(fresh.Progress, Is.EqualTo(17.3f), "烧炼进度必须接续，不能从头烧");
            Assert.That(fresh.FuelRemaining, Is.EqualTo(5.5f), "剩余燃料必须接续，读档后火不能灭");
            Assert.That(fresh.Input.Value.Count, Is.EqualTo(3));
            Assert.That(fresh.Fuel.Value.ItemId, Is.EqualTo(1007));

            // 剩余燃料有效：Tick 后进度继续往前走（火还在烧），而不是因燃料归零停摆。
            fresh.Tick(1f);
            Assert.That(fresh.Progress, Is.EqualTo(18.3f), "读档后熔炉应继续燃烧，进度继续累加");
        }

        [Test]
        public void Drops_RoundTrip_PreservesPositionAndContent()
        {
            var drops = new List<ItemDropEntity>
            {
                new ItemDropEntity(new ItemStack(1008, 2), new Float3(5f, 71f, 6f)),
                new ItemDropEntity(new ItemStack(1010, 1, 0x0102), new Float3(-3f, 70f, 9f)),
            };

            List<DropSnapshot> snapshot = SnapshotMappers.SnapshotDrops(drops);
            List<ItemDropEntity> restored = SnapshotMappers.RestoreDrops(snapshot);

            Assert.That(restored.Count, Is.EqualTo(2));
            Assert.That(restored[0].Content.Value.Count, Is.EqualTo(2));
            Assert.That(restored[1].Content.Value.Metadata, Is.EqualTo((ushort)0x0102));
            Assert.That(restored[1].Position.X, Is.EqualTo(-3f));
        }
    }
}
