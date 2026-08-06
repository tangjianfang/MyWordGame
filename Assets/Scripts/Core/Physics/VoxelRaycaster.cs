using MyWorld.Core.Math;

namespace MyWorld.Core.Physics
{
    /// <summary>
    /// 体素 DDA 射线步进（Amanatides-Woo）。逐格精确穿越，不会像固定步长采样那样漏掉薄方块。
    /// </summary>
    public static class VoxelRaycaster
    {
        public static VoxelRayHit Cast<TSource>(TSource source, Float3 origin, Float3 direction, float maxDistance)
            where TSource : ISolidBlockSource
        {
            var hit = default(VoxelRayHit);

            float length = (float)System.Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
            if (length <= 0f)
            {
                return hit;
            }

            float dx = direction.X / length;
            float dy = direction.Y / length;
            float dz = direction.Z / length;

            int x = FloorToInt(origin.X);
            int y = FloorToInt(origin.Y);
            int z = FloorToInt(origin.Z);

            if (source.IsSolidAt(x, y, z))
            {
                hit.Hit = true;
                hit.X = x;
                hit.Y = y;
                hit.Z = z;
                // 起点已在实心方块内，没有穿越面，法线取射线反方向的主轴
                hit.NormalX = -Sign(dx);
                hit.Distance = 0f;
                return hit;
            }

            int stepX = Sign(dx);
            int stepY = Sign(dy);
            int stepZ = Sign(dz);

            float deltaX = stepX == 0 ? float.PositiveInfinity : System.Math.Abs(1f / dx);
            float deltaY = stepY == 0 ? float.PositiveInfinity : System.Math.Abs(1f / dy);
            float deltaZ = stepZ == 0 ? float.PositiveInfinity : System.Math.Abs(1f / dz);

            float maxX = DistanceToNextBoundary(origin.X, x, stepX, deltaX);
            float maxY = DistanceToNextBoundary(origin.Y, y, stepY, deltaY);
            float maxZ = DistanceToNextBoundary(origin.Z, z, stepZ, deltaZ);

            float travelled = 0f;

            while (travelled <= maxDistance)
            {
                int normalX = 0;
                int normalY = 0;
                int normalZ = 0;

                // 先跨越最近的那一面，保证按穿越顺序访问格子
                if (maxX <= maxY && maxX <= maxZ)
                {
                    x += stepX;
                    travelled = maxX;
                    maxX += deltaX;
                    normalX = -stepX;
                }
                else if (maxY <= maxZ)
                {
                    y += stepY;
                    travelled = maxY;
                    maxY += deltaY;
                    normalY = -stepY;
                }
                else
                {
                    z += stepZ;
                    travelled = maxZ;
                    maxZ += deltaZ;
                    normalZ = -stepZ;
                }

                if (travelled > maxDistance)
                {
                    break;
                }

                if (source.IsSolidAt(x, y, z))
                {
                    hit.Hit = true;
                    hit.X = x;
                    hit.Y = y;
                    hit.Z = z;
                    hit.NormalX = normalX;
                    hit.NormalY = normalY;
                    hit.NormalZ = normalZ;
                    hit.Distance = travelled;
                    return hit;
                }
            }

            return hit;
        }

        private static int FloorToInt(float value)
        {
            var i = (int)value;
            return value < i ? i - 1 : i;
        }

        private static int Sign(float value)
        {
            if (value > 0f) return 1;
            if (value < 0f) return -1;
            return 0;
        }

        private static float DistanceToNextBoundary(float originComponent, int cell, int step, float delta)
        {
            if (step == 0)
            {
                return float.PositiveInfinity;
            }

            float boundary = step > 0 ? cell + 1 - originComponent : originComponent - cell;
            return boundary * delta;
        }
    }
}
