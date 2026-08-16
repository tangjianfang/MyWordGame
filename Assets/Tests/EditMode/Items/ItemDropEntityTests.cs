using MyWorld.Core.Items;
using MyWorld.Core.Math;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    /// <summary>
    /// <see cref="ItemDropEntity"/> 行为：重力下落 + 拾取判定（完成距离 0.3m）+
    /// m7 B1 吸附状态机（<see cref="ItemDropEntity.TickPickup"/>：进 2.5m 圈 → 飞行 →
    /// 贴脸入包）+ Content 可空 + 0.5s 拾取宽限期（spec B7）。
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
            // currentTime=1f 跳过宽限期（SpawnTime=0 默认值，未启用 grace）。
            // m7 B1 起 TryPickupBy 只判完成距离（<0.3m），0.2m 应可拾
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.2f, 0f, 0f), currentTime: 1f, out int count);
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
            Assert.That(picked, Is.False, "距离 10m > 2.5m 吸附半径，更不可能完成拾取");
            Assert.That(count, Is.EqualTo(0), "未拾取时 count=0");
        }

        /// <summary>m7 B1 关键边界：1.5m 在吸附半径（2.5m）内、但超出完成距离（0.3m）——
        /// 「进半径立即入包」的旧语义在这里必须返回 false，吸附到位才能入包。</summary>
        [Test]
        public void TryPickup_吸附半径内但未到位返回False()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 2),
                new Float3(0f, 0f, 0f));
            bool picked = e.TryPickupBy(playerPosition: new Float3(1.5f, 0f, 0f), currentTime: 1f, out int count);
            Assert.That(picked, Is.False, "1.5m 在吸附半径内但 > 0.3m 完成距离，未吸附到位不能入包");
            Assert.That(count, Is.EqualTo(0));
        }

        // ─── m7 B1：TickPickup 吸附状态机（进圈 → 飞行 → 贴脸入包） ────────────

        [Test]
        public void TickPickup_两米五内开始吸附并推进()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(2f, 0f, 0f));
            bool done = e.TickPickup(new Float3(0f, 0f, 0f), currentTime: 1f, dt: 1f / 60f);

            Assert.That(e.Attracting, Is.True, "2m < 2.5m 吸附半径，应进入吸附态");
            Assert.That(done, Is.False, "第一步只飞行 8/60≈0.13m，距 0.3m 完成距离尚远");
            Assert.That(e.Position.X, Is.EqualTo(2f - ItemDropEntity.AttractSpeed / 60f).Within(1e-4f),
                "应向玩家直线推进 AttractSpeed*dt");
        }

        [Test]
        public void TickPickup_半径外不动不吸附()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(3f, 0f, 0f));
            bool done = e.TickPickup(new Float3(0f, 0f, 0f), currentTime: 1f, dt: 1f / 60f);

            Assert.That(done, Is.False, "3m > 2.5m 吸附半径，不能完成拾取");
            Assert.That(e.Attracting, Is.False, "半径外不应进入吸附态");
            Assert.That(e.Position.X, Is.EqualTo(3f), "半径外掉落物不应移动");
        }

        [Test]
        public void TickPickup_宽限期内不吸附()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(0.2f, 0f, 0f)); // 已贴脸（<0.3m）
            e.SpawnTime = 10f;

            bool done = e.TickPickup(new Float3(0f, 0f, 0f), currentTime: 10.2f, dt: 1f / 60f);

            Assert.That(done, Is.False, "生成后 0.2s < 0.5s 宽限期，贴脸也不能拾");
            Assert.That(e.Attracting, Is.False, "宽限期内不应开始吸附");

            Assert.That(e.TickPickup(new Float3(0f, 0f, 0f), 10.6f, 1f / 60f), Is.True,
                "宽限期过后贴脸应同帧完成拾取");
        }

        [Test]
        public void TickPickup_逐帧飞行到位后返回True()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 4),
                new Float3(2f, 0f, 0f));

            // 步进到完成（2m → 0.3m，8m/s、60fps 约 13 帧）；600 帧上限防死循环
            bool done = false;
            int steps = 0;
            while (!done && steps < 600)
            {
                done = e.TickPickup(new Float3(0f, 0f, 0f), 1f, 1f / 60f);
                steps++;
            }

            Assert.That(done, Is.True, "吸附应在有限帧内完成");
            Assert.That(steps, Is.GreaterThan(1), "2m 距离必须有飞行过程（非瞬移）");
            Assert.That(e.Position.X, Is.LessThan(ItemDropEntity.PickupDistance + 1e-4f),
                "完成时掉落物应已贴近玩家");
            Assert.That(e.Content.HasValue, Is.True, "TickPickup 只做判定，Content 由调用方 MarkPicked 清空");
        }

        [Test]
        public void TickPickup_贴脸同帧完成()
        {
            var e = new ItemDropEntity(
                new ItemStack(itemId: 1, count: 1),
                new Float3(0.2f, 0f, 0f));
            Assert.That(e.TickPickup(new Float3(0f, 0f, 0f), 1f, 1f / 60f), Is.True,
                "距玩家 0.2m < 0.3m 完成距离，应同帧完成（挖脚下方块的场景）");
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
            // 0.3s 后玩家就位，距离 0.2m 已在完成距离内，但 grace 未满 → 拒绝
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.2f, 0f, 0f), currentTime: 10.3f, out int count);
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
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.2f, 0f, 0f), currentTime: 10.6f, out int count);
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
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.2f, 0f, 0f), currentTime: 10.5f, out int count);
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
            bool picked = e.TryPickupBy(playerPosition: new Float3(0.2f, 0f, 0f), currentTime: 0f, out int count);
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
            Assert.That(e.TryPickupBy(new Float3(0.2f, 0f, 0f), 10.2f, out _), Is.False);

            // grace 过后再拾 → 成；Content 仍应是原 stack（被拒时没消耗）
            Assert.That(e.TryPickupBy(new Float3(0.2f, 0f, 0f), 10.6f, out int count), Is.True);
            Assert.That(count, Is.EqualTo(2), "grace 拒绝不消耗 count");
            Assert.That(e.Content.HasValue, Is.True, "grace 拒绝不动 Content；MarkPicked 才清空");
        }
    }
}
