// m11 P0（前置·串行独占）：12 新生物枚举与分派预接线的 Core 侧守卫。
// P0 只接线、不写专属 AI：9 被动 kind（Sheep/Rabbit/Fox/Deer/Panda/Penguin/Goat/
// Raccoon/Hamster）行为等价猪组（wander + 受击逃 3s）；骷髅/蜘蛛/苦力怕暂等价
// 僵尸组（夜里追、白天不追）——专属行为由 W1 代理（W1-1/W1-2）替换。
// 安全性依据：真实 spawn_rules.json 尚未加这 12 个名字的条目，ShouldSpawn 恒 false，
// 接线后不会真的刷出（模型 mobs/models/*.json 同批未就绪，UsesPartTable=true 的
// 部位表拼装路径不会被走到）；等 W1 代理补条目 + 模型 JSON 后自然生效——
// PickKind_RealRules_NewKindsNotConfigured_NeverSpawn 守卫这条性质。
// MobManager 侧（UsesPartTable/MobKindToTypeId/昼夜候选表）的映射守卫在
// Combat/MobManagerSpawnTests.cs（MobManager 是 MonoBehaviour，编辑器链专用）。
using System;
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
        public void PickKind_RealRules_NewKindsNotConfigured_NeverSpawn()
        {
            // P0 安全性依据的守卫：真实 spawn_rules.json 尚未加 12 新名条目 →
            // 未配置的 kind ShouldSpawn 恒 false → MobManager 昼夜候选表带着它们
            // 也不会真的刷出（模型 JSON 未就绪前这是唯一的安全闸）。
            var rules = MobSpawnRules.Load(RealSpawnRulesPath());
            foreach (var (name, kind, _) in NewKinds)
            {
                for (int seed = 0; seed < 50; seed++)
                {
                    Assert.That(rules.ShouldSpawn(Biome.Plains, kind, lightLevel: 15, seed), Is.False,
                        $"{name} 在真实 spawn_rules.json 尚无条目，白天不应刷出（P0 只预接线不投放）");
                    Assert.That(rules.ShouldSpawn(Biome.Forest, kind, lightLevel: 0, seed), Is.False,
                        $"{name} 在真实 spawn_rules.json 尚无条目，夜间不应刷出（P0 只预接线不投放）");
                }
            }
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
        public void MobAI_SkeletonSpiderCreeper_EquivalentToZombieInP0()
        {
            var night = new TimeOfDay { CurrentTick = 15000 };
            foreach (var kind in NewHostileKinds)
            {
                // 夜间 15 格（<20 追击半径）应追击。Chasing 也证明没误入旧苦力怕
                // 引信分支——TickCreeper 从不置 Chasing。
                var m = NewHostileMob(kind);
                MobAI.Tick(m, new Float3(15, 64, 0), null, night, 0.1f, isNight: true);
                Assert.That(m.State, Is.EqualTo(MobState.Chasing),
                    $"{kind} P0 等价僵尸：夜间 20 格内应追击");

                // 贴脸 2 格（<3 旧引信触发半径）：不该走旧苦力怕引信（专属自爆 W1-1 再实现）
                var close = NewHostileMob(kind);
                MobAI.Tick(close, new Float3(2, 64, 0), null, night, 0.1f, isNight: true);
                Assert.That(close.State, Is.EqualTo(MobState.Chasing),
                    $"{kind} P0 走僵尸近战分支，贴脸也应处于 Chasing");
                Assert.That(close.FuseTimer, Is.EqualTo(0f),
                    $"{kind} P0 不进旧苦力怕引信（FuseTimer 不动）");

                // 白天（isNight=false）不追，回落被动流
                var day = NewHostileMob(kind);
                MobAI.Tick(day, new Float3(15, 64, 0), null, night, 0.1f, isNight: false);
                Assert.That(day.State, Is.Not.EqualTo(MobState.Chasing),
                    $"{kind} P0 等价僵尸：白天不追（威胁只在夜里成立）");

                // 敌对受击不逃（等价 Hostile/Zombie 语义）
                var h = NewHostileMob(kind);
                MobAI.TakeHit(h, new Float3(0, 64, 0), 1f);
                Assert.That(h.State, Is.Not.EqualTo(MobState.FleeingFromAttacker),
                    $"{kind} 敌对受击不逃（P0 等价僵尸）");
            }
        }

        /// <summary>被动 kind 的测试实体（新 kind 不在 Mob.Create 表内，直接按字段构造，模式抄 MobKindVillagerTests.NewVillagerMob）。</summary>
        private static Mob NewPassiveMob(MobKind kind)
        {
            return new Mob
            {
                MobTypeId = (int)kind, // P0 约定：新 kind 的 mobTypeId = 枚举数值
                Kind = kind,
                Health = new Health(10),
                Position = new Float3(0, 64, 0),
                WanderCooldown = 2f,
                MoveSpeed = 1.5f,
            };
        }

        /// <summary>敌对 kind 的测试实体（参数照抄 Mob.Create(9) 的新僵尸：射程 4 / 追击 20）。</summary>
        private static Mob NewHostileMob(MobKind kind)
        {
            return new Mob
            {
                MobTypeId = (int)kind, // ≠5：Mob.IsCreeper 判定不成立，P0 走僵尸分支
                Kind = kind,
                Health = new Health(20),
                Position = new Float3(0, 64, 0),
                AttackDamage = 2f,
                AttackRange = 4f,
                ChaseRadius = 20f,
                WanderCooldown = 2f,
                MoveSpeed = 3.5f,
            };
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
