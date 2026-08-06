namespace MyWorld.Core.Physics
{
    /// <summary>体素射线命中结果。</summary>
    public struct VoxelRayHit
    {
        public bool Hit;

        public int X;
        public int Y;
        public int Z;

        /// <summary>命中面的法线，指向射线来向。</summary>
        public int NormalX;
        public int NormalY;
        public int NormalZ;

        public float Distance;

        /// <summary>放置新方块的位置：命中方块沿其命中面法线偏移一格。</summary>
        public int PlacementX => X + NormalX;

        public int PlacementY => Y + NormalY;

        public int PlacementZ => Z + NormalZ;
    }
}
