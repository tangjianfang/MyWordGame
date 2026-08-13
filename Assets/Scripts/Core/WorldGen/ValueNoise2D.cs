namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 基于整数哈希的二维值噪声。不依赖任何随机数对象，因此同一 seed 与坐标恒定产生相同结果，
    /// 也可安全地并行调用。
    /// </summary>
    public sealed class ValueNoise2D
    {
        private readonly int _seed;

        public ValueNoise2D(int seed)
        {
            _seed = seed;
        }

        public float Sample(float x, float y)
        {
            int x0 = FloorToInt(x);
            int y0 = FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;

            // 五次平滑曲线，一阶与二阶导数在格点处均连续，避免地形出现折角
            float sx = Smooth(fx);
            float sy = Smooth(fy);

            float c00 = HashToUnit(x0, y0);
            float c10 = HashToUnit(x0 + 1, y0);
            float c01 = HashToUnit(x0, y0 + 1);
            float c11 = HashToUnit(x0 + 1, y0 + 1);

            float bottom = Lerp(c00, c10, sx);
            float top = Lerp(c01, c11, sx);
            return Lerp(bottom, top, sy);
        }

        public float SampleFbm(float x, float y, int octaves, float lacunarity, float gain)
        {
            float sum = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float normalization = 0f;

            for (var i = 0; i < octaves; i++)
            {
                sum += Sample(x * frequency, y * frequency) * amplitude;
                normalization += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return normalization > 0f ? sum / normalization : 0f;
        }

        private static int FloorToInt(float value)
        {
            var i = (int)value;
            return value < i ? i - 1 : i;
        }

        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>把格点坐标散列成 [-1, 1] 内的确定值。</summary>
        private float HashToUnit(int x, int y)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263 + _seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / (float)uint.MaxValue * 2f - 1f;
            }
        }

        /// <summary>
        /// 整型 3D 哈希 → [0, 1)。cave 等不需要空间插值的场景用，无需持久化（重建种子重算即可）。
        /// </summary>
        public static float Sample3D(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093 ^ y * 19349663 ^ z * 83492791 ^ seed * 2654435761);
                h ^= h >> 16;
                h *= 0x45d9f3b;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
