using System;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// Phase D 第二批测试：JSON 驱动的 <see cref="MobSpawnRules"/>。
    /// 数据从 <c>Assets/StreamingAssets/mobs/spawn_rules.json</c> 加载。
    /// </summary>
    [TestFixture]
    public class MobSpawnRulesTests
    {
        private static string SpawnRulesPath()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "mobs", "spawn_rules.json");
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "mobs", "spawn_rules.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException("未能找到 Assets/StreamingAssets/mobs/spawn_rules.json。");
#endif
        }

        [Test]
        public void Pig_SpawnsInPlains()
        {
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            int spawns = 0;
            for (int i = 0; i < 100; i++)
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Pig, lightLevel: 15, seed: i)) spawns++;
            Assert.That(spawns, Is.GreaterThan(50), $"平原+光 15 应高概率刷猪（实际 {spawns}/100）");
        }

        [Test]
        public void Zombie_SpawnsInAllConfiguredBiomes()
        {
            // brief JSON 给 Zombie 配置 4 个 biome——任选其一 + lightLevel=0 都应至少触发一次
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            int totalSpawns = 0;
            foreach (Biome biome in new[] { Biome.Plains, Biome.Desert, Biome.Forest, Biome.Mountains })
            {
                for (int i = 0; i < 100; i++)
                    if (rules.ShouldSpawn(biome, MobKind.Zombie, lightLevel: 0, seed: i)) totalSpawns++;
            }
            // 4 biome × 100 seed = 400 次尝试，weight=5 → ~25% → 期望 ~100 次 spawn
            Assert.That(totalSpawns, Is.GreaterThan(50),
                $"Zombie 应在 4 个 biome 都刷（实际 {totalSpawns}/400）");
        }

        [Test]
        public void Chicken_SpawnsInForest()
        {
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            int plainsSpawns = 0, forestSpawns = 0;
            for (int i = 0; i < 100; i++)
            {
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Chicken, lightLevel: 15, seed: i)) plainsSpawns++;
                if (rules.ShouldSpawn(Biome.Forest, MobKind.Chicken, lightLevel: 15, seed: i)) forestSpawns++;
            }
            Assert.That(forestSpawns, Is.GreaterThan(plainsSpawns),
                $"森林鸡应多于平原鸡（平原 {plainsSpawns}/100，森林 {forestSpawns}/100）");
        }

        [Test]
        public void Zombie_RejectsDaylight()
        {
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            // minLight=0，但夜间的 lightLevel=0 比白天 lightLevel=15 更低——确认 lightLevel < MinLight 被拒
            int blockedSpawns = 0;
            for (int i = 0; i < 100; i++)
            {
                // 设置 lightLevel=-1（<MinLight=0）应被拒绝
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Zombie, lightLevel: -1, seed: i)) blockedSpawns++;
            }
            Assert.That(blockedSpawns, Is.EqualTo(0), "lightLevel < MinLight 时不应刷怪");
        }

        [Test]
        public void Cow_SpawnsOnlyInPlains()
        {
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            int plainsSpawns = 0, desertSpawns = 0;
            for (int i = 0; i < 100; i++)
            {
                if (rules.ShouldSpawn(Biome.Plains, MobKind.Cow, lightLevel: 15, seed: i)) plainsSpawns++;
                if (rules.ShouldSpawn(Biome.Desert, MobKind.Cow, lightLevel: 15, seed: i)) desertSpawns++;
            }
            Assert.That(plainsSpawns, Is.GreaterThan(desertSpawns),
                $"Cow 应只在 Plains 刷（Plains {plainsSpawns}/100，Desert {desertSpawns}/100）");
        }

        /// <summary>
        /// 白天 + Plains + 候选 [Pig, Cow, Chicken] 应能挑出一个白天友好的 kind；
        /// Pig/Cow/Chicken 的 minLight=9，Plains 都在白名单里，至少应出现一次成功。
        /// </summary>
        [Test]
        public void PickKind_DaytimePlains_ReturnsFriendlyKind()
        {
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            var candidates = new[] { MobKind.Pig, MobKind.Cow, MobKind.Chicken };
            int hits = 0;
            for (int i = 0; i < 100; i++)
            {
                var kind = rules.PickKind(Biome.Plains, lightLevel: 15, candidates, seed: i);
                if (kind.HasValue)
                {
                    foreach (var c in candidates) if (c == kind.Value) { hits++; break; }
                }
            }
            Assert.That(hits, Is.GreaterThan(50),
                $"白天 + Plains 应高概率挑出友好 kind（实际 {hits}/100）");
        }

        /// <summary>
        /// 夜晚 + Plains + 候选 [Zombie] 应稳定挑出 Zombie：Zombie 的 minLight=0，
        /// Plains 在白名单，weight=5 → 期望约 25% 命中。
        /// </summary>
        [Test]
        public void PickKind_NightPlainsZombie_ReturnsZombie()
        {
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            var candidates = new[] { MobKind.Zombie };
            int hits = 0;
            for (int i = 0; i < 100; i++)
            {
                var kind = rules.PickKind(Biome.Plains, lightLevel: 0, candidates, seed: i);
                if (kind == MobKind.Zombie) hits++;
            }
            Assert.That(hits, Is.GreaterThan(0),
                $"夜晚 + Plains 应能刷出 Zombie（实际 {hits}/100）");
        }

        /// <summary>
        /// 夜晚 + 候选 [Pig, Cow, Chicken]：所有 kind 的 minLight=9，lightLevel=0 < 9，
        /// ShouldSpawn 全部返回 false → PickKind 返回 null。
        /// </summary>
        [Test]
        public void PickKind_NightFriendlies_AlwaysReturnsNull()
        {
            var rules = MobSpawnRules.Load(SpawnRulesPath());
            var candidates = new[] { MobKind.Pig, MobKind.Cow, MobKind.Chicken };
            for (int i = 0; i < 50; i++)
            {
                var kind = rules.PickKind(Biome.Plains, lightLevel: 0, candidates, seed: i);
                Assert.That(kind, Is.Null,
                    $"夜晚 lightLevel=0 < MinLight=9，候选友好 mob 一律不应刷（seed={i}）");
            }
        }
    }
}
