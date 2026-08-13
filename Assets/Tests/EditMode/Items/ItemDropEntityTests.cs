using MyWorld.Core.Items;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    /// <summary>
    /// <see cref="ItemDropEntity"/> 行为：重力下落 + 玩家拾取范围 + Content 可空。
    /// </summary>
    public class ItemDropEntityTests
    {
        [Test]
        public void TickGravity_FallsDownward()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(0f, 10f, 0f));
            e.TickGravity(dt: 1f, gravity: -10f);
            Assert.That(e.Position.Y, Is.LessThan(10f), "重力下落");
        }

        [Test]
        public void TickGravity_StopsAtFloor()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(0f, 0.5f, 0f));
            // 大 dt 让它直接冲穿 y=0，验证地板反弹并停下
            e.TickGravity(dt: 1f, gravity: -20f);
            Assert.That(e.Position.Y, Is.EqualTo(0f), "撞地板停在 y=0");
            Assert.That(e.VelocityY, Is.EqualTo(0f), "地板后 VelocityY 清零");
        }

        [Test]
        public void TryPickup_WithinRangeReturnsTrue()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(0f, 0f, 0f));
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.5f, 0f, 0f), out int count);
            Assert.That(picked, Is.True);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void TryPickup_OutOfRangeReturnsFalse()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 3),
                new Float3(0f, 0f, 0f));
            bool picked = e.TryPickupBy(playerPosition: new Float3(10f, 0f, 0f), out int count);
            Assert.That(picked, Is.False, "距离 10m > 1.5m 拾取半径");
            Assert.That(count, Is.EqualTo(0), "未拾取时 count=0");
        }
    }
}