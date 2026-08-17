#if UNITY_EDITOR
// m11 W2-2：BlockInteraction 右键路由的附魔书融合分支（食物之后、锄之前）。
// 依赖 Unity MonoBehaviour（PlayerContext / BlockInteraction / PlayerController），
// 照 BlockInteractionUseRoutingTests 的做法整文件 #if UNITY_EDITOR 包裹——
// EditMode 驱动不了 Input.GetMouseButtonDown，直调 public 入口 UseAt。
// 融合的纯逻辑（掷类型/找目标/消耗）由 EnchantSystemTests（双链）覆盖。
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Enchanting;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class BlockInteractionEnchantFuseTests
    {
        private GameObject _host;
        private PlayerContext _ctx;
        private BlockInteraction _block;
        private World _world;
        private BlockRegistry _registry;
        private EnchantStore _enchants;

        // 物品 numericId 与真实 items/*.json 对齐
        private const int EnchantedBookItemId = 1602;
        private const int IronPickaxeItemId = 1402;
        private const int DirtItemId = 1002;

        /// <summary>EditMode 下 AddComponent 不会跑 Awake，用反射补一脚（BlockToolTierTests 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        private static BlockRegistry BuildRegistry()
        {
            var docs = new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
                @"{ ""id"": ""grass"", ""numericId"": 3, ""textures"": { ""all"": ""grass"" } }",
            };
            return BlockRegistry.FromJson(docs);
        }

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""dirt"", ""numericId"": 1002 }",
                @"{ ""id"": ""iron_pickaxe"", ""numericId"": 1402, ""maxStack"": 1,
                    ""isTool"": true, ""miningLevel"": 3, ""toolTier"": 3, ""maxDurability"": 250 }",
                @"{ ""id"": ""enchanted_book"", ""numericId"": 1602, ""maxStack"": 1 }",
            });
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("EnchantFuseCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Items = BuildItems();

            _world = new World();
            _registry = BuildRegistry();

            var player = _host.AddComponent<PlayerController>();
            player.Bind(_world, _registry, new MyWorld.Core.Math.Float3(50f, 80f, 50f));

            _block = _host.AddComponent<BlockInteraction>();
            _block.Bind(_world, _registry, null, _host.transform);
            // 直注独立 store：不碰 EnchantStore.Default，测试之间零共享状态
            _enchants = new EnchantStore();
            _block.SetEnchantStore(_enchants);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>从 (8,70,8) 柱正上方垂直下探拿命中（与 BlockInteractionUseRoutingTests 同款）。</summary>
        private MyWorld.Core.Physics.VoxelRayHit CastDownAtColumn()
        {
            var source = new MyWorld.Core.Voxel.InteractionRaySource(_world, _registry);
            return MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new MyWorld.Core.Math.Float3(8.5f, 74.5f, 8.5f),
                new MyWorld.Core.Math.Float3(0f, -1f, 0f), 10f);
        }

        [Test]
        public void UseAt_EnchantedBookInHand_PickaxeInBag_Fuses_BookGone_NoPlace()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass); // 下面是草：若书分支失效会去锄地/放方块
            _ctx.Inventory.SetSlot(0, new ItemStack(EnchantedBookItemId, 1)); // 手持书
            _ctx.Inventory.SetSlot(5, new ItemStack(IronPickaxeItemId, 1));   // 背包铁镐
            _ctx.Inventory.SelectedHotbarIndex = 0;

            _block.UseAt(CastDownAtColumn());

            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.True, "书消失");
            Assert.That(_ctx.Inventory.GetSlot(5).ItemId, Is.EqualTo(IronPickaxeItemId), "镐还在原槽");
            Assert.That(_enchants.TryGet(5, IronPickaxeItemId, out var kind, out int level), Is.True,
                "镐带魔（写入注入的 store）");
            Assert.That(level, Is.GreaterThanOrEqualTo(1).And.LessThanOrEqualTo(3), "等级 1..3");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                "右键被融合消费，不放方块");
            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Grass),
                "也不触发锄地——书分支排在锄之前");
        }

        [Test]
        public void UseAt_EnchantedBookInHand_NoGear_HintShown_BookKept()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            _ctx.Inventory.SetSlot(0, new ItemStack(EnchantedBookItemId, 1));
            _ctx.Inventory.SetSlot(3, new ItemStack(DirtItemId, 32)); // 背包只有泥土
            _ctx.Inventory.SelectedHotbarIndex = 0;

            _block.UseAt(CastDownAtColumn());

            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.False, "没有可附魔装备时书不消耗");
            Assert.That(_block.InteractionHintCount, Is.EqualTo(1), "给一次提示");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                "提示性 no-op：也不放方块（别把方块放到石头上）");
        }

        [Test]
        public void UseAt_NotHoldingBook_PlaceBehaviorUnchanged()
        {
            // 反向守卫：手持别的物品右键照旧放 placeBlockId——书分支只对 enchanted_book 生效
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            _ctx.Inventory.SelectedHotbarIndex = 0; // 空手

            _block.UseAt(CastDownAtColumn());

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Stone), "空手照常放 placeBlockId（Stone）");
        }
    }
}
#endif
