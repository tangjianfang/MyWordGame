using MyWorld.Core.Items;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    /// <summary>
    /// <see cref="ItemDropEntity"/> 行为：重力下落 + 玩家拾取范围 + Content 可空 +
    /// 0.5s 拾取宽限期（spec B7）。
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
            // currentTime=1f 跳过宽限期（SpawnTime=0 默认值，未启用 grace）
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.5f, 0f, 0f), currentTime: 1f, out int count);
            Assert.That(picked, Is.True);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void TryPickup_OutOfRangeReturnsFalse()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 3),
                new Float3(0f, 0f, 0f));
            // currentTime=1f 跳过宽限期
            bool picked = e.TryPickupBy(playerPosition: new Float3(10f, 0f, 0f), currentTime: 1f, out int count);
            Assert.That(picked, Is.False, "距离 10m > 1.5m 拾取半径");
            Assert.That(count, Is.EqualTo(0), "未拾取时 count=0");
        }

        // ─── F1 follow-up：0.5s 拾取宽限期（spec B7） ────────────────────────────
        // review-final.md finding #16：原 TryPickupBy 立即可拾取，spec B7 要求
        // "0.5s 后可拾取"，让玩家有反应时间看到掉落物出现。

        /// <summary>SpawnTime 已设置且距 currentTime 不足 0.5s：返回 false（不让拾）。</summary>
        [Test]
        public void TryPickupBy_WithinGracePeriod_ReturnsFalse()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(0f, 0f, 0f));
            e.SpawnTime = 10f; // Unity 侧 spawn 时 set 为 Time.time
            // 0.3s 后玩家就位，距离 0.5m 在范围内，但 grace 未满 → 拒绝
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.5f, 0f, 0f), currentTime: 10.3f, out int count);
            Assert.That(picked, Is.False, "生成后 0.3s < 0.5s 宽限期，应拒绝拾取");
            Assert.That(count, Is.EqualTo(0), "拒绝拾取时 count=0");
        }

        /// <summary>SpawnTime 已设置且距 currentTime 超过 0.5s：返回 true（可拾）。</summary>
        [Test]
        public void TryPickupBy_AfterGracePeriod_ReturnsTrue()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(0f, 0f, 0f));
            e.SpawnTime = 10f;
            // 0.6s 后玩家就位 → grace 已满，可拾
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.5f, 0f, 0f), currentTime: 10.6f, out int count);
            Assert.That(picked, Is.True, "生成后 0.6s > 0.5s 宽限期，应可拾取");
            Assert.That(count, Is.EqualTo(1));
        }

        /// <summary>Spec B7 边界：恰好 0.5s 时应可拾取（"0.5s 后" 含等号）。</summary>
        [Test]
        public void TryPickupBy_AtGraceBoundary_ReturnsTrue()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(0f, 0f, 0f));
            e.SpawnTime = 10f;
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.5f, 0f, 0f), currentTime: 10.5f, out int count);
            Assert.That(picked, Is.True, "恰好 0.5s 应可拾取（spec 边界含等号）");
        }

        /// <summary>SpawnTime 未设置（=0）时跳过宽限期检查：原范围判断行为不变，
        /// 让 Core 单元测试不需要时间注入也能验证范围逻辑。</summary>
        [Test]
        public void TryPickupBy_UnsetSpawnTime_SkipsGraceCheck()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(0f, 0f, 0f));
            // SpawnTime 保持默认 0：宽限期自动失效，currentTime=0 也立即可拾
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.5f, 0f, 0f), currentTime: 0f, out int count);
            Assert.That(picked, Is.True, "SpawnTime=0 时不检查 grace，currentTime=0 也能拾");
        }

        /// <summary>grace 期内拾取被拒但不影响 Content：之后再拾仍可拿到 count。</summary>
        [Test]
        public void TryPickupBy_GracePeriodReject_DoesNotConsumeContent()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 2),
                new Float3(0f, 0f, 0f));
            e.SpawnTime = 10f;

            // grace 内尝试拾取 → 拒
            Assert.That(e.TryPickupBy(new Float3(0.5f, 0f, 0f), 10.2f, out _), Is.False);

            // grace 过后再拾 → 成；Content 仍应是原 stack（被拒时没消耗）
            Assert.That(e.TryPickupBy(new Float3(0.5f, 0f, 0f), 10.6f, out int count), Is.True);
            Assert.That(count, Is.EqualTo(2), "grace 拒绝不消耗 count");
            Assert.That(e.Content.HasValue, Is.True, "grace 拒绝不动 Content；MarkPicked 才清空");
        }
    }
}
