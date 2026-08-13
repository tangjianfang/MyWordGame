using MyWorld.Core.Math;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 3D 洞穴雕刻：基于 3D ValueNoise 阈值挖空。基岩层永不挖，C3 接入 WorldGenerator 时
    /// 把地表下方石头的 carve 改写为空气。完全依赖 seed + 世界坐标，可并行调用。
    /// </summary>
    public class CaveCarver
    {
        private const int BedrockLevel = -64;
        private const float NoiseThreshold = 0.65f;  // 噪声 > 此值挖空

        private readonly int _seed;

        public CaveCarver(int seed) { _seed = seed; }

        public bool ShouldCarve(Float3 worldPos, float localDensity)
        {
            if (worldPos.Y <= BedrockLevel) return false;
            float noise = ValueNoise2D.Sample3D(
                (int)worldPos.X, (int)worldPos.Y, (int)worldPos.Z, _seed);
            // localDensity 0=平原（少洞）1=山地（多洞）
            float threshold = NoiseThreshold - localDensity * 0.2f;
            return noise > threshold;
        }
    }
}
