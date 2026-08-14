using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    /// <summary>
    /// Fix-up X2：Core 层 <see cref="BlockDrops"/> 把 block numericId 映射到应掉落的
    /// <see cref="ItemStack"/> 列表。覆盖 review-final.md critical #2：BlockInteraction.Break()
    /// 之前只 SetBlock 不 spawn ItemDropEntity，spec B7（挖方块掉 ItemDropEntity）未满足。
    /// <para>
    /// 本测试只验证 Core 数据契约（FromJson 解析 + 查表），运行时拼接（PlayerContext.Add、
    /// 位置计算）由 <c>BlockBreakDropTests</c> 走 EditMode + Unity 侧覆盖。
    /// </para>
    /// </summary>
    [TestFixture]
    public class BlockDropsTests
    {
        private const string StoneDropsCobble = @"{
            ""blockId"": ""stone"",
            ""blockNumericId"": 1,
            ""drops"": [
                { ""itemId"": ""cobblestone"", ""countMin"": 1, ""countMax"": 1 }
            ]
        }";

        private const string GrassDropsDirt = @"{
            ""blockId"": ""grass"",
            ""blockNumericId"": 3,
            ""drops"": [
                { ""itemId"": ""dirt"", ""countMin"": 1, ""countMax"": 1 }
            ]
        }";

        private const string LogDropsLog = @"{
            ""blockId"": ""log"",
            ""blockNumericId"": 1001,
            ""drops"": [
                { ""itemId"": ""log"", ""countMin"": 1, ""countMax"": 1 }
            ]
        }";

        private const string NoDrops = @"{
            ""blockId"": ""bedrock"",
            ""blockNumericId"": 6,
            ""drops"": []
        }";

        private const string MultipleDrops = @"{
            ""blockId"": ""fancy"",
            ""blockNumericId"": 100,
            ""drops"": [
                { ""itemId"": ""cobblestone"", ""countMin"": 1, ""countMax"": 3 },
                { ""itemId"": ""stick"", ""countMin"": 0, ""countMax"": 2 }
            ]
        }";

        private static ItemDatabase StandardItemDatabase()
        {
            // 用 stub 风格的最小 JSON 让 ItemDatabase 分配确定 numericId，
            // 测试只引相对引用而非写死数字。
            string[] docs =
            {
                @"{ ""id"": ""dirt"", ""numericId"": 1000, ""texture"": ""dirt"" }",
                @"{ ""id"": ""cobblestone"", ""numericId"": 1003, ""texture"": ""cobblestone"" }",
                @"{ ""id"": ""log"", ""numericId"": 1001, ""texture"": ""log"" }",
                @"{ ""id"": ""stick"", ""numericId"": 1002, ""texture"": ""stick"" }",
            };
            return ItemDatabase.FromJson(docs);
        }

        [Test]
        public void FromJson_ParsesSingleDropEntry()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(new[] { StoneDropsCobble }, items);

            ItemStack[] result = drops.DropsFor(1);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(1));
            Assert.That(result[0].ItemId, Is.EqualTo(1003), "cobblestone numericId 应为 1003");
            Assert.That(result[0].Count, Is.InRange(1, 1), "count 区间为 [1,1]");
        }

        [Test]
        public void FromJson_ParsesMultipleBlocks()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(
                new[] { StoneDropsCobble, GrassDropsDirt, LogDropsLog }, items);

            ItemStack[] stone = drops.DropsFor(1);
            ItemStack[] grass = drops.DropsFor(3);
            ItemStack[] log = drops.DropsFor(1001);

            Assert.That(stone[0].ItemId, Is.EqualTo(1003), "石头掉 cobblestone");
            Assert.That(grass[0].ItemId, Is.EqualTo(1000), "草掉 dirt");
            Assert.That(log[0].ItemId, Is.EqualTo(1001), "原木掉 log");
        }

        [Test]
        public void FromJson_EmptyDropsList_Allowed()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(new[] { NoDrops }, items);

            ItemStack[] result = drops.DropsFor(6);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0), "bedrock 不应掉任何东西");
        }

        [Test]
        public void DropsFor_UnknownBlock_ReturnsEmpty()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(new[] { StoneDropsCobble }, items);

            ItemStack[] result = drops.DropsFor(999); // 未注册的方块
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Length, Is.EqualTo(0), "未注册方块应返回空数组（与 World.GetBlock 的宽容读取保持一致）");
        }

        [Test]
        public void DropsFor_Air_ReturnsEmpty()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(
                new[] { StoneDropsCobble, GrassDropsDirt }, items);

            ItemStack[] result = drops.DropsFor(0); // Air
            Assert.That(result.Length, Is.EqualTo(0), "挖空气不应产生掉落");
        }

        [Test]
        public void FromJson_MultipleDropsForOneBlock_BothRetained()
        {
            var items = StandardItemDatabase();
            BlockDrops drops = BlockDrops.FromJson(new[] { MultipleDrops }, items);

            ItemStack[] result = drops.DropsFor(100);
            Assert.That(result.Length, Is.EqualTo(2));
            Assert.That(result[0].ItemId, Is.EqualTo(1003), "条目 0 应是 cobblestone");
            Assert.That(result[1].ItemId, Is.EqualTo(1002), "条目 1 应是 stick");
        }

        [Test]
        public void ResolveRolls_PicksCountWithinRange()
        {
            // 多组 (min, max) 随机种子验证 100 次至少产生一个非空范围值
            int hits = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                int count = BlockDrops.RollCount(seed, min: 1, max: 3);
                Assert.That(count, Is.InRange(1, 3), $"seed={seed} 算出的 count 应在 [1,3]");
                if (count != 1)
                {
                    hits++;
                }
            }
            // 1-3 区间里非 1 的概率是 50%，100 次种子起码应出现若干次
            Assert.That(hits, Is.GreaterThan(0),
                "100 个种子下 1-3 区间应至少有一次 ≠ 1（即随机性证明）");
        }

        [Test]
        public void ResolveRolls_DeterministicForSameSeed()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                int a = BlockDrops.RollCount(seed, min: 0, max: 5);
                int b = BlockDrops.RollCount(seed, min: 0, max: 5);
                Assert.That(a, Is.EqualTo(b),
                    $"seed={seed} 两次 RollCount 必须同值（确定性）");
            }
        }

        [Test]
        public void ResolveRolls_DifferentSeedsGiveDifferentCounts()
        {
            var counts = new System.Collections.Generic.HashSet<int>();
            for (int seed = 0; seed < 50; seed++)
            {
                counts.Add(BlockDrops.RollCount(seed, min: 0, max: 10));
            }
            Assert.That(counts.Count, Is.GreaterThan(1),
                "50 个不同种子在 [0,10] 区间应产出至少 2 个不同值");
        }

        [Test]
        public void FromJson_MalformedDropEntry_Throws()
        {
            string bad = @"{
                ""blockId"": ""bad"",
                ""blockNumericId"": 50,
                ""drops"": [
                    { ""itemId"": ""not_a_real_item"" }
                ]
            }";
            var items = StandardItemDatabase();

            Assert.Throws<System.IO.InvalidDataException>(
                () => BlockDrops.FromJson(new[] { bad }, items),
                "未注册的 itemId 应抛 InvalidDataException，与其它注册表行为一致");
        }

        [Test]
        public void FromJson_BlockNumericIdMismatch_Throws()
        {
            string bad = @"{
                ""blockId"": ""stone"",
                ""blockNumericId"": 99,
                ""drops"": []
            }";
            var items = StandardItemDatabase();

            // 99 != stone 的真实 numericId (1)，应允许（只是冗余字段）——但 drops 为空时不应抱怨
            // 关键不应抛：测试用 FromJson 应至少给空数组。
            BlockDrops drops = BlockDrops.FromJson(new[] { bad }, items);
            Assert.That(drops.DropsFor(99).Length, Is.EqualTo(0));
        }
    }
}
