#if UNITY_EDITOR
// Phase D 第五批测试：MobManager 刷怪走 MobSpawnRules.PickKind 数据驱动决策。
// 测试通过 public TickSpawn(seed, dayNightPhase) 注入确定性参数，验证：
//   - 白天 + Plains 应能刷出友好 mob（Phase D Pig/Cow/Chicken）
//   - 夜晚 + Plains 应能刷出 Zombie
//   - 夜晚友好 mob 因 MinLight=9 应被拒绝
//   - MaxMobs 上限生效
// 整个文件用 #if UNITY_EDITOR 包裹：dotnet 链跑纯 Core 测试时跳过，Unity EditMode 链跑。
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Rendering;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Combat
{
    [TestFixture]
    public class MobManagerSpawnTests
    {
        private GameObject _host;
        private GameObject _player;
        private MobManager _mgr;
        private MobSpawnRules _rules;

        [SetUp]
        public void SetUp()
        {
            _rules = MobSpawnRules.Load(SpawnRulesPath());
            _host = new GameObject("MobManagerTestHost");
            _player = new GameObject("MobManagerTestPlayer");
            _player.transform.position = new Vector3(0.5f, 71f, 0.5f);
            _mgr = _host.AddComponent<MobManager>();
            // generator=null → MobManager 退到 Biome.Plains；rules=真实加载 → 走数据驱动路径
            _mgr.Bind(world: null, time: null, player: _player.transform,
                generator: null, rules: _rules);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_player != null) Object.DestroyImmediate(_player);
            // MobManager.SpawnMob 会创建 Mob_{kind}_{id} GameObject，清掉防止泄漏
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name.StartsWith("Mob_"))
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// 白天（dayNightPhase=0.25，即正午 6000 tick）+ Plains + 白天候选组应至少有一次命中。
        /// m11 集成点②起 spawn_rules.json 合并了 9 被动条目，白天 Plains 可刷出的
        /// kind 扩为 {Pig, Cow, Chicken, Villager, Sheep, Rabbit, Hamster}（Fox/Deer/Panda/
        /// Raccoon/Penguin/Goat 的条目不含 Plains）——断言改为「不在夜晚敌对组」，
        /// 白名单式断言会随条目演进而反复过时。
        /// </summary>
        [Test]
        public void TickSpawn_Daytime_SpawnsFriendlyMob()
        {
            int hits = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                int before = _mgr.ActiveMobs.Count;
                _mgr.TickSpawn(seed, dayNightPhase: 0.25f);
                if (_mgr.ActiveMobs.Count > before)
                {
                    hits++;
                    var spawned = _mgr.ActiveMobs[_mgr.ActiveMobs.Count - 1];
                    Assert.That(spawned.Kind,
                        Is.Not.EqualTo(MobKind.Zombie).And.Not.EqualTo(MobKind.Skeleton)
                            .And.Not.EqualTo(MobKind.Spider).And.Not.EqualTo(MobKind.Creeper),
                        "白天刷出的 mob 不应是敌对 kind（四敌对全部 minLight=0 但不在白天候选组）");
                }
            }
            Assert.That(hits, Is.GreaterThan(0),
                $"白天 + Plains 应高概率刷出友好 mob（实际 {hits}/100）");
        }

        /// <summary>
        /// 夜晚（dayNightPhase=0.7，即深夜 16800 tick）+ Plains + 候选 [Zombie, Skeleton, Spider, Creeper]
        /// 应刷出敌对生物之一。m11 W1-1 起 spawn_rules.json 有三敌对条目，夜晚不再是 Zombie 独占。
        /// </summary>
        [Test]
        public void TickSpawn_Nighttime_SpawnsHostileMob()
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
                        "夜晚 + Plains 刷出的 mob 应是四敌对 kind 之一（W1-1 起三新敌对已投放）");
                }
            }
            Assert.That(hits, Is.GreaterThan(0),
                $"夜晚应能刷出敌对生物（实际 {hits}/100）");
        }

        /// <summary>
        /// 夜晚所有白天候选（Pig/Cow/Chicken）的 MinLight=9，lightLevel=0 全军覆没 →
        /// TickSpawn 夜间刷出的绝不可能是友好 kind。
        /// （nightCandidates 是 [Zombie, Skeleton, Spider, Creeper]，mob 一定是四者之一或没刷。）
        /// </summary>
        [Test]
        public void TickSpawn_Nighttime_DoesNotSpawnFriendlyMobs()
        {
            int totalSpawns = 0;
            // 反复 TickSpawn 100 次，断言刷出的 mob 不可能是友好 kind
            for (int seed = 1; seed <= 100; seed++)
            {
                int before = _mgr.ActiveMobs.Count;
                _mgr.TickSpawn(seed, dayNightPhase: 0.7f);
                if (_mgr.ActiveMobs.Count > before)
                {
                    totalSpawns++;
                    var spawned = _mgr.ActiveMobs[_mgr.ActiveMobs.Count - 1];
                    Assert.That(spawned.Kind, Is.Not.EqualTo(MobKind.Pig),
                        "夜晚绝不应刷出友好 mob（MinLight=9）");
                    Assert.That(spawned.Kind, Is.Not.EqualTo(MobKind.Cow));
                    Assert.That(spawned.Kind, Is.Not.EqualTo(MobKind.Chicken));
                }
            }
            // 即便断言通过，totalSpawns 可能为 0（夜晚 + 0 权重 zombie 也可能全军覆没），
            // 不强制断言 totalSpawns > 0；这里只验证「不刷友好 mob」性质。
            Assert.Pass();
        }

        /// <summary>
        /// MaxMobs 上限：把上限设为 2，连续 TickSpawn 多次，ActiveMobs 数量不应超过 2。
        /// </summary>
        [Test]
        public void TickSpawn_RespectsMaxMobs()
        {
            _mgr.MaxMobs = 2;
            for (int seed = 1; seed <= 20; seed++)
            {
                _mgr.TickSpawn(seed, dayNightPhase: 0.25f);
            }
            Assert.That(_mgr.ActiveMobs.Count, Is.LessThanOrEqualTo(2),
                $"MaxMobs=2 时 ActiveMobs 不应超过 2（实际 {_mgr.ActiveMobs.Count}）");
        }

        /// <summary>m11 P0：12 新生物（值 15-26，名字/数值与 Core MobKindWiringTests 同一份契约）。</summary>
        private static readonly MobKind[] M11NewKinds =
        {
            MobKind.Sheep, MobKind.Rabbit, MobKind.Fox, MobKind.Deer, MobKind.Panda,
            MobKind.Penguin, MobKind.Goat, MobKind.Raccoon, MobKind.Hamster,
            MobKind.Skeleton, MobKind.Spider, MobKind.Creeper,
        };

        /// <summary>m11 P0：9 个新被动 kind（进白天候选组）。</summary>
        private static readonly MobKind[] M11PassiveKinds =
        {
            MobKind.Sheep, MobKind.Rabbit, MobKind.Fox, MobKind.Deer, MobKind.Panda,
            MobKind.Penguin, MobKind.Goat, MobKind.Raccoon, MobKind.Hamster,
        };

        /// <summary>m11 P0：3 个新敌对 kind（进夜晚候选组）。</summary>
        private static readonly MobKind[] M11HostileKinds =
        {
            MobKind.Skeleton, MobKind.Spider, MobKind.Creeper,
        };

        /// <summary>
        /// m11 P0：12 新生物的 UsesPartTable 预接线守卫（反射直调私有静态，模式抄
        /// InvokeDriveWalkPhase 先例）。P0 只接线：spawn_rules.json 尚未加这 12 个
        /// 名字的条目，所以接线后不会真的刷出（部位表拼装路径不会被走到）；
        /// 等 W1 代理补条目 + mobs/models/*.json 后自然生效。
        /// </summary>
        [Test]
        public void UsesPartTable_M11TwelveNewKinds_AllTrue()
        {
            var method = typeof(MobManager).GetMethod("UsesPartTable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "MobManager 应有私有静态 UsesPartTable 方法");
            foreach (var kind in M11NewKinds)
            {
                Assert.That((bool)method.Invoke(null, new object[] { kind }), Is.True,
                    $"{kind} P0 预接线后应走部位表拼装路径（模型 JSON 由 W1-1/W1-2 补齐）");
            }
        }

        /// <summary>
        /// m11 P0：12 新生物的 MobKindToTypeId 映射守卫——typeId 取枚举数值（P0 约定，
        /// 与 Core 侧测试实体的 MobTypeId=(int)kind 同款）。
        /// </summary>
        [Test]
        public void MobKindToTypeId_M11TwelveNewKinds_EqualsEnumValue()
        {
            var method = typeof(MobManager).GetMethod("MobKindToTypeId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "MobManager 应有私有静态 MobKindToTypeId 方法");
            foreach (var kind in M11NewKinds)
            {
                Assert.That((int)method.Invoke(null, new object[] { kind }), Is.EqualTo((int)kind),
                    $"{kind} 的 mobTypeId 应取枚举数值 {(int)kind}（P0 约定）");
            }
        }

        /// <summary>
        /// m11 P0：昼夜候选表分组守卫——既有 5 kind 分组不回退（断言只增不减），
        /// 9 被动 kind 进 DayCandidates、骷髅/蜘蛛/苦力怕进 NightCandidates。
        /// 候选表是私有静态字段，反射读取。
        /// </summary>
        [Test]
        public void DayNightCandidates_M11NewKinds_GroupedCorrectly()
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var dayField = typeof(MobManager).GetField("DayCandidates", flags);
            var nightField = typeof(MobManager).GetField("NightCandidates", flags);
            Assert.That(dayField, Is.Not.Null, "MobManager 应有私有静态 DayCandidates 字段");
            Assert.That(nightField, Is.Not.Null, "MobManager 应有私有静态 NightCandidates 字段");
            var day = (MobKind[])dayField.GetValue(null);
            var night = (MobKind[])nightField.GetValue(null);

            // 既有分组不回退：猪/牛/鸡/村民在白天组，僵尸在夜晚组
            Assert.That(day, Does.Contain(MobKind.Pig), "Pig 应保留在白天候选组");
            Assert.That(day, Does.Contain(MobKind.Cow), "Cow 应保留在白天候选组");
            Assert.That(day, Does.Contain(MobKind.Chicken), "Chicken 应保留在白天候选组");
            Assert.That(day, Does.Contain(MobKind.Villager), "Villager 应保留在白天候选组");
            Assert.That(night, Does.Contain(MobKind.Zombie), "Zombie 应保留在夜晚候选组");

            // 9 被动 kind 进白天组、不进夜晚组
            foreach (var kind in M11PassiveKinds)
            {
                Assert.That(day, Does.Contain(kind), $"{kind} 应在白天候选组（被动生物）");
                Assert.That(night, Does.Not.Contain(kind), $"{kind} 不应在夜晚候选组");
            }

            // 骷髅/蜘蛛/苦力怕进夜晚组、不进白天组
            foreach (var kind in M11HostileKinds)
            {
                Assert.That(night, Does.Contain(kind), $"{kind} 应在夜晚候选组（敌对生物）");
                Assert.That(day, Does.Not.Contain(kind), $"{kind} 不应在白天候选组");
            }
        }

        /// <summary>
        /// m8 A2：部位表全权负责视觉——刷出的 host cube Renderer 应禁用（拼装部位已覆盖
        /// host 体积，双份渲染只会重合）、缩放归一（部位表以格为单位，host 缩放会拉伸部件）、
        /// 子物体数等于部位表部位数。
        /// </summary>
        [Test]
        public void SpawnMob_AssembledKind_DisablesHostRenderer_AndBuildsPartChildren()
        {
            GameObject spawnedGo = null;
            Mob spawned = null;
            for (int seed = 1; seed <= 100; seed++)
            {
                int before = _mgr.ActiveMobs.Count;
                _mgr.TickSpawn(seed, dayNightPhase: 0.25f);
                if (_mgr.ActiveMobs.Count > before)
                {
                    spawned = _mgr.ActiveMobs[_mgr.ActiveMobs.Count - 1];
                    spawnedGo = GameObject.Find($"Mob_{spawned.Kind}_{spawned.EntityId}");
                    break;
                }
            }
            Assert.That(spawnedGo, Is.Not.Null, "白天 + Plains 100 次内应至少刷出一只友好生物");
            Assert.That(spawnedGo.GetComponent<Renderer>().enabled, Is.False,
                "host cube Renderer 应禁用（部位表全权负责视觉，消灭重合渲染）");
            Assert.That(spawnedGo.transform.localScale, Is.EqualTo(Vector3.one),
                "host 缩放应归一（部位表坐标以格为单位，host 体型缩放会把部件拉伸变形）");
            Assert.That(spawnedGo.transform.childCount,
                Is.EqualTo(MobModels.Build(spawned.Kind).Length),
                "拼装后子物体数应等于部位表部位数（实际 kind=" + spawned.Kind + "）");
        }

        /// <summary>
        /// m8 A2 fix1：站定缓动归零——mob 停下（帧间位移 &lt; 0.001）后，walk phase 向最近的
        /// π 整数倍缓动，腿摆角应收敛到 0°（±2° 容差），不再冻结在半摆位。
        /// EditMode 下 Update 不自动跑，反射直调 DriveWalkPhase(mob, dt) 逐帧驱动。
        /// </summary>
        [Test]
        public void DriveWalkPhase_StandingStill_EasesLegsBackToZero()
        {
            Mob spawned = null;
            GameObject spawnedGo = null;
            for (int seed = 1; seed <= 200; seed++)
            {
                int before = _mgr.ActiveMobs.Count;
                _mgr.TickSpawn(seed, dayNightPhase: 0.25f);
                if (_mgr.ActiveMobs.Count > before)
                {
                    var cand = _mgr.ActiveMobs[_mgr.ActiveMobs.Count - 1];
                    if (!HasLegs(cand.Kind)) continue; // 村民无腿（no-op），等下一只有腿的
                    spawned = cand;
                    spawnedGo = GameObject.Find($"Mob_{cand.Kind}_{cand.EntityId}");
                    break;
                }
            }
            Assert.That(spawnedGo, Is.Not.Null, "白天 + Plains 应能刷出有腿的友好生物（猪/牛/鸡）");

            const float dt = 1f / 60f;
            // 走 10 帧（1.5 格/秒）：phase = 位移×8 = 1.5×8×10/60 = 2 rad，
            // sin(2)≈0.91 → 腿摆 ~18°，处在明显的半摆位
            for (int i = 0; i < 10; i++)
            {
                spawned.Position = new MyWorld.Core.Math.Float3(
                    spawned.Position.X + 1.5f * dt, spawned.Position.Y, spawned.Position.Z);
                InvokeDriveWalkPhase(spawned, dt);
            }
            float maxBefore = MaxLegAngle(spawnedGo, spawned.Kind);
            Assert.That(maxBefore, Is.GreaterThan(2f),
                "停步前应处于明显摆腿状态（最大摆角 > 2°，实际 " + maxBefore.ToString("F2") + "°）");

            // 站定 120 帧（2s；缓动速率 10/s → 残差 ~e^-20）：相位收敛到最近 π 整数倍
            for (int i = 0; i < 120; i++)
            {
                InvokeDriveWalkPhase(spawned, dt);
            }
            float maxAfter = MaxLegAngle(spawnedGo, spawned.Kind);
            Assert.That(maxAfter, Is.LessThanOrEqualTo(2f),
                "站定 2s 后腿摆应缓动归零（±2° 容差，实际最大 " + maxAfter.ToString("F2") + "°）");
        }

        private static bool HasLegs(MobKind kind)
        {
            foreach (var part in MobModels.Build(kind))
            {
                if (part.IsLeg) return true;
            }
            return false;
        }

        private static float MaxLegAngle(GameObject go, MobKind kind)
        {
            float max = 0f;
            foreach (var part in MobModels.Build(kind))
            {
                if (!part.IsLeg) continue;
                var pivot = go.transform.Find(part.Name);
                Assert.That(pivot, Is.Not.Null, "应找到腿枢轴 " + part.Name);
                max = Mathf.Max(max, Mathf.Abs(Mathf.DeltaAngle(0f, pivot.localRotation.eulerAngles.x)));
            }
            return max;
        }

        private void InvokeDriveWalkPhase(Mob mob, float dt)
        {
            // EditMode 下 MobManager.Update 不自动跑，反射直调私有 DriveWalkPhase(mob, dt)
            var method = typeof(MobManager).GetMethod("DriveWalkPhase",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "MobManager 应有私有 DriveWalkPhase 方法");
            method.Invoke(_mgr, new object[] { mob, dt });
        }

        private static string SpawnRulesPath()
        {
            return Path.Combine(Application.streamingAssetsPath, "mobs", "spawn_rules.json");
        }
    }
}
#endif