// m7 A3：右键吃食物（打通零调用的 HungerSystem.Eat）。
// 双链分两段：
// - 无条件部分只依赖 Core，dotnet 与 EditMode 都跑：HungerSystem.Eat 的恢复 / 钳制 / 非法值 no-op；
// - #if UNITY_EDITOR 部分依赖 MonoBehaviour（PlayerContext / BlockInteraction），只跑 EditMode：
//   BlockInteraction 右键路由——食物优先于放方块（EditMode 没法驱动 Input.GetMouseButtonDown，
//   直接调 public 入口 UseAt，与 BlockBreakDropTests 驱动 BreakAt 同款做法）。
using MyWorld.Core.Player;
using NUnit.Framework;

#if UNITY_EDITOR
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using UnityEngine;
#endif

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class EatFoodTests
    {
        // ─── HungerSystem.Eat 纯逻辑（dotnet + EditMode 双链） ───────────────

        [Test]
        public void Eat_RestoresHungerAndHalfValueSaturation()
        {
            var h = new HungerSystem { Hunger = 5, Saturation = 0f };

            h.Eat(6);

            Assert.That(h.Hunger, Is.EqualTo(11), "Hunger 应 +foodValue（5→11）");
            Assert.That(h.Saturation, Is.EqualTo(3f).Within(0.0001f), "Saturation 应 +foodValue×0.5（0→3）");
        }

        [Test]
        public void Eat_ClampsAtMaxHungerAndSaturation()
        {
            var h = new HungerSystem { Hunger = 18, Saturation = 19f };

            h.Eat(6);

            Assert.That(h.Hunger, Is.EqualTo(HungerSystem.MaxHunger),
                "Hunger 18+6 应钳制在 20，不能是 24");
            Assert.That(h.Saturation, Is.EqualTo(HungerSystem.MaxSaturation).Within(0.0001f),
                "Saturation 19+3 应钳制在 20，不能是 22");
        }

        [Test]
        public void Eat_NonPositiveFoodValue_IsNoOp()
        {
            // bowl_of_water 之类 healAmount=0 的物品不算食物（判定是 >0 而非 !=null），
            // 即使有人绕过判定直接调 Eat(0)/Eat(负数)，也不该产生任何副作用
            var h = new HungerSystem { Hunger = 7, Saturation = 2f };

            h.Eat(0);
            h.Eat(-3);

            Assert.That(h.Hunger, Is.EqualTo(7), "foodValue=0 / 负数应保持 Hunger 不变");
            Assert.That(h.Saturation, Is.EqualTo(2f).Within(0.0001f), "foodValue=0 / 负数应保持 Saturation 不变");
        }

#if UNITY_EDITOR
        // ─── BlockInteraction 右键路由（仅 EditMode） ────────────────────────

        private GameObject _host;
        private PlayerContext _ctx;
        private BlockInteraction _block;
        private World _world;
        private BlockRegistry _registry;

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
            };
            return BlockRegistry.FromJson(docs);
        }

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""apple"", ""numericId"": 100, ""healAmount"": 4 }",
                @"{ ""id"": ""stick"", ""numericId"": 101 }",
                @"{ ""id"": ""bowl_of_water"", ""numericId"": 102, ""healAmount"": 0 }",
            });
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("EatFoodCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.HungerSystem = new HungerSystem();
            _ctx.Items = BuildItems();

            _world = new World();
            // 命中目标：一颗石头悬在空气里，顶面被射线打到时放置位是 (8,71,8)
            _world.SetBlock(8, 70, 8, BlockIds.Stone);
            _registry = BuildRegistry();

            // 玩家绑到远离 (8,71,8) 的位置——放置判定（IntersectsPlayer）不会拦截这次放置
            var player = _host.AddComponent<PlayerController>();
            player.Bind(_world, _registry, new Float3(50f, 80f, 50f));

            _block = _host.AddComponent<BlockInteraction>();
            _block.Bind(_world, _registry, null, _host.transform); // views=null：MarkBlockChanged 容错
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>从 (8,70,8) 正上方垂直下探——走真实 VoxelRaycaster 路径拿命中，
        /// 命中顶面 → 放置位 (8,71,8)。</summary>
        private VoxelRayHit CastDownAtTarget()
        {
            var source = new WorldSolidSource(_world, _registry);
            return VoxelRaycaster.Cast(source, new Float3(8.5f, 74.5f, 8.5f), new Float3(0f, -1f, 0f), 10f);
        }

        [Test]
        public void UseAt_FoodSelected_EatsOneAndSkipsPlacement()
        {
            _ctx.Inventory.SetSlot(0, new ItemStack(100, 3)); // 3 个 apple（healAmount=4）
            _ctx.Inventory.SelectedHotbarIndex = 0;
            _ctx.HungerSystem.Hunger = 5;
            _ctx.HungerSystem.Saturation = 0f;
            var hit = CastDownAtTarget();
            Assert.That(hit.Hit, Is.True, "前置条件：射线应命中 (8,70,8) 的石头");

            _block.UseAt(hit);

            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(2), "吃掉 1 个：3→2");
            Assert.That(_ctx.HungerSystem.Hunger, Is.EqualTo(9), "apple healAmount=4：Hunger 5→9");
            Assert.That(_ctx.HungerSystem.Saturation, Is.EqualTo(2f).Within(0.0001f), "Saturation +4×0.5：0→2");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                "食物优先：右键被吃消费掉，命中面外侧不放方块");
        }

        [Test]
        public void UseAt_NonFoodSelected_PlacesBlockAsBefore()
        {
            _ctx.Inventory.SetSlot(0, new ItemStack(101, 3)); // stick：healAmount 缺失，不可食用
            _ctx.Inventory.SelectedHotbarIndex = 0;
            var hit = CastDownAtTarget();

            _block.UseAt(hit);

            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Stone),
                "非食物右键：照常放 placeBlockId（序列化默认 Stone）");
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(3), "没吃东西，物品不减");
        }

        [Test]
        public void UseAt_ZeroHealAmountItem_PlacesInsteadOfEating()
        {
            _ctx.Inventory.SetSlot(0, new ItemStack(102, 2)); // bowl_of_water：healAmount=0
            _ctx.Inventory.SelectedHotbarIndex = 0;
            _ctx.HungerSystem.Hunger = 5;
            var hit = CastDownAtTarget();

            _block.UseAt(hit);

            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(2),
                "healAmount=0 不算食物（判 >0 而非 !=null），不吃不损耗");
            Assert.That(_ctx.HungerSystem.Hunger, Is.EqualTo(5), "饥饿不变");
            Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Stone), "右键落到放方块分支");
        }

        [Test]
        public void UseAt_FoodSelectedWithoutHit_StillEats()
        {
            // m7 A3：看天 / 看远处（射线落空）也必须能吃——
            // 原逻辑只有射线命中才能右键，饿急了抬头就吃不上东西
            _ctx.Inventory.SetSlot(0, new ItemStack(100, 1)); // 最后 1 个 apple
            _ctx.Inventory.SelectedHotbarIndex = 0;
            _ctx.HungerSystem.Hunger = 2;
            _ctx.HungerSystem.Saturation = 0f;

            _block.UseAt(default(VoxelRayHit)); // 未命中

            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.True, "最后 1 个吃掉：槽位变空");
            Assert.That(_ctx.HungerSystem.Hunger, Is.EqualTo(6), "Hunger 2→6");
            Assert.That(_ctx.HungerSystem.Saturation, Is.EqualTo(2f).Within(0.0001f), "Saturation 0→2");
        }

        [Test]
        public void UseAt_DeathScreenVisible_RightClickYieldsToRespawn()
        {
            // m10 C2 fix1（I1）：死亡画面激活期间右键已等效复活按钮——
            // 同一次右键绝不能再顺手吃掉手持食物 / 放方块（双触发）
            _ctx.Inventory.SetSlot(0, new ItemStack(100, 3)); // 3 个 apple（healAmount=4）
            _ctx.Inventory.SelectedHotbarIndex = 0;
            _ctx.HungerSystem.Hunger = 5;
            _ctx.HungerSystem.Saturation = 0f;

            var deathGo = new GameObject("DeathUI");
            try
            {
                var ui = deathGo.AddComponent<MyWorld.Unity.UI.DeathScreenUi>();
                InvokeAwake(ui);   // 挂到 ctx.DeathScreen
                ui.OnPlayerDied(); // IsVisible = true

                var hit = CastDownAtTarget();
                Assert.That(hit.Hit, Is.True, "前置条件：射线应命中石头");

                _block.UseAt(hit);

                Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(3),
                    "死亡画面右键只复活：apple 一个都不吃");
                Assert.That(_ctx.HungerSystem.Hunger, Is.EqualTo(5), "饥饿不动");
                Assert.That(_ctx.HungerSystem.Saturation, Is.EqualTo(0f).Within(0.0001f), "饱和不动");
                Assert.That(_world.GetBlock(8, 71, 8), Is.EqualTo(BlockIds.Air),
                    "也不放方块——本次右键整个让位给复活");
            }
            finally
            {
                Object.DestroyImmediate(deathGo); // ctx.DeathScreen 变 fake-null，不污染后续用例
            }

            // 对照：死亡画面不在（DeathScreen 引用失效视同无）右键恢复吃
            _block.UseAt(CastDownAtTarget());
            Assert.That(_ctx.Inventory.GetSlot(0).Count, Is.EqualTo(2), "对照：无死亡画面时右键照常吃 1 个");
        }
#endif
    }
}
