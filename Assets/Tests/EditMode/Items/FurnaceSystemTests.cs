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
            f.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 1));   // cobblestone
            f.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 1));          // coal
            f.Tick(dt: 1.1f);  // 超过 smelt time
            Assert.That(f.Output, Is.Not.Null);
            Assert.That(f.Output.Value.Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_PausesWhenFuelRunsOut()
        {
            var f = new FurnaceSystem(coalFuelValue: 4, smeltTimeSeconds: 2f);
            f.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 2));
            f.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 1));  // 4s fuel
            f.Tick(dt: 5f);  // 燃料耗尽 + 时间过
            // Progress 应暂停（不倒退）
            Assert.That(f.Progress, Is.LessThanOrEqualTo(2f));
        }

        [Test]
        public void AddInput_StacksUpToCapacity()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f) { MaxInputStack = 3 };
            f.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 2));
            bool added = f.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 2));
            Assert.That(added, Is.False, "超出容量拒绝");
        }

        [Test]
        public void Tick_NoFuelDoesNothing()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            f.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 1));
            f.Tick(dt: 5f);
            Assert.That(f.Output, Is.Null, "无燃料不烧");
            Assert.That(f.Progress, Is.EqualTo(0f));
        }

        // ---- m6 C2：占位 id 修正——燃料=真煤(1007)、圆石(1003)烧成铁锭(1004) ----

        [Test]
        public void Tick_CobblestoneSmeltsIntoIronIngot()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            Assert.That(f.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 1)), Is.True);
            Assert.That(f.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 1)), Is.True, "煤（1007）应是合法燃料");
            f.Tick(dt: 1.1f);

            Assert.That(f.Output, Is.Not.Null, "圆石 + 煤应烧出产物");
            Assert.That(f.Output.Value.ItemId, Is.EqualTo(FurnaceSystem.SmeltOutputItemId),
                "圆石烧炼应产出 iron_ingot（1004）——旧占位映射（1→2）烧出的是土，任务 7 永远无法完成");
            Assert.That(f.Output.Value.Count, Is.EqualTo(1));
            Assert.That(f.Input, Is.Null, "单个输入烧完应清空输入槽");
        }

        [Test]
        public void AddFuel_RejectsNonCoalItems()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            Assert.That(f.AddFuel(new ItemStack(FurnaceSystem.SmeltInputItemId, 64)), Is.False,
                "圆石不能当燃料（m3 占位 id 10 修正后只有真煤 1007 合法）");
            Assert.That(f.FuelRemaining, Is.EqualTo(0f), "被拒绝的物品不产生燃料值");
        }

        [Test]
        public void AddFuel_RealCoalAccumulatesFuelValue()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            Assert.That(f.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 2)), Is.True, "真煤（1007）应被接受");
            Assert.That(f.FuelRemaining, Is.EqualTo(16f), "2 块煤 × coalFuelValue 8 = 16 秒燃烧值");
        }
    }
}
