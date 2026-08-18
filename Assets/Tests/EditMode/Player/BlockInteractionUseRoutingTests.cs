#if UNITY_EDITOR
// m11 ②（B 批）：BlockInteraction 的右键交互路由 + 左键成熟收获 + 弓蓄力放箭。
// 依赖 Unity MonoBehaviour（PlayerContext / BlockInteraction / PlayerController），
// 照 BlockToolTierTests 的做法整文件 #if UNITY_EDITOR 包裹——EditMode 驱动不了
// Input.GetMouseButtonDown，全部直调 public 入口（UseAt / BreakAt / ReleaseBowCharge）。
// dotnet 链不跑本文件；Core 侧契约由 FarmSystemTests / ProjectileEntityTests 覆盖。
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Farming;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class BlockInteractionUseRoutingTests
    {
        private GameObject _host;
        private PlayerContext _ctx;
        private BlockInteraction _block;
        private World _world;
        private BlockRegistry _registry;
        private FarmSystem _farm;
        private BedSystem _beds;

        // 物品 numericId 与真实 items/*.json 对齐（arrow 尤其重要——ProjectileEntity.ArrowItemId=1300）
        private const int HoeWoodenItemId = 1430;
        private const int SeedsWheatItemId = 1019;
        private const int BoneMealItemId = 1505;
        private const int BowItemId = 1301;
        private const int ArrowItemId = 1300;
        // m13 W3：火枪 + 子弹 numericId。musket / bullet 物品 id 必须随 ItemDatabase 一并加载
        // （否则 BuildItems 的 GetById 在路由断言里抛 NullRef）。
        private const int MusketItemId = 1607;
        private const int MusketBulletItemId = 1608;

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
                @"{ ""id"": ""iron_door"", ""numericId"": 1005, ""textures"": { ""all"": ""door"" } }",
                @"{ ""id"": ""bed"", ""numericId"": 1022, ""textures"": { ""all"": ""bed"" } }",
                @"{ ""id"": ""chest"", ""numericId"": 1023, ""textures"": { ""all"": ""chest"" } }",
                @"{ ""id"": ""wooden_door"", ""numericId"": 1026, ""textures"": { ""all"": ""door"" } }",
                @"{ ""id"": ""farmland"", ""numericId"": 1050, ""textures"": { ""all"": ""farmland"" } }",
                @"{ ""id"": ""farmland_wet"", ""numericId"": 1051, ""textures"": { ""all"": ""farmland"" } }",
                @"{ ""id"": ""wheat_stage0"", ""numericId"": 1052, ""textures"": { ""all"": ""crop"" },
                    ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""wheat_stage2"", ""numericId"": 1054, ""textures"": { ""all"": ""crop"" },
                    ""solid"": false, ""opaque"": false }",
            };
            return BlockRegistry.FromJson(docs);
        }

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                // FarmSystem 构造校验六个跨表引用（产物×3 + 种子×3），缺一即抛
                @"{ ""id"": ""wheat"", ""numericId"": 1100 }",
                @"{ ""id"": ""beet"", ""numericId"": 1101 }",
                @"{ ""id"": ""mung_bean"", ""numericId"": 1102 }",
                @"{ ""id"": ""seeds_wheat"", ""numericId"": 1019 }",
                @"{ ""id"": ""seeds_beet"", ""numericId"": 1028 }",
                @"{ ""id"": ""seeds_mung"", ""numericId"": 1029 }",
                @"{ ""id"": ""hoe_wooden"", ""numericId"": 1430, ""maxStack"": 1,
                    ""isTool"": true, ""toolTier"": 1, ""maxDurability"": 59 }",
                @"{ ""id"": ""bone_meal"", ""numericId"": 1505 }",
                @"{ ""id"": ""bow"", ""numericId"": 1301, ""maxStack"": 1, ""attackDamage"": 1, ""range"": 60 }",
                @"{ ""id"": ""arrow"", ""numericId"": 1300 }",
                // m13 W3：火枪 + 子弹（BuildItems 必须把路由分支里被引用的物品都注册上）。
                @"{ ""id"": ""musket"", ""numericId"": 1607, ""maxStack"": 1, ""attackDamage"": 6, ""range"": 25 }",
                @"{ ""id"": ""bullet"", ""numericId"": 1608, ""maxStack"": 64 }",
            });
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("UseRoutingCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Items = BuildItems();
            _ctx.Time = new TimeOfDay(); // 正午起步 → IsNight = false

            _world = new World();
            _registry = BuildRegistry();

            // 玩家绑到远离 (8,71,8) 的位置——放置判定（IntersectsPlayer）不会拦截放置
            var player = _host.AddComponent<PlayerController>();
            player.Bind(_world, _registry, new Float3(50f, 80f, 50f));

            // m13 W3 火枪：PlayerController.Awake 自动找名为「相机」的子物体作为 eye Transform。
            // 没有它 BlockInteraction.TryFireMusket 会因 _player.Eye == null 静默拒绝，
            // 装填窗不开、子弹不扣、Projectile 不抛出——火枪 4 个 EditMode 测试一起炸。
            // （ReleaseBowCharge_FiresPlayerArrow_ConsumesOneArrow 已在测试体内补同名物体。）
            var eyeGo = new GameObject("相机");
            eyeGo.transform.SetParent(_host.transform);
            InvokeAwake(player);

            _block = _host.AddComponent<BlockInteraction>();
            _block.Bind(_world, _registry, null, _host.transform); // views=null：MarkBlockChanged 容错
            _farm = new FarmSystem(_ctx.Items, seed: 123);
            _beds = new BedSystem(_registry);
            _block.SetFarmSystem(_farm);
            _block.SetBedSystem(_beds);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>从 (8,70,8)/(8,71,8) 所在柱正上方垂直下探——走真实 VoxelRaycaster +
        /// InteractionRaySource 路径拿命中（作物 solid=false 也要能被打中，这是 m11 ②的先决修复）。</summary>
        private MyWorld.Core.Physics.VoxelRayHit CastDownAtColumn()
        {
            var source = new InteractionRaySource(_world, _registry);
            return MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new Float3(8.5f, 74.5f, 8.5f), new Float3(0f, -1f, 0f), 10f);
        }

        private void Select(int itemId, int count)
        {
            _ctx.Inventory.SetSlot(0, new ItemStack(itemId, count));
            _ctx.Inventory.SelectedHotbarIndex = 0;
        }

        // ─── 锄：草 → 耕地 ───────────────────────────────────────────────

        [Test]
        public void UseAt_HoeOnGrass_TillsFarmland_DoesNotPlace()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            Select(HoeWoodenItemId, 1);
            var hit = CastDownAtColumn();
            Assert.That(hit.Hit, Is.True, "前置条件：射线应命中草方块");

            _block.UseAt(hit);

            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(FarmSystem.FarmlandId),
                "锄 + 草 → 干耕地（FarmSystem.Till）");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                "右键被锄地消费，不放方块");
        }

        [Test]
        public void UseAt_EmptyHandOnGrass_PlacesAsBefore()
        {
            // 反向守卫：没手持锄时右键草方块保持旧放方块行为——新路由只对「锄 + 可锄目标」生效
            _world.SetBlock(8, 70, 8, BlockIds.Grass);
            _ctx.Inventory.SelectedHotbarIndex = 0; // 空手

            _block.UseAt(CastDownAtColumn());

            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Grass), "没锄不动草");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Stone), "照常放 placeBlockId");
        }

        // ─── 种子：耕地 → 播种 stage0 ────────────────────────────────────

        [Test]
        public void UseAt_SeedsOnFarmland_PlantsStage0_ConsumesOneSeed()
        {
            _world.SetBlock(8, 70, 8, FarmSystem.FarmlandId);
            Select(SeedsWheatItemId, 3);
            var hit = CastDownAtColumn();
            Assert.That(hit.Hit, Is.True);

            _block.UseAt(hit);

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(FarmSystem.WheatStage0Id),
                "耕地正上方长出 stage0 小麦");
            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(FarmSystem.FarmlandId), "耕地不被动");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(2), "扣 1 粒种子：3→2");
        }

        // ─── 骨粉：作物催熟一级 ───────────────────────────────────────────

        [Test]
        public void UseAt_BoneMealOnYoungCrop_AdvancesOneStage_ConsumesBoneMeal()
        {
            _world.SetBlock(8, 70, 8, FarmSystem.FarmlandId);
            _world.SetBlock(8, 71, 8, FarmSystem.WheatStage0Id);
            Select(BoneMealItemId, 2);
            var hit = CastDownAtColumn();
            Assert.That(hit.Hit, Is.True, "前置：solid=false 的作物要能被交互射线命中");

            _block.UseAt(hit);

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(FarmSystem.WheatStage1Id),
                "骨粉催熟一级：stage0 → stage1");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(1), "催熟成功扣 1 份骨粉");
        }

        [Test]
        public void UseAt_BoneMealOnMatureCrop_ConsumesClick_WithoutBoneMealLoss()
        {
            // 成熟作物催不动（ApplyBoneMeal 返回 false），但右键仍要被消费——
            // 不能往自家作物上顺手放一个方块
            _world.SetBlock(8, 70, 8, FarmSystem.FarmlandId);
            _world.SetBlock(8, 71, 8, FarmSystem.WheatStage2Id);
            Select(BoneMealItemId, 2);

            _block.UseAt(CastDownAtColumn());

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(FarmSystem.WheatStage2Id), "成熟的不再长");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(2), "催不动不扣骨粉");
            Assert.That(_world.GetBlock(8, 72, 8), Is.EqualTo(BlockIds.Air), "也不放方块");
        }

        // ─── 左键：成熟作物 → FarmSystem.Harvest ─────────────────────────

        [Test]
        public void BreakAt_MatureWheat_HarvestsViaFarmSystem_DropsFromHarvest()
        {
            _world.SetBlock(8, 70, 8, FarmSystem.FarmlandId);
            _world.SetBlock(8, 71, 8, FarmSystem.WheatStage2Id);
            _ctx.Inventory.SelectedHotbarIndex = 0; // 空手收获

            _block.BreakAt(8, 71, 8);

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air), "收获清掉作物方块");
            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(FarmSystem.FarmlandId),
                "收获不破坏脚下的耕地");
            Assert.That(_ctx.ItemDrops.Count, Is.GreaterThanOrEqualTo(1),
                "掉落走 FarmSystem.Harvest 的确定性掷骰（产物 1-2 + 种子），不走 BlockDrops");
            int wheatItem = _ctx.Items.GetById("wheat").NumericId;
            int seedItem = _ctx.Items.GetById("seeds_wheat").NumericId;
            foreach (var drop in _ctx.ItemDrops)
            {
                int id = drop.Content.Value.ItemId;
                Assert.That(id == wheatItem || id == seedItem, Is.True,
                    $"收获掉落只应是小麦产物或种子，实际 itemId={id}");
            }
        }

        // ─── 床：夜间睡 / 白天提示 ────────────────────────────────────────

        [Test]
        public void UseAt_BedAtNight_Sleeps_JumpsToMorning_SetsRespawn()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Bed);
            _ctx.Time.CurrentTick = 14000f; // 夜间（13000..23000）
            _ctx.Inventory.SelectedHotbarIndex = 0; // 空手

            _block.UseAt(CastDownAtColumn());

            Assert.That(_ctx.Time.CurrentTick, Is.EqualTo(0f), "睡成：时间跳早晨 0 tick");
            Assert.That(_beds.RespawnPoint, Is.Not.Null, "重生点设到该床");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air), "右键被睡觉消费");
        }

        [Test]
        public void UseAt_BedAtDaytime_HintShown_NoPlace()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Bed);
            // 默认正午 IsNight=false

            _block.UseAt(CastDownAtColumn());

            Assert.That(_block.InteractionHintCount, Is.EqualTo(1), "白天给一次「只能在夜里睡觉」提示");
            Assert.That(_ctx.Time.CurrentTick, Is.EqualTo(6000f), "时间不动");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air), "提示性 no-op：不放方块");
        }

        // ─── 箱子：右键开箱子 UI（m11 W2-3，替换 v1 no-op 占位） ─────────

        [Test]
        public void UseAt_Chest_OpensChestUi_ConsumesClick_NoPlace()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Chest);
            _ctx.Inventory.SelectedHotbarIndex = 0;
            _ctx.ChestSystem = new ChestSystem(_ctx.Items); // 运行时 WorldBootstrap 已挂，这里直注

            _block.UseAt(CastDownAtColumn());

            var chestUi = _host.GetComponent<MyWorld.Unity.UI.ChestUi>();
            Assert.That(chestUi, Is.Not.Null, "右键箱子懒挂并打开 ChestUi（WorldBootstrap 零装配）");
            Assert.That(chestUi.IsOpen, Is.True);
            Assert.That((chestUi.ChestX, chestUi.ChestY, chestUi.ChestZ), Is.EqualTo((8, 70, 8)),
                "打开的正是被右键的那格箱子");
            Assert.That(MyWorld.Unity.UI.UiCursorGate.IsOpen, Is.True, "开箱登记指针门");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                "右键被开箱消费，不对着箱子放方块");

            chestUi.Close();
            UiCursorGate.Reset(); // 门位还清，别泄漏给后续测试
        }

        [Test]
        public void UseAt_Chest_WithoutChestSystem_StillConsumesClick_NoPlace()
        {
            // ChestSystem 降级（数据表缺失时 WorldBootstrap 置 null）：开不了箱，
            // 但右键仍被消费——不能对着箱子放方块（旧 no-op 行为保留）
            _world.SetBlock(8, 70, 8, BlockIds.Chest);
            _ctx.Inventory.SelectedHotbarIndex = 0;
            _ctx.ChestSystem = null;

            _block.UseAt(CastDownAtColumn());

            Assert.That(_host.GetComponent<MyWorld.Unity.UI.ChestUi>(), Is.Null, "系统未接好不懒挂 UI");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air), "右键仍被消费");
        }

        [TestCase(1026)] // wooden_door（= BlockIds.WoodenDoor）
        [TestCase(1005)] // iron_door
        public void UseAt_Door_ConsumesClick_NoPlace(int doorId)
        {
            _world.SetBlock(8, 70, 8, (ushort)doorId);
            _ctx.Inventory.SelectedHotbarIndex = 0;

            _block.UseAt(CastDownAtColumn());

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                $"右键门（numericId={doorId}）让位给 RedstoneSystem 的既有切换通道，不放方块");
        }

        // ─── 弓：蓄力 / 放箭 / 无箭 ──────────────────────────────────────

        [Test]
        public void UseAt_BowWithArrows_StartsCharging_DoesNotPlace()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            Select(BowItemId, 1);
            _ctx.Inventory.SetSlot(3, new ItemStack(ArrowItemId, 5)); // 箭在背包另一格

            _block.UseAt(CastDownAtColumn());

            Assert.That(_block.IsBowCharging, Is.True, "手持弓 + 背包有箭 → 开始蓄力");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air), "右键被拉弓消费，不放方块");
        }

        [Test]
        public void UseAt_BowWithoutArrows_NoCharge_HintShown()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            Select(BowItemId, 1); // 背包无 arrow

            _block.UseAt(CastDownAtColumn());

            Assert.That(_block.IsBowCharging, Is.False, "无箭不开弓");
            Assert.That(_block.InteractionHintCount, Is.EqualTo(1), "给一次「没有箭了」提示");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air), "也不放方块");
        }

        [Test]
        public void ReleaseBowCharge_FiresPlayerArrow_ConsumesOneArrow()
        {
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            Select(BowItemId, 1);
            _ctx.Inventory.SetSlot(3, new ItemStack(ArrowItemId, 5));
            _block.UseAt(CastDownAtColumn()); // 开始蓄力
            Assert.That(_block.IsBowCharging, Is.True);

            // Eye：PlayerController.Awake 取第一个子 transform——补一个子物体再补跑 Awake
            var eyeGo = new GameObject("相机");
            eyeGo.transform.SetParent(_host.transform);
            InvokeAwake(_host.GetComponent<PlayerController>());

            ProjectileEntity fired = null;
            MobAI.OnProjectileFired += Capture;
            try
            {
                _block.ReleaseBowCharge();
            }
            finally
            {
                MobAI.OnProjectileFired -= Capture;
            }

            Assert.That(fired, Is.Not.Null, "放箭经 MobAI.OnProjectileFired 抛出（与骷髅同一事件）");
            Assert.That(fired.OwnerEntityId, Is.EqualTo(0), "owner=0 表示玩家箭（不自伤）");
            Assert.That(fired.Damage, Is.EqualTo(BlockInteraction.BowMinDamage).Within(1e-4f),
                "EditMode 下 Time.time 恒 0 → 蓄力比例 0 → 伤害取下限（1-4 按比例的另一端由 Core 弹道测试守）");
            float speed = Float3Length(fired.Velocity);
            Assert.That(speed, Is.EqualTo(BlockInteraction.BowMinArrowSpeed).Within(1e-3f),
                "箭速同样按蓄力比例取下限");
            Assert.That(_ctx.Inventory.CountOf(ArrowItemId), Is.EqualTo(4), "消耗背包 arrow ×1：5→4");
            Assert.That(_block.IsBowCharging, Is.False, "放箭后蓄力状态复位");

            void Capture(ProjectileEntity arrow) => fired = arrow;
        }

        private static float Float3Length(Float3 v)
            => (float)System.Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

        // ─── m13 W3：火枪（开火 / 装填 1.5s / 无弹咔哒 / 直射无重力） ─────

        [Test]
        public void TryFireMusket_WithBullet_FiresProjectile_ConsumesOneBullet_NoReloadYet()
        {
            // 玩家面对方块（不要求命中：直射武器对射线无依赖），手持火枪 + 背包 3 颗子弹
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            Select(MusketItemId, 1);
            _ctx.Inventory.SetSlot(3, new ItemStack(MusketBulletItemId, 3));

            ProjectileEntity fired = null;
            MobAI.OnProjectileFired += Capture;
            try
            {
                _block.TryFireMusket();
            }
            finally
            {
                MobAI.OnProjectileFired -= Capture;
            }

            Assert.That(fired, Is.Not.Null, "开火经 MobAI.OnProjectileFired 抛出");
            Assert.That(fired.OwnerEntityId, Is.EqualTo(0), "owner=0 表示玩家弹");
            Assert.That(fired.Damage, Is.EqualTo(BlockInteraction.MusketBulletDamage).Within(1e-4f),
                "伤害 = MusketBulletDamage（6）");
            Assert.That(fired.IsStraightLine, Is.True, "火枪：直射无重力（IsStraightLine=true）");
            Assert.That(fired.Range, Is.EqualTo(25f).Within(1e-4f),
                "火枪：射程 25m");
            Assert.That(fired.ReachedRange(), Is.False, "刚开火时位移=0，未达射程");
            float speed = Float3Length(fired.Velocity);
            Assert.That(speed, Is.EqualTo(BlockInteraction.MusketBulletSpeed).Within(1e-3f),
                "初速 = MusketBulletSpeed（32 格/s）");
            Assert.That(_ctx.Inventory.CountOf(MusketBulletItemId), Is.EqualTo(2),
                "扣 1 颗子弹：3→2");
            Assert.That(_block.IsMusketReloading, Is.True,
                "开火后进入装填中状态（IsMusketReloading=true）");

            void Capture(ProjectileEntity p) => fired = p;
        }

        [Test]
        public void TryFireMusket_RightAfterFire_Reloading_SecondFireIgnored()
        {
            // 第一次开火后立即第二次：装填窗 1.5s 内右键被拒，无新弹抛出
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            Select(MusketItemId, 1);
            _ctx.Inventory.SetSlot(3, new ItemStack(MusketBulletItemId, 5));

            // 第一次开火
            ProjectileEntity firstFire = null;
            MobAI.OnProjectileFired += Capture;
            try
            {
                _block.TryFireMusket();
            }
            finally
            {
                MobAI.OnProjectileFired -= Capture;
            }
            Assert.That(firstFire, Is.Not.Null, "第一次开火成功");
            int firstBulletCount = _ctx.Inventory.CountOf(MusketBulletItemId);
            Assert.That(firstBulletCount, Is.EqualTo(4), "第一次扣 1 颗：5→4");
            Assert.That(_block.IsMusketReloading, Is.True);

            // 第二次开火（装填中）—— 应被静默拒绝
            int firedCount = 0;
            MobAI.OnProjectileFired += _ => firedCount++;
            try
            {
                _block.TryFireMusket();
                _block.TryFireMusket(); // 多调几次也无害
            }
            finally
            {
                MobAI.OnProjectileFired -= _ => firedCount++;
            }
            Assert.That(firedCount, Is.EqualTo(0),
                "装填窗内第二次 / 第三次开火均被静默拒绝，无 Projectile 抛出");
            Assert.That(_ctx.Inventory.CountOf(MusketBulletItemId), Is.EqualTo(firstBulletCount),
                "装填窗内不扣子弹");

            void Capture(ProjectileEntity p) => firstFire = p;
        }

        [Test]
        public void TryFireMusket_ReloadElapsed_CanFireAgain()
        {
            // 模拟装填窗过去：EditMode 下 Time.time 永远 0，所以走不到 ReloadSeconds 自动过期。
            // 改用反射清回 _musketReloadUntil = float.NegativeInfinity（与既有测试反射调 Awake 同款）。
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            Select(MusketItemId, 1);
            _ctx.Inventory.SetSlot(3, new ItemStack(MusketBulletItemId, 5));

            // 第一次开火
            _block.TryFireMusket();
            Assert.That(_block.IsMusketReloading, Is.True);

            // 反射清装填窗
            var field = typeof(BlockInteraction).GetField(
                "_musketReloadUntil",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "BlockInteraction 应有私有 _musketReloadUntil 字段");
            field.SetValue(_block, float.NegativeInfinity);

            Assert.That(_block.IsMusketReloading, Is.False, "清装填窗后 IsMusketReloading=false");

            // 第二次开火：可开火
            ProjectileEntity second = null;
            MobAI.OnProjectileFired += Capture;
            try
            {
                _block.TryFireMusket();
            }
            finally
            {
                MobAI.OnProjectileFired -= Capture;
            }
            Assert.That(second, Is.Not.Null, "装填窗过后能再开火");
            Assert.That(_ctx.Inventory.CountOf(MusketBulletItemId), Is.EqualTo(3),
                "第二次再扣 1 颗：5→3");

            void Capture(ProjectileEntity p) => second = p;
        }

        [Test]
        public void TryFireMusket_NoBullets_NoFire_DoesNotConsumeBullet()
        {
            // 无弹：右键被消费但不开火（不动子弹数、不发 Projectile）
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            Select(MusketItemId, 1);
            // 背包 0 颗 bullet

            int firedCount = 0;
            MobAI.OnProjectileFired += _ => firedCount++;
            try
            {
                _block.TryFireMusket();
            }
            finally
            {
                MobAI.OnProjectileFired -= _ => firedCount++;
            }
            Assert.That(firedCount, Is.EqualTo(0), "无弹：不开火，无 Projectile 抛出");
            Assert.That(_ctx.Inventory.CountOf(MusketBulletItemId), Is.EqualTo(0),
                "无弹：不消耗子弹（背包里没有也没变）");
            Assert.That(_block.IsMusketReloading, Is.False,
                "无弹：装填窗不锁定（拒绝后可以立刻再试——也许玩家下一瞬拿到子弹）");
        }

        [Test]
        public void UseAt_Musket_WithBullets_FiresImmediately_NoChargingState()
        {
            // 弓流程不回归 + 火枪不走蓄力：手持火枪右键一次直接开火，不进入 IsBowCharging
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            Select(MusketItemId, 1);
            _ctx.Inventory.SetSlot(3, new ItemStack(MusketBulletItemId, 3));

            int firedCount = 0;
            MobAI.OnProjectileFired += _ => firedCount++;
            try
            {
                _block.UseAt(CastDownAtColumn());
            }
            finally
            {
                MobAI.OnProjectileFired -= _ => firedCount++;
            }

            Assert.That(firedCount, Is.EqualTo(1),
                "UseAt 走火枪分支：直接开火，无蓄力");
            Assert.That(_block.IsBowCharging, Is.False,
                "火枪路径不影响弓的蓄力状态——两条武器分支相互独立");
            Assert.That(_block.IsMusketReloading, Is.True,
                "UseAt 走完后进入装填中（与 TryFireMusket 直接调一致）");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                "右键被开火消费，不放方块（即便准星后面有方块）");
        }
    }
}
#endif
