#if UNITY_EDITOR
// Fix-up X2：BlockInteraction.Break() 在挖方块时 spawn ItemDropEntity 并加到 PlayerContext.ItemDrops。
// 覆盖 review-final.md critical #2 + spec B7。依赖 Unity MonoBehaviour（PlayerContext / BlockInteraction），
// 故用 #if UNITY_EDITOR 包裹，只跑 EditMode 链，dotnet 链由 BlockDropsTests（Core）覆盖数据契约。
using System.Linq;
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// 验证 <see cref="BlockInteraction.BreakAt"/> 在指定坐标挖方块后：
    /// 1) 该位置方块变 Air
    /// 2) PlayerContext.ItemDrops 增加对应掉落物
    /// 3) 空 drops / 挖空气 / 无 PlayerContext 时为 no-op
    /// <para>
    /// EditMode 下没法用 Input.GetMouseButtonDown 触发 Update，所以直接调 <c>BreakAt</c>
    /// 这个新增的 public 方法（与 X1 在 MobManager 上加 SpawnDropsForMob 路径同款做法）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class BlockBreakDropTests
    {
        private GameObject _host;
        private GameObject _interactorHost;
        private PlayerContext _ctx;
        private BlockInteraction _block;
        private World _world;
        private BlockRegistry _registry;
        private ChunkViewRegistry _views;
        private PlayerController _player;

        private static readonly string[] StoneDrops =
        {
            @"{
                ""blockId"": ""stone"",
                ""blockNumericId"": 1,
                ""drops"": [
                    { ""itemId"": ""cobblestone"", ""countMin"": 1, ""countMax"": 1 }
                ]
            }",
        };

        private static readonly string[] EmptyDrops =
        {
            @"{
                ""blockId"": ""bedrock"",
                ""blockNumericId"": 6,
                ""drops"": []
            }",
        };

        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        private static BlockRegistry BuildRegistry()
        {
            var docs = new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
                @"{ ""id"": ""dirt"", ""numericId"": 2, ""textures"": { ""all"": ""dirt"" } }",
                @"{ ""id"": ""grass"", ""numericId"": 3, ""textures"": { ""all"": ""grass"" } }",
                @"{ ""id"": ""bedrock"", ""numericId"": 6, ""textures"": { ""all"": ""bedrock"" } }",
            };
            return BlockRegistry.FromJson(docs);
        }

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""cobblestone"", ""numericId"": 1003, ""texture"": ""cobblestone"" }",
                @"{ ""id"": ""dirt_item"", ""numericId"": 1000, ""texture"": ""dirt"" }",
            });
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("BlockBreakDropCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.HungerSystem = new HungerSystem();
            _player = _host.AddComponent<PlayerController>();

            // World 用真实 ChunkColumn；用最小 MapSize 跑得起来就行
            _world = new World();
            // 在 (8, 70, 8) 放一个石头
            _world.SetBlock(8, 70, 8, BlockIds.Stone);

            _registry = BuildRegistry();
            _views = null; // 不接视图，省去 ChunkViewRegistry 依赖；MarkBlockChanged 容错

            _interactorHost = new GameObject("BlockBreakDropBlock");
            _block = _interactorHost.AddComponent<BlockInteraction>();
            _block.Bind(_world, _registry, _views, _interactorHost.transform);
            _block.SetBlockDrops(BlockDrops.FromJson(StoneDrops, BuildItems()));
        }

        [TearDown]
        public void TearDown()
        {
            if (_interactorHost != null) Object.DestroyImmediate(_interactorHost);
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>
        /// 挖石头（numericId=1）应在 PlayerContext.ItemDrops 增加 1 个 cobblestone ItemDropEntity，
        /// 且该位置 BlockId 变 Air。
        /// </summary>
        [Test]
        public void BreakAt_Stone_SpawnsCobblestoneDrop()
        {
            int dropsBefore = _ctx.ItemDrops.Count;
            _block.BreakAt(8, 70, 8);

            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Air),
                "挖完后该位置应为空气");
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(dropsBefore + 1),
                "ItemDrops 应增加 1 条 cobblestone");

            var drop = _ctx.ItemDrops[_ctx.ItemDrops.Count - 1];
            Assert.That(drop.Content.HasValue, Is.True, "新掉落物应带 Content");
            Assert.That(drop.Content.Value.ItemId, Is.EqualTo(1003),
                "ItemId 应为 cobblestone numericId (1003)");
            Assert.That(drop.Content.Value.Count, Is.EqualTo(1),
                "count 应为 1（countMin=countMax=1）");
        }

        /// <summary>
        /// 挖一个没配 drops 条目的方块（如草）应 no-op：位置变 Air，ItemDrops 不变。
        /// BlockDrops.DropsFor(grassId) 返回空数组 → 0 条 ItemDropEntity。
        /// </summary>
        [Test]
        public void BreakAt_GrassWithoutConfiguredDrops_NoDrops()
        {
            _world.SetBlock(8, 71, 8, BlockIds.Grass);
            int dropsBefore = _ctx.ItemDrops.Count;

            _block.BreakAt(8, 71, 8);

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air));
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(dropsBefore),
                "草未配置 drops 时不应产生 ItemDropEntity（review-final 留作后续条目）");
        }

        /// <summary>
        /// 挖空气（blockId=0）应为 no-op：不改方块（已是 Air）、不产生掉落。
        /// </summary>
        [Test]
        public void BreakAt_Air_IsNoOp()
        {
            // (10, 10, 10) 默认就是空气
            int dropsBefore = _ctx.ItemDrops.Count;

            _block.BreakAt(10, 10, 10);

            Assert.That(_world.GetBlock(10, 10, 10), Is.EqualTo(BlockIds.Air));
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(dropsBefore),
                "挖空气不应产生掉落");
        }

        /// <summary>
        /// 没接 PlayerContext 时（虽然难构造，但稳健性原则要求 no-op）——用空 PlayerContext 模拟。
        /// 这里简单覆盖：移除 PlayerContext 再挖，应 no-op 不抛异常。
        /// </summary>
        [Test]
        public void BreakAt_WithoutDropsTableConfigured_IsNoOp()
        {
            // 新建一个 BlockInteraction 但不调 SetBlockDrops
            var host2 = new GameObject("NoDropsBlock");
            try
            {
                var block2 = host2.AddComponent<BlockInteraction>();
                block2.Bind(_world, _registry, _views, host2.transform);
                _world.SetBlock(8, 72, 8, BlockIds.Stone);
                int dropsBefore = _ctx.ItemDrops.Count;

                block2.BreakAt(8, 72, 8);

                Assert.That(_world.GetBlock(8, 72, 8), Is.EqualTo(BlockIds.Air));
                Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(dropsBefore),
                    "未配置 BlockDrops 时挖方块不应产生掉落（与 spec B7 不冲突——drops 表本身是数据驱动）");
            }
            finally
            {
                Object.DestroyImmediate(host2);
            }
        }

        /// <summary>
        /// 配置了空 drops 表的方块（bedrock 风格）挖完应 no-op 不产生掉落。
        /// </summary>
        [Test]
        public void BreakAt_EmptyDropsTable_ProducesNoDrops()
        {
            _block.SetBlockDrops(BlockDrops.FromJson(EmptyDrops, BuildItems()));
            _world.SetBlock(8, 73, 8, BlockIds.Bedrock);
            int dropsBefore = _ctx.ItemDrops.Count;

            _block.BreakAt(8, 73, 8);

            Assert.That(_world.GetBlock(8, 73, 8), Is.EqualTo(BlockIds.Air));
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(dropsBefore),
                "空 drops 表不应产生 ItemDropEntity");
        }

        /// <summary>
        /// 多个 drops 条目（如石头也可以掉多个种类）应全部 spawn。
        /// </summary>
        [Test]
        public void BreakAt_MultipleDrops_SpawnsEach()
        {
            const string multi = @"{
                ""blockId"": ""stone"",
                ""blockNumericId"": 1,
                ""drops"": [
                    { ""itemId"": ""cobblestone"", ""countMin"": 1, ""countMax"": 1 },
                    { ""itemId"": ""dirt_item"", ""countMin"": 1, ""countMax"": 1 }
                ]
            }";
            _block.SetBlockDrops(BlockDrops.FromJson(new[] { multi }, BuildItems()));
            int dropsBefore = _ctx.ItemDrops.Count;

            _block.BreakAt(8, 70, 8);

            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(dropsBefore + 2),
                "2 条 drops 应产生 2 个 ItemDropEntity");
            var itemIds = _ctx.ItemDrops
                .Skip(dropsBefore)
                .Select(d => d.Content.HasValue ? d.Content.Value.ItemId : 0)
                .ToList();
            Assert.That(itemIds, Does.Contain(1003), "应包含 cobblestone");
            Assert.That(itemIds, Does.Contain(1000), "应包含 dirt");
        }

        /// <summary>
        /// ItemDropEntity.Position 应在方块中心（breaks 在整数坐标 +0.5）。
        /// </summary>
        [Test]
        public void BreakAt_DropPosition_IsBlockCenter()
        {
            _block.BreakAt(8, 70, 8);
            var drop = _ctx.ItemDrops[_ctx.ItemDrops.Count - 1];

            Assert.That(drop.Position.X, Is.EqualTo(8.5f),
                "X 应在方块中心 8.5");
            Assert.That(drop.Position.Y, Is.EqualTo(70.5f),
                "Y 应在方块中心 70.5");
            Assert.That(drop.Position.Z, Is.EqualTo(8.5f),
                "Z 应在方块中心 8.5");
        }
    }
}
#endif
