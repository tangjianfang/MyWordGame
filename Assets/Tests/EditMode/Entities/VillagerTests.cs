using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Time;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class VillagerTests
    {
        [Test]
        public void Create_Farmer_HasProfession()
        {
            var v = Villager.Create(1, VillagerProfession.Farmer, new Float3(0, 64, 0));
            Assert.That(v.Profession, Is.EqualTo(VillagerProfession.Farmer));
            Assert.That(v.IsAlive, Is.True);
            Assert.That(v.Offers, Is.Empty);
        }

        [Test]
        public void Create_WithOffers_PopulatesList()
        {
            var offers = new[]
            {
                new TradeOffer("emerald", 1, "bread", 1, uses: 0, maxUses: 16),
                new TradeOffer("emerald", 5, "diamond_sword", 1, uses: 0, maxUses: 3),
            };
            var v = Villager.Create(2, VillagerProfession.Blacksmith, default, offers);
            Assert.That(v.Offers.Count, Is.EqualTo(2));
            Assert.That(v.Offers[0].BuyItem, Is.EqualTo("emerald"));
            Assert.That(v.Offers[0].CanTrade, Is.True);
        }

        [Test]
        public void TradeOffer_CanTrade_FalseWhenAtMax()
        {
            var o = new TradeOffer("x", 1, "y", 1, uses: 3, maxUses: 3);
            Assert.That(o.CanTrade, Is.False);
        }

        [Test]
        public void VillagerAI_Daytime_Idle()
        {
            var v = Villager.Create(1, VillagerProfession.Farmer, new Float3(0, 64, 0));
            var day = new TimeOfDay { CurrentTick = 6000 };
            var pos = v.Position;
            for (int i = 0; i < 30; i++) VillagerAI.Tick(v, default, day, 0.1f);
            Assert.That(v.Position, Is.EqualTo(pos), "白天村民不应自动移动（演示版）");
        }

        [Test]
        public void VillagerAI_Nighttime_NoMovement()
        {
            var v = Villager.Create(1, VillagerProfession.Farmer, new Float3(5, 64, 5));
            var night = new TimeOfDay { CurrentTick = 15000 };
            var pos = v.Position;
            for (int i = 0; i < 30; i++) VillagerAI.Tick(v, default, night, 0.1f);
            Assert.That(v.Position, Is.EqualTo(pos));
        }
    }
}