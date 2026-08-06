using System;

namespace MyWorld.Core.Voxel
{
    /// <summary>区块在水平面上的坐标。</summary>
    public readonly struct ChunkPos : IEquatable<ChunkPos>
    {
        public readonly int X;
        public readonly int Z;

        public ChunkPos(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(ChunkPos other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is ChunkPos other && Equals(other);

        public override int GetHashCode() => unchecked((X * 397) ^ Z);

        public override string ToString() => $"({X}, {Z})";
    }
}
