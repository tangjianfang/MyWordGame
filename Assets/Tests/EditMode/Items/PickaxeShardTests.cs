#if UNITY_EDITOR
// m10 B2：镐碎裂——碎块散落 + 碰到玩家 0.5 伤害 + 2 秒消失 + 不可拾取
// （孩子的原创机制：「碎掉的镐子碰到会受伤」）。碎块是独立轻实体 PickaxeShard，
// **不进 PlayerContext.ItemDrops**——本文件锁死它与掉落物的结构性区分、
// 接触伤害语义（fix1 起同一次碎裂的所有碎块共享一次 0.5 伤害）、
// 以及时间注入的 2s 寿命边界。
// 依赖 Unity MonoBehaviour，#if UNITY_EDITOR 包裹只跑 EditMode 链（dotnet 链不编译）。
using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Items;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class PickaxeShardTests
    {
        private GameObject _host;
        private Transform _shardRoot;
        private PlayerContext _ctx;
        private PlayerController _player;
        private BlockInteraction _block;
        private World _world;

        private const int BrittlePickaxeItemId = 1490;

        /// <summary>EditMode 下 AddComponent 不会跑 Awake，用反射补一脚（BlockDigDurabilityTests 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        private static MyWorld.Core.Blocks.BlockRegistry BuildRegistry()
        {
            return MyWorld.Core.Blocks.BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
            });
        }

        private static ItemDatabase BuildItems()
        {
            return ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""cobblestone"", ""numericId"": 1003, ""texture"": ""cobblestone"" }",
                // 1.0 版「脆镐」：耐久 1，一挖即碎——专测耐久尽分支
                @"{ ""id"": ""brittle_pickaxe"", ""numericId"": 1490, ""texture"": ""wooden_pickaxe"",
                    ""isTool"": true, ""miningLevel"": 1, ""toolTier"": 1, ""maxDurability"": 1 }",
            });
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("PickaxeShardCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Items = BuildItems();
            _ctx.Health = new Health(20f);

            // PlayerController 与 BlockInteraction 必须同宿主：Bind 里
            // GetComponent<PlayerController>() 取不到就没人给碎块当伤害对象
            _player = _host.AddComponent<PlayerController>();
            _host.transform.position = new Vector3(8.5f, 70f, 8.5f);

            _shardRoot = new GameObject("碎块根").transform;

            _world = new World();
            _block = _host.AddComponent<BlockInteraction>();
            _block.Bind(_world, BuildRegistry(), null, _host.transform);
            // 不注入 BlockDrops：碎块在掉落 spawn 之前独立发生，缺表不应影响本 fixture 的断言
        }

        [TearDown]
        public void TearDown()
        {
            // 生产路径的碎块挂场景根（世界空间独立，不跟玩家走），不随 _host 销毁——
            // 显式清干净，防止泄漏到下一个 fixture 的 FindObjectsOfType
            ClearShards();
            if (_shardRoot != null) Object.DestroyImmediate(_shardRoot.gameObject);
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>脆镐（maxDurability=1）在指定格挖一块石头 → 一挖即碎。
        /// 返回**本次新碎出**的碎块（场上可能还留着上次碎裂未过期的旧块，diff 掉）。</summary>
        private PickaxeShard[] BreakBrittlePickaxeAt(int x, int z)
        {
            var existing = new HashSet<PickaxeShard>(Object.FindObjectsOfType<PickaxeShard>());
            _ctx.Inventory.SetSlot(0, new ItemStack(BrittlePickaxeItemId, 1));
            _ctx.Inventory.SelectedHotbarIndex = 0;
            _world.SetBlock(x, 70, z, BlockIds.Stone);
            _block.BreakAt(x, 70, z);
            var fresh = new List<PickaxeShard>();
            foreach (var shard in Object.FindObjectsOfType<PickaxeShard>())
            {
                if (!existing.Contains(shard)) fresh.Add(shard);
            }
            return fresh.ToArray();
        }

        /// <summary>在 (8,70,8) 碎裂（多数测试的默认坐标）。</summary>
        private PickaxeShard[] BreakBrittlePickaxe() => BreakBrittlePickaxeAt(8, 8);

        private static void ClearShards()
        {
            foreach (var shard in Object.FindObjectsOfType<PickaxeShard>())
            {
                Object.DestroyImmediate(shard.gameObject);
            }
        }

        /// <summary>在玩家脚边 0.3m 处放一个静止碎块（已落地、零速度）——接触判定的最小单元。</summary>
        private PickaxeShard SpawnRestingShardNearFeet()
        {
            return PickaxeShard.Create(_shardRoot, new Float3(8.8f, 70.3f, 8.5f), 70.3f,
                new Float3(0f, 0f, 0f), Color.gray, _player, 0f);
        }

        // ─── 1) 碎裂触发：数量 / 背包 / 提示 ─────────────────────────────────

        [Test]
        public void 耐久尽_散出四到六个碎块_同坐标数量可复现()
        {
            var first = BreakBrittlePickaxe();
            Assert.That(first.Length, Is.InRange(PickaxeShard.CountMin, PickaxeShard.CountMax),
                "碎块数应在 4-6 区间（spec §2，整数哈希掷点）");

            ClearShards();
            var second = BreakBrittlePickaxe();
            Assert.That(second.Length, Is.EqualTo(first.Length),
                "同物品 + 同方块坐标两次碎裂，碎块数应一致（确定性哈希，跨机器可复现）");
        }

        [Test]
        public void 碎裂时_镐已从背包移除_提示文案为镐碎了()
        {
            BreakBrittlePickaxe();

            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.True,
                "碎裂 = 镐从选中槽消失（B1 语义不回退）");
            Assert.That(_block.ToolBreakHintCount, Is.EqualTo(1), "碎裂应触发一次性提示");
            Assert.That(BlockInteraction.ToolBreakHintText, Is.EqualTo("镐碎了！"),
                "提示文案（m10 B2 起「坏掉」升级为「碎」——真的有碎块了）");
        }

        // ─── 2) 与掉落物的结构性区分：不可拾取 ───────────────────────────────

        [Test]
        public void 碎块不进掉落物列表_拾取路径收不到()
        {
            var shards = BreakBrittlePickaxe();
            Assert.That(shards.Length, Is.GreaterThan(0), "前置：碎裂应产生碎块");
            Assert.That(_ctx.ItemDrops.Count, Is.EqualTo(0),
                "碎块不是掉落物：不进 PlayerContext.ItemDrops（独立轻实体）");

            // 把玩家整个挪进碎块堆再跑拾取（大 dt 一步到位的吸附语义）：一个都捡不起来
            _host.transform.position = shards[0].transform.position;
            int picked = _player.PickupNearbyDrops(10f);

            Assert.That(picked, Is.EqualTo(0), "碎块不可拾取");
            Assert.That(_ctx.Inventory.GetSlot(0).IsEmpty, Is.True, "刚碎掉的镐不该被「捡」回来");
        }

        // ─── 3) 接触伤害：0.5 / 每块一次 / 距离 / 无敌帧 ─────────────────────

        [Test]
        public void 碎块碰到玩家_恰好扣零点五血()
        {
            var shard = SpawnRestingShardNearFeet();
            Assert.That(_ctx.Health.Current, Is.EqualTo(20f).Within(1e-4f), "前置：满血");

            shard.Tick(_host.transform.position, 0.1f, 0.1f);

            Assert.That(_ctx.Health.Current, Is.EqualTo(19.5f).Within(1e-4f),
                "0.5 伤害（走 TakeDamage 小数重载，Health 本就是 float）");
            Assert.That(shard.HasStung, Is.True, "接触后应标记已扎");
        }

        [Test]
        public void 同一碎块持续接触_只扎一次()
        {
            var shard = SpawnRestingShardNearFeet();

            shard.Tick(_host.transform.position, 0.1f, 0.1f); // 第一扎
            shard.Tick(_host.transform.position, 0.2f, 0.1f); // 仍贴着
            shard.Tick(_host.transform.position, 0.3f, 0.1f);

            Assert.That(_ctx.Health.Current, Is.EqualTo(19.5f).Within(1e-4f),
                "站着不动也不该被同一块反复扎——每块最多一次");
        }

        [Test]
        public void 玩家远离碎块_不扣血()
        {
            // 3m 外的静止碎块（接触半径 0.6m）
            var shard = PickaxeShard.Create(_shardRoot, new Float3(11.5f, 70.3f, 8.5f), 70.3f,
                new Float3(0f, 0f, 0f), Color.gray, _player, 0f);

            shard.Tick(_host.transform.position, 0.5f, 0.1f);

            Assert.That(_ctx.Health.Current, Is.EqualTo(20f).Within(1e-4f), "3m 外的碎块扎不到");
        }

        [Test]
        public void 复活无敌帧内_碎块伤害被忽略()
        {
            var shard = SpawnRestingShardNearFeet();
            _player.InvincibleUntil = 100f; // EditMode 下 Time.time 恒 0 < 100 → 无敌

            shard.Tick(_host.transform.position, 0.1f, 0.1f);

            Assert.That(_ctx.Health.Current, Is.EqualTo(20f).Within(1e-4f),
                "碎块伤害走 TakeDamage，复活无敌帧语义自动生效");
        }

        // ─── 4) 寿命：2 秒消失（时间注入） ───────────────────────────────────

        [Test]
        public void 碎块两秒后消失_边界含等号()
        {
            var shard = SpawnRestingShardNearFeet();

            shard.Tick(_host.transform.position, 1.99f, 0.01f);
            Assert.That(shard == null, Is.False, "2 秒内碎块应还在");

            shard.Tick(_host.transform.position, 2.0f, 0.01f);
            Assert.That(shard == null, Is.True, "满 2 秒碎块应自毁（边界含等号）");
        }

        [Test]
        public void 碎块出生在胸口_落地前不扎脚()
        {
            var shards = BreakBrittlePickaxe();
            Assert.That(shards.Length, Is.GreaterThan(0), "前置：碎裂应产生碎块");

            // 第一拍（0.1s）：碎块还在胸口高度（>0.6m 接触半径），扎不到脚
            foreach (var shard in shards)
            {
                shard.Tick(_host.transform.position, 0.1f, 0.1f);
            }
            Assert.That(_ctx.Health.Current, Is.EqualTo(20f).Within(1e-4f),
                "出生在胸口、落地前不扎脚——给玩家一个「碎裂了快躲开」的反应窗口");

            // 大步推进到落地（累计 1.0s < 2s 寿命，玩家仍站原地——落点散在周边 0.5m 圈内）
            foreach (var shard in shards)
            {
                shard.Tick(_host.transform.position, 1.0f, 0.9f);
            }

            // 玩家踩到某块落定碎块的正上方（距离=0）再步一小帧 → 必扎；
            // fix1 起同组共享一次伤害——无论踩进几块，总共恰好 0.5
            _host.transform.position = shards[0].transform.position;
            foreach (var shard in shards)
            {
                shard.Tick(_host.transform.position, 1.1f, 0.1f);
            }
            Assert.That(_ctx.Health.Current, Is.EqualTo(19.5f).Within(1e-4f),
                "踩进碎块堆总共恰好 0.5（fix1：一次碎裂共享一次伤害）");
        }

        [Test]
        public void 一次碎裂站定不动_整组共享只扣零点五血()
        {
            var shards = BreakBrittlePickaxe();

            // 站定不动步进到全部落定（10 × 0.1s = 1.0s < 2s 寿命）——fix1 前这里会被
            // 落进接触半径的多块连环扣血（典型 1.0-1.5，最坏 3.0）
            for (int step = 1; step <= 10; step++)
            {
                foreach (var shard in shards)
                {
                    shard.Tick(_host.transform.position, step * 0.1f, 0.1f);
                }
            }
            Assert.That(_ctx.Health.Current, Is.GreaterThanOrEqualTo(19.5f),
                "站定最多掉 0.5 血（fix1：同一次碎裂的所有碎块共享一次伤害）");

            // 人为踩上任一块落定碎块补一脚：已扎过则不再扣、没扎过则恰好补 0.5——
            // 无论掷点落位如何，一次碎裂的伤害总量恰好 0.5
            _host.transform.position = shards[0].transform.position;
            foreach (var shard in shards)
            {
                shard.Tick(_host.transform.position, 1.1f, 0.1f);
            }
            Assert.That(_ctx.Health.Current, Is.EqualTo(19.5f).Within(1e-4f),
                "一次碎裂无论站定还是踩上去，总共恰好 0.5 伤害（孩子的设定是「碰到受伤」不是爆炸）");
        }

        [Test]
        public void 两次碎裂_各组各扎一次共一血()
        {
            // 第一组 @ (8,70,8)：落定 + 踩上 → 19.5
            var first = BreakBrittlePickaxeAt(8, 8);
            for (int step = 1; step <= 8; step++)
            {
                foreach (var shard in first)
                {
                    shard.Tick(_host.transform.position, step * 0.1f, 0.1f);
                }
            }
            _host.transform.position = first[0].transform.position;
            foreach (var shard in first)
            {
                shard.Tick(_host.transform.position, 0.9f, 0.1f);
            }
            Assert.That(_ctx.Health.Current, Is.EqualTo(19.5f).Within(1e-4f), "第一组：恰好 0.5");

            // 第二次碎裂在相邻格（掷点 seed 不同）：新组，不与第一组共享
            var second = BreakBrittlePickaxeAt(9, 8);
            Assert.That(second.Length, Is.InRange(PickaxeShard.CountMin, PickaxeShard.CountMax),
                "前置：第二组碎块已生成");

            // 落定步进（t = 1.1..1.8，全程 < 2s 旧碎块不自毁）；第一组的旧块一并步进——
            // 它们已扎过，玩家就站在上面也不得再扣血（回归守卫）
            for (int step = 1; step <= 8; step++)
            {
                float t = 1.0f + step * 0.1f;
                foreach (var shard in first)
                {
                    shard.Tick(_host.transform.position, t, 0.1f);
                }
                foreach (var shard in second)
                {
                    shard.Tick(_host.transform.position, t, 0.1f);
                }
            }
            _host.transform.position = second[0].transform.position;
            foreach (var shard in second)
            {
                shard.Tick(_host.transform.position, 1.9f, 0.1f);
            }
            Assert.That(_ctx.Health.Current, Is.EqualTo(19.0f).Within(1e-4f),
                "两次碎裂各扎一次：19.5 - 0.5 = 19.0（组间不共享、组内共享）");
        }

        // ─── 5) TakeDamage 重构守卫：int 重载行为不变 ────────────────────────

        [Test]
        public void 整数伤害入口_经小数重载行为不变()
        {
            _player.TakeDamage(3, null);
            Assert.That(_ctx.Health.Current, Is.EqualTo(17f).Within(1e-4f),
                "int 重载委托到 float 后，既有摔落/饥饿伤害路径不受影响");
        }
    }
}
#endif
