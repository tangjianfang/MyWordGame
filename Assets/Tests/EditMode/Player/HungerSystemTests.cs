using NUnit.Framework;
using MyWorld.Core.Player;

namespace MyWorld.Core.Tests.Player
{
    public class HungerSystemTests
    {
        [Test]
        public void Tick_DecaysHungerEvery30Seconds()
        {
            var h = new HungerSystem();
            h.Hunger = 20;
            h.Saturation = 5f;
            // 模拟 30s
            h.Tick(dt: 30f);  // 600 ticks @ 20 t/s
            Assert.That(h.Hunger, Is.LessThan(20), "30s 后 Hunger 衰减");
        }

        [Test]
        public void Tick_AcceleratesWhenSaturationZero()
        {
            var h1 = new HungerSystem { Hunger = 20, Saturation = 5f };
            var h2 = new HungerSystem { Hunger = 20, Saturation = 0f };
            h1.Tick(dt: 60f);
            h2.Tick(dt: 60f);
            Assert.That(h2.Hunger, Is.LessThan(h1.Hunger), "Saturation=0 衰减更快");
        }

        [Test]
        public void Eat_RestoresHungerAndSaturation()
        {
            var h = new HungerSystem { Hunger = 5, Saturation = 0f };
            h.Eat(hunger: 6, saturation: 7.2f);
            Assert.That(h.Hunger, Is.EqualTo(11));
            Assert.That(h.Saturation, Is.GreaterThan(0f));
        }

        [Test]
        public void IsStarving_WhenHungerZero()
        {
            var h = new HungerSystem { Hunger = 0, Saturation = 0f };
            Assert.That(h.IsStarving(), Is.True);
        }
    }
}
