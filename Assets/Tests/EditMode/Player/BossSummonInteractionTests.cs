#if UNITY_EDITOR
// m11 W3-3：机元图腾 → BlockInteraction.UseAt 召唤路由（EditMode 直调 public 入口，
// BlockInteractionUseRoutingTests 同款做法——EditMode 驱动不了 Input.GetMouseButtonDown）。
// 检测条件的纯逻辑面（2×2/y 门槛/键）在 Entities/MachineGuardianTests（Core 双链）；
// 这里守 Unity 接线：MobManager 生成、Boss 生成位置、防重复、文案常量。
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class BossSummonInteractionTests
    {
        private GameObject _host;
        private GameObject _mgrHost;
        private PlayerContext _ctx;
        private BlockInteraction _block;
        private MobManager _mgr;
        private World _world;
        private BlockRegistry _registry;

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
                // 机元矿石（图腾原料）；numericId 与 BlockIds.MachineEssenceOre=1012 手动一致
                @"{ ""id"": ""machine_essence_ore"", ""numericId"": 1012, ""textures"": { ""all"": ""machine-essence-ore"" } }",
            };
            return BlockRegistry.FromJson(docs);
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("BossSummonCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new MyWorld.Core.Player.PlayerInventory();
            _ctx.Items = MyWorld.Core.Items.ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""stone"", ""numericId"": 1 }",
            });
            _ctx.Time = new MyWorld.Core.Time.TimeOfDay(); // 正午 → IsNight = false

            _world = new World();
            _registry = BuildRegistry();

            // 玩家绑到远处——放置判定的 IntersectsPlayer 不会干扰，MobManager despawn 也不波及
            var player = _host.AddComponent<PlayerController>();
            player.Bind(_world, _registry, new Float3(50f, 80f, 50f));

            _block = _host.AddComponent<BlockInteraction>();
            _block.Bind(_world, _registry, null, _host.transform); // views=null：MarkBlockChanged 容错

            _mgrHost = new GameObject("BossSummonMobManager");
            var playerMarker = new GameObject("BossSummonPlayerMarker");
            playerMarker.transform.position = new Vector3(50f, 80f, 50f);
            _mgr = _mgrHost.AddComponent<MobManager>();
            _mgr.Bind(world: _world, time: null, player: playerMarker.transform,
                generator: null, rules: null);

            // 已用图腾是全局静态单例（EnchantStore.Default 同款取舍）——测试间必须清
            BossSummonState.Default.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            BossSummonState.Default.Clear();
            if (_mgrHost != null) Object.DestroyImmediate(_mgrHost);
            if (_host != null) Object.DestroyImmediate(_host);
        }

        /// <summary>在世界里摆一个 2×2 机元矿石图腾，锚点 (ax, y, az)。</summary>
        private void PlaceTotem(int ax, int y, int az)
        {
            for (int dx = 0; dx < 2; dx++)
            {
                for (int dz = 0; dz < 2; dz++)
                {
                    _world.SetBlock(ax + dx, y, az + dz, BlockIds.MachineEssenceOre);
                }
            }
        }

        /// <summary>从 (ax+0.5, y+4.5, az+0.5) 垂直下探——走真实 VoxelRaycaster 路径拿命中。</summary>
        private MyWorld.Core.Physics.VoxelRayHit CastDownAt(int ax, int y, int az)
        {
            var source = new InteractionRaySource(_world, _registry);
            return MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new Float3(ax + 0.5f, y + 4.5f, az + 0.5f), new Float3(0f, -1f, 0f), 10f);
        }

        private int BossCount
        {
            get
            {
                int n = 0;
                foreach (var m in _mgr.ActiveMobs)
                {
                    if (m.Kind == MobKind.MachineGuardian) n++;
                }
                return n;
            }
        }

        [Test]
        public void UseAt_2x2机元图腾_召唤Boss于图腾上方_矿石不消耗()
        {
            PlaceTotem(8, 10, 8);
            var hit = CastDownAt(8, 10, 8);
            Assert.That(hit.Hit, Is.True, "前置条件：射线应命中图腾矿石");

            _block.UseAt(hit);

            Assert.That(BossCount, Is.EqualTo(1), "应召唤出一只机元守卫");
            var boss = _mgr.ActiveMobs[_mgr.ActiveMobs.Count - 1];
            Assert.That(boss.Kind, Is.EqualTo(MobKind.MachineGuardian), "生成的是 Boss kind");
            Assert.That(boss.Health.Max, Is.EqualTo(60f), "Boss 建档血 60（生成链走 Mob.Create 27）");
            Assert.That(boss.Position.X, Is.EqualTo(9f), "Boss 落点 x = 图腾中心（锚 8 +1）");
            Assert.That(boss.Position.Y, Is.EqualTo(11f), "Boss 脚底 = 图腾顶面上方一格（10+1）");
            Assert.That(boss.Position.Z, Is.EqualTo(9f), "Boss 落点 z = 图腾中心");

            // 消耗无：四块矿石原样留着（挖掉任一块即自然拆除图腾）
            for (int dx = 0; dx < 2; dx++)
            {
                for (int dz = 0; dz < 2; dz++)
                {
                    Assert.That(_world.GetBlock(8 + dx, 10, 8 + dz), Is.EqualTo(BlockIds.MachineEssenceOre),
                        $"({8 + dx},10,{8 + dz}) 图腾矿石不该被消耗");
                }
            }

            // 已用登记进全局单例（存档往返的运行时真源）
            Assert.That(BossSummonState.Default.IsUsed("8,10,8"), Is.True,
                "图腾键（锚点最小角）应登记已用");
        }

        [Test]
        public void UseAt_同一图腾再右键_不召唤第二只()
        {
            PlaceTotem(8, 10, 8);
            _block.UseAt(CastDownAt(8, 10, 8));
            Assert.That(BossCount, Is.EqualTo(1), "前置条件：第一只已召唤");

            _block.UseAt(CastDownAt(8, 10, 8)); // 命中同图腾另一角也识别为同一图腾
            _block.UseAt(CastDownAt(9, 10, 9));

            Assert.That(BossCount, Is.EqualTo(1), "同一图腾只出一只 Boss（防重复）");
        }

        [Test]
        public void UseAt_缺角不成图腾_不召唤()
        {
            PlaceTotem(8, 10, 8);
            _world.SetBlock(9, 10, 9, BlockIds.Air); // 拆掉一角
            _block.UseAt(CastDownAt(8, 10, 8));
            Assert.That(BossCount, Is.EqualTo(0), "四角缺一不成图腾，不召唤");
        }

        [Test]
        public void UseAt_表层图腾y超16_不召唤()
        {
            PlaceTotem(8, 70, 8); // y=70 ≥ 16：机元矿生成层之外
            _block.UseAt(CastDownAt(8, 70, 8));
            Assert.That(BossCount, Is.EqualTo(0), "y≥16 的图腾无效（Boss 只认深层机元矿）");
        }

        [Test]
        public void UseAt_单块矿石_不召唤_右键落到放置路由()
        {
            _world.SetBlock(8, 10, 8, BlockIds.MachineEssenceOre); // 孤零零一块
            _block.UseAt(CastDownAt(8, 10, 8));
            Assert.That(BossCount, Is.EqualTo(0), "单块矿石不是图腾");
        }

        [Test]
        public void 守卫不进昼夜候选数组_自然刷怪永不产生Boss()
        {
            // 「不自然刷」的第二道闸（第一道在 Core：spawn_rules 无条目 ShouldSpawn 恒 false）：
            // MobManager 的昼夜候选表也不含 Boss——两条都守住，Boss 的唯一来源就是图腾召唤
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            foreach (string name in new[] { "DayCandidates", "NightCandidates" })
            {
                var field = typeof(MobManager).GetField(name, flags);
                Assert.That(field, Is.Not.Null, "MobManager 应有 " + name + " 候选表");
                var candidates = (MobKind[])field.GetValue(null);
                Assert.That(candidates, Does.Not.Contain(MobKind.MachineGuardian),
                    name + " 不该含 Boss——只经图腾召唤，不进自然刷怪候选");
            }
        }

        [Test]
        public void 文案常量_召唤与已用提示锁定()
        {
            // OnGUI 本身 EditMode 不跑，锁常量防误改（与床/无箭提示同款守卫）
            Assert.That(BlockInteraction.BossSummonedHintText, Is.EqualTo("机元守卫苏醒了！"));
            Assert.That(BlockInteraction.BossTotemUsedHintText, Is.EqualTo("图腾已沉寂"));
        }
    }
}
#endif
