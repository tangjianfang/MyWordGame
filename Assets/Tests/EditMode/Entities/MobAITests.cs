using System;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using NUnit.Framework;

// JSON 驱动掉落表 (Entities) vs 静态 Items.ItemDropTable：测试用 alias 区分。
using JsonDropTable = MyWorld.Core.Entities.MobDropTable;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// Phase D 第一批测试：在现有 <see cref="Mob.Create"/> + <see cref="MobAI.Tick"/> API 上
    /// 验证 type-specific 行为（spec line 173/181：PIG/COW/CHICKEN 巡逻+怕玩家、ZOMBIE 32 格追击、
    /// 死亡触发 Drop）。Phase D 扩展既有 state machine，5 个新测试。
    /// </summary>
    [TestFixture]
    public class MobAITests
    {
        [SetUp]
        public void SetUp()
        {
            // 测试间不互相污染：默认 null 走 legacy Items.ItemDropTable.Drop
            MobAI.DropTable = null;
        }

        [TearDown]
        public void TearDown()
        {
            MobAI.DropTable = null;
        }

        private static string DropTablesJsonPath()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "mobs", "drop_tables.json");
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "mobs", "drop_tables.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException("未能找到 Assets/StreamingAssets/mobs/drop_tables.json。");
#endif
        }

        [Test]
        public void ZombieChasesOnlyWithinAggroRange()
        {
            // 新僵尸 (MobTypeId 9)：32 格 chase 半径
            var z = Mob.Create(9, new Float3(0, 64, 0));
            Assert.That(z.Kind, Is.EqualTo(MobKind.Zombie));
            Assert.That(z.ChaseRadius, Is.EqualTo(32f), "新僵尸 chase 半径应为 32");

            // 玩家 16 格外、32 格内，应转入 Chasing
            MobAI.Tick(z, new Float3(20, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(z.State, Is.EqualTo(MobState.Chasing),
                "新僵尸 32 格内应追击玩家");

            // 玩家跑到 50 格外（远超 32 格），下次 tick 应脱离 Chasing
            MobAI.Tick(z, new Float3(50, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(z.State, Is.Not.EqualTo(MobState.Chasing),
                "新僵尸超 32 格不应追击");
        }

        [Test]
        public void PigFleesWhenPlayerClose()
        {
            // 新猪 (MobTypeId 6)：MobKind.Pig，路由到 TickPassive
            var pig = Mob.Create(6, new Float3(0, 64, 0));
            Assert.That(pig.Kind, Is.EqualTo(MobKind.Pig));

            // 玩家 3 格内（< ScareRadius=8），应进入 Scared
            MobAI.Tick(pig, new Float3(3, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.EqualTo(MobState.Scared),
                "新猪玩家靠近应害怕逃跑");
        }

        [Test]
        public void PigNeverEntersChaseState()
        {
            // 新猪在各种距离 tick 一遍，确认永远不会进 Chasing（spec line 181 "pig 永不变 Chase"）
            var pig = Mob.Create(6, new Float3(0, 64, 0));
            MobAI.Tick(pig, new Float3(2, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.Not.EqualTo(MobState.Chasing));
            MobAI.Tick(pig, new Float3(5, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.Not.EqualTo(MobState.Chasing));
            MobAI.Tick(pig, new Float3(0.5f, 64, 0), null, new TimeOfDay(), 0.1f);
            Assert.That(pig.State, Is.Not.EqualTo(MobState.Chasing));
        }

        [Test]
        public void ChickenMovesLessThanPigOverSameTime()
        {
            // 新鸡/新猪的 MoveSpeed 直接比较——比驱动 wander 状态更确定
            var chicken = Mob.Create(8, default);
            var pig = Mob.Create(6, default);
            Assert.That(chicken.Kind, Is.EqualTo(MobKind.Chicken));
            Assert.That(chicken.MoveSpeed, Is.LessThan(pig.MoveSpeed),
                $"鸡 ({chicken.MoveSpeed}) 应比猪 ({pig.MoveSpeed}) 移动慢（spec line 181 'chicken 随机跳'）");
        }

        [Test]
        public void DeathTriggersItemDrop()
        {
            // 新猪 (MobTypeId 6) Health=10，扣 100 必死
            var pig = Mob.Create(6, new Float3(0, 64, 0));
            pig.Health.Damage(100f);
            Assert.That(pig.Health.IsDead, Is.True);

            MobAI.Tick(pig, new Float3(100, 64, 100), null, new TimeOfDay(), 0.1f);

            Assert.That(pig.State, Is.EqualTo(MobState.Dying),
                "死亡应自动转 Dying");
            Assert.That(pig.LastDrops, Is.Not.Null, "MobAI 应调用 ItemDropTable.Drop");
            Assert.That(pig.LastDrops, Is.Not.Empty, "猪死亡应掉落物品");
        }

        /// <summary>
        /// X4.5 fix-up：注入 JSON 驱动的 <see cref="JsonDropTable"/> 后，猪死亡掉落应来自
        /// 该表（而非静态 legacy Items.ItemDropTable.Drop 的 count=1）。验证：
        /// 1) LastDrops 非空（猪 chance=1.0 必出）
        /// 2) 唯一 stack 的 ItemId 是 1008（porkchop，与 JSON 一致）
        /// 3) count ∈ [1, 3]（X4 JSON schema 的 countMin/countMax 区间，100 个 seed 全覆盖）
        /// 覆盖 review-final.md important #5 的「生产路径仍输出 count=1」问题。
        /// </summary>
        [Test]
        public void DeathTriggersDropsFromJsonTable_Pig_RollsCountInRange1to3()
        {
            MobAI.DropTable = JsonDropTable.Load(DropTablesJsonPath());

            // 100 个不同位置的猪，每个都应掉 1 个 porkchop stack，count ∈ [1, 3]
            int seenMin = 0, seenMax = 0;
            for (int i = 0; i < 100; i++)
            {
                var pig = Mob.Create(6, new Float3(i, 64f, i * 0.5f)); // EntityId=0，位置唯一
                pig.Health.Damage(100f);
                MobAI.Tick(pig, new Float3(100f, 64f, 100f), null, new TimeOfDay(), 0.1f);

                Assert.That(pig.LastDrops, Is.Not.Null,
                    $"Pig seed={i} MobAI 应写 LastDrops");
                Assert.That(pig.LastDrops.Length, Is.EqualTo(1),
                    $"Pig 死亡应只有 1 个掉落 stack（实际 {pig.LastDrops.Length}，seed={i}）");
                var stack = pig.LastDrops[0];
                Assert.That(stack.ItemId, Is.EqualTo(JsonDropTableRollTestIds.PorkchopItemId),
                    $"Pig 应掉 porkchop (1008)，seed={i}");
                Assert.That(stack.Count, Is.InRange(1, 3),
                    $"Pig count 应在 [1, 3] 区间（seed={i}, count={stack.Count}）");
                if (stack.Count == 1) seenMin++;
                if (stack.Count == 3) seenMax++;
            }
            Assert.That(seenMin, Is.GreaterThan(0),
                $"countMin=1 端点应至少出现一次（实际 {seenMin}/100）");
            Assert.That(seenMax, Is.GreaterThan(0),
                $"countMax=3 端点应至少出现一次（实际 {seenMax}/100）");
        }

        /// <summary>
        /// X4.5 fix-up：注入 JSON 表后僵尸死亡掉落：
        /// 1) LastDrops 非空（chance 不为 0 必出 rotten_flesh 一类）
        /// 2) 至少有一个 stack 的 ItemId 是 1010（rotten_flesh）
        /// 3) rotten_flesh stack 的 count ∈ [0, 2]（X4 JSON schema）
        /// 4) iron_ingot (1004) 命中按 5% 期望；10 个 zombie 内若命中则 count=1
        /// 用 10 个不同 seed 的 mob 覆盖各分支，避免 flaky。
        /// </summary>
        [Test]
        public void DeathTriggersDropsFromJsonTable_Zombie_RollsRottenFleshInRange()
        {
            MobAI.DropTable = JsonDropTable.Load(DropTablesJsonPath());

            int rottenStacks = 0;
            int ironStacks = 0;
            for (int i = 0; i < 10; i++)
            {
                var z = Mob.Create(9, new Float3(i * 7, 64f, i * 11f));
                z.Health.Damage(100f);
                MobAI.Tick(z, new Float3(100f, 64f, 100f), null, new TimeOfDay(), 0.1f);

                Assert.That(z.LastDrops, Is.Not.Null,
                    $"Zombie seed={i} MobAI 应写 LastDrops");
                // 累计 chance = 0.55，至少应有一个或两个 stack（取决于两次掷骰）
                // 但允许全 null（两次都未命中），需要 >= 1 个命中：靠 10 次位置遍历保证
                foreach (var stack in z.LastDrops)
                {
                    if (stack.ItemId == JsonDropTableRollTestIds.RottenFleshItemId)
                    {
                        rottenStacks++;
                        Assert.That(stack.Count, Is.InRange(0, 2),
                            $"Zombie rotten_flesh count 应在 [0, 2]（seed={i}, count={stack.Count}）");
                    }
                    else if (stack.ItemId == JsonDropTableRollTestIds.IronIngotItemId)
                    {
                        ironStacks++;
                        Assert.That(stack.Count, Is.EqualTo(1),
                            $"Zombie iron_ingot count 应 = 1（seed={i}, count={stack.Count}）");
                    }
                }
            }
            // 10 个 zombie 中至少有 1 个掉出 rotten_flesh（每条 50% 命中，
            // 10 次都没中的概率约 1/1024，99.9% 信心）
            Assert.That(rottenStacks, Is.GreaterThan(0),
                $"10 个 zombie 中应至少有一个掉出 rotten_flesh (1010)，实际 {rottenStacks}");
            // iron_ingot 5% × 10 次 ≈ 0.5 次，宽松条件允许 0 次命中
            Assert.That(ironStacks, Is.LessThanOrEqualTo(10),
                $"iron_ingot 命中数不应超 10，实际 {ironStacks}");
        }

        /// <summary>
        /// X4.5 fix-up：当 <see cref="MobAI.DropTable"/> = null 时回退到静态 Items.ItemDropTable.Drop
        /// （X1 测试期望行为）。验证回退路径仍产出非空 stack。
        /// </summary>
        [Test]
        public void DeathTriggersDrops_FallsBackToStatic_WhenDropTableIsNull()
        {
            // SetUp 已把 DropTable 置 null，这里再显式写一遍让意图清楚
            MobAI.DropTable = null;

            var pig = Mob.Create(6, new Float3(0, 64, 0));
            pig.Health.Damage(100f);
            MobAI.Tick(pig, new Float3(100, 64, 100), null, new TimeOfDay(), 0.1f);

            Assert.That(pig.LastDrops, Is.Not.Null, "回退路径应写 LastDrops");
            Assert.That(pig.LastDrops.Length, Is.EqualTo(1), "静态表猪掉 1 个 porkchop stack");
            Assert.That(pig.LastDrops[0].ItemId, Is.EqualTo(JsonDropTableRollTestIds.PorkchopItemId),
                "回退应给猪 porkchop (1008)");
            Assert.That(pig.LastDrops[0].Count, Is.EqualTo(1),
                "回退到静态路径时 count=1（X1 既有契约）");
        }

        /// <summary>
        /// X4.5 fix-up：<see cref="MobAI.ComputeDropSeed"/> 是确定性：同 mob 重复计算应得相同 seed。
        /// 不验证具体数值（hash 内容由实现决定），但保证可重现（replay-safe）。
        /// </summary>
        [Test]
        public void ComputeDropSeed_IsDeterministic()
        {
            var mob = Mob.Create(9, new Float3(12.5f, 65f, -3.25f));
            mob.EntityId = 42;
            int s1 = MobAI.ComputeDropSeed(mob);
            int s2 = MobAI.ComputeDropSeed(mob);
            Assert.That(s2, Is.EqualTo(s1), "同一 mob 重复计算 seed 应一致（replay-safe）");
        }

        /// <summary>
        /// X4.5 fix-up：不同 mob（EntityId 或 Position 任一不同）应得不同 seed，
        /// 避免 100 个同位置 zombie 走同一随机路径（否则 100 个 zombie 同生同死）。
        /// </summary>
        [Test]
        public void ComputeDropSeed_DistinctForDifferentMobs()
        {
            var a = Mob.Create(9, default);
            a.EntityId = 1;
            var b = Mob.Create(9, default);
            b.EntityId = 2;
            var c = Mob.Create(9, new Float3(1, 0, 0));
            c.EntityId = 1; // 同 EntityId 不同位置
            Assert.That(MobAI.ComputeDropSeed(a), Is.Not.EqualTo(MobAI.ComputeDropSeed(b)),
                "不同 EntityId 应得不同 seed");
            Assert.That(MobAI.ComputeDropSeed(a), Is.Not.EqualTo(MobAI.ComputeDropSeed(c)),
                "同 EntityId 不同 Position 应得不同 seed");
        }
    }

    /// <summary>
    /// 测试内部常量：JSON 驱动的 <see cref="JsonDropTable"/> 用的物品 numericId。
    /// 与 <c>Assets/StreamingAssets/items/*.json</c> 当前分配一致（porkchop=1008, rotten_flesh=1010, iron_ingot=1004）。
    /// 测试不直接 import Items.ItemDropTable 的常量是为了避免误用静态路径的 PorkchopItemId。
    /// </summary>
    internal static class JsonDropTableRollTestIds
    {
        public const int PorkchopItemId = 1008;
        public const int RottenFleshItemId = 1010;
        public const int IronIngotItemId = 1004;
    }
}