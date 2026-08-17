#if UNITY_EDITOR
// m11 W1-1 集成点②：爆炸掉落实体化接线守卫。WorldBootstrap 注入
// Explosion.BoundRegistry/BoundDrops 后，苦力怕自爆的掉落清单由
// MobManager.TickExplosionDrops 消费（幂等：引用比对最近一次产物）。
// 依赖 MyWorld.Unity 程序集与 GameObject，dotnet 链跑不动，
// 整个文件用 #if UNITY_EDITOR 包裹（与 PlayerAttackTests 同款）。
using MyWorld.Core.Blocks;
using MyWorld.Core.Combat;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Combat
{
    [TestFixture]
    public class ExplosionDropWiringTests
    {
        private GameObject _host;
        private GameObject _contextHost;
        private MobManager _manager;
        private PlayerContext _context;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("ExplosionHost");
            _contextHost = new GameObject("ContextHost");
            _context = _contextHost.AddComponent<PlayerContext>();
            _manager = _host.AddComponent<MobManager>();

            // 集成点② WorldBootstrap 同款注入（stub 表：teststone → testitem ×1）
            _context.Items = StubItems();
            Explosion.BoundRegistry = StubRegistry();
            Explosion.BoundDrops = StubDrops(_context.Items);
        }

        [TearDown]
        public void TearDown()
        {
            // 静态注入还原，防串档到其它 EditMode 测试（生产侧由 WorldBootstrap 每次启动注入）
            Explosion.BoundRegistry = null;
            Explosion.BoundDrops = null;
            if (_host != null) Object.DestroyImmediate(_host);
            if (_contextHost != null) Object.DestroyImmediate(_contextHost);
        }

        private static BlockRegistry StubRegistry()
        {
            return BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""teststone"", ""numericId"": 9002, ""textures"": { ""all"": ""stone"" }, ""opaque"": true }",
            });
        }

        private static ItemDatabase StubItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""testitem"", ""numericId"": 2101, ""texture"": ""testitem"" }",
            });
        }

        private static BlockDrops StubDrops(ItemDatabase items)
        {
            return BlockDrops.FromJson(new[]
            {
                @"[{ ""blockId"": ""teststone"", ""blockNumericId"": 9002, ""drops"": [ { ""itemId"": ""testitem"", ""countMin"": 1, ""countMax"": 1 } ] }]",
            }, items);
        }

        [Test]
        public void 起爆后掉落实体化进ItemDrops_重复调用不重复发()
        {
            // 爆心周围摆一圈可破坏方块；玩家放远处（半径 3 外）不结算伤害事件
            var world = new World();
            world.SetBlock(9, 64, 9, 9002);
            world.SetBlock(11, 64, 9, 9002);

            Explosion.Detonate(world, new Float3(10.5f, 64.5f, 9.5f), new Float3(100f, 64f, 9f),
                attackerEntityId: 7, radius: 3f);

            Assert.That(Explosion.LastResult.Drops.Count, Is.EqualTo(2), "前置：注入掉落表后两块方块各滚出 1 件");

            _manager.TickExplosionDrops();
            Assert.That(_context.ItemDrops.Count, Is.EqualTo(2), "掉落应实体化进 PlayerContext.ItemDrops");

            // 幂等：同一份 LastResult 再消费不重复发
            _manager.TickExplosionDrops();
            Assert.That(_context.ItemDrops.Count, Is.EqualTo(2), "引用比对去重——同一次爆炸只发一批");
        }

        [Test]
        public void 未注入掉落表_方块直接消失_不发掉落也不炸()
        {
            Explosion.BoundDrops = null; // 模拟「掉落表加载失败」的降级路径
            var world = new World();
            world.SetBlock(9, 64, 9, 9002);

            Explosion.Detonate(world, new Float3(9.5f, 64.5f, 9.5f), new Float3(100f, 64f, 9f),
                attackerEntityId: 7, radius: 3f);

            Assert.That(world.GetBlock(9, 64, 9), Is.EqualTo((ushort)0), "方块仍被破坏（消失）");
            _manager.TickExplosionDrops();
            Assert.That(_context.ItemDrops.Count, Is.EqualTo(0), "无掉落表 = 不发掉落（不炸不抛）");
        }
    }
}
#endif
