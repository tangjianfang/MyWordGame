using System;
using System.Collections.Generic;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;

namespace MyWorld.Core.Persistence
{
    /// <summary>游戏对象与 level.dat 快照的双向映射（milestone-4）。</summary>
    public static class SnapshotMappers
    {
        /// <summary>背包全部 36 槽转快照（含 Metadata——工具耐久在里面）。</summary>
        public static SlotSnapshot[] SnapshotSlots(PlayerInventory inventory)
        {
            var slots = new SlotSnapshot[PlayerInventory.TotalSize];
            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack stack = inventory.GetSlot(i);
                slots[i] = stack.IsEmpty
                    ? new SlotSnapshot()
                    : new SlotSnapshot { ItemId = stack.ItemId, Count = stack.Count, Metadata = stack.Metadata };
            }
            return slots;
        }

        /// <summary>快照逐槽写回背包。空数组/null 视为清空全部槽位。</summary>
        public static void RestoreSlots(PlayerInventory inventory, SlotSnapshot[] slots)
        {
            for (int i = 0; i < PlayerInventory.TotalSize; i++)
            {
                SlotSnapshot snap = slots != null && i < slots.Length ? slots[i] : null;
                inventory.SetSlot(i, snap == null || snap.ItemId == 0 || snap.Count <= 0
                    ? ItemStack.Empty
                    : new ItemStack(snap.ItemId, snap.Count, snap.Metadata));
            }
        }

        /// <summary>熔炉状态转快照。空槽为 null。剩余燃料一并保存，否则读档后火会灭。</summary>
        public static FurnaceSnapshot SnapshotFurnace(FurnaceSystem furnace)
        {
            return new FurnaceSnapshot
            {
                Input = ToSnap(furnace.Input),
                Fuel = ToSnap(furnace.Fuel),
                Output = ToSnap(furnace.Output),
                Progress = furnace.Progress,
                FuelRemaining = furnace.FuelRemaining,
            };
        }

        /// <summary>快照恢复熔炉。snapshot 为 null 时不动。</summary>
        public static void RestoreFurnace(FurnaceSystem furnace, FurnaceSnapshot snapshot)
        {
            if (snapshot == null) return;
            furnace.Restore(FromSnap(snapshot.Input), FromSnap(snapshot.Fuel),
                FromSnap(snapshot.Output), snapshot.Progress, snapshot.FuelRemaining);
        }

        /// <summary>地面掉落物列表转快照。</summary>
        public static List<DropSnapshot> SnapshotDrops(IEnumerable<ItemDropEntity> drops)
        {
            var result = new List<DropSnapshot>();
            foreach (ItemDropEntity drop in drops)
            {
                if (!drop.Content.HasValue) continue; // 已被拾空的壳实体不存
                result.Add(new DropSnapshot
                {
                    ItemId = drop.Content.Value.ItemId,
                    Count = drop.Content.Value.Count,
                    Metadata = drop.Content.Value.Metadata,
                    X = drop.Position.X, Y = drop.Position.Y, Z = drop.Position.Z,
                });
            }
            return result;
        }

        /// <summary>快照重建掉落实体。SpawnTime 置 0，由 Unity 侧赋当前 Time.time（宽限期重计）。</summary>
        public static List<ItemDropEntity> RestoreDrops(List<DropSnapshot> snapshots)
        {
            var result = new List<ItemDropEntity>();
            if (snapshots == null) return result;
            foreach (DropSnapshot snap in snapshots)
            {
                if (snap.ItemId == 0 || snap.Count <= 0) continue;
                result.Add(new ItemDropEntity(
                    new ItemStack(snap.ItemId, snap.Count, snap.Metadata),
                    new Float3(snap.X, snap.Y, snap.Z)));
            }
            return result;
        }

        private static SlotSnapshot ToSnap(ItemStack? stack) => stack.HasValue
            ? new SlotSnapshot { ItemId = stack.Value.ItemId, Count = stack.Value.Count, Metadata = stack.Value.Metadata }
            : null;

        private static ItemStack? FromSnap(SlotSnapshot snap) => snap == null || snap.ItemId == 0 || snap.Count <= 0
            ? (ItemStack?)null
            : new ItemStack(snap.ItemId, snap.Count, snap.Metadata);
    }
}
