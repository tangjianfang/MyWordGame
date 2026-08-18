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
        private GameObject _mobHost;
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

            // EditMode 修复（EditMode 首跑暴露）：AddComponent 不触发 MonoBehaviour.
            // OnEnable（订阅 MobAI.OnProjectileFired 的唯一入口在那里，组件无
            // [ExecuteAlways]），不显式调用的话箭永远进不了 tick 列表——六测全挂
            // 同因。反射直调私有 OnEnable，RespawnSafetyTests.InvokeOnEnable 同款。
            InvokeOnEnable(_manager);
        }

        /// <summary>EditMode 下 AddComponent 不会触发 MonoBehaviour.OnEnable
        /// （ProjectileManager 订阅 MobAI.OnProjectileFired 的入口在那里），
        /// 反射显式调用（RespawnSafetyTests 同款）。</summary>
        private static void InvokeOnEnable(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("OnEnable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 OnEnable");
            method.Invoke(mb, null);
        }

        /// <summary>EditMode 下 DestroyImmediate 不触发 MonoBehaviour.OnDisable
        /// （无 [ExecuteAlways]），解绑 MobAI.OnProjectileFired 也要反射显式调用，
        /// 否则静态事件订阅泄漏给后续测试。</summary>
        private static void InvokeOnDisable(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 OnDisable");
            method.Invoke(mb, null);
        }

        /// <summary>
        /// m11 ②C（任务 1）：给 mob 命中测试挂 MobManager（ProjectileManager 惰性
        /// FindObjectOfType 解析的就是它）。不放在 SetUp——MobManager 订阅
        /// CombatEvents.OnDamageTaken，既有「命中玩家」测试的 RaiseTaken 会被它接走，
        /// 走到「无 PlayerController」兜底分支时 ctx.Experience 未初始化会 NRE 串坏老测试。
        /// </summary>
        private MobManager SpawnMobManager()
        {
            _mobHost = new GameObject("MobManagerHost");
            return _mobHost.AddComponent<MobManager>();
        }

        [TearDown]
        public void TearDown()
        {
            // 清空静态事件订阅，防串档到其它测试：EditMode 下销毁宿主不回调
            // OnDisable（无 [ExecuteAlways]），解绑须反射显式调用（OnDisable 顺带
            // ClearAll 销毁箭视觉，与下面的全场景清扫双保险）
            if (_manager != null) InvokeOnDisable(_manager);
            if (_host != null) Object.DestroyImmediate(_host);
            if (_player != null) Object.DestroyImmediate(_player);
            if (_contextHost != null) Object.DestroyImmediate(_contextHost);
            if (_parent != null) Object.DestroyImmediate(_parent);
            if (_mobHost != null) Object.DestroyImmediate(_mobHost);
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name == "箭") Object.DestroyImmediate(go);
                // SpawnMobAt 刷的 mob 视图不挂宿主层级，销毁宿主带不走（FarmingHostTests 同款）
                if (go.name.StartsWith("Mob_")) Object.DestroyImmediate(go);
            }
            DifficultyMode.ResetCache(); // m13 W2：宝宝模式静态 bool 跨测试隔离（清回默认 false）
        }离（清回默认 false）
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

        // ─── m11 ②C（任务 1）：玩家箭（owner=0）mob 命中 ─────────────────────
        // 伤害唯一入口 MobAI.TakeHit；击杀的掉落/经验走 TakeHit 死亡序列 + MobManager
        // 的 Dying 分支（与近战同一条链），宿主不另写。

        [Test]
        public void 玩家箭命中mob_经TakeHit扣血_箭消亡不掉落()
        {
            var pig = SpawnMobManager().SpawnMobAt(
                MyWorld.Core.Entities.MobKind.Pig, new Float3(0f, 70f, 0f));
            Assume.That(pig.Health.Current, Is.EqualTo(10f), "前置：猪满血 10");

            // 玩家箭（owner=0）从 -3 格外朝猪平射；Damage=3 是玩家弓蓄力的注入值
            MobAI.OnProjectileFired?.Invoke(new ProjectileEntity(
                new Float3(0f, 70f, -3f), new Float3(0f, 0f, 9f), ownerEntityId: 0)
            {
                Damage = 3f,
            });

            for (int i = 0; i < 30; i++)
            {
                _manager.TickManually(0.05f);
                if (_manager.ActiveArrowCount == 0) break;
            }

            Assert.That(_manager.ActiveArrowCount, Is.EqualTo(0), "命中 mob 的箭应消亡");
            Assert.That(pig.Health.Current, Is.EqualTo(7f),
                "伤害应经 MobAI.TakeHit 读箭的 Damage 字段：10 - 3 = 7");
            Assert.That(pig.IsAlive, Is.True, "3 伤不致死（受击逃跑是 TakeHit 对被动系的既有行为）");
            Assert.That(pig.LastAttackerPos.X, Is.EqualTo(50f),
                "TakeHit 的 attackerPos 应传射手（玩家）位置，不是箭落点");
            Assert.That(_contextHost.GetComponent<PlayerContext>().ItemDrops.Count, Is.EqualTo(0),
                "命中实体的箭按折损处理，不转可拾取掉落（可捡的箭由 Stuck 路径出）");
        }

        [Test]
        public void 玩家箭距离阈值_边界内命中边界外掠过()
        {
            var pig = SpawnMobManager().SpawnMobAt(
                MyWorld.Core.Entities.MobKind.Pig, new Float3(0f, 70f, 0f));
            Assume.That(pig.Health.Current, Is.EqualTo(10f), "前置：猪满血 10");

            // dt=0：位置积分不动，纯距离判定（MobHitRadius=0.9 两侧各探 0.01）
            MobAI.OnProjectileFired?.Invoke(new ProjectileEntity(
                new Float3(0.89f, 70f, 0f), new Float3(0f, 0f, 9f), ownerEntityId: 0));
            _manager.TickManually(0f);
            Assert.That(_manager.ActiveArrowCount, Is.EqualTo(0), "0.89 < 0.9：应命中，箭消亡");
            Assert.That(pig.Health.Current, Is.EqualTo(8f),
                "箭缺省 Damage=PlayerHitDamage(2)：10 - 2 = 8");

            MobAI.OnProjectileFired?.Invoke(new ProjectileEntity(
                new Float3(0.91f, 70f, 0f), new Float3(0f, 0f, 9f), ownerEntityId: 0));
            _manager.TickManually(0f);
            Assert.That(_manager.ActiveArrowCount, Is.EqualTo(1), "0.91 ≥ 0.9：不命中，箭继续飞");
            Assert.That(pig.Health.Current, Is.EqualTo(8f), "掠过不扣血");
        }

        [Test]
        public void 骷髅箭穿过mob不判命中_掉落与行为不变()
        {
            var pig = SpawnMobManager().SpawnMobAt(
                MyWorld.Core.Entities.MobKind.Pig, new Float3(0f, 70f, 0f));

            // owner=99 的骷髅箭贴着猪锚点（dt=0 位置不动）——mob 判定只对玩家箭开放
            MobAI.OnProjectileFired?.Invoke(new ProjectileEntity(
                new Float3(0f, 70f, 0f), new Float3(0f, 0f, 9f), ownerEntityId: 99));
            _manager.TickManually(0f);

            Assert.That(_manager.ActiveArrowCount, Is.EqualTo(1), "骷髅箭不应被 mob 命中消耗");
            Assert.That(pig.Health.Current, Is.EqualTo(10f), "骷髅箭不判 mob 命中，猪不掉血");
        }

        [Test]
        public void 玩家箭致死_走TakeHit死亡序列_击杀标记与掉落同近战()
        {
            var chicken = SpawnMobManager().SpawnMobAt(
                MyWorld.Core.Entities.MobKind.Chicken, new Float3(0f, 70f, 0f));
            Assume.That(chicken.Health.Current, Is.EqualTo(4f), "前置：鸡满血 4");

            MobAI.OnProjectileFired?.Invoke(new ProjectileEntity(
                new Float3(0f, 70f, 0.5f), new Float3(0f, 0f, -1f), ownerEntityId: 0)
            {
                Damage = 4f, // 满蓄力一箭带走
            });
            _manager.TickManually(0f);

            Assert.That(_manager.ActiveArrowCount, Is.EqualTo(0), "致死箭命中即消亡");
            Assert.That(chicken.State, Is.EqualTo(MyWorld.Core.Entities.MobState.Dying),
                "致死应经 MobAI.TakeHit 死亡分支转 Dying（宿主不另写击杀）");
            Assert.That(chicken.KilledByPlayer, Is.True,
                "玩家箭致死应置击杀标记——MobManager 的 Dying 分支据此入账经验");
            Assert.That(chicken.LastDrops, Is.Not.Null,
                "掉落由 TakeHit 死亡序列写 LastDrops（MobManager 消费，与近战同一条链）");
        }
    }
}
#endif
