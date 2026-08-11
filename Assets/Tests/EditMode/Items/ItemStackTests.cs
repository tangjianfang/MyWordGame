using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    [TestFixture]
    public class ItemStackTests
    {
        [Test]
        public void Empty_HasIdZero()
        {
            Assert.That(ItemStack.Empty.IsEmpty, Is.True);
        }

        [Test]
        public void CountZero_IsEmpty()
        {
            var s = new ItemStack(5, 0);
            Assert.That(s.IsEmpty, Is.True);
        }

        [Test]
        public void NegativeCount_BecomesZero()
        {
            var s = new ItemStack(5, -3);
            Assert.That(s.Count, Is.EqualTo(0));
            Assert.That(s.IsEmpty, Is.True);
        }

        [Test]
        public void WithCount_KeepsIdAndMeta()
        {
            var s = new ItemStack(7, 3, 11);
            var s2 = s.WithCount(10);
            Assert.That(s2.ItemId, Is.EqualTo(7));
            Assert.That(s2.Count, Is.EqualTo(10));
            Assert.That(s2.Metadata, Is.EqualTo(11));
        }

        [Test]
        public void IsEmpty_False_WhenIdAndCountPositive()
        {
            var s = new ItemStack(3, 1);
            Assert.That(s.IsEmpty, Is.False);
        }
    }
}
