#if UNITY_EDITOR
// m11 W3-5：和平模式三件套守卫——
//   1) 开关持久化（PlayerPrefs 键 PeacefulMode，默认关；设置面板 Awake 读回）
//   2) TickSpawn 门控（开 = 敌对 kind 全不刷，被动照刷；关 = 既有行为不回退）
//   3) 死亡保留背包（死亡 → 复活全链不清背包 / 不产生掉落物；同时钉住现状：
//      普通模式同样保留——m7 spec 非目标「死了不掉东西，保持宽容」，
//      未来实现普通模式死亡掉落时必须先判 PeaceMode.Enabled 绕过）
// 整个文件用 #if UNITY_EDITOR 包裹：dotnet 链不编译 UnityEngine / Unity 侧类型，
// 与 MobManagerSpawnTests / SettingsPanelUiTests 同款约定。
using System.IO;
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Gameplay
{
    [TestFixture]
    public class PeaceModeTests
    {
        [SetUp]
        public void SetUp() => ResetPeaceKey();

        [TearDown]
        public void TearDown() => ResetPeaceKey();

        /// <summary>清键 + 失效静态缓存：每个用例从「默认关」起步，跑完不污染其它夹具
        /// （PeaceMode 缓存是静态的，只清 PlayerPrefs 不清缓存会让下一夹具读到脏值）。</summary>
        private static void ResetPeaceKey()
        {
            PlayerPrefs.DeleteKey(PeaceMode.Key);
            PeaceMode.ResetCache();
        }

        // ─── 1) 开关持久化 ────────────────────────────────────────────────────

        [Test]
        public void 开关_未存键默认关_SetEnabled往返与落盘一致()
        {
            Assert.That(PeaceMode.Enabled, Is.False, "未存键时和平模式应默认关（孩子档不该被吓到）");

            PeaceMode.SetEnabled(true);
            Assert.That(PeaceMode.Enabled, Is.True, "SetEnabled(true) 后缓存应立即为开（无需重读）");
            Assert.That(PlayerPrefs.GetInt(PeaceMode.Key, 0), Is.EqualTo(1),
                "键名应为 PeacefulMode 且落盘 1（键名是任务卡指定的全局硬约束）");

            // 缓存失效后必须重读 PlayerPrefs 仍是开——验证真的落了盘，不是只写了内存
            PeaceMode.ResetCache();
            Assert.That(PeaceMode.Enabled, Is.True, "缓存失效后重读 PlayerPrefs 应仍是开");
        }

        /// <summary>EditMode 下 AddComponent 不会自动触发 MonoBehaviour.Awake，
        /// 用反射显式调用（与 SettingsPanelUiTests.InvokeAwake 同款）。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        [Test]
        public void 设置面板_Awake_从PlayerPrefs读回和平模式开关()
        {
            PeaceMode.SetEnabled(true);
            var go = new GameObject("和平模式设置面板");
            try
            {
                var panel = go.AddComponent<SettingsPanelUi>();
                InvokeAwake(panel);
                Assert.That(panel.CurrentPeacefulMode, Is.True,
                    "面板 Awake 应经 PeaceMode.Enabled 读回开关（已落盘开）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ─── 2) TickSpawn 门控 ────────────────────────────────────────────────

        private GameObject _host;
        private GameObject _player;
        private MobManager _mgr;

        /// <summary>建一个走真实 spawn_rules.json 的 MobManager（数据驱动路径，
        /// 与 MobManagerSpawnTests 同款装配：world/generator 置空退 Plains + 表面 y=70）。</summary>
        private void NewSpawnManager()
        {
            var rules = MobSpawnRules.Load(Path.Combine(
                Application.streamingAssetsPath, "mobs", "spawn_rules.json"));
            _host = new GameObject("和平模式刷怪宿主");
            _player = new GameObject("和平模式刷怪玩家");
            _player.transform.position = new Vector3(0.5f, 71f, 0.5f);
            _mgr = _host.AddComponent<MobManager>();
            _mgr.Bind(world: null, time: null, player: _player.transform,
                generator: null, rules: rules);
        }

        /// <summary>清掉刷怪测试的 GameObject（含 SpawnMob 建出的 Mob_* 视图，防泄漏）。</summary>
        private void CleanupSpawn()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_player != null) Object.DestroyImmediate(_player);
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name.StartsWith("Mob_")) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TickSpawn_和平模式开_夜间敌对全不刷()
        {
            PeaceMode.SetEnabled(true);
            NewSpawnManager();
            try
            {
                for (int seed = 1; seed <= 100; seed++)
                {
                    _mgr.TickSpawn(seed, dayNightPhase: 0.7f); // 深夜：候选组全是敌对
                }
                Assert.That(_mgr.ActiveMobs.Count, Is.EqualTo(0),
                    "和平模式开时夜间 100 次刷怪尝试不应出现任何敌对 mob（门只挡新增）");
            }
            finally
            {
                CleanupSpawn();
            }
        }

        [Test]
        public void TickSpawn_和平模式开_白天被动照刷()
        {
            PeaceMode.SetEnabled(true);
            NewSpawnManager();
            try
            {
                int hits = 0;
                for (int seed = 1; seed <= 100; seed++)
                {
                    int before = _mgr.ActiveMobs.Count;
                    _mgr.TickSpawn(seed, dayNightPhase: 0.25f); // 正午：候选组全是被动
                    if (_mgr.ActiveMobs.Count > before)
                    {
                        hits++;
                        var spawned = _mgr.ActiveMobs[_mgr.ActiveMobs.Count - 1];
                        Assert.That(spawned.Kind,
                            Is.Not.EqualTo(MobKind.Zombie).And.Not.EqualTo(MobKind.Skeleton)
                                .And.Not.EqualTo(MobKind.Spider).And.Not.EqualTo(MobKind.Creeper),
                            "和平模式不该误伤被动生物组（白天候选本就不含敌对）");
                    }
                }
                Assert.That(hits, Is.GreaterThan(0),
                    $"和平模式下白天被动生物应照常刷新（实际 {hits}/100）——门只挡敌对");
            }
            finally
            {
                CleanupSpawn();
            }
        }

        [Test]
        public void TickSpawn_和平模式关_夜间敌对照常刷新()
        {
            // 门的反向对照：开关关时门必须放行，不能把普通难度也变成「永夜无怪」
            PeaceMode.SetEnabled(false);
            NewSpawnManager();
            try
            {
                int hits = 0;
                for (int seed = 1; seed <= 100; seed++)
                {
                    int before = _mgr.ActiveMobs.Count;
                    _mgr.TickSpawn(seed, dayNightPhase: 0.7f);
                    if (_mgr.ActiveMobs.Count > before)
                    {
                        hits++;
                        var spawned = _mgr.ActiveMobs[_mgr.ActiveMobs.Count - 1];
                        Assert.That(spawned.Kind,
                            Is.EqualTo(MobKind.Zombie).Or.EqualTo(MobKind.Skeleton)
                                .Or.EqualTo(MobKind.Spider).Or.EqualTo(MobKind.Creeper),
                            "关掉开关后夜间刷出的应回到四敌对之一（既有行为不回退）");
                    }
                }
                Assert.That(hits, Is.GreaterThan(0),
                    $"和平模式关时夜间敌对应照常刷新（实际 {hits}/100）");
            }
            finally
            {
                CleanupSpawn();
            }
        }

        // ─── 3) 死亡保留背包 ─────────────────────────────────────────────────

        /// <summary>装配「玩家 + 上下文」并塞两件背包物品 + 一件穿戴（EditMode 下
        /// AddComponent 不触发 Awake，字段显式初始化，与 RespawnSafetyTests 同款）。</summary>
        private PlayerController NewPlayerWithContext(out PlayerContext ctx)
        {
            var go = new GameObject("和平模式死亡玩家");
            ctx = go.AddComponent<PlayerContext>();
            ctx.Inventory = new PlayerInventory();
            ctx.HungerSystem = new HungerSystem();
            ctx.Health = new Health(20f);
            var player = go.AddComponent<PlayerController>();
            player.Bind(new World(), null, new Float3(10f, 70f, -5f));

            // 背包两格：猪排 1008 ×3、任意物品 1024 ×5；穿戴栏头槽一件——
            // 死亡若被清空 / 倒成掉落物，这三处任一都会被抓到
            ctx.Inventory.TryAdd(new ItemStack(1008, 3), out _);
            ctx.Inventory.TryAdd(new ItemStack(1024, 5), out _);
            ctx.ArmorSlots.SetSlotRaw(0, new ItemStack(2000, 1));
            return player;
        }

        [Test]
        public void 死亡_和平模式开_复活后背包与穿戴原样保留且无掉落物()
        {
            PeaceMode.SetEnabled(true);
            var player = NewPlayerWithContext(out var ctx);
            try
            {
                player.TakeDamage(999, null);
                Assert.That(ctx.Health.IsDead, Is.True, "前置：玩家应已死亡");
                player.RespawnAtSpawn(); // 死亡画面的复活终点（TriggerRespawn 同款调用）

                Assert.That(ctx.Inventory.CountOf(1008), Is.EqualTo(3),
                    "和平模式死亡复活后猪排应原样在包（不掉落不清空）");
                Assert.That(ctx.Inventory.CountOf(1024), Is.EqualTo(5),
                    "和平模式死亡复活后第二格物品应原样在包");
                Assert.That(ctx.ArmorSlots.GetSlot(0).ItemId, Is.EqualTo(2000),
                    "和平模式死亡复活后穿戴栏应原样保留");
                Assert.That(ctx.ItemDrops.Count, Is.EqualTo(0),
                    "和平模式死亡不应把背包倒成地面掉落物");
            }
            finally
            {
                Object.DestroyImmediate(ctx.gameObject);
            }
        }

        [Test]
        public void 死亡_和平模式关_背包同样保留_现状宽容设计()
        {
            // 钉住现状（不是和平模式的断言）：普通模式死亡同样不清背包——
            // m7 spec 把「死亡掉落物品」列为非目标（保持宽容）。这条用例守住
            // 「未来实现普通模式死亡掉落」必须是一次有意识的显式改动，且届时
            // 必须给 PeaceMode.Enabled 留绕过分支（见 PlayerController.TakeDamage 契约注释）。
            PeaceMode.SetEnabled(false);
            var player = NewPlayerWithContext(out var ctx);
            try
            {
                player.TakeDamage(999, null);
                Assert.That(ctx.Health.IsDead, Is.True, "前置：玩家应已死亡");
                player.RespawnAtSpawn();

                Assert.That(ctx.Inventory.CountOf(1008), Is.EqualTo(3),
                    "现状：普通模式死亡也不清背包（m7 宽容设计，动了这条请同步改 spec）");
                Assert.That(ctx.ItemDrops.Count, Is.EqualTo(0),
                    "现状：普通模式死亡也不产生掉落物");
            }
            finally
            {
                Object.DestroyImmediate(ctx.gameObject);
            }
        }
    }
}
#endif
