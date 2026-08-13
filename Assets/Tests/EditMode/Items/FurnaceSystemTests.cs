using NUnit.Framework;
using MyWorld.Core.Items;

namespace MyWorld.Core.Tests.Items
{
    public class FurnaceSystemTests
    {
        [Test]
        public void Tick_ProgressesAndProducesOutput()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            f.AddInput(new ItemStack(itemId: 1, count: 1));   // iron_ore
            f.AddFuel(new ItemStack(itemId: 10, count: 1));   // coal
            f.Tick(dt: 1.1f);  // 超过 smelt time
            Assert.That(f.Output, Is.Not.Null);
            Assert.That(f.Output.Value.Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_PausesWhenFuelRunsOut()
        {
            var f = new FurnaceSystem(coalFuelValue: 4, smeltTimeSeconds: 2f);
            f.AddInput(new ItemStack(itemId: 1, count: 2));
            f.AddFuel(new ItemStack(itemId: 10, count: 1));  // 4s fuel
            f.Tick(dt: 5f);  // 燃料耗尽 + 时间过
            // Progress 应暂停（不倒退）
            Assert.That(f.Progress, Is.LessThanOrEqualTo(2f));
        }

        [Test]
        public void AddInput_StacksUpToCapacity()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f) { MaxInputStack = 3 };
            f.AddInput(new ItemStack(itemId: 1, count: 2));
            bool added = f.AddInput(new ItemStack(itemId: 1, count: 2));
            Assert.That(added, Is.False, "超出容量拒绝");
        }

        [Test]
        public void Tick_NoFuelDoesNothing()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            f.AddInput(new ItemStack(itemId: 1, count: 1));
            f.Tick(dt: 5f);
            Assert.That(f.Output, Is.Null, "无燃料不烧");
            Assert.That(f.Progress, Is.EqualTo(0f));
        }
    }
}