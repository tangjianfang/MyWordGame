namespace MyWorld.Core.Math
{
    /// <summary>二分量浮点向量。字段顺序与 UnityEngine.Vector2 一致。</summary>
    public struct Float2
    {
        public float X;
        public float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X}, {Y})";
    }
}
