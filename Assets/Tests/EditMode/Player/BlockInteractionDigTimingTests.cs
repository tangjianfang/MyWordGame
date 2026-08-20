#if UNITY_EDITOR
// m12 P0-a：挖掘蓄力接线测试（EditMode）——EditMode 驱动不了 Input.GetMouseButton，
// 直调 BlockInteraction.TryTickDig 喂显式 dt（与 UseAt / BreakAt 同款做法）。
// 钉死孩子实机反馈的根因 1 修复：「一次点击瞬挖」→ 按住蓄力满 BreakTime 才破坏。
using System.Reflection;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Player
{
    [TestFixture]
    public class BlockInteractionDigTimingTests
    {
        private GameObject _host;
        private PlayerContext _ctx;
        private BlockInteraction _block;
        private World _world;
        private PlayerController _player;

        private const ushort StoneId = BlockIds.Stone;

        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("DigTimingCtx");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx);
            _ctx.Inventory = new PlayerInventory();
            _ctx.Items = ItemDatabase.FromJson(new[]
            {
                @"{ ""id"": ""plank"", ""numericId"": 1000 }",
            });
            _ctx.Time = new TimeOfDay();

            _world = new World();
            var registry = BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
            });

            var player = _host.AddComponent<PlayerController>();
            _player = player;
            player.Bind(_world, registry, new Float3(50f, 80f, 50f));
            InvokeAwake(player);

            _block = _host.AddComponent<BlockInteraction>();
            _block.Bind(_world, registry, null, _host.transform);
            // 注意：SetGenerator 不调——群系回落 Plains（×1），石头徒手 = 4s 整，断言可控
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
        }

        [Test]
        public void HoldToBreakTime_Stone_Hand_4s_NotBefore()
        {
            _world.SetBlock(8, 70, 8, StoneId);
            var source = new InteractionRaySource(_world, BuildRegistryForRay());
            var hit = MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new Float3(8.5f, 74.5f, 8.5f), new Float3(0f, -1f, 0f), 10f);
            Assert.That(hit.Hit, Is.True, "应命中石头柱");
            var hitPoint = new Float3(8.5f, 74.5f, 8.5f) + new Float3(0f, -1f, 0f) * hit.Distance;

            // 空手（tier 0）挖石：Plains 回落 ×1 → BreakSeconds = 1s ×4 = 4s
            for (int i = 0; i < 39; i++)
            {
                bool broke = _block.TryTickDig(hit, hitPoint, 0.1f);
                Assert.That(broke, Is.False, $"第 {(i + 1) * 0.1f:F1}s 不应破坏（徒手挖石 4s）");
            }

            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(StoneId), "3.9s 时石头还在");

            // 末拍给 0.2s（合计 4.1s）——0.1×40 的浮点累加可能落在 0.9999…，
            // 语义边界「≥4s 可破坏」用越过一拍来钉，避免跟浮点尾数较劲
            bool finalTick = _block.TryTickDig(hit, hitPoint, 0.2f);
            Assert.That(finalTick, Is.True, "蓄力越过 4s 满格");
            _block.BreakAt(8, 70, 8);
            Assert.That(_world.GetBlock(8, 70, 8), Is.EqualTo(BlockIds.Air), "满格后破坏生效");
        }

        [Test]
        public void Release_MidCharge_ClearsProgress()
        {
            _world.SetBlock(8, 70, 8, StoneId);
            var source = new InteractionRaySource(_world, BuildRegistryForRay());
            var hit = MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new Float3(8.5f, 74.5f, 8.5f), new Float3(0f, -1f, 0f), 10f);
            var hitPoint = new Float3(8.5f, 74.5f, 8.5f) + new Float3(0f, -1f, 0f) * hit.Distance;

            for (int i = 0; i < 20; i++)
            {
                _block.TryTickDig(hit, hitPoint, 0.1f); // 攒到 2.0s（半程）
            }

            // 松手（准星移开）→ 清零
            var miss = new MyWorld.Core.Physics.VoxelRayHit { Hit = false };
            Assert.That(_block.TryTickDig(miss, hitPoint, 0.1f), Is.False);

            // 重新按住：必须重新攒满 4s——若进度残留，3.9-2.0=1.9s 后就会假破坏
            for (int i = 0; i < 39; i++)
            {
                Assert.That(_block.TryTickDig(hit, hitPoint, 0.1f), Is.False,
                    "松手后必须从零重蓄：3.9s 内不应破坏");
            }
            Assert.That(_block.TryTickDig(hit, hitPoint, 0.2f), Is.True, "重新攒满越过 4s 破坏");
        }

        [Test]
        public void TargetSwitch_ClearsProgress()
        {
            _world.SetBlock(8, 70, 8, StoneId);
            _world.SetBlock(2, 70, 8, StoneId);
            var source = new InteractionRaySource(_world, BuildRegistryForRay());
            var hitA = MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new Float3(8.5f, 74.5f, 8.5f), new Float3(0f, -1f, 0f), 10f);
            var hitB = MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new Float3(2.5f, 74.5f, 8.5f), new Float3(0f, -1f, 0f), 10f);
            var pointA = new Float3(8.5f, 74.5f, 8.5f) + new Float3(0f, -1f, 0f) * hitA.Distance;
            var pointB = new Float3(2.5f, 74.5f, 8.5f) + new Float3(0f, -1f, 0f) * hitB.Distance;

            for (int i = 0; i < 20; i++)
            {
                _block.TryTickDig(hitA, pointA, 0.1f); // A 攒到 2.0s
            }

            // 切到 B（距离远超容差）→ A 的进度作废
            _block.TryTickDig(hitB, pointB, 0.1f);
            for (int i = 0; i < 38; i++)
            {
                Assert.That(_block.TryTickDig(hitB, pointB, 0.1f), Is.False,
                    "换目标后从零重蓄：B 不应继承 A 的进度");
            }
            Assert.That(_block.TryTickDig(hitB, pointB, 0.2f), Is.True, "B 攒满越过 4s 破坏");
        }

        [Test]
        public void InstantBreak_FlowerHardness_FastPath()
        {
            // hardness=0 的装饰方块走即挖分支（0.15s），两次 0.1s tick 即满。
            // 不建第二个 PlayerContext——它的 Awake 撞单例 Destroy 在 EditMode 是非法调用；
            // 复用夹具宿主，只把玩家 / 交互组件重绑到带花的新注册表 + 新世界。
            var registry = BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
                @"{ ""id"": ""flower_red"", ""numericId"": 2000, ""textures"": { ""all"": ""flower"" },
                    ""solid"": false, ""opaque"": false, ""hardness"": 0.0 }",
            });
            var world2 = new World();
            _player.Bind(world2, registry, new Float3(50f, 80f, 50f));
            _block.Bind(world2, registry, null, _host.transform);

            world2.SetBlock(8, 70, 8, 2000);
            var source = new InteractionRaySource(world2, registry);
            var hit = MyWorld.Core.Physics.VoxelRaycaster.Cast(
                source, new Float3(8.5f, 74.5f, 8.5f), new Float3(0f, -1f, 0f), 10f);
            Assert.That(hit.Hit, Is.True, "造型方块也要能命中（m11 ② InteractionRaySource）");
            var hitPoint = new Float3(8.5f, 74.5f, 8.5f) + new Float3(0f, -1f, 0f) * hit.Distance;

            Assert.That(_block.TryTickDig(hit, hitPoint, 0.1f), Is.False, "0.1s < 0.15s 不破坏");
            Assert.That(_block.TryTickDig(hit, hitPoint, 0.1f), Is.True, "0.2s ≥ 0.15s 即挖完成");
        }

        private BlockRegistry BuildRegistryForRay()
        {
            // InteractionRaySource 要查注册表（solid=false 造型方块判定），给最小集
            return BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" } }",
            });
        }
    }
}
#endif
