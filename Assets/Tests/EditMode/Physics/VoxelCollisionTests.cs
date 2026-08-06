using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Physics
{
    [TestFixture]
    public class VoxelCollisionTests
    {
        private const float PlayerWidth = 0.6f;
        private const float PlayerHeight = 1.8f;
        private const float Tolerance = 0.01f;

        [Test]
        public void Move_InEmptySpace_AppliesFullDelta()
        {
            var world = new TestSolidSource();
            Aabb box = Player(0.5f, 10f, 0.5f);

            MoveResult result = VoxelCollision.Move(world, box, new Float3(1.5f, -2f, 0.75f));

            Assert.That(result.Delta.X, Is.EqualTo(1.5f).Within(Tolerance));
            Assert.That(result.Delta.Y, Is.EqualTo(-2f).Within(Tolerance));
            Assert.That(result.Delta.Z, Is.EqualTo(0.75f).Within(Tolerance));
            Assert.That(result.IsGrounded, Is.False);
        }

        [Test]
        public void Move_WithZeroDelta_DoesNotMove()
        {
            var world = new TestSolidSource();
            FillGround(world);

            MoveResult result = VoxelCollision.Move(world, Player(0.5f, 1f, 0.5f), new Float3(0f, 0f, 0f));

            Assert.That(result.Delta.X, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.Delta.Y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(result.Delta.Z, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void Move_FallingOntoGround_StopsOnSurfaceAndReportsGrounded()
        {
            var world = new TestSolidSource();
            FillGround(world);
            Aabb box = Player(0.5f, 5f, 0.5f);

            MoveResult result = VoxelCollision.Move(world, box, new Float3(0f, -10f, 0f));

            // 地面方块顶面在 y=1，玩家从 y=5 落下应恰好停在 y=1
            Assert.That(result.Delta.Y, Is.EqualTo(-4f).Within(Tolerance));
            Assert.That(result.IsGrounded, Is.True);
        }

        [Test]
        public void Move_IntoWall_StopsFlushAgainstIt()
        {
            var world = new TestSolidSource();
            FillWallAtX(world, 3);
            Aabb box = Player(1f, 0f, 0.5f);

            MoveResult result = VoxelCollision.Move(world, box, new Float3(5f, 0f, 0f));

            // 玩家右边缘从 1.3 推进到墙面 x=3
            Assert.That(result.Delta.X, Is.EqualTo(1.7f).Within(Tolerance));
            Assert.That(result.HitX, Is.True);
        }

        [Test]
        public void Move_DiagonallyIntoWall_SlidesAlongIt()
        {
            var world = new TestSolidSource();
            FillWallAtX(world, 3);
            Aabb box = Player(1f, 0f, 0.5f);

            MoveResult result = VoxelCollision.Move(world, box, new Float3(5f, 0f, 2f));

            Assert.That(result.HitX, Is.True, "朝墙的分量应被阻挡");
            Assert.That(result.Delta.Z, Is.EqualTo(2f).Within(Tolerance), "沿墙的分量应完整保留");
            Assert.That(result.HitZ, Is.False);
        }

        [Test]
        public void Move_UpwardIntoCeiling_StopsBelowIt()
        {
            var world = new TestSolidSource();
            FillCeilingAtY(world, 5);
            Aabb box = Player(0.5f, 0f, 0.5f);

            MoveResult result = VoxelCollision.Move(world, box, new Float3(0f, 10f, 0f));

            // 玩家头顶从 1.8 升到天花板底面 y=5
            Assert.That(result.Delta.Y, Is.EqualTo(3.2f).Within(Tolerance));
            Assert.That(result.HitY, Is.True);
            Assert.That(result.IsGrounded, Is.False, "撞天花板不算落地");
        }

        [Test]
        public void Move_AlongCorridorThatExactlyFits_IsNotBlocked()
        {
            var world = new TestSolidSource();
            for (var z = -2; z <= 8; z++)
            {
                for (var y = 0; y <= 3; y++)
                {
                    world.Add(0, y, z);
                    world.Add(2, y, z);
                }
            }

            // 玩家宽 0.6，位于 x=1 的格子中央，通道净宽 1.0，应当能通过
            Aabb box = Player(1.5f, 0f, 0.5f);

            MoveResult result = VoxelCollision.Move(world, box, new Float3(0f, 0f, 5f));

            Assert.That(result.Delta.Z, Is.EqualTo(5f).Within(Tolerance));
            Assert.That(result.HitZ, Is.False);
        }

        [Test]
        public void Move_IntoWallTwice_DoesNotSinkIntoIt()
        {
            var world = new TestSolidSource();
            FillWallAtX(world, 3);
            Aabb box = Player(1f, 0f, 0.5f);

            MoveResult first = VoxelCollision.Move(world, box, new Float3(5f, 0f, 0f));
            Aabb moved = box.Translated(first.Delta);
            MoveResult second = VoxelCollision.Move(world, moved, new Float3(5f, 0f, 0f));

            Assert.That(second.Delta.X, Is.EqualTo(0f).Within(Tolerance), "已贴墙时不应再前进");
            Assert.That(moved.Max.X, Is.LessThanOrEqualTo(3f), "玩家不应嵌入墙体");
        }

        [Test]
        public void Move_FallingFreely_IsNotGrounded()
        {
            var world = new TestSolidSource();
            FillGround(world);

            MoveResult result = VoxelCollision.Move(world, Player(0.5f, 20f, 0.5f), new Float3(0f, -1f, 0f));

            Assert.That(result.Delta.Y, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(result.IsGrounded, Is.False);
        }

        private static Aabb Player(float centerX, float bottomY, float centerZ)
        {
            return Aabb.FromBottomCenter(new Float3(centerX, bottomY, centerZ), PlayerWidth, PlayerHeight);
        }

        private static void FillGround(TestSolidSource world)
        {
            for (var x = -4; x <= 4; x++)
            for (var z = -4; z <= 4; z++)
            {
                world.Add(x, 0, z);
            }
        }

        private static void FillWallAtX(TestSolidSource world, int wallX)
        {
            for (var y = -1; y <= 4; y++)
            for (var z = -4; z <= 8; z++)
            {
                world.Add(wallX, y, z);
            }
        }

        private static void FillCeilingAtY(TestSolidSource world, int ceilingY)
        {
            for (var x = -4; x <= 4; x++)
            for (var z = -4; z <= 4; z++)
            {
                world.Add(x, ceilingY, z);
            }
        }
    }
}
