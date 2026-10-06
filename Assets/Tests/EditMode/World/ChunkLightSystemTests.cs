#if UNITY_EDITOR
// m11 W1-4 集成点②：玩家周界光照采样系统接线守卫。ChunkLightSystem 定时重建
// WorldLightVolumeBuilder 的两份体积（白天合成 / 夜间纯方块光），TrySampleLight
// 按昼夜语义返回真值、区域外返回 false 让调用方降级。依赖 MyWorld.Unity 程序集，
// dotnet 链跑不动，整个文件用 #if UNITY_EDITOR 包裹（与 PlayerAttackTests 同款）。
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Lighting;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Lighting
{
    [TestFixture]
    public class ChunkLightSystemTests
    {
        private const ushort TorchId = 9001;

        private GameObject _host;
        private GameObject _player;
        private ChunkLightSystem _system;
        private World _world;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("ChunkLightHost");
            _player = new GameObject("LightPlayer");
            _player.transform.position = new Vector3(8.5f, 71f, 8.5f); // 体积中心（3×3 区块 × 96 层）
            _world = new World();

            _system = _host.AddComponent<ChunkLightSystem>();
            _system.Bind(_world, StubRegistry(), _player.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_player != null) Object.DestroyImmediate(_player);
        }

        private static BlockRegistry StubRegistry()
        {
            return BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""torch"", ""numericId"": 9001, ""textures"": { ""all"": ""torch"" }, ""opaque"": false, ""lightEmission"": 14 }",
            });
        }

        [Test]
        public void 手动重建_白天露天15_夜间火把圈亮露天黑()
        {
            _world.SetBlock(8, 70, 8, TorchId); // 玩家脚下一格火把
            _system.RebuildNow();

            Assert.That(_system.HasVolume, Is.True, "重建后应有可用体积");

            // 白天：露天取天光 15；火把正上方一格 max(15, 13) = 15
            Assert.That(_system.TrySampleLight(8, 71, 8, isNight: false, out int dayNear), Is.True);
            Assert.That(dayNear, Is.EqualTo(15), "白天露天格天光满级");
            Assert.That(_system.TrySampleLight(20, 71, 20, isNight: false, out int dayFar), Is.True);
            Assert.That(dayFar, Is.EqualTo(15), "白天远处露天同样天光满级");

            // 夜间：天光不计入——露天 0，火把正上方 13（14-1）
            Assert.That(_system.TrySampleLight(8, 71, 8, isNight: true, out int nightNear), Is.True);
            Assert.That(nightNear, Is.EqualTo(13), "夜间火把正上方一格 = 13（≥ 被动 minLight 9，亮处可刷）");
            Assert.That(_system.TrySampleLight(20, 71, 20, isNight: true, out int nightFar), Is.True);
            Assert.That(nightFar, Is.EqualTo(0), "夜间露天纯方块光 = 0");
        }

        [Test]
        public void 重建前采样_返回false供调用方降级()
        {
            // 不调 RebuildNow（EditMode 也不自动跑 Update）——体积未建必须 false，
            // MobManager 据此退回「白天 15/夜晚 0」近似，绝不抛异常
            Assert.That(_system.TrySampleLight(8, 71, 8, isNight: false, out _), Is.False,
                "体积未建时采样应返回 false（读容忍降级）");
        }

        [Test]
        public void 按需门控_无变化不重建_世界编辑才重建()
        {
            // 评审 02#1/03 B-1：旧逻辑每 2s 无条件重建——挂机/主菜单白烧 83-270ms/轮
            // + MB 级垃圾。现在「无脏 + 人没挪 + 世界没变」必须零重建（本测试是
            // 0.5Hz 规律卡顿归零的直接守卫）。
            _system.IntervalSecondsForTests = 0.01f;
            _system.DeltaTimeForTests = () => 1f; // 假时钟恒过节流——排除时间变量

            _system.Update(); // 初始脏 → 建
            Assert.That(_system.RebuildCountForTests, Is.EqualTo(1), "初始脏必须建一次");

            _system.Update(); // 无变化 → 跳过
            Assert.That(_system.RebuildCountForTests, Is.EqualTo(1),
                "无脏无挪动无编辑——零重建（评审核心断言）");

            _world.SetBlock(9, 70, 9, TorchId); // 走 World.SetBlock → EditCounter 变化
            _system.Update();
            Assert.That(_system.RebuildCountForTests, Is.EqualTo(2),
                "世界编辑（EditCounter）要触发重建");

            ChunkLightSystem.MarkWorldDirty(); // 静态转发路径
            _system.Update();
            Assert.That(_system.RebuildCountForTests, Is.EqualTo(3), "静态 MarkWorldDirty 生效");
        }

        [Test]
        public void 按需门控_玩家挪出脚印触发重建()
        {
            _system.IntervalSecondsForTests = 0.01f;
            _system.DeltaTimeForTests = () => 1f;

            _system.Update();
            Assert.That(_system.RebuildCountForTests, Is.EqualTo(1));

            _player.transform.position += new Vector3(64f, 0f, 0f); // 挪出 3×3 区块脚印
            _system.Update();
            Assert.That(_system.RebuildCountForTests, Is.EqualTo(2),
                "玩家挪出体积脚印（4 区块）要重建——否则采样退化为昼夜近似");
        }

        [Test]
        public void 脚印外采样_返回false()
        {
            _system.RebuildNow();
            // 3×3 区块脚印 = 玩家所在区块 (0,0) ±1 → 覆盖 [-16, 32)；x=1000 远在界外
            Assert.That(_system.TrySampleLight(1000, 71, 8, isNight: false, out _), Is.False,
                "水平脚印外的采样应返回 false（调用方降级到昼夜近似）");
        }
    }
}
#endif
