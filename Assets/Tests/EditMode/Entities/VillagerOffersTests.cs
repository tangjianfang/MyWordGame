// X3 fix-up：原 VillagerManager.BuildOffersFor 只返回 2 条交易（spec D7 要求 3-5）。
// 现搬到 Core 层 VillagerOffers.Build，按 (worldX, worldZ, profession) 哈希挑 3-5 条，
// 既满足 spec，又让交易模板可被 dotnet 链独立验证。
using MyWorld.Core.Entities;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    /// <summary>
    /// X3 fix-up：BuildOffersFor 升到 3-5 条交易，搬到 Core 层使其可被 dotnet 测试验证。
    /// </summary>
    [TestFixture]
    public class VillagerOffersTests
    {
        [Test]
        public void Build_Farmer_ReturnsBetweenThreeAndFiveOffers()
        {
            var offers = VillagerOffers.Build(VillagerProfession.Farmer, worldX: 0, worldZ: 0);
            Assert.That(offers.Length, Is.InRange(3, 5),
                $"Farmer 应返回 3-5 条交易（实际 {offers.Length}）");
        }

        [Test]
        public void Build_Librarian_ReturnsBetweenThreeAndFiveOffers()
        {
            var offers = VillagerOffers.Build(VillagerProfession.Librarian, worldX: 0, worldZ: 0);
            Assert.That(offers.Length, Is.InRange(3, 5),
                $"Librarian 应返回 3-5 条交易（实际 {offers.Length}）");
        }

        [Test]
        public void Build_Blacksmith_ReturnsBetweenThreeAndFiveOffers()
        {
            var offers = VillagerOffers.Build(VillagerProfession.Blacksmith, worldX: 0, worldZ: 0);
            Assert.That(offers.Length, Is.InRange(3, 5),
                $"Blacksmith 应返回 3-5 条交易（实际 {offers.Length}）");
        }

        [Test]
        public void Build_Deterministic_SamePositionReturnsSameOffers()
        {
            // 同一坐标 + 同一职业应总是返回同样的 offer 列表（位置哈希稳定）。
            var a = VillagerOffers.Build(VillagerProfession.Farmer, 100, 200);
            var b = VillagerOffers.Build(VillagerProfession.Farmer, 100, 200);
            Assert.That(a.Length, Is.EqualTo(b.Length),
                "同一 (wx, wz, profession) 应返回相同数量的 offer");
            for (int i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].BuyItem, Is.EqualTo(b[i].BuyItem),
                    $"第 {i} 条 buy 物品应一致");
                Assert.That(a[i].SellItem, Is.EqualTo(b[i].SellItem),
                    $"第 {i} 条 sell 物品应一致");
                Assert.That(a[i].BuyCount, Is.EqualTo(b[i].BuyCount),
                    $"第 {i} 条 buy 数量应一致");
                Assert.That(a[i].SellCount, Is.EqualTo(b[i].SellCount),
                    $"第 {i} 条 sell 数量应一致");
            }
        }

        [Test]
        public void Build_DifferentPositions_CanProduceDifferentOfferSets()
        {
            // 不同位置应至少出现一次 offer 不同（验证位置真的影响结果，不是常量）。
            var positions = new (int x, int z)[]
            {
                (0, 0), (10, 20), (100, 200), (-50, 30), (500, 500), (1024, -1024),
            };
            var first = VillagerOffers.Build(VillagerProfession.Farmer, positions[0].x, positions[0].z);
            bool sawDifferent = false;
            for (int i = 1; i < positions.Length; i++)
            {
                var p = VillagerOffers.Build(VillagerProfession.Farmer, positions[i].x, positions[i].z);
                if (p.Length != first.Length || p[0].BuyItem != first[0].BuyItem || p[0].SellItem != first[0].SellItem)
                {
                    sawDifferent = true;
                    break;
                }
            }
            Assert.That(sawDifferent, Is.True,
                "不同 (wx, wz) 应至少出现一次 offer 不同（验证哈希真起作用）");
        }

        [Test]
        public void Build_NoneProfession_ReturnsFallback()
        {
            // None 职业保持原行为，返回 1 条 log 兜底交易。
            var offers = VillagerOffers.Build(VillagerProfession.None, 0, 0);
            Assert.That(offers.Length, Is.EqualTo(1),
                "None 职业应回退到 1 条兜底交易（保持原行为）");
            Assert.That(offers[0].SellItem, Is.EqualTo("log"));
        }

        [Test]
        public void Build_DifferentProfessions_ReturnDifferentPools()
        {
            // Farmer/Librarian/Blacksmith 应各自有一组不同模板（至少第一条不同）。
            var farmerFirst = VillagerOffers.Build(VillagerProfession.Farmer, 0, 0);
            var librarianFirst = VillagerOffers.Build(VillagerProfession.Librarian, 0, 0);
            var blacksmithFirst = VillagerOffers.Build(VillagerProfession.Blacksmith, 0, 0);
            Assert.That(librarianFirst[0].BuyItem, Is.Not.EqualTo(farmerFirst[0].BuyItem)
                .Or.Not.EqualTo(librarianFirst[0].SellItem),
                "Librarian 与 Farmer 应至少有一条 offer 不同");
            Assert.That(blacksmithFirst[0].BuyItem, Is.Not.EqualTo(farmerFirst[0].BuyItem)
                .Or.Not.EqualTo(blacksmithFirst[0].SellItem),
                "Blacksmith 与 Farmer 应至少有一条 offer 不同");
        }

        [Test]
        public void 煤获取链_农夫卖绿宝石_铁匠绿宝石换煤()
        {
            // m6 终审修 I3：煤无掉落、无配方，唯一来源是 Blacksmith 的
            // 「2 绿宝石 → 8 煤」；而绿宝石此前全表无来源（Librarian 的 book 换绿宝石
            // 也没有配方可用）——链断在第一环。农夫池必须保留一条「用可获得的物品
            // （木板，可由原木合成）换绿宝石」的交易，首章任务 7 才真的可达。
            bool farmerSellsEmerald = false;
            foreach (var o in VillagerOffers.FarmerPool)
            {
                if (o.SellItem == "emerald" && o.BuyItem == "plank")
                {
                    farmerSellsEmerald = true;
                }
            }
            Assert.That(farmerSellsEmerald, Is.True,
                "农夫池应有「木板 → 绿宝石」交易（原方向 emerald→plank 会让绿宝石无来源，任务 7 的煤不可达）");

            bool blacksmithSellsCoal = false;
            foreach (var o in VillagerOffers.BlacksmithPool)
            {
                if (o.SellItem == "coal" && o.BuyItem == "emerald")
                {
                    blacksmithSellsCoal = true;
                }
            }
            Assert.That(blacksmithSellsCoal, Is.True,
                "铁匠池应有「绿宝石 → 煤」交易（煤的唯一来源，desc 指引指向这里）");
        }
    }
}