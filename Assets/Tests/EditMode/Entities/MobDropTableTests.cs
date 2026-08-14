using System;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using NUnit.Framework;

// 测试所在 namespace 是 MyWorld.Core.Tests.Entities，与 Entities 同名段冲突，
// 用别名区分掉落表：CoreItemDropTable（Items/静态）+ MobDropTable（Entities/JSON 实例，F6 重命名）。
using CoreItemDropTable = MyWorld.Core.Items.ItemDropTable;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// Phase D 第二批测试：JSON 驱动的 <see cref="MobDropTable"/>。
    /// 数据从 <c>Assets/StreamingAssets/mobs/drop_tables.json</c> 加载，
    /// <see cref="MobDropTable.Roll"/> 根据每条 <see cref="DropEntry.Chance"/> 概率掉落。
    /// </summary>
    [TestFixture]
    public class MobDropTableTests
    {
        private static string DropTablesPath()
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
        public void Pig_AlwaysDropsPork()
        {
            var table = MobDropTable.Load(DropTablesPath());
            var drop = table.Roll(MobKind.Pig, seed: 42);
            Assert.That(drop.HasValue, Is.True, "猪死亡应掉出 ItemStack");
            Assert.That(drop.Value.ItemId, Is.GreaterThan(0), "掉落的物品 id 必须 > 0");
        }

        [Test]
        public void Chicken_DropsChicken()
        {
            var table = MobDropTable.Load(DropTablesPath());
            var drop = table.Roll(MobKind.Chicken, seed: 42);
            Assert.That(drop.HasValue, Is.True, "鸡死亡应掉出 ItemStack");
            Assert.That(drop.Value.ItemId, Is.EqualTo(1017), "Chicken 应掉 chicken (1017)，不再是 iron_ingot 占位");
        }

        [Test]
        public void Cow_DropsBeef()
        {
            var table = MobDropTable.Load(DropTablesPath());
            var drop = table.Roll(MobKind.Cow, seed: 42);
            Assert.That(drop.HasValue, Is.True, "牛死亡应掉出 ItemStack");
            Assert.That(drop.Value.ItemId, Is.EqualTo(1016), "Cow 应掉 beef (1016)，不再是 cobblestone 占位");
        }

        [Test]
        public void Pig_DropIsPorkchop()
        {
            // 验证 JSON 的 itemId 与现有 ItemDatabase 一致（porkchop=1008）
            var table = MobDropTable.Load(DropTablesPath());
            var drop = table.Roll(MobKind.Pig, seed: 1);
            Assert.That(drop.HasValue, Is.True);
            Assert.That(drop.Value.ItemId, Is.EqualTo(1008), "Pig 应掉 porkchop (1008)");
        }

        [Test]
        public void Zombie_DropIsRottenFlesh()
        {
            // 验证 Zombie 用现有 ItemDatabase 的 rotten_flesh (1010)
            var table = MobDropTable.Load(DropTablesPath());
            var drop = table.Roll(MobKind.Zombie, seed: 1);
            Assert.That(drop.HasValue, Is.True);
            Assert.That(drop.Value.ItemId, Is.EqualTo(1010), "Zombie 应掉 rotten_flesh (1010)");
        }

        [Test]
        public void Zombie_ChanceBelowOne_MayReturnNull()
        {
            // Zombie chance=0.5：100 个 seed 中至少应有一个返回 null（如果分布合理）
            var table = MobDropTable.Load(DropTablesPath());
            int nullCount = 0;
            for (int i = 0; i < 100; i++)
                if (!table.Roll(MobKind.Zombie, seed: i).HasValue) nullCount++;
            // 0.5 chance → 期望约 50 次 null，留大余量避免 flaky
            Assert.That(nullCount, Is.GreaterThan(0).And.LessThan(100),
                $"Zombie chance=0.5 在 100 个 seed 中应有部分返回 null（实际 {nullCount}/100）");
        }

        /// <summary>
        /// Fix-up X4：review-final.md important #5——Spec D4 规定猪掉 1-3 个 porkchop。
        /// 100 个 seed 多次 Roll，count 必须落在 [1, 3] 闭区间内，且至少出现 1 与 3 两个端点
        /// （分布 `countMin=1, countMax=3` 用整数哈希 RollCount 必然到达两端）。
        /// </summary>
        [Test]
        public void Pig_DropsPorkchop_CountInRange1to3()
        {
            var table = MobDropTable.Load(DropTablesPath());
            int seenMin = 0, seenMax = 0;
            for (int i = 0; i < 100; i++)
            {
                var drop = table.Roll(MobKind.Pig, seed: i);
                Assert.That(drop.HasValue, Is.True, $"Pig seed={i} 应掉 porkchop");
                int count = drop.Value.Count;
                Assert.That(count, Is.InRange(1, 3),
                    $"Pig count 应在 [1, 3] 区间（seed={i}, count={count}）");
                if (count == 1) seenMin++;
                if (count == 3) seenMax++;
            }
            Assert.That(seenMin, Is.GreaterThan(0),
                $"countMin=1 端点应至少出现一次（实际 {seenMin}/100）");
            Assert.That(seenMax, Is.GreaterThan(0),
                $"countMax=3 端点应至少出现一次（实际 {seenMax}/100）");
        }

        /// <summary>
        /// Fix-up X4：review-final.md important #5——Spec D5 规定僵尸掉 0-2 个 rotten_flesh。
        /// 验 count ∈ [0, 2]，且两端都应被命中。只看 rotten_flesh 命中（过滤 1004 iron_ingot）。
        /// </summary>
        [Test]
        public void Zombie_DropsRottenFlesh_CountInRange0to2()
        {
            var table = MobDropTable.Load(DropTablesPath());
            int seenZero = 0, seenTwo = 0, rottenHits = 0;
            for (int i = 0; i < 200; i++)
            {
                var drop = table.Roll(MobKind.Zombie, seed: i);
                if (!drop.HasValue) continue; // chance=0.5，未命中跳过
                if (drop.Value.ItemId != 1010) continue; // 只看 rotten_flesh 区间
                rottenHits++;
                int count = drop.Value.Count;
                Assert.That(count, Is.InRange(0, 2),
                    $"Zombie rotten_flesh count 应在 [0, 2] 区间（seed={i}, count={count}）");
                if (count == 0) seenZero++;
                if (count == 2) seenTwo++;
            }
            Assert.That(rottenHits, Is.GreaterThan(0),
                $"应至少有一次 rotten_flesh 命中（{rottenHits}/200）");
            Assert.That(seenZero, Is.GreaterThan(0),
                $"countMin=0 端点应至少出现一次（实际 {seenZero}）");
            Assert.That(seenTwo, Is.GreaterThan(0),
                $"countMax=2 端点应至少出现一次（实际 {seenTwo}）");
        }

        /// <summary>
        /// Fix-up X4：review-final.md important #5——Spec D5 规定僵尸 5% 概率掉 iron_ingot (1004)。
        /// 1000 次 Roll 中 iron_ingot 命中应在 [25, 100] 区间（5% ± 3 σ 噪声），ItemId 必须是 1004。
        /// </summary>
        [Test]
        public void Zombie_DropsIronIngot_5PercentChance()
        {
            var table = MobDropTable.Load(DropTablesPath());
            int ironHits = 0;
            const int trials = 1000;
            for (int i = 0; i < trials; i++)
            {
                var drop = table.Roll(MobKind.Zombie, seed: i);
                if (drop.HasValue && drop.Value.ItemId == 1004)
                {
                    ironHits++;
                }
            }
            // 5% 期望 50 次，三 σ 区间约 ±2.4%，留 25~100 留余量
            Assert.That(ironHits, Is.InRange(25, 100),
                $"Zombie iron_ingot 5% 命中 1000 次应约 50 次（实际 {ironHits}/1000）");
        }

        /// <summary>
        /// X4.5 fix-up：<see cref="MobDropTable.RollAll"/> 与 <see cref="MobDropTable.Roll"/> 的核心区别——
        /// 每条 entry 独立掷 chance，可同时命中多条。Zombie 200 次 RollAll 中 rotten_flesh
        /// 命中 ≥ 1 + iron_ingot 命中 ≥ 1 应同时出现，证明两条 entry 是独立的骰子
        /// （而非互斥的单一掷骰）。Zombie 累计 chance=0.5 + 0.05，200 次双零概率约
        /// e^(-200×0.5) × e^(-200×0.05) ≈ 0，留大余量。
        /// </summary>
        [Test]
        public void RollAll_Zombie_RottenAndIronIndependent()
        {
            var table = MobDropTable.Load(DropTablesPath());
            int rottenHits = 0, ironHits = 0;
            for (int i = 0; i < 200; i++)
            {
                var drops = table.RollAll(MobKind.Zombie, seed: i);
                foreach (var stack in drops)
                {
                    if (stack.ItemId == 1010) rottenHits++;
                    else if (stack.ItemId == 1004) ironHits++;
                }
            }
            Assert.That(rottenHits, Is.GreaterThan(0),
                $"200 次 RollAll Zombie 应至少出 1 次 rotten_flesh（实际 {rottenHits}）");
            Assert.That(ironHits, Is.GreaterThan(0),
                $"200 次 RollAll Zombie 应至少出 1 次 iron_ingot（实际 {ironHits}）");
            // 独立掷骰：iron_ingot 命中数 ≈ 200×0.05 = 10，留 5σ 余量
            Assert.That(ironHits, Is.LessThanOrEqualTo(50),
                $"iron_ingot 命中不应超 50（实际 {ironHits}）");
        }

        /// <summary>
        /// X4.5 fix-up：<see cref="MobDropTable.RollAll"/> 对未配置的 MobKind（Villager 等）
        /// 返回空数组，不抛异常。Unity 侧 MobAI.Tick 写到 LastDrops 时不能假设非空。
        /// </summary>
        [Test]
        public void RollAll_UnknownKind_ReturnsEmpty()
        {
            var table = MobDropTable.Load(DropTablesPath());
            var drops = table.RollAll(MobKind.Villager, seed: 42);
            Assert.That(drops, Is.Not.Null, "RollAll 不应返回 null");
            Assert.That(drops.Length, Is.EqualTo(0), "Villager 不在 JSON 表中，应返回空数组");
        }

        /// <summary>
        /// X4.5 fix-up：<see cref="MobDropTable.RollAll"/> 对 Pig（chance=1.0）每次都至少返回 1 个
        /// stack，且 stack 的 count 仍在 [countMin, countMax] 区间——独立掷骰不能改变 count 范围。
        /// </summary>
        [Test]
        public void RollAll_Pig_AlwaysReturnsSingleStackInRange()
        {
            var table = MobDropTable.Load(DropTablesPath());
            for (int i = 0; i < 50; i++)
            {
                var drops = table.RollAll(MobKind.Pig, seed: i);
                Assert.That(drops.Length, Is.GreaterThanOrEqualTo(1),
                    $"Pig seed={i} RollAll 应至少 1 个 stack（chance=1.0 必命中）");
                foreach (var stack in drops)
                {
                    Assert.That(stack.ItemId, Is.EqualTo(1008),
                        $"Pig stack 应是 porkchop (1008)，实际 {stack.ItemId}（seed={i}）");
                    Assert.That(stack.Count, Is.InRange(1, 3),
                        $"Pig count 应在 [1, 3]（seed={i}, count={stack.Count}）");
                }
            }
        }
    }
}
