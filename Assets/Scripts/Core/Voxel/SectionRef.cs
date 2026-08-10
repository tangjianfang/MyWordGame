using System;

namespace MyWorld.Core.Voxel
{
    /// <summary>一个区块段的定位：哪根区块列的第几段。</summary>
    public readonly struct SectionRef : IEquatable<SectionRef>
    {
        public readonly ChunkPos Chunk;
        public readonly int SectionIndex;

        public SectionRef(ChunkPos chunk, int sectionIndex)
        {
            Chunk = chunk;
            SectionIndex = sectionIndex;
        }

        public bool Equals(SectionRef other) => Chunk.Equals(other.Chunk) && SectionIndex == other.SectionIndex;

        public override bool Equals(object obj) => obj is SectionRef other && Equals(other);

        public override int GetHashCode() => (Chunk.GetHashCode() * 397) ^ SectionIndex;

        public override string ToString() => $"{Chunk} 段 {SectionIndex}";
    }
}
