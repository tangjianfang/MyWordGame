// m12 W3：药水 buff 纯逻辑契约（dotnet / EditMode 双链同源）。
// 显式时间驱动（照 FlightStateTests 的做法，不 mock Time）。
using MyWorld.Core.Buffs;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Buffs
{
    [TestFixture]
    public class PotionSystemTests
    {
        [Test]
        public void Drink_ActivatesFor30Seconds()
        {
            var potions = new PotionSystem();
            potions.Drink(BuffKind.Swiftness, now: 10f);

            Assert.That(potions.Active(BuffKind.Swiftness, 39.9f), Is.True, "29.9s 仍在");
            Assert.That(potions.Active(BuffKind.Swiftness, 40.1f), Is.False, "30s 后过期");
            Assert.That(potions.Active(BuffKind.Strength, 10f), Is.False, "没喝的 buff 不受影响");
        }

        [Test]
        public void Redrink_RefreshesExpiry()
        {
            var potions = new PotionSystem();
            potions.Drink(BuffKind.Strength, now: 0f);
            potions.Drink(BuffKind.Strength, now: 20f); // 续杯刷新

            Assert.That(potions.Active(BuffKind.Strength, 45f), Is.True, "从续杯点重算 30s");
            Assert.That(potions.Active(BuffKind.Strength, 51f), Is.False);
        }

        [Test]
        public void MoveSpeedBonus_OnlyWhileSwiftnessActive()
        {
            var potions = new PotionSystem();
            Assert.That(potions.MoveSpeedBonus(0f), Is.EqualTo(0f));

            potions.Drink(BuffKind.Swiftness, 0f);
            Assert.That(potions.MoveSpeedBonus(10f), Is.EqualTo(PotionSystem.SwiftnessMoveBonus).Within(1e-4));
            Assert.That(potions.MoveSpeedBonus(31f), Is.EqualTo(0f), "过期归零");
        }

        [Test]
        public void AttackBonus_StrengthGives2()
        {
            var potions = new PotionSystem();
            potions.Drink(BuffKind.Strength, 0f);
            Assert.That(potions.AttackBonus(1f), Is.EqualTo(2));
        }

        [Test]
        public void JumpMultiplier_LeapingGives1Point3()
        {
            var potions = new PotionSystem();
            Assert.That(potions.JumpMultiplier(0f), Is.EqualTo(1f), "无 buff 恒 1（旧版逐值一致）");

            potions.Drink(BuffKind.Leaping, 0f);
            Assert.That(potions.JumpMultiplier(1f), Is.EqualTo(1.3f).Within(1e-4));
        }

        [Test]
        public void MaxHealthBonus_WaterBreathingPlaceholder()
        {
            var potions = new PotionSystem();
            potions.Drink(BuffKind.WaterBreathing, 0f);
            Assert.That(potions.MaxHealthBonus(1f), Is.EqualTo(4), "水肺占位 = 血上限 +4");
            Assert.That(potions.MaxHealthBonus(31f), Is.EqualTo(0));
        }

        [Test]
        public void ResetAll_ClearsEverything()
        {
            var potions = new PotionSystem();
            potions.Drink(BuffKind.Swiftness, 0f);
            potions.Drink(BuffKind.NightVision, 0f);

            potions.ResetAll();

            Assert.That(potions.Active(BuffKind.Swiftness, 1f), Is.False);
            Assert.That(potions.Active(BuffKind.NightVision, 1f), Is.False);
        }
    }
}
