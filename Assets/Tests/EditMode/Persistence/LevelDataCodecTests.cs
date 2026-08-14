using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Persistence;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Persistence
{
    /// <summary>
    /// milestone-4 A2：level.dat 的 JSON 序列化 + 原子写。
    /// </summary>
    [TestFixture]
    public class LevelDataCodecTests
    {
        private static LevelData BuildSample()
        {
            return new LevelData
            {
                Seed = 42,
                TimeTick = 6500.5f,
                Player = new PlayerSnapshot
                {
                    X = 1.5f, Y = 70f, Z = -3.5f, VX = 0f, VY = -0.1f, VZ = 2f,
                    HealthCurrent = 18.5f, HealthMax = 20f,
                    Hunger = 17, Saturation = 4.5f,
                    ExpCurrent = 30, ExpLevel = 2,
                    SelectedHotbarIndex = 3,
                    Slots = new[]
                    {
                        new SlotSnapshot { ItemId = 1000, Count = 12, Metadata = 0 },
                        new SlotSnapshot { ItemId = 1006, Count = 1, Metadata = 0x0503 }, // 工具：耐久在 Metadata
                        new SlotSnapshot { ItemId = 0, Count = 0, Metadata = 0 },         // 空槽
                    }
                },
                Furnace = new FurnaceSnapshot
                {
                    Input = new SlotSnapshot { ItemId = 1004, Count = 3, Metadata = 0 },
                    Fuel = new SlotSnapshot { ItemId = 1007, Count = 5, Metadata = 0 },
                    Output = null,
                    Progress = 12.7f,
                    FuelRemaining = 3.25f,
                },
                Drops = new List<DropSnapshot>
                {
                    new DropSnapshot { ItemId = 1008, Count = 2, Metadata = 0, X = 5f, Y = 71f, Z = 6f },
                }
            };
        }

        [Test]
        public void RoundTrip_AllFieldsPreserved()
        {
            string path = Path.Combine(Path.GetTempPath(), $"level-{Guid.NewGuid():N}.dat");
            try
            {
                var original = BuildSample();
                LevelDataCodec.Save(original, path);
                var loaded = LevelDataCodec.Load(path);

                Assert.That(loaded.Seed, Is.EqualTo(42));
                Assert.That(loaded.TimeTick, Is.EqualTo(6500.5f));
                Assert.That(loaded.Player.HealthCurrent, Is.EqualTo(18.5f));
                Assert.That(loaded.Player.SelectedHotbarIndex, Is.EqualTo(3));
                Assert.That(loaded.Player.Slots.Length, Is.EqualTo(3));
                Assert.That(loaded.Player.Slots[1].Metadata, Is.EqualTo((ushort)0x0503),
                    "Metadata 必须无损——工具耐久存在这里");
                Assert.That(loaded.Furnace.Progress, Is.EqualTo(12.7f));
                Assert.That(loaded.Furnace.FuelRemaining, Is.EqualTo(3.25f),
                    "剩余燃烧时间必须无损——读档后火不能灭");
                Assert.That(loaded.Furnace.Fuel.Count, Is.EqualTo(5));
                Assert.That(loaded.Furnace.Output, Is.Null);
                Assert.That(loaded.Drops.Count, Is.EqualTo(1));
                Assert.That(loaded.Drops[0].ItemId, Is.EqualTo(1008));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Save_IsAtomic_TmpDoesNotOverwriteGoodFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"lv-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "level.dat");
            try
            {
                var good = BuildSample();
                LevelDataCodec.Save(good, path); // 先有一份好档

                // 第二次保存中途死掉：只留下半截 tmp，level.dat 本体必须还是好档
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, "{ 半截 JSON");
                var reloaded = LevelDataCodec.Load(path);
                Assert.That(reloaded.Seed, Is.EqualTo(42), "半截 tmp 不能破坏上一次的好档");
                Assert.That(File.Exists(tmp), Is.True, "本测试模拟崩溃残留，tmp 存在是前提");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void Save_Twice_SecondSaveWinsAndNoTmpLeftover()
        {
            string dir = Path.Combine(Path.GetTempPath(), $"lv-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "level.dat");
            try
            {
                // 第一次保存（level.dat 不存在 → Move 分支）
                var first = BuildSample();
                LevelDataCodec.Save(first, path);

                // 第二次保存（level.dat 已存在 → File.Replace 分支），改关键字段验证新档生效
                first.Player.HealthCurrent = 3.25f;
                first.Player.Slots[0].Count = 7;
                first.Drops.Add(new DropSnapshot { ItemId = 1009, Count = 1, Metadata = 0, X = -1f, Y = 72f, Z = 0f });
                LevelDataCodec.Save(first, path);

                var reloaded = LevelDataCodec.Load(path);
                Assert.That(reloaded.Player.HealthCurrent, Is.EqualTo(3.25f), "第二次保存必须生效（Replace 分支）");
                Assert.That(reloaded.Player.Slots[0].Count, Is.EqualTo(7));
                Assert.That(reloaded.Drops.Count, Is.EqualTo(2));
                Assert.That(File.Exists(path + ".tmp"), Is.False, "保存完成后 tmp 必须被改名掉，不能残留");
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void Load_CorruptFile_ThrowsInvalidData()
        {
            string path = Path.Combine(Path.GetTempPath(), $"level-{Guid.NewGuid():N}.dat");
            File.WriteAllText(path, "不是 JSON");
            try
            {
                Assert.Throws<InvalidDataException>(() => LevelDataCodec.Load(path));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void Load_MissingFile_Throws()
        {
            Assert.Throws<FileNotFoundException>(
                () => LevelDataCodec.Load(Path.Combine(Path.GetTempPath(), $"nope-{Guid.NewGuid():N}.dat")));
        }
    }
}
