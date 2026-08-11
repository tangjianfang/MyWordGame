namespace MyWorld.Core.Math
{
    /// <summary>三分量浮点向量。字段顺序与 UnityEngine.Vector3 一致，便于后续直接按内存块上传。</summary>
    public struct Float3
    {
        public float X;
        public float Y;
        public float Z;

        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Float3 operator +(Float3 a, Float3 b) => new Float3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Float3 operator -(Float3 a, Float3 b) => new Float3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Float3 operator *(Float3 a, float s) => new Float3(a.X * s, a.Y * s, a.Z * s);
        public static Float3 operator /(Float3 a, float s) => new Float3(a.X / s, a.Y / s, a.Z / s);

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
