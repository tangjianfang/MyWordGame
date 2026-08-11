using MyWorld.Core.Entities;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Entities
{
    [TestFixture]
    public class RedstoneCircuitTests
    {
        [Test]
        public void Tick_LeverAt15_PropagatesToNeighborWith14()
        {
            var c = new RedstoneCircuit();
            c.SetSource(0, 64, 0, powered: true);
            c.SetWire(1, 64, 0, 1);    // 已有 1 强度
            c.Tick();

            Assert.That(c.GetPower(1, 64, 0), Is.EqualTo(RedstoneCircuit.MaxPower - 1),
                "拉杆旁红石粉应为 14");
        }

        [Test]
        public void Tick_LeverPowered_WiresTwoBlocksAway_Get13()
        {
            var c = new RedstoneCircuit();
            c.SetSource(0, 64, 0, powered: true);
            c.SetWire(1, 64, 0, 1);
            c.SetWire(2, 64, 0, 1);
            c.SetWire(3, 64, 0, 1);
            c.Tick();
            c.Tick();
            c.Tick();

            Assert.That(c.GetPower(3, 64, 0), Is.EqualTo(RedstoneCircuit.MaxPower - 3),
                "三格外的红石粉应为 12");
        }

        [Test]
        public void Tick_LeverUnpowered_AllWiresDecayToZero()
        {
            var c = new RedstoneCircuit();
            c.SetSource(0, 64, 0, powered: true);
            c.SetWire(1, 64, 0, 1);
            c.SetWire(2, 64, 0, 1);
            c.Tick();
            Assert.That(c.GetPower(2, 64, 0), Is.GreaterThan((byte)0));

            c.SetSource(0, 64, 0, powered: false);
            // 多次 Tick 让信号逐格衰减
            for (int i = 0; i < 20; i++) c.Tick();
            Assert.That(c.GetPower(2, 64, 0), Is.EqualTo((byte)0));
        }

        [Test]
        public void Tick_NoLeversOrWires_StaysEmpty()
        {
            var c = new RedstoneCircuit();
            bool changed = c.Tick();
            Assert.That(changed, Is.False);
        }

        [Test]
        public void Tick_Stabilizes_AfterFewPasses()
        {
            var c = new RedstoneCircuit();
            c.SetSource(0, 64, 0, powered: true);
            for (int x = 1; x <= 5; x++) c.SetWire(x, 64, 0, 1);

            bool changedLast = true;
            for (int i = 0; i < 20 && changedLast; i++) changedLast = c.Tick();
            Assert.That(changedLast, Is.False, "信号应在若干 tick 后稳定");
        }

        [Test]
        public void IsPowered_LeverAboveZero_ReturnsTrue()
        {
            var c = new RedstoneCircuit();
            c.SetSource(5, 64, 5, powered: true);
            Assert.That(c.IsPowered(5, 64, 5), Is.True);
        }
    }

    [TestFixture]
    public class DoorControllerTests
    {
        [Test]
        public void Door_StartsClosed_AfterRegister()
        {
            var d = new DoorController();
            d.Register(0, 64, 0);
            Assert.That(d.GetState(0, 64, 0), Is.EqualTo(DoorState.Closed));
        }

        [Test]
        public void Door_UpdateSignal_True_Opens()
        {
            var d = new DoorController();
            d.Register(0, 64, 0);
            bool changed = d.UpdateSignal(0, 64, 0, true);
            Assert.That(changed, Is.True);
            Assert.That(d.IsOpen(0, 64, 0), Is.True);
        }

        [Test]
        public void Door_UpdateSignal_FalseAfterTrue_Closes()
        {
            var d = new DoorController();
            d.Register(0, 64, 0);
            d.UpdateSignal(0, 64, 0, true);
            bool changed = d.UpdateSignal(0, 64, 0, false);
            Assert.That(changed, Is.True);
            Assert.That(d.GetState(0, 64, 0), Is.EqualTo(DoorState.Closed));
        }

        [Test]
        public void Door_UpdateSignal_SameValueTwice_SecondReturnsFalse()
        {
            var d = new DoorController();
            d.Register(0, 64, 0);
            d.UpdateSignal(0, 64, 0, true);
            bool changed = d.UpdateSignal(0, 64, 0, true);
            Assert.That(changed, Is.False);
        }
    }
}