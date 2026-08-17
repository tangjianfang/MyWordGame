using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using NUnit.Framework;

#if UNITY_EDITOR
// m7 A2 despawn 接线段（文末 Combat fixture）只有 EditMode 链编译：MobManager 是
// MonoBehaviour，dotnet 链不引用 UnityEngine。using 必须放在文件首个 namespace 之前。
using System.IO;
using MyWorld.Unity.Combat;
using UnityEngine;
#endif

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// m7 A2：僵尸平衡（白天不追 / 射程 8→4 / 追击 32→20）。
    /// 背景：僵尸「永不消失 + 昼夜追击 + 8 格 2 伤」叠加复活点原地 = 死亡循环，
    /// 威胁侧三收——白天不进 Chasing、夜间追击半径与攻击射程收窄。
    /// 昼夜判定由 <see cref="MobAI.Tick"/> 的 isNight 参数传入（生产侧单一真源
    /// <c>MobManager.IsNightPhase</c>），Core 测试直接注入，不走第二套判定。
    /// despawn（距玩家 &gt;40 格）的 MobManager 接线在本文件末尾 EditMode 段。
    /// </summary>
    [TestFixture]
    public class ZombieBalanceTests
    {
        [Test]
        public void Zombie_Daytime_DoesNotChase()
        {
            var z = Mob.Create(9, new Float3(0, 64, 0));
            // 时钟故意拨到深夜：证明分支只认 isNight 参数（生产由 MobManager.IsNightPhase
            // 单一真源传入），而不是 MobAI 自己再读一遍 TimeOfDay.IsNight
            var nightClock = new TimeOfDay { CurrentTick = 15000 };

            // 白天（isNight=false）：玩家贴到 5 格（旧射程 8 内）也不追，走 wander 流
            MobAI.Tick(z, new Float3(5, 64, 0), null, nightClock, 0.1f, isNight: false);
            Assert.That(z.State, Is.Not.EqualTo(MobState.Chasing),
                "白天（isNight=false）僵尸不应追击玩家——威胁必须在夜里才成立");

            // 夜→昼切换：夜里先追起来，白天一到立即脱离 Chasing 残留
            // （不清残留的话 state 不进 passive 的 switch，僵尸带着追击速度滑行）
            var z2 = Mob.Create(9, new Float3(0, 64, 0));
            MobAI.Tick(z2, new Float3(5, 64, 0), null, nightClock, 0.1f, isNight: true);
            Assert.That(z2.State, Is.EqualTo(MobState.Chasing), "前置：夜间 5 格内僵尸在追");
            MobAI.Tick(z2, new Float3(5, 64, 0), null, nightClock, 0.1f, isNight: false);
            Assert.That(z2.State, Is.Not.EqualTo(MobState.Chasing),
                "入昼后应立即脱离 Chasing，不该有追击残留");
        }

        [Test]
        public void Zombie_Night_ChasesWithinNewRange()
        {
            var z = Mob.Create(9, new Float3(0, 64, 0));
            Assert.That(z.AttackRange, Is.EqualTo(4f),
                "m7 A2：僵尸攻击射程应从 8 收窄到 4");
            Assert.That(z.ChaseRadius, Is.EqualTo(20f),
                "m7 A2：僵尸追击半径应从 32 收窄到 20");

            var night = new TimeOfDay { CurrentTick = 15000 };

            // 15 格（≤20）：夜间应追击
            MobAI.Tick(z, new Float3(15, 64, 0), null, night, 0.1f, isNight: true);
            Assert.That(z.State, Is.EqualTo(MobState.Chasing),
                "夜间 20 格内僵尸应追击玩家");

            // 25 格（>20，旧半径 32 内）：脱离追击
            MobAI.Tick(z, new Float3(25, 64, 0), null, night, 0.1f, isNight: true);
            Assert.That(z.State, Is.Not.EqualTo(MobState.Chasing),
                "超过 20 格僵尸不应追击（旧 32 格追击是死亡循环的一环）");

            // 射程内攻击 / 射程外只追不打（新僵尸 AttackCooldown 初始为 0，首 tick 即攻击）
            int dealt = 0;
            CombatEvents.OnDamageDealt += _ => dealt++;
            try
            {
                var close = Mob.Create(9, new Float3(0, 64, 0));
                MobAI.Tick(close, new Float3(3, 64, 0), null, night, 0.1f, isNight: true);
                Assert.That(dealt, Is.EqualTo(1),
                    "3 格（< AttackRange=4）僵尸应立即攻击");

                var mid = Mob.Create(9, new Float3(0, 64, 0));
                MobAI.Tick(mid, new Float3(6, 64, 0), null, night, 0.1f, isNight: true);
                Assert.That(mid.State, Is.EqualTo(MobState.Chasing),
                    "6 格在追击半径内应保持 Chasing");
                Assert.That(dealt, Is.EqualTo(1),
                    "6 格（> AttackRange=4，旧射程 8 内）不应再攻击");
            }
            finally
            {
                CombatEvents.Reset();
            }
        }
    }
}

