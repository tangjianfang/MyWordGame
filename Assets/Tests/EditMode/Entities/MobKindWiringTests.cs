// m11 P0（前置·串行独占）→ W1-1（战斗）→ 集成点② 演进：12 新生物枚举与分派接线守卫。
// P0 阶段（commit f317e33）只接线、不写专属 AI 且不加条目；W1-1 起：
//   - 骷髅/蜘蛛/苦力怕换上专属 AI（远程风筝/夜间 4.5 追击/引信自爆），
//     spawn_rules.json + drop_tables.json 加了三敌对条目，夜间会真的刷出；
//     专属行为断言在 Entities/HostileAiTests（W1-1 新增）。
//   - 集成点②起 spawn_rules.json 合并了 W1-2 汇报的 9 被动条目（全部 minLight 9），
//     白天按群系真的刷出——守卫随之翻转为「配置群系内可命中 / 夜间与未配置群系不刷」
//     （PickKind_RealRules_NinePassives_MergedByIntegrationPoint2_*）。
// MobManager 侧（UsesPartTable/MobKindToTypeId/昼夜候选表）的映射守卫在
// Combat/MobManagerSpawnTests.cs（MobManager 是 MonoBehaviour，编辑器链专用）。
using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using MyWorld.Core.WorldGen;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class MobKindWiringTests
    {
        /// <summary>P0 Interfaces 固定的 12 新生物（名字/枚举值一字不差——存档 spawn 数据按值序列化，W1 任务卡按名引用）。</summary>
        private static readonly (string Name, MobKind Kind, int Value)[] NewKinds =
        {
            ("Sheep", MobKind.Sheep, 15),
            ("Rabbit", MobKind.Rabbit, 16),
            ("Fox", MobKind.Fox, 17),
            ("Deer", MobKind.Deer, 18),
            ("Panda", MobKind.Panda, 19),
            ("Penguin", MobKind.Penguin, 20),
            ("Goat", MobKind.Goat, 21),
            ("Raccoon", MobKind.Raccoon, 22),
            ("Hamster", MobKind.Hamster, 23),
            ("Skeleton", MobKind.Skeleton, 24),
            ("Spider", MobKind.Spider, 25),
            ("Creeper", MobKind.Creeper, 26),
        };

        /// <summary>9 个新被动 kind（P0 行为等价猪组）。</summary>
        private static readonly MobKind[] NewPassiveKinds =
        {
            MobKind.Sheep, MobKind.Rabbit, MobKind.Fox, MobKind.Deer, MobKind.Panda,
            MobKind.Penguin, MobKind.Goat, MobKind.Raccoon, MobKind.Hamster,
        };

        /// <summary>3 个新敌对 kind（P0 暂等价僵尸组，W1-1 替换专属 AI）。</summary>
        private static readonly MobKind[] NewHostileKinds =
        {
            MobKind.Skeleton, MobKind.Spider, MobKind.Creeper,
        };

        [Test]
        public void MobKind_M11TwelveNewKinds_EnumValuesExact()
        {
            foreach (var (name, kind, value) in NewKinds)
            {
                Assert.That((int)kind, Is.EqualTo(value),
                    $"{name} 枚举值应固定为 {value}（存档 spawn 数据按值序列化，顺序/数值不得改动）");
            }
        }

        [Test]
        public void PickKind_TempRulesWithNewNames_AllResolve()
        {
            // 12 新名各配一条 weight=20（必中）规则写临时 JSON：
            // Load 成功 = ParseKind 认识全部新名（未知名字会抛 ArgumentException）；
            // 逐名 ShouldSpawn 命中 = 名字→kind 映射正确（错任何一条映射都会让
            // 某个 kind 查无规则而漏出，12 名 12 kind 的错位必留缺口）。
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, @"
{
  ""Sheep"":    { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Rabbit"":   { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Fox"":      { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Deer"":     { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Panda"":    { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Penguin"":  { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Goat"":     { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Raccoon"":  { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Hamster"":  { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Skeleton"": { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Spider"":   { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
  ""Creeper"":  { ""biomes"": [""Plains""], ""minLight"": 0, ""weight"": 20 },
}");
                var rules = MobSpawnRules.Load(path);
                foreach (var (name, kind, _) in NewKinds)
                {
                    Assert.That(rules.ShouldSpawn(Biome.Plains, kind, lightLevel: 15, seed: 1), Is.True,
                        $"规则表配了 {name} 条目，PickKind 应能解析该名并命中对应 kind");
                }

                // 候选表整组塞进 PickKind 也应正常解析（MobManager 候选数组的真实形态）
                var all = Array.ConvertAll(NewKinds, k => k.Kind);
                Assert.That(rules.PickKind(Biome.Plains, lightLevel: 15, all, seed: 0).HasValue, Is.True,
                    "12 新 kind 全配置 weight=20 时，PickKind 应至少解析出一个");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void PickKind_RealRules_ThreeHostiles_ConfiguredAndGated()
        {
            // W1-1 起真实 spawn_rules.json 有三敌对条目：
            // Skeleton/Spider 夜间 Plains/Forest/Mountains，Creeper 夜间 Plains/Forest。
            // MobManager 的 NightCandidates 带着它们 → 夜间会真的刷出（模型与 AI 同批就绪）。
            var rules = MobSpawnRules.Load(RealSpawnRulesPath());
            foreach (var (name, kind, _) in NewKinds)
            {
                if (kind != MobKind.Skeleton && kind != MobKind.Spider && kind != MobKind.Creeper)
                {
                    continue;
                }

                // 条目群系内、夜间光照（0 ≥ minLight）应能命中（weight 3-4 → 15%-20%）
                int plainsHits = 0, forestHits = 0;
                for (int seed = 0; seed < 100; seed++)
                {
                    if (rules.ShouldSpawn(Biome.Plains, kind, lightLevel: 0, seed)) plainsHits++;
                    if (rules.ShouldSpawn(Biome.Forest, kind, lightLevel: 0, seed)) forestHits++;
                }
                Assert.That(plainsHits, Is.GreaterThan(0), $"{name} 夜间 Plains 应能刷出（实际 {plainsHits}/100）");
                Assert.That(forestHits, Is.GreaterThan(0), $"{name} 夜间 Forest 应能刷出（实际 {forestHits}/100）");

                // Desert 不在任何条目群系里：一个 seed 都不许命中
                for (int seed = 0; seed < 100; seed++)
                {
                    Assert.That(rules.ShouldSpawn(Biome.Desert, kind, lightLevel: 0, seed), Is.False,
                        $"{name} 的条目不含 Desert，不应在沙漠刷出");
                }
            }

            // Mountains 只配给 Skeleton/Spider，Creeper 不上山
            int mountainHits = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                if (rules.ShouldSpawn(Biome.Mountains, MobKind.Skeleton, 0, seed)) mountainHits++;
                if (rules.ShouldSpawn(Biome.Mountains, MobKind.Spider, 0, seed)) mountainHits++;
                Assert.That(rules.ShouldSpawn(Biome.Mountains, MobKind.Creeper, 0, seed), Is.False,
                    "Creeper 条目不含 Mountains，不应上山刷出");
            }
            Assert.That(mountainHits, Is.GreaterThan(0),
                "Skeleton/Spider 应在 Mountains 有条目（100 seed 至少命中一次，实际 " + mountainHits + "）");
        }

        [Test]
        public void PickKind_RealRules_NinePassives_MergedByIntegrationPoint2_DayOnlyInConfiguredBiomes()
        {
            // m11 集成点②：spawn_rules.json 已合并 W1-2 的 9 被动条目（全部 minLight 9）——
            // 白天（light 15 ≥ 9）在各自声明群系内能命中；夜间光照 0 < 9 一律不刷
            // （真实光照采样在 MobManager/ChunkLightSystem 侧，规则层只看传入 lightLevel）。
            // 群系表与 W1-2 汇报的建议条目一一对应，配错群系名这里直接红。
            var rules = MobSpawnRules.Load(RealSpawnRulesPath());
            var biomeCoverage = new Dictionary<MobKind, Biome[]>
            {
                { MobKind.Sheep,   new[] { Biome.Plains, Biome.Forest } },
                { MobKind.Rabbit,  new[] { Biome.Plains, Biome.Forest, Biome.Snow } },
                { MobKind.Fox,     new[] { Biome.Forest } },
                { MobKind.Deer,    new[] { Biome.Forest, Biome.Mountains } },
                { MobKind.Panda,   new[] { Biome.Forest } },
                { MobKind.Penguin, new[] { Biome.Snow } },
                { MobKind.Goat,    new[] { Biome.Mountains, Biome.Snow } },
                { MobKind.Raccoon, new[] { Biome.Forest } },
                { MobKind.Hamster, new[] { Biome.Plains, Biome.Desert } },
            };

            foreach (var pair in biomeCoverage)
            {
                MobKind kind = pair.Key;
                foreach (Biome biome in pair.Value)
                {
                    int hits = 0;
                    for (int seed = 0; seed < 100; seed++)
                    {
                        if (rules.ShouldSpawn(biome, kind, lightLevel: 15, seed)) hits++;
                    }
                    // weight 2-8 → 10%-40%：100 seed 至少命中一次（weight=2 时漏检概率 ~0.9^100）
                    Assert.That(hits, Is.GreaterThan(0),
                        $"{kind} 条目声明了 {biome}，白天（光照 15）应能刷出（实际 {hits}/100）");
                }
            }

            // 夜间（光照 0 < minLight 9）：黑暗处一律不刷——被动动物不摸黑出门
            foreach (MobKind kind in NewPassiveKinds)
            {
                for (int seed = 0; seed < 50; seed++)
                {
                    Assert.That(rules.ShouldSpawn(Biome.Plains, kind, lightLevel: 0, seed), Is.False,
                        $"{kind} minLight=9，夜间光照 0 不应刷出");
                }
            }

            // 未声明群系一个 seed 都不许命中（防条目抄错群系名导致全图乱刷）
            Assert.That(NotConfiguredBiomeHits(rules, MobKind.Sheep, Biome.Desert), Is.Empty, "Sheep 条目不含 Desert");
            Assert.That(NotConfiguredBiomeHits(rules, MobKind.Fox, Biome.Plains), Is.Empty, "Fox 条目不含 Plains");
            Assert.That(NotConfiguredBiomeHits(rules, MobKind.Penguin, Biome.Plains), Is.Empty, "Penguin 条目不含 Plains");
            Assert.That(NotConfiguredBiomeHits(rules, MobKind.Hamster, Biome.Snow), Is.Empty, "Hamster 条目不含 Snow");
        }

        /// <summary>统计某 kind 在「未配置群系」的命中 seed 列表（期望恒空）。</summary>
        private static List<int> NotConfiguredBiomeHits(
            MobSpawnRules rules, MobKind kind, Biome biome)
        {
            var hits = new List<int>();
            for (int seed = 0; seed < 100; seed++)
            {
                if (rules.ShouldSpawn(biome, kind, lightLevel: 15, seed)) hits.Add(seed);
            }
            return hits;
        }

        [Test]
        public void MobAI_NineNewPassiveKinds_BehaveLikePig()
        {
            var night = new TimeOfDay { CurrentTick = 15000 };
            foreach (var kind in NewPassiveKinds)
            {
                // 夜里玩家贴脸也不追（被动：不进 Chasing）
                var m = NewPassiveMob(kind);
                MobAI.Tick(m, new Float3(1, 64, 0), null, night, 0.1f, isNight: true);
                Assert.That(m.State, Is.Not.EqualTo(MobState.Chasing),
                    $"{kind} 是被动生物，夜里玩家贴脸也不追击（P0 等价猪组）");

                // 受击触发逃跑（与猪同款：FleeingFromAttacker + FleeDuration 秒窗）
                m = NewPassiveMob(kind);
                MobAI.TakeHit(m, new Float3(0, 64, 0), 1f);
                Assert.That(m.State, Is.EqualTo(MobState.FleeingFromAttacker),
                    $"{kind} 受击应进逃跑状态（P0 等价猪组，Tick/TakeHit 两处 switch 都要接）");
                Assert.That(m.FleeUntil, Is.EqualTo(MobAI.FleeDuration),
                    $"{kind} 逃跑窗长度应与猪一致（{MobAI.FleeDuration}s）");

                // 逃跑窗烧完回落 wander 流（先站定 Idle）。必须拆两步 tick：过期分支把
                // WanderCooldown 重置为 2f 后，同一次 tick 的 dt 也会立刻烧它——单步
                // dt>3 必然 >2，同帧连跳到 Wander，观察不到 Idle 落点（生产是 60fps 小步长）
                MobAI.Tick(m, new Float3(0, 64, 0), null, night, 1.5f, isNight: true);
                MobAI.Tick(m, new Float3(0, 64, 0), null, night, 1.6f, isNight: true);
                Assert.That(m.State, Is.EqualTo(MobState.Idle),
                    $"{kind} 逃跑 {MobAI.FleeDuration}s 到期应回落 Idle（wander 流）");
            }
        }

        [Test]
        public void MobAI_SkeletonSpiderCreeper_W11专属行为已替换_昼夜与受击语义不回退()
        {
            var night = new TimeOfDay { CurrentTick = 15000 };
            foreach (var kind in NewHostileKinds)
            {
                // 夜间 15 格（<20 追击半径）三敌对都进入交战（骷髅在窗口外逼近、
                // 蜘蛛直线追、苦力怕贴身导向）。专属行为的细节断言在 HostileAiTests。
                var m = Mob.Create((int)kind, new Float3(0, 64, 0));
                MobAI.Tick(m, new Float3(15, 64, 0), null, night, 0.1f, isNight: true);
                Assert.That(m.State, Is.EqualTo(MobState.Chasing),
                    $"{kind} 夜间 20 格内应交战（专属 AI 不改追击半径语义）");

                // 白天（isNight=false）不追，回落被动流
                var day = Mob.Create((int)kind, new Float3(0, 64, 0));
                MobAI.Tick(day, new Float3(15, 64, 0), null, night, 0.1f, isNight: false);
                Assert.That(day.State, Is.Not.EqualTo(MobState.Chasing),
                    $"{kind} 白天不追（威胁只在夜里成立，W1-1 不改昼夜门）");

                // 敌对受击不逃（等价 Hostile/Zombie 语义）
                var h = Mob.Create((int)kind, new Float3(0, 64, 0));
                MobAI.TakeHit(h, new Float3(0, 64, 0), 1f);
                Assert.That(h.State, Is.Not.EqualTo(MobState.FleeingFromAttacker),
                    $"{kind} 敌对受击不逃");
            }
        }

        /// <summary>被动 kind 的测试实体（W1-1 起 15-23 已进 Mob.Create 表，直接建档创建）。</summary>
        private static Mob NewPassiveMob(MobKind kind)
        {
            return Mob.Create((int)kind, new Float3(0, 64, 0));
        }

        private static string RealSpawnRulesPath()
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
    }
}
