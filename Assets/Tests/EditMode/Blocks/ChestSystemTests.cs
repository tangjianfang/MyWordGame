using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Persistence;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    /// <summary>
    /// m11 W1-4：箱子内容存储（Core/Blocks/ChestSystem）。坐标 "x,y,z" → 内容行，
    /// 行结构复用 <see cref="DropSnapshot"/>（与 LevelData.ChestContents 的 I3 契约一致）。
    /// 守：Put/Take/List 语义、容量上限、与 level.dat 的存档往返（照 LevelDataSchemaTests 模式）。
    /// </summary>
    [TestFixture]
    public class ChestSystemTests
    {
        private const int PlankItemId = 9001;
        private const int PickaxeItemId = 9002;

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""plank"", ""numericId"": 9001, ""maxStack"": 64, ""texture"": ""plank"" }",
                @"{ ""id"": ""pickaxe"", ""numericId"": 9002, ""maxStack"": 1, ""texture"": ""pickaxe"" }",
            });
        }

        [Test]
        public void Put_首个物品_建行并出现在List里()
        {
            var chest = new ChestSystem(BuildItems());

            bool ok = chest.Put(10, 70, -3, new ItemStack(PlankItemId, 12));

            Assert.That(ok, Is.True);
            var rows = chest.List(10, 70, -3);
            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].ItemId, Is.EqualTo(PlankItemId));
            Assert.That(rows[0].Count, Is.EqualTo(12));
            Assert.That(rows[0].Metadata, Is.EqualTo((ushort)0));
        }

        [Test]
        public void Put_同物品分两笔_并入同一行()
        {
            var chest = new ChestSystem(BuildItems());

            chest.Put(0, 64, 0, new ItemStack(PlankItemId, 5));
            chest.Put(0, 64, 0, new ItemStack(PlankItemId, 7));

            var rows = chest.List(0, 64, 0);
            Assert.That(rows.Count, Is.EqualTo(1), "同 id 同 Metadata 且装得下应并入既有行");
            Assert.That(rows[0].Count, Is.EqualTo(12));
        }

        [Test]
        public void Put_带耐久Metadata的工具_不与普通行合并且Metadata无损()
        {
            var chest = new ChestSystem(BuildItems());

            // maxStack=1 的工具：各自独立成行（工具不可堆叠）
            chest.Put(1, 65, 1, new ItemStack(PickaxeItemId, 1, 0x3B00));
            chest.Put(1, 65, 1, new ItemStack(PickaxeItemId, 1, 0x3B00));

            var rows = chest.List(1, 65, 1);
            Assert.That(rows.Count, Is.EqualTo(2), "maxStack=1 的工具两把 = 两行，不能堆成 count=2");
            Assert.That(rows[0].Metadata, Is.EqualTo((ushort)0x3B00), "耐久编码必须原样保存");
        }

        [Test]
        public void Put_装不进既有行时_开新行()
        {
            var chest = new ChestSystem(BuildItems());

            chest.Put(0, 64, 0, new ItemStack(PlankItemId, 60));
            chest.Put(0, 64, 0, new ItemStack(PlankItemId, 10));

            var rows = chest.List(0, 64, 0);
            Assert.That(rows.Count, Is.EqualTo(2), "60+10 超 64 上限应开新行，不硬塞也不丢");
            Assert.That(rows[0].Count, Is.EqualTo(60));
            Assert.That(rows[1].Count, Is.EqualTo(10));
        }

        [Test]
        public void Put_超过27行_拒绝()
        {
            var chest = new ChestSystem(BuildItems());

            for (var i = 0; i < ChestSystem.Capacity; i++)
            {
                // 每行不同 Metadata，逼出独立行
                Assert.That(chest.Put(5, 70, 5, new ItemStack(PlankItemId, 1, (ushort)(i + 1))), Is.True);
            }

            bool overflow = chest.Put(5, 70, 5, new ItemStack(PlankItemId, 1, (ushort)0x7FFF));
            Assert.That(overflow, Is.False, "满 27 行后应拒绝，不能静默丢物品");
            Assert.That(chest.List(5, 70, 5).Count, Is.EqualTo(ChestSystem.Capacity));
        }

        [Test]
        public void Put_空箱坐标为负_键与正坐标互不串()
        {
            var chest = new ChestSystem(BuildItems());

            chest.Put(-1, -2, -3, new ItemStack(PlankItemId, 1));
            chest.Put(1, 2, 3, new ItemStack(PlankItemId, 2));

            Assert.That(chest.List(-1, -2, -3).Count, Is.EqualTo(1));
            Assert.That(chest.List(-1, -2, -3)[0].Count, Is.EqualTo(1));
            Assert.That(chest.List(1, 2, 3)[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void Take_取走整行_下标重排()
        {
            var chest = new ChestSystem(BuildItems());
            chest.Put(0, 64, 0, new ItemStack(PlankItemId, 5, 1));
            chest.Put(0, 64, 0, new ItemStack(PlankItemId, 6, 2));

            var taken = chest.Take(0, 64, 0, 0);

            Assert.That(taken, Is.Not.Null);
            Assert.That(taken.Value.Count, Is.EqualTo(5));
            Assert.That(taken.Value.Metadata, Is.EqualTo((ushort)1));
            var rest = chest.List(0, 64, 0);
            Assert.That(rest.Count, Is.EqualTo(1), "取走一行后其余行前移");
            Assert.That(rest[0].Metadata, Is.EqualTo((ushort)2));
        }

        [Test]
        public void Take_空箱或越界下标_返回null不抛()
        {
            var chest = new ChestSystem(BuildItems());

            Assert.That(chest.Take(0, 64, 0, 0), Is.Null, "空箱取行 = null");
            chest.Put(0, 64, 0, new ItemStack(PlankItemId, 1));
            Assert.That(chest.Take(0, 64, 0, 99), Is.Null, "越界下标 = null");
        }

        [Test]
        public void RemoveChest_倒出全部内容并清空坐标()
        {
            var chest = new ChestSystem(BuildItems());
            chest.Put(7, 68, 9, new ItemStack(PlankItemId, 5));
            chest.Put(7, 68, 9, new ItemStack(PickaxeItemId, 1, 0x3B00));

            var spilled = chest.RemoveChest(7, 68, 9);

            Assert.That(spilled, Is.Not.Null);
            Assert.That(spilled.Count, Is.EqualTo(2));
            Assert.That(chest.List(7, 68, 9), Is.Empty, "拆箱后该坐标不再有内容");
            Assert.That(chest.RemoveChest(7, 68, 9), Is.Null, "重复拆同一坐标 = null");
        }

        [Test]
        public void Roundtrip_经level_dat存档往返_内容一致()
        {
            var chest = new ChestSystem(BuildItems());
            chest.Put(10, 70, -3, new ItemStack(PlankItemId, 12));
            chest.Put(10, 70, -3, new ItemStack(PickaxeItemId, 1, 0x3B00));
            chest.Put(-5, 64, 8, new ItemStack(PlankItemId, 3));

            var data = new LevelData { Seed = 7 };
            chest.SaveTo(data);

            string path = Path.Combine(Path.GetTempPath(), $"chest-{Guid.NewGuid():N}.dat");
            try
            {
                LevelDataCodec.Save(data, path);
                var loaded = LevelDataCodec.Load(path);

                var restored = new ChestSystem(BuildItems());
                restored.LoadFrom(loaded);

                var rows = restored.List(10, 70, -3);
                Assert.That(rows.Count, Is.EqualTo(2));
                Assert.That(rows[0].ItemId, Is.EqualTo(PlankItemId));
                Assert.That(rows[0].Count, Is.EqualTo(12));
                Assert.That(rows[1].Metadata, Is.EqualTo((ushort)0x3B00),
                    "工具耐久编码经箱子→存档→箱子必须无损");
                Assert.That(restored.List(-5, 64, 8)[0].Count, Is.EqualTo(3));
                Assert.That(restored.List(1, 1, 1), Is.Empty, "没存过的坐标 = 空");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void LoadFrom_旧档空字段_不炸且清空状态()
        {
            var chest = new ChestSystem(BuildItems());
            chest.Put(0, 64, 0, new ItemStack(PlankItemId, 1));

            chest.LoadFrom(new LevelData { ChestContents = null });

            Assert.That(chest.List(0, 64, 0), Is.Empty, "旧档无 ChestContents = 全新箱子");
        }

        [Test]
        public void SaveTo_目标对象的箱子字典被全量替换()
        {
            var chest = new ChestSystem(BuildItems());
            chest.Put(1, 2, 3, new ItemStack(PlankItemId, 9));

            var data = new LevelData
            {
                ChestContents = new Dictionary<string, List<DropSnapshot>>
                {
                    { "99,99,99", new List<DropSnapshot> { new DropSnapshot { ItemId = 1, Count = 1 } } },
                },
            };

            chest.SaveTo(data);

            Assert.That(data.ChestContents.Keys, Is.EqualTo(new[] { "1,2,3" }),
                "SaveTo 是全量替换（本次快照就是箱子的全部状态），旧键残留会在读档后复活幽灵物品");
        }
    }
}