#if UNITY_EDITOR
namespace MyWorld.Core.Tests.Combat
{
    /// <summary>
    /// m7 A2：MobManager despawn 接线。MobManager 是 MonoBehaviour，dotnet 链构造不了，
    /// 整段用 #if UNITY_EDITOR 包裹只在 EditMode 跑（模式抄 MobManagerSpawnTests）。
    /// 僵尸永不消失是死亡循环威胁侧根因之一：MobManager.Update 此前只加不减。
    /// </summary>
    [TestFixture]
    public class ZombieBalanceDespawnTests
    {
        private GameObject _host;
        private GameObject _player;
        private MobManager _mgr;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("ZombieBalanceTestHost");
            _player = new GameObject("ZombieBalanceTestPlayer");
            _player.transform.position = new Vector3(0.5f, 71f, 0.5f);
            _mgr = _host.AddComponent<MobManager>();
            // rules 用真实 spawn_rules.json（同 MobManagerSpawnTests）→ 走数据驱动路径刷
            // Zombie。注意不能走 rules=null 的 legacy 路径：legacy 的 MobKind.Passive 染色
            // 分支依赖 MobView.Awake 初始化的 MPB，而 EditMode 下 AddComponent 不触发 Awake，
            // 会 ArgumentNullException（Phase D 分支用局部 MPB，EditMode 安全）。
            _mgr.Bind(world: null, time: null, player: _player.transform,
                generator: null, rules: MobSpawnRules.Load(SpawnRulesPath()));
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_player != null) Object.DestroyImmediate(_player);
            // TickSpawn/SpawnMob 创建的 Mob_{kind}_{id} 视图清掉，防泄漏到其它测试
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name.StartsWith("Mob_"))
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// 距玩家 &gt;40 格的 mob 应被 despawn（实体出列表、视图销毁）；
        /// 40 格内不受影响。Update 每帧调用 TickDespawn（见 MobManager.Update）。
        /// </summary>
        [Test]
        public void MobManager_Despawn_Beyond40()
        {
            Assert.That(MobManager.DespawnDistance, Is.EqualTo(40f),
                "m7 A2：despawn 阈值应为 40 格");

            // 夜晚 + 真实规则：种子扫到第一只即停（保持「恰好 1 只」，下面的计数断言
            // 才可读；同 MobManagerSpawnTests 的 100 种子扫描模式）。
            // EditMode 修复（m11 打挂）：m7 时代夜晚候选只有 Zombie，m11 W1-1 起扩为
            // 僵尸/骷髅/蜘蛛/苦力怕四敌对（MobManager.NightCandidates + spawn_rules.json
            // 的既定产品变更）——前置改为四敌对之一，despawn 本身与 kind 无关不受影响
            for (int seed = 1; seed <= 100 && _mgr.ActiveMobs.Count == 0; seed++)
            {
                _mgr.TickSpawn(seed, dayNightPhase: 0.7f);
            }
            Assert.That(_mgr.ActiveMobs.Count, Is.EqualTo(1), "前置：应已刷出 1 只 mob");
            var mob = _mgr.ActiveMobs[0];
            Assert.That(mob.Kind, Is.EqualTo(MobKind.Zombie).Or.EqualTo(MobKind.Skeleton)
                .Or.EqualTo(MobKind.Spider).Or.EqualTo(MobKind.Creeper),
                "前置：夜晚候选是四敌对之一（m11 W1-1 起不再 Zombie 独占）");

            // 控制组：玩家挪到距 mob 10 格（<40）→ 不 despawn
            // （按实际出生点相对定位，与种子换算出的绝对坐标解耦）
            _player.transform.position = new Vector3(mob.Position.X + 10f, mob.Position.Y, mob.Position.Z);
            _mgr.TickDespawn();
            Assert.That(_mgr.ActiveMobs.Count, Is.EqualTo(1),
                "40 格内的 mob 不应被 despawn");

            // 玩家沿 +X 挪到距 mob 41 格（> DespawnDistance）
            _player.transform.position = new Vector3(mob.Position.X + 41f, mob.Position.Y, mob.Position.Z);
            _mgr.TickDespawn();
            Assert.That(_mgr.ActiveMobs.Count, Is.EqualTo(0),
                "距玩家 >40 格的 mob 应被 despawn");

            // 视图也应一并销毁（与 mob 死亡清理同一条路径）
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                Assert.That(go.name.StartsWith("Mob_"), Is.False,
                    $"despawn 后不应残留 Mob_ 视图：{go.name}");
            }
        }

        private static string SpawnRulesPath()
        {
            return Path.Combine(Application.streamingAssetsPath, "mobs", "spawn_rules.json");
        }
    }
}
#endif
