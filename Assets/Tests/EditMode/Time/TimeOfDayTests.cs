using MyWorld.Core.Time;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Time
{
    [TestFixture]
    public class TimeOfDayTests
    {
        [Test]
        public void New_StartsAtNoon()
        {
            var t = new TimeOfDay();
            Assert.That(t.CurrentTick, Is.EqualTo(6000f));
            Assert.That(t.IsDay, Is.True);
        }

        [Test]
        public void Advance_IncrementsTick()
        {
            var t = new TimeOfDay { Speed = 60 };
            float before = t.CurrentTick;
            t.Advance(1f);
            Assert.That(t.CurrentTick, Is.EqualTo(before + 60));
        }

        [Test]
        public void Advance_WrapsAt24000()
        {
            var t = new TimeOfDay { Speed = 60, CurrentTick = 23990f };
            t.Advance(1f);
            Assert.That(t.CurrentTick, Is.LessThan(TimeOfDay.DayLengthTicks));
        }

        [Test]
        public void IsNight_DuringNightRange()
        {
            var t = new TimeOfDay { CurrentTick = 15000 };
            Assert.That(t.IsNight, Is.True);
        }

        [Test]
        public void IsDay_OutsideNightRange()
        {
            var t = new TimeOfDay { CurrentTick = 1000 };
            Assert.That(t.IsDay, Is.True);
        }

        [Test]
        public void Phase_DawnBefore5000()
        {
            var t = new TimeOfDay { CurrentTick = 1000 };
            Assert.That(t.Phase, Is.EqualTo(0));
        }

        [Test]
        public void Phase_DayBetween5000And13000()
        {
            var t = new TimeOfDay { CurrentTick = 8000 };
            Assert.That(t.Phase, Is.EqualTo(1));
        }

        [Test]
        public void Phase_NightAbove18000()
        {
            var t = new TimeOfDay { CurrentTick = 20000 };
            Assert.That(t.Phase, Is.EqualTo(3));
        }
    }
}
