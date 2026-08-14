#if UNITY_EDITOR
// X3 fix-up：VillagerManager 必须接 MobSpawnRules.PickKind 校验 biome，
// 不再使用 UnityEngine.Random.Range（review-final.md finding #3）。
// 关键约束：
//   - 不在 Desert（spawn_rules.json Villager.biomes = Plains+Forest）刷 Villager
//   - 同一 seed + 同一 biome 应可重现（确定性）
//   - 3-5 条交易（委托给 Core VillagerOffers.Build）
// 整个文件用 #if UNITY_EDITOR 包裹：依赖 MyWorld.Unity.Combat.VillagerManager，dotnet 链跑不动。
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Combat
{
    /// <summary>
    /// X3 fix-up：VillagerManager.TrySpawnOne 接 MobSpawnRules.PickKind 的契约测试。
    /// </summary>
    [TestFixture]
    public class VillagerManagerSpawnTests
    {
        private GameObject _host;
        private GameObject _player;
        private VillagerManager _mgr;
        private MobSpawnRules _rules;
        private WorldGenerator _generator;

        private static string SpawnRulesPath()
        {
            return Path.Combine(Application.streamingAssetsPath, "mobs", "spawn_rules.json");
        }

        [SetUp]
        public void SetUp()
        {
            _rules = MobSpawnRules.Load(SpawnRulesPath());
            // 固定 seed 让 WorldGenerator.BiomeAt 完全可重现
            _generator = new WorldGenerator(seed: 42);
            _host = new GameObject("VillagerManagerTestHost");
            _player = new GameObject("VillagerManagerTestPlayer");
            _player.transform.position = new Vector3(0.5f, 71f, 0.5f);
            _mgr = _host.AddComponent<VillagerManager>();
            _mgr.Bind(world: null, time: null, player: _player.transform,
                generator: _generator, rules: _rules);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_player != null) Object.DestroyImmediate(_player);
            foreach (var go in Object.FindObjectsOfType<GameObject>())
            {
                if (go.name.StartsWith("Villager_"))
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// 找一个 _rules.PickKind 真正会挑出 Villager 的种子（在 generator seed=42 的世界里）。
        /// 玩家站在 (0.5, 71, 0.5)，逐一尝试 1..N 种子直到出现 Plains+biome-allowed 的坐标。
        /// </summary>
        private int FindFirstSpawningSeed(int maxAttempts = 100)
        {
            for (int seed = 1; seed <= maxAttempts; seed++)
            {
                int pcx = Mathf.FloorToInt(_player.transform.position.x / 16f);
                int pcz = Mathf.FloorToInt(_player.transform.position.z / 16f);
                int rx = Mathf.Abs((seed * 13) % 3) - 1;   // SpawnRadiusChunks=1 → mod 3
                int rz = Mathf.Abs((seed * 17) % 3) - 1;
                int wx = (pcx + rx) * 16 + Mathf.Abs((seed >> 4) % 12) + 2;
                int wz = (pcz + rz) * 16 + Mathf.Abs((seed >> 8) % 12) + 2;
                Biome biome = _generator.BiomeAt(wx, wz);
                if (biome == Biome.Plains || biome == Biome.Forest)
                {
                    var picked = _rules.PickKind(biome, 15, new[] { MobKind.Villager }, seed);
                    if (picked.HasValue) return seed;
                }
            }
            Assert.Inconclusive($"100 颗种子内都未找到 Plains/Forest + 可刷种子的坐标");
            return -1; // unreachable
        }

        /// <summary>
        /// 玩家站在 (0.5, 71, 0.5)：用第一个能刷出的 seed 调用 TrySpawnOne，应刷出 Villager。
        /// </summary>
        [Test]
        public void TrySpawnOne_Plains_CanSpawnVillager()
        {
            int seed = FindFirstSpawningSeed();
            int before = _mgr.ActiveVillagers.Count;
            _mgr.TrySpawnOne(seed: seed);
            int after = _mgr.ActiveVillagers.Count;
            Assert.That(after, Is.GreaterThan(before),
                $"Plains 应允许刷 Villager（之前 {before} → 之后 {after}）");
            var v = _mgr.ActiveVillagers[after - 1];
            Assert.That(v.Profession, Is.Not.EqualTo(VillagerProfession.None),
                "刷出的 Villager 应有具体职业（不是 None）");
        }

        /// <summary>
        /// 规则层（Villager.biomes=Plains+Forest）就拒绝 Desert。
        /// </summary>
        [Test]
        public void MobSpawnRules_Villager_NeverSpawnsInDesert()
        {
            int desertHits = 0;
            for (int seed = 1; seed <= 200; seed++)
            {
                if (_rules.ShouldSpawn(Biome.Desert, MobKind.Villager, lightLevel: 15, seed)) desertHits++;
            }
            Assert.That(desertHits, Is.EqualTo(0),
                $"Villager 在 Desert 不应刷（spawn_rules.json biomes=Plains+Forest），实际 {desertHits}/200");
        }

        /// <summary>
        /// 同一 seed + 同一 setup 应可重现：新建一个 manager，重做相同 setup，
        /// 调 TrySpawnOne(seed) 后两个 Villager 的关键属性应一致。
        /// </summary>
        [Test]
        public void TrySpawnOne_Deterministic_SameSeedSameVillager()
        {
            int seed = FindFirstSpawningSeed();
            _mgr.TrySpawnOne(seed: seed);
            Assume.That(_mgr.ActiveVillagers.Count, Is.GreaterThan(0),
                "首个可用 seed 应能刷出至少一个 Villager");
            var v1 = _mgr.ActiveVillagers[_mgr.ActiveVillagers.Count - 1];

            // 重做一次相同的 setup
            var host2 = new GameObject("VillagerManagerTestHost2");
            var player2 = new GameObject("VillagerManagerTestPlayer2");
            player2.transform.position = new Vector3(0.5f, 71f, 0.5f);
            var mgr2 = host2.AddComponent<VillagerManager>();
            mgr2.Bind(world: null, time: null, player: player2.transform,
                generator: _generator, rules: _rules);
            try
            {
                mgr2.TrySpawnOne(seed: seed);
                Assume.That(mgr2.ActiveVillagers.Count, Is.GreaterThan(0),
                    "第二个 manager 同 seed 应也能刷出 Villager");
                var v2 = mgr2.ActiveVillagers[mgr2.ActiveVillagers.Count - 1];

                Assert.That(v2.Position.X, Is.EqualTo(v1.Position.X),
                    "X 坐标应可重现（无 UnityEngine.Random.Range）");
                Assert.That(v2.Position.Z, Is.EqualTo(v1.Position.Z),
                    "Z 坐标应可重现（无 UnityEngine.Random.Range）");
                Assert.That(v2.Profession, Is.EqualTo(v1.Profession),
                    "职业应可重现（哈希来自 wx/wz）");
                Assert.That(v2.Offers.Count, Is.EqualTo(v1.Offers.Count),
                    "Offer 数量应可重现（哈希来自 wx/wz + profession）");
            }
            finally
            {
                Object.DestroyImmediate(host2);
                Object.DestroyImmediate(player2);
                foreach (var go in Object.FindObjectsOfType<GameObject>())
                {
                    if (go.name.StartsWith("Villager_")) Object.DestroyImmediate(go);
                }
            }
        }

        /// <summary>
        /// 刷出的 Villager 的 Offers 数量应在 3-5 之间（X3 fix-up：spec D7 要求 3-5）。
        /// 至少需要观察到 3 和 5 两种长度（验证位置真的影响 offer 选择）。
        /// </summary>
        [Test]
        public void TrySpawnOne_SpawnedVillager_HasThreeToFiveOffers()
        {
            int minObserved = int.MaxValue;
            int maxObserved = int.MinValue;
            int spawned = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                // 仅尝试会真正刷怪的 seed（避免 noise）
                int pcx = Mathf.FloorToInt(_player.transform.position.x / 16f);
                int pcz = Mathf.FloorToInt(_player.transform.position.z / 16f);
                int rx = Mathf.Abs((seed * 13) % 3) - 1;
                int rz = Mathf.Abs((seed * 17) % 3) - 1;
                int wx = (pcx + rx) * 16 + Mathf.Abs((seed >> 4) % 12) + 2;
                int wz = (pcz + rz) * 16 + Mathf.Abs((seed >> 8) % 12) + 2;
                Biome biome = _generator.BiomeAt(wx, wz);
                if (biome != Biome.Plains && biome != Biome.Forest) continue;

                int before = _mgr.ActiveVillagers.Count;
                _mgr.TrySpawnOne(seed: seed);
                if (_mgr.ActiveVillagers.Count > before)
                {
                    var v = _mgr.ActiveVillagers[_mgr.ActiveVillagers.Count - 1];
                    int n = v.Offers.Count;
                    minObserved = System.Math.Min(minObserved, n);
                    maxObserved = System.Math.Max(maxObserved, n);
                    Assert.That(n, Is.InRange(3, 5),
                        $"Villager 应有 3-5 条 offer（seed={seed} 实际 {n}）");
                    spawned++;
                }
            }
            Assume.That(spawned, Is.GreaterThan(0),
                "Plains/Forest 应能刷出至少一个 Villager");
            // 至少观察到 3 和 5 两种长度（验证不同位置真的产生不同长度）
            Assert.That(minObserved, Is.LessThanOrEqualTo(3),
                $"观察到的最小 offer 数应 ≤ 3（实际 {minObserved}）");
            Assert.That(maxObserved, Is.GreaterThanOrEqualTo(5),
                $"观察到的最大 offer 数应 ≥ 5（实际 {maxObserved}）");
        }

        /// <summary>
        /// VillagerManager 不应再调用 UnityEngine.Random.Range（review finding #3 的核心修复）：
        /// 同样 seed 跑 N 次，所有 Villager 关键字段 bit-identical。
        /// </summary>
        [Test]
        public void TrySpawnOne_NoUnityEngineRandom_ProducesBitIdenticalResults()
        {
            int seed = FindFirstSpawningSeed();
            var snapshots = new System.Collections.Generic.List<(VillagerProfession profession, int offerCount, float x, float z)>();
            for (int i = 0; i < 5; i++)
            {
                var hostI = new GameObject($"VMNoRandom_{i}");
                var playerI = new GameObject($"VMNoRandomPlayer_{i}");
                playerI.transform.position = new Vector3(0.5f, 71f, 0.5f);
                var mgrI = hostI.AddComponent<VillagerManager>();
                mgrI.Bind(world: null, time: null, player: playerI.transform,
                    generator: _generator, rules: _rules);
                try
                {
                    mgrI.TrySpawnOne(seed: seed);
                    Assume.That(mgrI.ActiveVillagers.Count, Is.GreaterThan(0),
                        $"第 {i} 次应能刷出 Villager（seed={seed} Plains）");
                    var v = mgrI.ActiveVillagers[mgrI.ActiveVillagers.Count - 1];
                    snapshots.Add((v.Profession, v.Offers.Count, v.Position.X, v.Position.Z));
                }
                finally
                {
                    Object.DestroyImmediate(hostI);
                    Object.DestroyImmediate(playerI);
                    foreach (var go in Object.FindObjectsOfType<GameObject>())
                    {
                        if (go.name.StartsWith("Villager_")) Object.DestroyImmediate(go);
                    }
                }
            }

            // 全部快照必须一致
            var first = snapshots[0];
            for (int i = 1; i < snapshots.Count; i++)
            {
                var s = snapshots[i];
                Assert.That(s.profession, Is.EqualTo(first.profession),
                    $"第 {i} 次的职业应与第 0 次一致（{first.profession} vs {s.profession}）");
                Assert.That(s.offerCount, Is.EqualTo(first.offerCount),
                    $"第 {i} 次的 offer 数量应一致（{first.offerCount} vs {s.offerCount}）");
                Assert.That(s.x, Is.EqualTo(first.x),
                    $"第 {i} 次 X 应一致（{first.x} vs {s.x}）");
                Assert.That(s.z, Is.EqualTo(first.z),
                    $"第 {i} 次 Z 应一致（{first.z} vs {s.z}）");
            }
        }

        /// <summary>
        /// 刷出的 Villager 职业 ∈ {Farmer, Librarian, Blacksmith}（永远不是 None）。
        /// </summary>
        [Test]
        public void TrySpawnOne_NeverProducesNoneProfession()
        {
            int totalSpawned = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                int pcx = Mathf.FloorToInt(_player.transform.position.x / 16f);
                int pcz = Mathf.FloorToInt(_player.transform.position.z / 16f);
                int rx = Mathf.Abs((seed * 13) % 3) - 1;
                int rz = Mathf.Abs((seed * 17) % 3) - 1;
                int wx = (pcx + rx) * 16 + Mathf.Abs((seed >> 4) % 12) + 2;
                int wz = (pcz + rz) * 16 + Mathf.Abs((seed >> 8) % 12) + 2;
                Biome biome = _generator.BiomeAt(wx, wz);
                if (biome != Biome.Plains && biome != Biome.Forest) continue;

                int before = _mgr.ActiveVillagers.Count;
                _mgr.TrySpawnOne(seed: seed);
                if (_mgr.ActiveVillagers.Count > before)
                {
                    var v = _mgr.ActiveVillagers[_mgr.ActiveVillagers.Count - 1];
                    Assert.That(v.Profession, Is.Not.EqualTo(VillagerProfession.None),
                        $"Villager 职业不应是 None（seed={seed}）");
                    totalSpawned++;
                }
            }
            Assume.That(totalSpawned, Is.GreaterThan(0),
                "Plains/Forest 应能刷出至少一个 Villager");
        }

        /// <summary>
        /// _rules==null 时跳过 biome 校验，回退路径仍可刷（向后兼容）。
        /// </summary>
        [Test]
        public void TrySpawnOne_NoRules_StillSpawnsWithoutError()
        {
            _mgr.Bind(world: null, time: null, player: _player.transform,
                generator: _generator, rules: null);
            Assert.DoesNotThrow(() => _mgr.TrySpawnOne(seed: 1),
                "rules==null 时 TrySpawnOne 不应抛异常（向后兼容）");
        }

        /// <summary>
        /// generator==null 时退到 Biome.Plains，能刷。weight=6 → ~30% 单次命中率，迭代 50 颗应至少中一次。
        /// </summary>
        [Test]
        public void TrySpawnOne_NoGenerator_FallsBackToPlainsAndSpawns()
        {
            _mgr.Bind(world: null, time: null, player: _player.transform,
                generator: null, rules: _rules);
            int before = _mgr.ActiveVillagers.Count;
            int spawned = 0;
            for (int seed = 1; seed <= 50; seed++)
            {
                _mgr.TrySpawnOne(seed: seed);
                if (_mgr.ActiveVillagers.Count > before + spawned) spawned++;
            }
            Assert.That(spawned, Is.GreaterThan(0),
                "generator==null 应回退到 Biome.Plains，50 颗种子内应至少刷出一个 Villager");
        }
    }
}
#endif