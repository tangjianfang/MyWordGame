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

        // ---- m10 C2：粗矿冶炼——粗金→金锭 / 粗铁→铁锭，10s（矿石精炼比圆石 1s 慢一个量级）----

        [Test]
        public void Tick_RawGold_SmeltsIntoGoldIngot_InTenSeconds()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            Assert.That(f.AddInput(new ItemStack(FurnaceSystem.RawGoldItemId, 1)), Is.True, "粗金（1023）应可作输入");
            f.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 2));  // 16s 燃料 > 10s 烧炼

            f.Tick(dt: 9.9f);
            Assert.That(f.Output, Is.Null,
                "粗金要烧满 10s——构造时长的 1s 只管圆石/透传路径，9.9s 不该出锭");

            f.Tick(dt: 0.2f);
            Assert.That(f.Output, Is.Not.Null, "满 10s 应出锭");
            Assert.That(f.Output.Value.ItemId, Is.EqualTo(FurnaceSystem.GoldIngotItemId),
                "粗金（1023）应烧成 gold_ingot（1027）");
            Assert.That(f.Output.Value.Count, Is.EqualTo(1));
            Assert.That(f.Input, Is.Null, "单个输入烧完应清空输入槽");
        }

        [Test]
        public void Tick_RawIron_SmeltsIntoIronIngot_InTenSeconds()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            Assert.That(f.AddInput(new ItemStack(FurnaceSystem.RawIronItemId, 2)), Is.True, "粗铁（1024）应可作输入");
            f.AddFuel(new ItemStack(FurnaceSystem.CoalItemId, 2));  // 16s 燃料

            f.Tick(dt: 10f);
            Assert.That(f.Output, Is.Not.Null, "10s 到点应出锭");
            Assert.That(f.Output.Value.ItemId, Is.EqualTo(FurnaceSystem.SmeltOutputItemId),
                "粗铁（1024）应烧成 iron_ingot（1004）——与圆石冶炼殊途同归");
            Assert.That(f.Input.Value.Count, Is.EqualTo(1), "烧掉一个，输入槽还剩一个粗铁");
        }

        // ---- m10 C2 fix1（I2）：烧炼时长跟输入走——UI 进度条的真分母 ----

        [Test]
        public void CurrentSmeltDuration_FollowsCurrentInput()
        {
            var f = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            Assert.That(f.CurrentSmeltDuration, Is.EqualTo(1f),
                "空炉（无输入）按构造时长——UI 空闲时不除零也不乱跳");

            Assert.That(f.AddInput(new ItemStack(FurnaceSystem.RawGoldItemId, 1)), Is.True);
            Assert.That(f.CurrentSmeltDuration, Is.EqualTo(FurnaceSystem.RawOreSmeltSeconds),
                "粗金在炉：烧炼时长 10s");

            var f2 = new FurnaceSystem(coalFuelValue: 8, smeltTimeSeconds: 1f);
            Assert.That(f2.AddInput(new ItemStack(FurnaceSystem.SmeltInputItemId, 1)), Is.True);
            Assert.That(f2.CurrentSmeltDuration, Is.EqualTo(1f),
                "圆石照旧按构造时长（1s）——旧映射行为不动");
        }
    }
}
