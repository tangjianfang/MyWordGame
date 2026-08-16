#if UNITY_EDITOR
// m10 B1：挖掉方块成功 → 选中镐耐久 -1 的 Unity 侧行为（BlockInteraction.BreakAt）。
// 契约：扣减走 ItemStack.WithDurabilityUsed(def.MaxDurability)（Metadata=0 存量兼容），
// 耐久尽 → 镐从选中槽消失 + hotbar 上方一次性提示（m10 B2 起文案「镐碎了！」并散碎块，
// 碎块行为由 PickaxeShardTests 覆盖）。
// 依赖 Unity MonoBehaviour（PlayerContext / BlockInteraction），dotnet 链跑不动，
// 整个文件用 #if UNITY_EDITOR 包裹（与 BlockToolTierTests 同款）；Core 侧的纯扣减
// 语义由 DurabilityTests / ItemDatabaseTests（dotnet + EditMode 双链）覆盖。
using MyWorld.Core.Blocks;
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
    public class BlockDigDurabilityTests
    {
        private GameObject _host;
        private GameObject _interactorHost;
        private PlayerContext _ctx;
        private BlockInteraction _block;
        private World _world;

        private const int WoodenPickaxeItemId = 1400;
        private const int BrittlePickaxeItemId = 1490;

        /// <summary>EditMode 下 AddComponent 不会跑 Awake，用反射补一脚（BlockToolTierTests 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        private static BlockRegistry BuildRegistry()
        {
            var docs = new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
                // 铁矿：minToolTier=2——木镐挖了「白挖」（不掉落），但仍应磨损
                @"{ ""id"": ""raw_iron_ore"", ""numericId"": 1010, ""textures"": { ""all"": ""iron-ore"" },
                    ""hardness"": 2.0, ""minToolTier"": 2 }",
            };
            return BlockRegistry.FromJson(docs);
        }

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""cobblestone"", ""numericId"": 1003, ""texture"": ""cobblestone"" }",
                @"{ ""id"": ""wooden_pickaxe"", ""numericId"": 1400, ""texture"": ""wooden_pickaxe"",
                    ""isTool"": true, ""miningLevel"": 1, ""toolTier"": 1, ""maxDurability"": 59 }",
                // 1.0 版「脆镐」：耐久 1，一挖即碎——专测耐久尽分支
                @"{ ""id"": ""brittle_pickaxe"", ""numericId"": 1490, ""texture"": ""wooden_pickaxe"",
                    ""isTool"": true, ""miningLevel"": 1, ""toolTier"": 1, ""maxDurability"": 1 }",
            });
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("BlockDigDurabilityCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Items = BuildItems();

            _world = new World();

            _interactorHost = new GameObject("BlockDigDurabilityBlock");
            _block = _interactorHost.AddComponent<BlockInteraction>();
            _block.Bind(_world, BuildRegistry(), null, _interactorHost.transform);
            // 不注入 BlockDrops：耐久扣减在掉落 spawn 之前独立发生，缺表不应影响本 fixture 的断言
        }

        [TearDown]
        public void TearDown()
        {
            if (_interactorHost != null) Object.DestroyImmediate(_interactorHost);
            if (_host != null) Object.DestroyImmediate(_host);
        }

        private void SelectItem(int itemId)
        {
            _ctx.Inventory.SetSlot(0, new ItemStack(itemId, 1));
            _ctx.Inventory.SelectedHotbarIndex = 0;
        }

        /// <summary>
        /// 核心路径：木镐（JSON maxDurability=59，Metadata=0 视为满耐久）连挖 3 块石头，
        /// 每挖一次扣 1 点——第 3 次后剩余 56，Metadata 原始编码同步钉死。
        /// </summary>
        [Test]
        public void BreakAt_WithPickaxe_DecrementsDurabilityEachDig()
        {
            SelectItem(WoodenPickaxeItemId);

            for (int i = 0; i < 3; i++)
            {
                _world.SetBlock(8 + i, 70, 8, BlockIds.Stone);
                _block.BreakAt(8 + i, 70, 8);
            }

            var stack = _ctx.Inventory.GetSlot(0);
            Assert.That(stack.IsEmpty, Is.False, "59 耐久只挖了 3 次，镐不应消失");
            Assert.That(stack.HasDurability, Is.True, "首次挖掘后耐久位应已写入（Metadata=0 → 先初始化再扣）");
            Assert.That(stack.MaxDurability, Is.EqualTo(59), "上限取物品表 maxDurability");
            Assert.That(stack.CurrentDurability, Is.EqualTo(56), "59 - 3 次挖掘 = 56");
            Assert.That(stack.Metadata, Is.EqualTo((ushort)((59 << 8) | 56)),
                "Metadata 位段应为 (max<<8)|cur——存档持久化依赖这个编码");
            Assert.That(_block.ToolBreakHintCount, Is.EqualTo(0), "没挖坏不应有提示");
        }

        /// <summary>徒手 / 非工具挖掘：没有耐久概念（maxDurability=0），Metadata 不被触碰。</summary>
        [Test]
        public void BreakAt_NonToolSelected_MetadataUntouched()
        {
            SelectItem(1003); // cobblestone：非工具

            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            _block.BreakAt(8, 70, 8);

            var stack = _ctx.Inventory.GetSlot(0);
            Assert.That(stack.Metadata, Is.EqualTo((ushort)0), "非工具不应被写入耐久位");
            Assert.That(stack.Count, Is.EqualTo(1), "手里这块「石头物品」不该被挖矿消耗");
        }

        /// <summary>
        /// 门槛不够的「白挖」（木镐挖铁矿：挖得掉、不掉落）同样磨损镐——
        /// 工具挥出去了就是用了，MC 语义如此，也给孩子一个「别拿错镐硬挖」的反馈。
        /// </summary>
        [Test]
        public void BreakAt_TierGated_StillDamagesPickaxe()
        {
            SelectItem(WoodenPickaxeItemId);
            _world.SetBlock(8, 70, 8, BlockIds.RawIronOre);

            _block.BreakAt(8, 70, 8);

            var stack = _ctx.Inventory.GetSlot(0);
            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Air), "前置：门槛不够也挖得掉");
            Assert.That(stack.CurrentDurability, Is.EqualTo(58), "白挖也应扣 1 点耐久");
        }

        /// <summary>
        /// 耐久尽：脆镐（maxDurability=1）挖一次 → 选中槽变空（镐消失）+ 一次性提示。
        /// B2 会在同一条分支上加碎块散落与扎脚伤害。
        /// </summary>
        [Test]
        public void BreakAt_DurabilityExhausted_PickaxeVanishesWithHint()
        {
            SelectItem(BrittlePickaxeItemId);
            _world.SetBlock(8, 70, 8, BlockIds.Stone);

            _block.BreakAt(8, 70, 8);

            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Air), "方块照常被挖掉");
            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.True,
                "耐久尽 = 镐从选中槽消失（B1 先「消失+提示」，碎块是 B2）");
            Assert.That(_block.ToolBreakHintCount, Is.EqualTo(1), "镐坏时应触发一次提示");
        }

        /// <summary>
        /// 提示的「一次性」语义（与门槛提示同款）：2 秒显示窗口内接连挖碎两把镐，
        /// 提示不叠加——EditMode 下 Time.time 恒为 0，两次必落在同一窗口。
        /// </summary>
        [Test]
        public void BreakAt_BreakHint_TriggeredOncePerWindow()
        {
            SelectItem(BrittlePickaxeItemId);
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            _world.SetBlock(9, 70, 8, BlockIds.Stone);

            _block.BreakAt(8, 70, 8); // 第一把脆镐碎

            SelectItem(BrittlePickaxeItemId); // 换第二把
            _block.BreakAt(9, 70, 8);         // 也碎

            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.True, "第二把脆镐也应碎");
            Assert.That(_block.ToolBreakHintCount, Is.EqualTo(1),
                "显示窗口内连续碎两把镐，提示不应重复触发（不刷屏）");
        }

        /// <summary>挖空气是 no-op：不触发耐久扣减（与「挖掉方块成功才扣」的契约对齐）。</summary>
        [Test]
        public void BreakAt_Air_NoDurabilityChange()
        {
            SelectItem(BrittlePickaxeItemId);

            _block.BreakAt(8, 70, 8); // 该处是空气

            var stack = _ctx.Inventory.GetSlot(0);
            Assert.That(stack.IsEmpty, Is.False, "挖空气不应消耗镐（1 耐久也该留着）");
            Assert.That(stack.Metadata, Is.EqualTo((ushort)0), "连耐久位都不应初始化");
        }
    }
}
#endif
