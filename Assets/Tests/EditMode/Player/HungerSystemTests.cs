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

        // Eat 的恢复 / 钳制 / 非法值测试自 m7 A3 起迁至 EatFoodTests
        //（右键吃食物那条链路的测试统一放一个文件里）

        [Test]
        public void IsStarving_WhenHungerZero()
        {
            var h = new HungerSystem { Hunger = 0, Saturation = 0f };
            Assert.That(h.IsStarving(), Is.True);
        }
    }
}
