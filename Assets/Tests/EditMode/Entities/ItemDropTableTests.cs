using System;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using NUnit.Framework;

// 测试所在 namespace 是 MyWorld.Core.Tests.Entities，与 Entities 同名段冲突，
// 用别名区分两个 ItemDropTable：CoreItemDropTable（Items/静态）+ MobDropTable（Entities/JSON 实例）。
using CoreItemDropTable = MyWorld.Core.Items.ItemDropTable;
using MobDropTable = MyWorld.Core.Entities.ItemDropTable;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// Phase D 第二批测试：JSON 驱动的 <see cref="MobDropTable"/>。
    /// 数据从 <c>Assets/StreamingAssets/mobs/drop_tables.json</c> 加载，
    /// <see cref="MobDropTable.Roll"/> 根据每条 <see cref="DropEntry.Chance"/> 概率掉落。
    /// </summary>
    [TestFixture]
    public class ItemDropTableTests
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
        public void Chicken_DropsFeather()
        {
            var table = MobDropTable.Load(DropTablesPath());
            var drop = table.Roll(MobKind.Chicken, seed: 42);
            Assert.That(drop.HasValue, Is.True, "鸡死亡应掉出 ItemStack");
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
    }
}
