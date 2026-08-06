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

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
