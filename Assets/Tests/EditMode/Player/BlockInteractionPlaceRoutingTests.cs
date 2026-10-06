// m11 W3-1：放置路由——手持物品 blockId → 对应方块（家具/箱子/附魔台单格、门两格竖放、
// 床双格取向）。两个 Fixture：
//   ① ItemBlockIdDataTests：纯数据（ItemDatabase 解析 blockId + 真实 items/blocks 对账），
//      不依赖 Unity，dotnet 与 EditMode 双链都跑；
//   ② BlockInteractionPlaceRoutingTests：MonoBehaviour 路由断言（直调 UseAt 的放置分支，
//      模式照 BlockInteractionUseRoutingTests），整段 #if UNITY_EDITOR 包裹。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;   // PlayerInventory（Core 层，不在 Unity.Gameplay）
using MyWorld.Core.Voxel;
using NUnit.Framework;
#if UNITY_EDITOR
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;
#endif

namespace MyWorld.Core.Tests.Player
{
    /// <summary>
    /// m11 W3-1 的数据侧守卫：ItemDatabase 必须把 <c>items/*.json</c> 的 <c>blockId</c>
    /// 解析进 <see cref="ItemDefinition.BlockId"/>，且仓库里每一份真实物品定义的
    /// blockId 都能在真实方块注册表解析到（悬空引用在这里红，轮不到进游戏 warn）。
    /// 与 GearUpgradeRecipeTests 同模式读真实文件，双链跑。
    /// </summary>
    [TestFixture]
    public class ItemBlockIdDataTests
    {
        /// <summary>九件家具物品 id（W1-5 落地，全部应带 blockId）。</summary>
        private static readonly string[] FurnitureItemIds =
        {
            "chair", "table", "office_desk", "laptop", "keyboard",
            "mouse", "notebook", "hacker_pc", "globe",
        };

        private ItemDatabase _items;
        private BlockRegistry _blocks;

        [OneTimeSetUp]
        public void LoadRealDefinitions()
        {
            // GetFiles 非递归，drops/ 子目录天然不在内（与 BlockDefinitionFilesTests 同款）
            _items = ItemDatabase.FromJson(
                Directory.GetFiles(LocateDirectory("items"), "*.json").Select(File.ReadAllText));
            _blocks = BlockRegistry.FromJson(
                Directory.GetFiles(LocateDirectory("blocks"), "*.json").Select(File.ReadAllText));
        }

