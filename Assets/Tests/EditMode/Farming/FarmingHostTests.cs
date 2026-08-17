#if UNITY_EDITOR
// m11 W1-6 集成点②：农业 tick 宿主接线守卫。FarmingHost 以 0.5s 累计器批量推进
// FarmSystem（世界时钟 tick）与 BreedingSystem（真实秒），孕期到点的幼崽经
// MobManager.SpawnMobAt 刷成 0.5 缩放 mob。依赖 MyWorld.Unity 程序集与 GameObject，
// dotnet 链跑不动，整个文件用 #if UNITY_EDITOR 包裹（与 PlayerAttackTests 同款）。
using MyWorld.Core.Farming;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Farming
{
    [TestFixture]
    public class FarmingHostTests
    {
        private GameObject _host;
        private GameObject _mobHost;
        private GameObject _contextHost;
        private World _world;
        private PlayerContext _context;
        private FarmingHost _farmingHost;
        private MobManager _mobManager;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("FarmingHostGO");
            _mobHost = new GameObject("MobHost");
            _contextHost = new GameObject("ContextHost");
            _context = _contextHost.AddComponent<PlayerContext>();
            _context.Items = StubItems();
            _context.FarmSystem = new FarmSystem(_context.Items, 20260817);
            _context.BreedingSystem = new BreedingSystem();
            _world = new World();
            _mobManager = _mobHost.AddComponent<MobManager>();

            _farmingHost = _host.AddComponent<FarmingHost>();
            _farmingHost.Bind(_world, _context, _mobManager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_mobHost != null) Object.DestroyImmediate(_mobHost);
            if (_contextHost != null) Object.DestroyImmediate(_contextHost);
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name.StartsWith("Mob_")) Object.DestroyImmediate(go);
            }
        }

        /// <summary>stub 物品表（照 FarmSystemTests.StubItems 模式，id 挑 2000 段与真实表无关）。</summary>
        private static ItemDatabase StubItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""wheat"", ""numericId"": 2001, ""texture"": ""wheat-item"" }",
                @"{ ""id"": ""seeds_wheat"", ""numericId"": 2002, ""texture"": ""seeds-wheat"" }",
                @"{ ""id"": ""beet"", ""numericId"": 2003, ""texture"": ""beet"" }",
                @"{ ""id"": ""seeds_beet"", ""numericId"": 2004, ""texture"": ""beet"" }",
                @"{ ""id"": ""mung_bean"", ""numericId"": 2005, ""texture"": ""mung_bean"" }",
                @"{ ""id"": ""seeds_mung"", ""numericId"": 2006, ""texture"": ""mung_bean"" }",
            });
        }

        [Test]
        public void 批量推进_农田按世界时钟tick喂_繁殖按真实秒喂()
        {
            // 播一株小麦（耕地 y=63 上方 y=64）
            _world.SetBlock(0, 63, 0, FarmSystem.FarmlandId);
            Assert.That(_context.FarmSystem.TryPlant(_world, 0, 64, 0, "seeds_wheat"), Is.True,
                "前置：播种应成功");

            float beforeFarm = _context.FarmSystem.TotalTicks;
            float beforeClock = _context.BreedingSystem.ClockSeconds;

            _farmingHost.TickBatch(2f);

            // TimeOfDay 未建 → 兜底速率 60：2s × 60 = 120 tick
            Assert.That(_context.FarmSystem.TotalTicks - beforeFarm, Is.EqualTo(120f).Within(0.001f),
                "农田应按 delta × TimeOfDay.Speed(默认 60) 换算成世界时钟 tick");
            Assert.That(_context.BreedingSystem.ClockSeconds - beforeClock, Is.EqualTo(2f).Within(0.001f),
                "繁殖计时按真实秒推进");
        }

        [Test]
        public void 繁殖周期跑满_幼崽刷成半大mob_长大恢复缩放()
        {
            // 两只羊各吃一份小麦（Sheep 的饲料是 wheat）→ 立即配对 → 孕期 30s
            Assert.That(_context.BreedingSystem.TryFeed(101, MyWorld.Core.Entities.MobKind.Sheep,
                new Float3(0, 64, 0), "wheat"), Is.True, "前置：第一只羊入发情名单");
            Assert.That(_context.BreedingSystem.TryFeed(102, MyWorld.Core.Entities.MobKind.Sheep,
                new Float3(1, 64, 0), "wheat"), Is.True, "前置：第二只羊喂下立即配对");

            // 孕期 30s：29s 还没生，30s 一批推完出生
            _farmingHost.TickBatch(29f);
            Assert.That(_mobManager.ActiveMobs.Count, Is.EqualTo(0), "孕期未到不应有幼崽");
            _farmingHost.TickBatch(1f);

            Assert.That(_mobManager.ActiveMobs.Count, Is.EqualTo(1), "孕期到点应刷出一只幼崽");
            var baby = _mobManager.ActiveMobs[0];
            Assert.That(baby.Kind, Is.EqualTo(MyWorld.Core.Entities.MobKind.Sheep), "幼崽随父母 kind");
            Assert.That(baby.VisualScale, Is.EqualTo(BreedingSystem.BabyScale),
                "幼崽 VisualScale 应为 0.5（MobView 每帧按它缩放）");

            // 600s 长大：分两批推满（每批 dt 上限无约束，直接一大批也行——绝对时间阈值语义）
            _farmingHost.TickBatch(BreedingSystem.BabyGrowSeconds + 1f);
            Assert.That(baby.VisualScale, Is.EqualTo(1f), "长大的幼崽应恢复 scale 1");
        }
    }
}
#endif
