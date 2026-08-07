using System;
using MyWorld.Core.Blocks;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Blocks
{
    [TestFixture]
    public class BlockFacesTests
    {
        [TestCase(0, true, BlockFace.East)]
        [TestCase(0, false, BlockFace.West)]
        [TestCase(1, true, BlockFace.Top)]
        [TestCase(1, false, BlockFace.Bottom)]
        [TestCase(2, true, BlockFace.North)]
        [TestCase(2, false, BlockFace.South)]
        public void FromAxis_MapsEachAxisAndDirectionToItsFace(int axis, bool facingPositive, BlockFace expected)
        {
            Assert.That(BlockFaces.FromAxis(axis, facingPositive), Is.EqualTo(expected));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void FromAxis_WithInvalidAxis_Throws(int axis)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BlockFaces.FromAxis(axis, true));
        }

        [Test]
        public void FromAxis_NeverMapsTwoAxesToTheSameFace()
        {
            var faces = new[]
            {
                BlockFaces.FromAxis(0, true), BlockFaces.FromAxis(0, false),
                BlockFaces.FromAxis(1, true), BlockFaces.FromAxis(1, false),
                BlockFaces.FromAxis(2, true), BlockFaces.FromAxis(2, false)
            };

            Assert.That(faces, Is.Unique, "六个轴向必须一一对应六个面，不能重复");
        }
    }
}