        [Test]
        public void FromJson_ParsesBlockId_MissingFieldStaysNull()
        {
            var db = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""chair"", ""numericId"": 1201, ""blockId"": ""chair_block"" }",
                @"{ ""id"": ""stick"", ""numericId"": 1100 }",
            });

            Assert.That(db.GetById("chair").BlockId, Is.EqualTo("chair_block"),
                "blockId 字段要解析进 ItemDefinition.BlockId（放置路由的唯一输入）");
            Assert.That(db.GetById("stick").BlockId, Is.Null,
                "不写 blockId = 无关联方块，保持 null（评审 07#9 起右键不再回落放石头）");
        }

        [Test]
        public void RealFiles_EveryItemBlockId_ResolvesToRegisteredBlock()
        {
            var dangling = _items.ById.Values
                .Where(d => !string.IsNullOrEmpty(d.BlockId) && !_blocks.TryGetById(d.BlockId, out _))
                .Select(d => $"{d.Id} -> {d.BlockId}")
                .ToList();
            Assert.That(dangling, Is.Empty,
                "物品 blockId 悬空引用（blocks 注册表里没有这个方块）：放置路由只能 warn 后不放，必须在数据层修掉");
        }

        [Test]
        public void RealFiles_FivePlaceableFamilies_WiredWithBlockId()
        {
            // 家具 9 + 箱子 / 床 / 木门 / 附魔台 = m11 W3-1 放置路由逐类断言的数据前提
            foreach (string itemId in FurnitureItemIds)
            {
                Assert.That(_items.GetById(itemId).BlockId, Is.Not.Null.And.Not.Empty,
                    $"家具物品 {itemId} 必须声明 blockId（W1-5 数据约定，W3-1 起被放置路由消费）");
            }

            Assert.That(_items.GetById("chest").BlockId, Is.EqualTo("chest"), "箱子物品 → chest 方块");
            Assert.That(_items.GetById("bed").BlockId, Is.EqualTo("bed"), "床物品 → bed 方块");
            Assert.That(_items.GetById("wooden_door").BlockId, Is.EqualTo("wooden_door"), "木门物品 → wooden_door 方块");
            Assert.That(_items.GetById("enchanting_table").BlockId, Is.EqualTo("enchanting_table"),
                "附魔台物品 → enchanting_table 方块");
        }

        private static string LocateDirectory(string subdir)
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, subdir);
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", subdir);
                if (Directory.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException($"未能从测试输出目录向上找到 Assets/StreamingAssets/{subdir}。");
#endif
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// m11 W3-1 放置路由的 MonoBehaviour 断言（EditMode 直调 UseAt，驱动不了 Input）。
    /// fixture 模式照 BlockInteractionUseRoutingTests：玩家绑在远离 (8,71,8) 的位置，
    /// 射线从 (8.5,74.5,8.5) 垂直下探打草方块顶面 → 放置格 (8,71,8)。
    /// </summary>
    [TestFixture]
    public class BlockInteractionPlaceRoutingTests
    {
        private GameObject _host;
        private PlayerContext _ctx;
        private BlockInteraction _block;
        private World _world;
        private BlockRegistry _registry;
        private BedSystem _beds;

        // fixture 内嵌注册表/物品表的 numericId（真实文件同款值，见 ItemBlockIdDataTests 的真数据对账）
        private const ushort ChairBlockId = 1013;
        private const ushort EnchantingTableBlockId = 1062;
        private const int ChairItemId = 1201;
        private const int EnchantingTableItemId = 1604;
        private const int ChestItemId = 1351;
        private const int BedItemId = 1353;
        private const int WoodenDoorItemId = 1352;
        private const int DirtItemId = 1015;   // 无 blockId 的对照物品
        private const int BadItemId = 1999;    // blockId 指向未注册方块的悬空引用

        /// <summary>EditMode 下 AddComponent 不会跑 Awake，用反射补一脚（BlockBreakDropTests 同款）。</summary>
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
                @"{ ""id"": ""chair_block"", ""numericId"": 1013, ""textures"": { ""all"": ""chair"" },
                    ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""bed"", ""numericId"": 1022, ""textures"": { ""all"": ""bed"" } }",
                @"{ ""id"": ""chest"", ""numericId"": 1023, ""textures"": { ""all"": ""chest"" } }",
                @"{ ""id"": ""wooden_door"", ""numericId"": 1026, ""textures"": { ""all"": ""door"" } }",
                @"{ ""id"": ""enchanting_table"", ""numericId"": 1062, ""textures"": { ""all"": ""ench"" } }",
            };
            return BlockRegistry.FromJson(docs);
        }

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""chair"", ""numericId"": 1201, ""blockId"": ""chair_block"" }",
                @"{ ""id"": ""chest"", ""numericId"": 1351, ""blockId"": ""chest"" }",
                @"{ ""id"": ""bed"", ""numericId"": 1353, ""maxStack"": 1, ""blockId"": ""bed"" }",
                @"{ ""id"": ""wooden_door"", ""numericId"": 1352, ""blockId"": ""wooden_door"" }",
                @"{ ""id"": ""enchanting_table"", ""numericId"": 1604, ""blockId"": ""enchanting_table"" }",
                @"{ ""id"": ""dirt"", ""numericId"": 1015 }", // 对照：无 blockId
                @"{ ""id"": ""bad_item"", ""numericId"": 1999, ""blockId"": ""no_such_block"" }",
            });
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PlaceRoutingCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Items = BuildItems();

            _world = new World();
            _registry = BuildRegistry();

            var player = _host.AddComponent<PlayerController>();
            player.Bind(_world, _registry, new Float3(50f, 80f, 50f)); // 远离 (8,71,8)，不触发防卡身

            _block = _host.AddComponent<BlockInteraction>();
            _block.Bind(_world, _registry, null, _host.transform); // views=null：MarkBlockChanged 容错
            _beds = new BedSystem(_registry);
            _block.SetBedSystem(_beds);
        }

        [TearDown]
        public void TearDown()
        {
            // 全限定：本文件同时 using System（数据 fixture 的 AppContext）与 UnityEngine，
            // 裸 Object 会二义（CS0104）
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        }

        /// <summary>从 (8,70,8) 草方块顶面垂直下探拿命中（放置格恒为 (8,71,8)，照 UseRoutingTests）。
        /// 适用：单格放置 / 门两格成功 / 占位兼容——放置列上没有障碍。</summary>
        private MyWorld.Core.Physics.VoxelRayHit CastDownAtColumn()
        {
            var source = new InteractionRaySource(_world, _registry);
            return MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new Float3(8.5f, 74.5f, 8.5f), new Float3(0f, -1f, 0f), 10f);
        }

        /// <summary>从西侧水平打进 (8,70,8) 侧面 → 放置格 (7,70,8)。上格 (7,71,8) 与
        /// +Z 头格 (7,70,9) 都不在射线通路上——「上格/床头被占 → 整体拒绝」的用例
        /// 不会被射线先撞上那个障碍（垂直下探会，命中面就变了）。</summary>
        private MyWorld.Core.Physics.VoxelRayHit CastWestAtColumn()
        {
            var source = new InteractionRaySource(_world, _registry);
            return MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new Float3(4.5f, 70.5f, 8.5f), new Float3(1f, 0f, 0f), 10f);
        }

        private void Select(int itemId, int count)
        {
            _ctx.Inventory.SetSlot(0, new ItemStack(itemId, count));
            _ctx.Inventory.SelectedHotbarIndex = 0;
        }

        /// <summary>给玩家补一个朝 <paramref name="yaw"/>（度）的「相机」子物体并重跑 Awake——
        /// ResolveBedFacing 读 Eye.forward，EditMode fixture 默认没有 eye（UseRoutingTests 弓测试同款做法）。</summary>
        private void AttachEye(float yaw)
        {
            var eyeGo = new GameObject("相机");
            eyeGo.transform.SetParent(_host.transform);
            eyeGo.transform.localPosition = Vector3.zero;
            eyeGo.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            InvokeAwake(_host.GetComponent<PlayerController>());
        }

        // ─── 单格：家具 / 箱子 / 附魔台 ─────────────────────────────────

        [Test]
        public void UseAt_HeldChair_PlacesChairBlock_ConsumesOne()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            Select(ChairItemId, 2);

            _block.UseAt(CastDownAtColumn());

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(ChairBlockId),
                "手持椅子 → 放 chair_block（替换 m3 恒放石头的占位）");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(1), "放置成功扣 1 个物品：2→1");
        }

        [Test]
        public void UseAt_HeldChest_PlacesChestBlock_ConsumesOne()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            Select(ChestItemId, 3);

            _block.UseAt(CastDownAtColumn());

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Chest), "手持箱子 → 放 chest 方块");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(2), "扣 1：3→2");
        }

        [Test]
        public void UseAt_HeldEnchantingTable_PlacesBlock_ConsumesOne()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            Select(EnchantingTableItemId, 1);

            _block.UseAt(CastDownAtColumn());

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(EnchantingTableBlockId),
                "手持附魔台 → 放 enchanting_table 方块");
            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.True, "扣 1：1→空");
        }

        // ─── 门：贴地两格竖放 ──────────────────────────────────────────

        [Test]
        public void UseAt_HeldWoodenDoor_PlacesTwoVerticalCells_ConsumesOne()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            Select(WoodenDoorItemId, 2);

            _block.UseAt(CastWestAtColumn());

            Assert.That(_world.GetBlock(7, 70, 8), Is.EqualTo(BlockIds.WoodenDoor), "门下格（贴地）");
            Assert.That(_world.GetBlock(7, 71, 8), Is.EqualTo(BlockIds.WoodenDoor),
                "门上格（同 id 双格——无 upper 方块 id / 无 metadata 位，取舍见 PlaceDoorTwoCells 注释）");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(1), "一扇门扣 1 个物品（两格只扣一次）：2→1");
        }

        [Test]
        public void UseAt_HeldDoor_UpperCellBlocked_PlacesNothing()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            _world.SetBlock(7, 71, 8, BlockIds.Stone); // 上格被占（天花板抵头）；不在射线通路上
            Select(WoodenDoorItemId, 2);

            _block.UseAt(CastWestAtColumn());

            Assert.That(_world.GetBlock(7, 70, 8), Is.EqualTo(BlockIds.Air), "不留半扇门：上格放不下就整体不放");
            Assert.That(_world.GetBlock(7, 71, 8), Is.EqualTo(BlockIds.Stone), "占住上格的方块不动");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(2), "放置失败不扣物品");
        }

        // ─── 床：双格 + 取向 ──────────────────────────────────────────

        [Test]
        public void UseAt_HeldBed_PlacesFootAndHeadAlongPlayerFacing_ConsumesOne()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            Select(BedItemId, 1);
            AttachEye(yaw: 0f); // 面朝 +Z（North）→ 头格在脚格 +Z 一格

            _block.UseAt(CastWestAtColumn()); // 放置格（脚格）= (7,70,8)

            Assert.That(_world.GetBlock(7, 70, 8), Is.EqualTo(BlockIds.Bed), "床脚格 = 解算出的放置格");
            Assert.That(_world.GetBlock(7, 70, 9), Is.EqualTo(BlockIds.Bed),
                "床头格 = 脚格沿玩家视向偏移一格（BedSystem.PlaceBed 双格 API）");
            Assert.That(_beds.GetFacing(7, 70, 8), Is.EqualTo(BedFacing.North), "脚格朝向记录为玩家视向（North）");
            Assert.That(_beds.SpawnPoints.Count, Is.EqualTo(1), "放床登记一个重生点");
            Float3 spawn = _beds.SpawnPoints[0];
            Assert.That((spawn.X, spawn.Y, spawn.Z), Is.EqualTo((7.5f, 71f, 8.5f)),
                "重生点 = 脚格中心、床面上一格（PlaceBed 按脚格登记）");
            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.True, "扣 1：1→空（maxStack=1 整格清空）");
        }

        [Test]
        public void UseAt_HeldBed_HeadCellBlocked_PlacesNothing()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            _world.SetBlock(7, 70, 9, BlockIds.Stone); // 头格被占；不在射线通路上
            Select(BedItemId, 1);
            AttachEye(yaw: 0f); // 头格 (7,70,9)

            _block.UseAt(CastWestAtColumn());

            Assert.That(_world.GetBlock(7, 70, 8), Is.EqualTo(BlockIds.Air),
                "不留半张床：头格放不下就整体拒绝（PlaceBed 两格原子性）");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(1), "放置失败不扣物品");
            Assert.That(_beds.SpawnPoints, Is.Empty, "失败不登记重生点");
        }

        [Test]
        public void UseAt_HeldBed_HeadCellWouldTrapPlayer_PlacesNothing()
        {
            // 防卡身复用：脚格 (7,70,8) 已由 TryResolve 把过关，但头格 (7,70,9) 正压着玩家——
            // IntersectsPlayer 必须对多格放置的每一格都生效，否则把自己封进床里
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            Select(BedItemId, 1);
            AttachEye(yaw: 0f); // 头格在 +Z → (7,70,9)
            // 玩家挪进头格（宽 0.6 高 1.8，盒 [7.2,7.8]×[70,71.8]×[9.2,9.8] 压着格子 (7,70,9)，
            // 与脚格 z 区间 [8,9] 严格不相交——TryResolve 的脚格关照常通过）
            _host.GetComponent<PlayerController>().Bind(
                _world, _registry, new Float3(7.5f, 70f, 9.5f));

            _block.UseAt(CastWestAtColumn());

            Assert.That(_world.GetBlock(7, 70, 8), Is.EqualTo(BlockIds.Air), "头格卡身：整体不放");
            Assert.That(_world.GetBlock(7, 70, 9), Is.EqualTo(BlockIds.Air), "头格保持空气");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(1), "不扣物品");
        }

        // ─── 无 blockId 让位 + 悬空 blockId ──────────────────────────────

        [Test]
        public void UseAt_ItemWithoutBlockId_PlacesNothing_DoesNotConsume()
        {
            // 评审 07#9：m3「恒放石头占位」路径退役——无 blockId 物品（如泥土本身不可放）
            // 右键不放任何方块、不扣物品
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            Select(DirtItemId, 5);

            _block.UseAt(CastDownAtColumn());

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                "无 blockId 物品不放方块（占位路径已退役）");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(5), "不扣物品");
        }

        [Test]
        public void UseAt_UnregisteredBlockId_PlacesNothing_NoConsume()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            Select(BadItemId, 2); // bad_item → no_such_block（悬空引用）

            _block.UseAt(CastDownAtColumn());

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                "悬空 blockId 不放任何方块（也不回落占位——对家具物品放石头更错）");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(2), "不扣物品");
        }
    }
#endif
}
