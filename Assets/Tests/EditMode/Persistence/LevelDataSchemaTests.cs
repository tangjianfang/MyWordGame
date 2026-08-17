using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Persistence
{
    /// <summary>
    /// milestone-11 I3：level.dat schema 一次性扩展（箱子/床/附魔/统计/农田）。
    /// 守三条契约：roundtrip 无损、旧档缺新字段加载成功且默认空集合、坏类型报错不静默。
    /// </summary>
    [TestFixture]
    public class LevelDataSchemaTests
    {
        /// <summary>五组新字段全部填真实数据的样例档。</summary>
        private static LevelData BuildSampleWithNewFields()
        {
            return new LevelData
            {
                Seed = 42,
                TimeTick = 6500.5f,
                Player = new PlayerSnapshot
                {
                    X = 1.5f, Y = 70f, Z = -3.5f,
                    Slots = new[]
                    {
                        new SlotSnapshot { ItemId = 1000, Count = 12, Metadata = 0 },
                    }
                },
                ChestContents = new Dictionary<string, List<DropSnapshot>>
                {
                    // 箱子条目复用掉落物快照结构：ItemId/Count/Metadata 有效，X/Y/Z 不用（保持 0）
                    { "10,70,-3", new List<DropSnapshot>
                        {
                            new DropSnapshot { ItemId = 1004, Count = 12, Metadata = 0 },
                            new DropSnapshot { ItemId = 1006, Count = 1, Metadata = 0x0503 },
                        } },
                    { "-5,64,8", new List<DropSnapshot>
                        {
                            new DropSnapshot { ItemId = 1008, Count = 3, Metadata = 0 },
                        } },
                },
                BedSpawnPoints = new List<Float3>
                {
                    new Float3(1.5f, 70f, -3.5f),
                    new Float3(-128.25f, 64f, 96f),
                },
                PlayerEnchantments = new Dictionary<string, string>
                {
                    { "hotbar:2", "sharpness" },
                    { "armor:helmet", "unbreaking" },
                },
                Stats = new Dictionary<string, int>
                {
                    { "kills", 17 },
                    { "blocksMined", 4096 },
                    { "playSeconds", 7200 },
                },
                FarmStates = new Dictionary<string, string>
                {
                    { "12,68,-4", "wheat:2" },
                    { "13,68,-4", "beet:0" },
                },
            };
        }

        /// <summary>旧档样例：m11 之前世代的 level.dat 字面量，完全不含五个新字段的键。</summary>
        private const string LegacyLevelJson =
@"{
  ""Seed"": 42,
  ""TimeTick"": 6500.5,
  ""Player"": {
    ""X"": 1.5, ""Y"": 70.0, ""Z"": -3.5,
    ""VX"": 0.0, ""VY"": -0.1, ""VZ"": 2.0,
    ""HealthCurrent"": 18.5, ""HealthMax"": 20.0,
    ""Hunger"": 17, ""Saturation"": 4.5,
    ""ExpCurrent"": 30, ""ExpLevel"": 2,
    ""SelectedHotbarIndex"": 3,
    ""Slots"": [
      { ""ItemId"": 1000, ""Count"": 12, ""Metadata"": 0 },
      { ""ItemId"": 0, ""Count"": 0, ""Metadata"": 0 }
    ]
  },
  ""Furnace"": null,
  ""Drops"": [
    { ""ItemId"": 1008, ""Count"": 2, ""Metadata"": 0, ""X"": 5.0, ""Y"": 71.0, ""Z"": 6.0 }
  ],
  ""Quest"": null
}";

        private static string WriteTemp(string json)
        {
            string path = Path.Combine(Path.GetTempPath(), $"level-{Guid.NewGuid():N}.dat");
            File.WriteAllText(path, json);
            return path;
        }

        [Test]
        public void RoundTrip_新字段全写_回读一致()
        {
            string path = Path.Combine(Path.GetTempPath(), $"level-{Guid.NewGuid():N}.dat");
            try
            {
                LevelDataCodec.Save(BuildSampleWithNewFields(), path);
                LevelData loaded = LevelDataCodec.Load(path);

                // 箱子：两个坐标键、每键条目内容逐项一致（含 Metadata——耐久在里面的老规矩）
                Assert.That(loaded.ChestContents, Is.Not.Null, "ChestContents 回读不得为 null");
                Assert.That(loaded.ChestContents.Count, Is.EqualTo(2));
                Assert.That(loaded.ChestContents["10,70,-3"].Count, Is.EqualTo(2));
                Assert.That(loaded.ChestContents["10,70,-3"][0].ItemId, Is.EqualTo(1004));
                Assert.That(loaded.ChestContents["10,70,-3"][0].Count, Is.EqualTo(12));
                Assert.That(loaded.ChestContents["10,70,-3"][1].Metadata, Is.EqualTo((ushort)0x0503),
                    "箱子条目 Metadata 必须无损——附魔工具放箱子再取出不能掉耐久");
                Assert.That(loaded.ChestContents["-5,64,8"][0].ItemId, Is.EqualTo(1008));

                // 床重生点：数量与每个坐标三分量逐一相等
                Assert.That(loaded.BedSpawnPoints, Is.Not.Null);
                Assert.That(loaded.BedSpawnPoints.Count, Is.EqualTo(2));
                Assert.That(loaded.BedSpawnPoints[0].X, Is.EqualTo(1.5f));
                Assert.That(loaded.BedSpawnPoints[0].Y, Is.EqualTo(70f));
                Assert.That(loaded.BedSpawnPoints[0].Z, Is.EqualTo(-3.5f));
                Assert.That(loaded.BedSpawnPoints[1].X, Is.EqualTo(-128.25f));

                // 附魔：键值对一致
                Assert.That(loaded.PlayerEnchantments, Is.Not.Null);
                Assert.That(loaded.PlayerEnchantments.Count, Is.EqualTo(2));
                Assert.That(loaded.PlayerEnchantments["hotbar:2"], Is.EqualTo("sharpness"));
                Assert.That(loaded.PlayerEnchantments["armor:helmet"], Is.EqualTo("unbreaking"));

                // 统计：键值对一致
                Assert.That(loaded.Stats, Is.Not.Null);
                Assert.That(loaded.Stats.Count, Is.EqualTo(3));
                Assert.That(loaded.Stats["kills"], Is.EqualTo(17));
                Assert.That(loaded.Stats["blocksMined"], Is.EqualTo(4096));
                Assert.That(loaded.Stats["playSeconds"], Is.EqualTo(7200));

                // 农田：键值对一致
                Assert.That(loaded.FarmStates, Is.Not.Null);
                Assert.That(loaded.FarmStates.Count, Is.EqualTo(2));
                Assert.That(loaded.FarmStates["12,68,-4"], Is.EqualTo("wheat:2"));
                Assert.That(loaded.FarmStates["13,68,-4"], Is.EqualTo("beet:0"));

                // 既有字段不受影响
                Assert.That(loaded.Seed, Is.EqualTo(42));
                Assert.That(loaded.Player.Slots[0].Count, Is.EqualTo(12));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void 旧档无新字段_加载成功_默认空()
        {
            string path = WriteTemp(LegacyLevelJson);
            try
            {
                LevelData loaded = LevelDataCodec.Load(path);

                Assert.That(loaded, Is.Not.Null, "旧档（m11 之前的世代）必须能加载");
                // 五组新字段全部落到「非 null 的空集合」，后续波次拿去直接用不会 NPE
                Assert.That(loaded.ChestContents, Is.Not.Null.And.Empty, "旧档缺 ChestContents → 空字典");
                Assert.That(loaded.BedSpawnPoints, Is.Not.Null.And.Empty, "旧档缺 BedSpawnPoints → 空列表");
                Assert.That(loaded.PlayerEnchantments, Is.Not.Null.And.Empty, "旧档缺 PlayerEnchantments → 空字典");
                Assert.That(loaded.Stats, Is.Not.Null.And.Empty, "旧档缺 Stats → 空字典");
                Assert.That(loaded.FarmStates, Is.Not.Null.And.Empty, "旧档缺 FarmStates → 空字典");
                // 既有字段照常恢复
                Assert.That(loaded.Seed, Is.EqualTo(42));
                Assert.That(loaded.Drops.Count, Is.EqualTo(1));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void 坏类型_Stats整体为字符串_抛InvalidData不静默()
        {
            string path = WriteTemp(LegacyLevelJson.Replace("\"Quest\": null", "\"Quest\": null, \"Stats\": \"not-a-table\""));
            try
            {
                Assert.Throws<InvalidDataException>(() => LevelDataCodec.Load(path),
                    "Stats 是 Dictionary<string,int>，给字符串必须报错而非静默吞掉");
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void 坏类型_床坐标元素非对象_抛InvalidData()
        {
            string path = WriteTemp(LegacyLevelJson.Replace("\"Quest\": null", "\"Quest\": null, \"BedSpawnPoints\": [\"oops\"]"));
            try
            {
                Assert.Throws<InvalidDataException>(() => LevelDataCodec.Load(path),
                    "BedSpawnPoints 是 List<Float3>，元素给字符串必须报错");
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void 坏类型_箱子内容非列表_抛InvalidData()
        {
            string path = WriteTemp(LegacyLevelJson.Replace(
                "\"Quest\": null", "\"Quest\": null, \"ChestContents\": { \"10,70,-3\": 42 }"));
            try
            {
                Assert.Throws<InvalidDataException>(() => LevelDataCodec.Load(path),
                    "ChestContents 值是 List<DropSnapshot>，给数字必须报错");
            }
            finally { File.Delete(path); }
        }
    }
}
