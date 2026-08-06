using MyWorld.Core.Math;

namespace MyWorld.Core.Physics
{
    /// <summary>轴对齐包围盒。</summary>
    public readonly struct Aabb
    {
        public readonly Float3 Min;
        public readonly Float3 Max;

        public Aabb(Float3 min, Float3 max)
        {
            Min = min;
            Max = max;
        }

        public static Aabb FromBottomCenter(Float3 bottomCenter, float width, float height)
        {
            float half = width * 0.5f;
            return new Aabb(
                new Float3(bottomCenter.X - half, bottomCenter.Y, bottomCenter.Z - half),
                new Float3(bottomCenter.X + half, bottomCenter.Y + height, bottomCenter.Z + half));
        }

        public Aabb Translated(Float3 delta)
        {
            return new Aabb(
                new Float3(Min.X + delta.X, Min.Y + delta.Y, Min.Z + delta.Z),
                new Float3(Max.X + delta.X, Max.Y + delta.Y, Max.Z + delta.Z));
        }
    }
}
