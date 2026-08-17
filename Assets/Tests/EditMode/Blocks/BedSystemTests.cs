using System;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Persistence;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    /// <summary>
    /// m11 W1-4：床系统（Core/Blocks/BedSystem）。占头脚两格放置、记脚朝向、
    /// 夜间睡觉跳到早晨并设重生点、状态进 LevelData.BedSpawnPoints 往返。
    /// </summary>
    [TestFixture]
    public class BedSystemTests
    {
        private static BlockRegistry BuildRegistry()
        {
            // 最小注册表：bed（自动 id）+ stone（占头格用）
            return BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""bed"", ""textures"": { ""all"": ""bed-side"" }, ""solid"": true, ""opaque"": true, ""hardness"": 1 }",
                @"{ ""id"": ""stone"", ""textures"": { ""all"": ""stone"" }, ""solid"": true, ""opaque"": true, ""hardness"": 1 }",
            });
        }

        private static ushort BedId(BlockRegistry registry) => registry.GetById("bed").NumericId;

        [Test]
        public void PlaceBed_占脚头两格_记脚朝向_登记重生点()
        {
            var registry = BuildRegistry();
            var world = new World();
            var beds = new BedSystem(registry);

            bool ok = beds.PlaceBed(world, 10, 70, 20, BedFacing.North);

            Assert.That(ok, Is.True);
            Assert.That(world.GetBlock(10, 70, 20), Is.EqualTo(BedId(registry)), "脚格应是床");
            Assert.That(world.GetBlock(10, 70, 21), Is.EqualTo(BedId(registry)), "头格 = 脚格 + 朝向一格（North=+Z）");
            Assert.That(beds.GetFacing(10, 70, 20), Is.EqualTo(BedFacing.North));
            Assert.That(beds.SpawnPoints.Count, Is.EqualTo(1), "放置即登记进 BedSpawnPoints（睡过才成为当前重生点）");
            Assert.That(beds.RespawnPoint, Is.Null, "只是放床没睡过：不设当前重生点");
        }

        [Test]
        public void PlaceBed_头格被占_整体不放()
        {
            var registry = BuildRegistry();
            var world = new World();
            ushort stoneId = registry.GetById("stone").NumericId;
            world.SetBlock(10, 70, 21, stoneId); // North 的头格位置放块石头

            var beds = new BedSystem(registry);

            bool ok = beds.PlaceBed(world, 10, 70, 20, BedFacing.North);

            Assert.That(ok, Is.False, "头脚两格必须都空才放得下");
            Assert.That(world.GetBlock(10, 70, 20), Is.EqualTo(ChunkSection.AirId), "失败的放置不应留下半张床");
            Assert.That(beds.SpawnPoints, Is.Empty);
        }

        [Test]
        public void PlaceBed_四个朝向_头格落在对应偏移()
        {
            var registry = BuildRegistry();
            var world = new World();
            var beds = new BedSystem(registry);

            Assert.That(beds.PlaceBed(world, 0, 64, 0, BedFacing.North), Is.True);
            Assert.That(world.GetBlock(0, 64, 1), Is.EqualTo(BedId(registry)));

            Assert.That(beds.PlaceBed(world, 10, 64, 0, BedFacing.South), Is.True);
            Assert.That(world.GetBlock(10, 64, -1), Is.EqualTo(BedId(registry)));

            Assert.That(beds.PlaceBed(world, 20, 64, 0, BedFacing.East), Is.True);
            Assert.That(world.GetBlock(21, 64, 0), Is.EqualTo(BedId(registry)));

            Assert.That(beds.PlaceBed(world, 30, 64, 0, BedFacing.West), Is.True);
            Assert.That(world.GetBlock(29, 64, 0), Is.EqualTo(BedId(registry)));
        }

        [Test]
        public void PlaceBed_原位重放_不重复登记重生点()
        {
            var registry = BuildRegistry();
            var beds = new BedSystem(registry);

            beds.RegisterBed(4, 65, 4);
            beds.RegisterBed(4, 65, 4);

            Assert.That(beds.SpawnPoints.Count, Is.EqualTo(1), "同一坐标重复登记去重");
        }

        [Test]
        public void Sleep_夜间_跳到早晨0tick并设重生点()
        {
            var registry = BuildRegistry();
            var world = new World();
            var beds = new BedSystem(registry);
            beds.PlaceBed(world, 10, 70, 20, BedFacing.North);

            var time = new TimeOfDay { CurrentTick = TimeOfDay.NightStartTick + 500f };

            var result = beds.Sleep(world, 10, 70, 20, time);

            Assert.That(result, Is.EqualTo(SleepResult.Slept));
            Assert.That(time.CurrentTick, Is.EqualTo(0f), "睡到天亮：直接跳到早晨 0 tick");
            Assert.That(time.IsNight, Is.False);
            Assert.That(beds.RespawnPoint, Is.Not.Null, "睡过的床成为当前重生点");
            Assert.That(beds.RespawnPoint.Value.X, Is.EqualTo(10.5f).Within(1e-5f), "重生点在脚格中心上方");
            Assert.That(beds.RespawnPoint.Value.Y, Is.EqualTo(71f).Within(1e-5f));
            Assert.That(beds.RespawnPoint.Value.Z, Is.EqualTo(20.5f).Within(1e-5f));
            Assert.That(beds.SpawnPoints.Count, Is.EqualTo(1));
        }

        [Test]
        public void Sleep_白天_拒绝且时间不动()
        {
            var registry = BuildRegistry();
            var world = new World();
            var beds = new BedSystem(registry);
            beds.PlaceBed(world, 0, 64, 0, BedFacing.North);

            var time = new TimeOfDay { CurrentTick = 6000f }; // 正午

            var result = beds.Sleep(world, 0, 64, 0, time);

            Assert.That(result, Is.EqualTo(SleepResult.NotNight));
            Assert.That(time.CurrentTick, Is.EqualTo(6000f), "白天不能睡，时间不应被拨动");
            Assert.That(beds.RespawnPoint, Is.Null);
        }

        [Test]
        public void Sleep_床被拆掉_返回BedMissing()
        {
            var registry = BuildRegistry();
            var world = new World();
            var beds = new BedSystem(registry);
            beds.PlaceBed(world, 0, 64, 0, BedFacing.North);
            world.SetBlock(0, 64, 0, ChunkSection.AirId); // 床被挖了

            var time = new TimeOfDay { CurrentTick = TimeOfDay.NightStartTick + 1f };

            Assert.That(beds.Sleep(world, 0, 64, 0, time), Is.EqualTo(SleepResult.BedMissing));
            Assert.That(time.CurrentTick, Is.EqualTo(TimeOfDay.NightStartTick + 1f), "床没了不睡，时间不动");
        }

        [Test]
        public void SaveLoad_BedSpawnPoints往返_当前重生点为最后一条()
        {
            var registry = BuildRegistry();
            var world = new World();
            var beds = new BedSystem(registry);
            beds.PlaceBed(world, 0, 64, 0, BedFacing.North);
            beds.PlaceBed(world, 10, 64, 0, BedFacing.East);

            var time = new TimeOfDay { CurrentTick = 14000f };
            Assert.That(beds.Sleep(world, 10, 64, 0, time), Is.EqualTo(SleepResult.Slept), "睡第二张床");

            var data = new LevelData { Seed = 1 };
            beds.SaveTo(data);

            string path = Path.Combine(Path.GetTempPath(), $"bed-{Guid.NewGuid():N}.dat");
            try
            {
                LevelDataCodec.Save(data, path);
                var loaded = LevelDataCodec.Load(path);

                var restored = new BedSystem(registry);
                restored.LoadFrom(loaded);

                Assert.That(restored.SpawnPoints.Count, Is.EqualTo(2));
                Assert.That(restored.RespawnPoint, Is.Not.Null, "睡过的床（列表最后一条）恢复为当前重生点");
                Assert.That(restored.RespawnPoint.Value.X, Is.EqualTo(10.5f).Within(1e-5f),
                    "最后睡的床才是重生点");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void LoadFrom_旧档空列表_重生点为null()
        {
            var beds = new BedSystem(BuildRegistry());

            beds.LoadFrom(new LevelData { BedSpawnPoints = null });

            Assert.That(beds.SpawnPoints, Is.Empty);
            Assert.That(beds.RespawnPoint, Is.Null, "旧档没有床 = 没有床重生点，玩家走世界出生点");
        }
    }
}
