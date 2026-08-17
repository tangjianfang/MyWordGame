#if UNITY_EDITOR
// m11 W1-1 集成点②：箭实体宿主接线守卫。ProjectileManager 订阅 MobAI.OnProjectileFired、
// tick 推进弹道、命中方块转可拾取掉落。依赖 MyWorld.Unity 程序集与 GameObject，
// dotnet 链跑不动，整个文件用 #if UNITY_EDITOR 包裹（与 PlayerAttackTests 同款）。
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Combat
{
    [TestFixture]
    public class ProjectileManagerTests
    {
        private GameObject _host;
        private GameObject _player;
        private GameObject _contextHost;
        private GameObject _parent;
        private ProjectileManager _manager;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("ProjectileManagerHost");
            _player = new GameObject("Player");
            _player.transform.position = new Vector3(50f, 64f, 50f); // 远离弹道，不误吃箭
            _contextHost = new GameObject("ContextHost");
            _contextHost.AddComponent<PlayerContext>();
            _parent = new GameObject("WorldRoot");

            _manager = _host.AddComponent<ProjectileManager>();
            _manager.Bind(new World(), _player.transform,
                _contextHost.GetComponent<PlayerContext>(), _parent.transform);
        }

        [TearDown]
        public void TearDown()
        {
            // 清空静态事件订阅，防串档到其它测试（OnDisable 里解绑，销毁宿主即触发）
            if (_host != null) Object.DestroyImmediate(_host);
            if (_player != null) Object.DestroyImmediate(_player);
            if (_contextHost != null) Object.DestroyImmediate(_contextHost);
            if (_parent != null) Object.DestroyImmediate(_parent);
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name == "箭") Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 骷髅开火事件_箭入列表并建视觉()
        {
            MobAI.OnProjectileFired?.Invoke(
                new ProjectileEntity(new Float3(0f, 70f, 0f), new Float3(1f, 0f, 0f), ownerEntityId: 99));

            Assert.That(_manager.ActiveArrowCount, Is.EqualTo(1), "OnProjectileFired 抛出后箭应入 tick 列表");
            Assert.That(_parent.transform.childCount, Is.EqualTo(1), "箭视觉（0.25 格小方块）应挂到世界根节点下");
            var visual = _parent.transform.GetChild(0);
            Assert.That(visual.localScale.x, Is.EqualTo(ProjectileManager.VisualSize), "箭视觉边长 0.25 格");
            Assert.That(visual.GetComponent<Collider>(), Is.Null, "箭视觉不得带碰撞体（会挡玩家移动与挖掘射线）");
        }

        [Test]
        public void 命中方块_Stuck箭转可拾取掉落_视觉移交掉落物管线()
        {
            var world = new World();
            world.SetBlock(0, 64, 0, 1); // 落点正下方一格实心方块
            _manager.Bind(world, _player.transform,
                _contextHost.GetComponent<PlayerContext>(), _parent.transform);

            MobAI.OnProjectileFired?.Invoke(
                new ProjectileEntity(new Float3(0.5f, 68f, 0.5f), new Float3(0f, 0f, 0f), ownerEntityId: 99));

            // 逐步 tick 到命中（重力 12 拉它下坠，4 格落差几帧内必然 Stuck）
            for (int i = 0; i < 60; i++)
            {
                _manager.TickManually(0.05f);
                if (_manager.ActiveArrowCount == 0) break;
            }

            var drops = _contextHost.GetComponent<PlayerContext>().ItemDrops;
            Assert.That(_manager.ActiveArrowCount, Is.EqualTo(0), "Stuck 的箭应移出列表（掉落物接管视觉）");
            Assert.That(drops.Count, Is.EqualTo(1), "Stuck 箭应转成 1 件可拾取掉落");
            Assert.That(drops[0].Content.Value.ItemId, Is.EqualTo(ProjectileEntity.ArrowItemId),
                "掉落物应是箭物品（items/arrow.json 的 numericId 1300）");
        }

        [Test]
        public void 命中玩家_Dead箭直接移除_不掉落()
        {
            // 玩家就站在箭的飞行终点上（命中半径 0.6m 内）→ 箭转 Dead
            _player.transform.position = new Vector3(1.4f, 70f, 0f);
            MobAI.OnProjectileFired?.Invoke(
                new ProjectileEntity(new Float3(0f, 70f, 0f), new Float3(9f, 0f, 0f), ownerEntityId: 99));

            for (int i = 0; i < 30; i++)
            {
                _manager.TickManually(0.05f);
                if (_manager.ActiveArrowCount == 0) break;
            }

            Assert.That(_manager.ActiveArrowCount, Is.EqualTo(0), "命中玩家的箭应移除");
            Assert.That(_contextHost.GetComponent<PlayerContext>().ItemDrops.Count, Is.EqualTo(0),
                "命中玩家的箭不是可拾取掉落（Dead 不转掉落）");
        }
    }
}
#endif
