using System;
using System.IO;
using MyWorld.Core.Items;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Persistence
{
    /// <summary>
    /// m11 W2-1：穿戴栏进存档——<see cref="SnapshotMappers"/> 双向映射 +
    /// <see cref="LevelDataCodec"/> JSON 往返 + 旧档无 armorSlots 字段兼容
    ///（照 I3「缺键 → 空集合」归一模式）。纯 Core，双链都跑。
    /// </summary>
    [TestFixture]
    public class ArmorSlotsPersistenceTests
    {
        private static ArmorInventory BuildWornArmor(ItemDatabase db)
        {
            var armor = new ArmorInventory();
            var inv = new PlayerInventory();
            inv.SetSlot(0, new ItemStack(db.GetById("iron_helmet").NumericId, 1));
            inv.SetSlot(1, new ItemStack(db.GetById("alloy_chest").NumericId, 1, 0x0302));
            inv.SetSlot(2, new ItemStack(db.GetById("gold_boots").NumericId, 1));
            armor.TryEquipFrom(inv, 0, db);
            armor.TryEquipFrom(inv, 1, db);
            armor.TryEquipFrom(inv, 2, db);
            return armor;
        }

        private static ItemDatabase BuildDb()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""iron_helmet"", ""numericId"": 1450, ""armorPart"": ""helmet"" }",
                @"{ ""id"": ""alloy_chest"", ""numericId"": 1459, ""armorPart"": ""chest"" }",
                @"{ ""id"": ""gold_boots"", ""numericId"": 1457, ""armorPart"": ""boots"" }",
            });
        }

        // ─── SnapshotMappers 双向映射 ────────────────────────────────────

        [Test]
        public void 快照_四槽定长_含Metadata_空槽照存()
        {
            var db = BuildDb();
            var armor = BuildWornArmor(db); // 头/胸/脚三件，腿空

            SlotSnapshot[] snapshot = SnapshotMappers.SnapshotArmor(armor);

            Assert.That(snapshot.Length, Is.EqualTo(ArmorInventory.SlotCount), "快照恒 4 槽定长");
            Assert.That(snapshot[0].ItemId, Is.EqualTo(1450), "头盔在第 0 槽");
            Assert.That(snapshot[1].Metadata, Is.EqualTo((ushort)0x0302), "Metadata 必须无损（耐久/附魔在里面）");
            Assert.That(snapshot[3].ItemId, Is.EqualTo(1457), "靴子在第 3 槽");
            Assert.That(snapshot[2].ItemId, Is.EqualTo(0), "空腿槽存空槽快照");
        }

        [Test]
        public void 恢复_逐槽写回_往返一致()
        {
            var db = BuildDb();
            var armor = BuildWornArmor(db);

            var restored = new ArmorInventory();
            SnapshotMappers.RestoreArmor(restored, SnapshotMappers.SnapshotArmor(armor));

            for (int i = 0; i < ArmorInventory.SlotCount; i++)
            {
                Assert.That(restored.GetSlot(i), Is.EqualTo(armor.GetSlot(i)), $"第 {i} 槽往返必须一致");
            }
        }

        [Test]
        public void 恢复_null或空数组_清空全部穿戴()
        {
            var db = BuildDb();
            var armor = BuildWornArmor(db);

            SnapshotMappers.RestoreArmor(armor, null);
            for (int i = 0; i < ArmorInventory.SlotCount; i++)
            {
                Assert.That(armor.GetSlot(i), Is.EqualTo(ItemStack.Empty), "null 快照 = 清空穿戴");
            }

            armor = BuildWornArmor(db);
            SnapshotMappers.RestoreArmor(armor, new SlotSnapshot[0]);
            for (int i = 0; i < ArmorInventory.SlotCount; i++)
            {
                Assert.That(armor.GetSlot(i), Is.EqualTo(ItemStack.Empty), "空数组（旧档归一形态）同样清空");
            }
        }

        [Test]
        public void 恢复_短数组_缺的槽清空_存到的写回()
        {
            var armor = new ArmorInventory();
            SnapshotMappers.RestoreArmor(armor, new[]
            {
                new SlotSnapshot { ItemId = 1450, Count = 1, Metadata = 0x0101 },
            });

            Assert.That(armor.GetSlot(0).ItemId, Is.EqualTo(1450), "存到的第 0 槽写回");
            for (int i = 1; i < ArmorInventory.SlotCount; i++)
            {
                Assert.That(armor.GetSlot(i), Is.EqualTo(ItemStack.Empty), "没存到的槽清空");
            }
        }

        // ─── LevelDataCodec JSON 往返 ────────────────────────────────────

        [Test]
        public void Codec往返_穿戴四槽无损()
        {
            string path = Path.Combine(Path.GetTempPath(), $"armor-{Guid.NewGuid():N}.dat");
            try
            {
                var data = new LevelData
                {
                    Seed = 7,
                    Player = new PlayerSnapshot
                    {
                        Slots = new SlotSnapshot[0],
                        ArmorSlots = new[]
                        {
                            new SlotSnapshot { ItemId = 1450, Count = 1, Metadata = 0 },
                            new SlotSnapshot { ItemId = 1459, Count = 1, Metadata = 0x0302 },
                            new SlotSnapshot(),
                            new SlotSnapshot { ItemId = 1457, Count = 1, Metadata = 0 },
                        },
                    },
                };
                LevelDataCodec.Save(data, path);

                var loaded = LevelDataCodec.Load(path);

                Assert.That(loaded.Player.ArmorSlots, Is.Not.Null, "穿戴栏必须随档保存");
                Assert.That(loaded.Player.ArmorSlots.Length, Is.EqualTo(4));
                Assert.That(loaded.Player.ArmorSlots[0].ItemId, Is.EqualTo(1450));
                Assert.That(loaded.Player.ArmorSlots[1].Metadata, Is.EqualTo((ushort)0x0302), "耐久/附魔元数据无损");
                Assert.That(loaded.Player.ArmorSlots[2].ItemId, Is.EqualTo(0), "空槽往返仍是空");
                Assert.That(loaded.Player.ArmorSlots[3].ItemId, Is.EqualTo(1457));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void 旧档无armorSlots字段_归一为空数组_不炸()
        {
            // 旧档 JSON 字面量：Player 节点没有任何 armorSlots 键（m11 之前的真实档形态）
            string path = Path.Combine(Path.GetTempPath(), $"armor-old-{Guid.NewGuid():N}.dat");
            try
            {
                File.WriteAllText(path,
                    @"{ ""Seed"": 7, ""TimeTick"": 100.0, ""Player"": { ""X"": 1, ""Y"": 70, ""Z"": 2,
                       ""HealthCurrent"": 20, ""HealthMax"": 20, ""Slots"": [] } }");

                var loaded = LevelDataCodec.Load(path);

                Assert.That(loaded.Player.ArmorSlots, Is.Not.Null,
                    "旧档缺键归一为空数组（照 I3 模式），后续代码直接用不 NPE");
                Assert.That(loaded.Player.ArmorSlots.Length, Is.EqualTo(0), "空数组 = 空穿戴");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void 旧档无Player节点_加载仍成功()
        {
            string path = Path.Combine(Path.GetTempPath(), $"armor-noplayer-{Guid.NewGuid():N}.dat");
            try
            {
                File.WriteAllText(path, @"{ ""Seed"": 7, ""TimeTick"": 100.0 }");

                var loaded = LevelDataCodec.Load(path);

                Assert.That(loaded.Player, Is.Null, "无 Player 节点保持 null（调用方既有降级路径）");
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
