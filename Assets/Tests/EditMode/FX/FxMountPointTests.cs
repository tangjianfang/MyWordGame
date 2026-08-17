#if UNITY_EDITOR
// m11 W3-4：粒子特效三个挂载点的接线——玩法路径真的会发事件、池真的会响应：
//   1) BlockInteraction.BreakAt 挖方块 → BlockBroken(坐标, 方块id) → 池出碎屑
//   2) EnchantingUi.DoEnchant 附魔扣费成功 → Enchanted → 池出光柱
//   3) Core Explosion.Detonate → AfterDetonate → 池出爆炸面片
//      （AfterDetonate 的参数契约在 ExplosionDetonateEventTests，纯 Core 双链同跑）
// 事件在声明类之外只能 +=/-=（CS0079），所以「池响应」一律经真实发射端驱动（端到端）。
// 依赖 UnityEngine + 真实物品表（DoEnchant 走 lapis 扣费），dotnet 链跑不动，
// 整文件 #if UNITY_EDITOR 包裹。fixture 照 BlockBreakDropTests / EnchantingUiTests 同款。
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.FX;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.FX
{
    [TestFixture]
    public class FxMountPointTests
    {
        // ── 挂载点 1：挖掘碎屑（BlockInteraction.BreakAt → BlockBroken） ──

        [Test]
        public void 挖掘挂载点_BreakAt挖石头_广播BlockBroken带坐标与方块id()
        {
            var world = new World();
            world.SetBlock(8, 70, 8, BlockIds.Stone);
            var registry = BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
            });

            var host = new GameObject("FxMountBlock");
            try
            {
                var block = host.AddComponent<BlockInteraction>();
                block.Bind(world, registry, null, host.transform);

                int calls = 0;
                int seenX = -1, seenY = -1, seenZ = -1;
                ushort seenId = 999;
                System.Action<int, int, int, ushort> handler = (x, y, z, id) =>
                {
                    calls++;
                    seenX = x;
                    seenY = y;
                    seenZ = z;
                    seenId = id;
                };
                MyWorld.Unity.Player.BlockInteraction.BlockBroken += handler;
                try
                {
                    block.BreakAt(8, 70, 8);
                }
                finally
                {
                    MyWorld.Unity.Player.BlockInteraction.BlockBroken -= handler;
                }

                Assert.That(world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Air), "前置：方块真的被挖掉");
                Assert.That(calls, Is.EqualTo(1), "挖一次广播一次");
                Assert.That((seenX, seenY, seenZ), Is.EqualTo((8, 70, 8)), "事件带被挖坐标");
                Assert.That(seenId, Is.EqualTo(BlockIds.Stone), "事件带被挖前方块 id（碎屑按它取均值色）");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void 挖掘挂载点_挖空气不发事件()
        {
            var world = new World();
            var host = new GameObject("FxMountAir");
            try
            {
                var block = host.AddComponent<BlockInteraction>();
                block.Bind(world, null, null, host.transform);

                int calls = 0;
                System.Action<int, int, int, ushort> handler = (x, y, z, id) => calls++;
                MyWorld.Unity.Player.BlockInteraction.BlockBroken += handler;
                try
                {
                    block.BreakAt(0, 70, 0); // 空气：no-op
                }
                finally
                {
                    MyWorld.Unity.Player.BlockInteraction.BlockBroken -= handler;
                }
                Assert.That(calls, Is.EqualTo(0), "挖空气不产生碎屑事件");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>数池内指定种类的槽位数（与 ParticlePoolTests.CountKind 同款）。</summary>
        private static int CountKind(ParticlePool pool, ParticlePool.FxKind kind)
        {
            int n = 0;
            for (int i = 0; i < ParticlePool.Capacity; i++)
            {
                if (pool.SlotKind(i) == kind) n++;
            }
            return n;
        }

        /// <summary>
        /// EditMode 下 AddComponent 不回调 OnEnable（运行时才回调），池的事件订阅靠它——
        /// 用反射补一脚（InvokeAwake 同款做法）。返回池引用便于链式。
        /// </summary>
        private static ParticlePool CreateSubscribedPool(string name)
        {
            var host = new GameObject(name);
            var pool = host.AddComponent<ParticlePool>();
            var method = typeof(ParticlePool).GetMethod("OnEnable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "ParticlePool 应有私有 OnEnable（订阅三个挂载点事件）");
            method.Invoke(pool, null);
            return pool;
        }

        /// <summary>对称退订 + 销毁（EditMode 不回调 OnDisable，不退订会让销毁后的池收到事件）。</summary>
        private static void DestroySubscribedPool(ParticlePool pool)
        {
            if (pool == null) return;
            var method = typeof(ParticlePool).GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(pool, null);
            Object.DestroyImmediate(pool.gameObject);
        }

        [Test]
        public void 挖掘挂载点_端到端_BreakAt挖方块_池内出现碎屑()
        {
            var host = new GameObject("FxMountE2E(挖掘)");
            ParticlePool pool = null;
            try
            {
                // 池不注入注册表：走「贴图均值缺失 → 兜底亮灰」路径也要能出碎屑（不抛）
                pool = CreateSubscribedPool("PoolE2E(挖掘)");

                var world = new World();
                world.SetBlock(8, 70, 8, BlockIds.Stone);
                var registry = BlockRegistry.FromJson(new[]
                {
                    @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                    @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
                });
                var block = host.AddComponent<BlockInteraction>();
                block.Bind(world, registry, null, host.transform);

                block.BreakAt(8, 70, 8);
                Assert.That(CountKind(pool, ParticlePool.FxKind.Debris),
                    Is.InRange(ParticlePool.DebrisMinPerBreak, ParticlePool.DebrisMaxPerBreak),
                    "BreakAt → BlockBroken → 池出 4-6 粒碎屑（端到端）");
            }
            finally
            {
                Object.DestroyImmediate(host);
                DestroySubscribedPool(pool);
            }
        }

        [Test]
        public void 爆炸挂载点_端到端_Detonate起爆_池内出现爆炸面片()
        {
            ParticlePool pool = null;
            try
            {
                pool = CreateSubscribedPool("PoolE2E(爆炸)");

                var world = new World();
                world.SetBlock(8, 70, 8, BlockIds.Stone);
                MyWorld.Core.Combat.Explosion.Detonate(
                    world, new MyWorld.Core.Math.Float3(8.5f, 70.5f, 8.5f),
                    new MyWorld.Core.Math.Float3(200f, 90f, 200f), attackerEntityId: 1, radius: 3f);

                Assert.That(CountKind(pool, ParticlePool.FxKind.Explosion), Is.EqualTo(1),
                    "Detonate → AfterDetonate → 池出一张三帧爆炸面片（端到端）");
            }
            finally
            {
                DestroySubscribedPool(pool);
            }
        }

        // ── 挂载点 2：附魔光柱（EnchantingUi.DoEnchant → Enchanted） ──

        [Test]
        public void 附魔挂载点_DoEnchant扣费成功_广播Enchanted_池内出现光柱()
        {
            // 真实物品表：DoEnchant 扣 lapis/经验要走真数据（EnchantingUiTests 同款）
            string itemsDir = Path.Combine(Application.streamingAssetsPath, "items");
            var db = ItemDatabase.FromJson(Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText));

            var go = new GameObject("FxMountEnchant");
            ParticlePool pool = null;
            try
            {
                pool = CreateSubscribedPool("PoolE2E(附魔)");

                var ctx = go.AddComponent<PlayerContext>();
                ctx.Inventory = new PlayerInventory();
                ctx.Health = new Health(20f);
                ctx.Items = db;
                ctx.Experience = new Experience(30, 0);

                var pick = db.GetById("iron_pickaxe");
                var lapis = db.GetById("lapis");
                ctx.Inventory.SetSlot(0, new ItemStack(pick.NumericId, 1));
                ctx.Inventory.SelectedHotbarIndex = 0;
                ctx.Inventory.SetSlot(1, new ItemStack(lapis.NumericId, 64));

                int calls = 0;
                System.Action handler = () => calls++;
                MyWorld.Unity.UI.EnchantingUi.Enchanted += handler;
                try
                {
                    MyWorld.Unity.UI.EnchantingUi.DoEnchant(ctx, ctx.Inventory.GetSlot(0),
                        EnchantingTable.CostForLevel(1), 1);
                }
                finally
                {
                    MyWorld.Unity.UI.EnchantingUi.Enchanted -= handler;
                }

                Assert.That(ctx.Inventory.GetSlot(1).Count, Is.EqualTo(63), "前置：青金石真的扣了 1");
                Assert.That(calls, Is.EqualTo(1), "附魔扣费成功 → 光柱事件恰好一次");
                Assert.That(CountKind(pool, ParticlePool.FxKind.EnchantColumn), Is.EqualTo(1),
                    "Enchanted → 池出一根附魔光柱（端到端，锚在池宿主位置）");
            }
            finally
            {
                Object.DestroyImmediate(go);
                DestroySubscribedPool(pool);
            }
        }
    }
}
#endif
