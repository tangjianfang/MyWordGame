using MyWorld.Core.Voxel;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 石层嵌矿（m10 A2）。每个方块位置独立用世界坐标哈希判定矿石，无矿返回 <see cref="BlockIds.Stone"/>。
    ///
    /// 纯静态、不持随机对象：同 (seed, x, y, z) 恒定同结果，与生成顺序无关，可并行调用——
    /// 与 <see cref="ValueNoise2D"/> / <see cref="TreeFeature"/> 同一模式。
    ///
    /// 地层与稀有度（h % N == 0 的 N 越大越稀有，N 即「平均每 N 格石头出一个矿」）：
    ///   机元   y &lt; 16，1/400（最稀有，最深）
    ///   合金   y &lt; 24，1/120
    ///   金     y &lt; 32，1/90
    ///   粗铁   y &lt; 48，1/60（最常见，最浅）
    /// 越深的层位能同时刷出越多矿种；y ≥ 48 一律石头。
    /// </summary>
    public static class OreFeature
    {
        /// <summary>机元矿地层上限（不含）。</summary>
        public const int MachineEssenceMaxY = 16;

        /// <summary>夏季合金矿地层上限（不含）。</summary>
        public const int SummerAlloyMaxY = 24;

        /// <summary>金矿地层上限（不含）。</summary>
        public const int GoldMaxY = 32;

        /// <summary>粗铁矿地层上限（不含）。</summary>
        public const int RawIronMaxY = 48;

        /// <summary>机元矿稀有度分母：平均每 400 格石头一个。</summary>
        public const int MachineEssenceDenominator = 400;

        /// <summary>合金矿稀有度分母：平均每 120 格石头一个。</summary>
        public const int SummerAlloyDenominator = 120;

        /// <summary>金矿稀有度分母：平均每 90 格石头一个。</summary>
        public const int GoldDenominator = 90;

        /// <summary>粗铁矿稀有度分母：平均每 60 格石头一个。</summary>
        public const int RawIronDenominator = 60;

        /// <summary>
        /// 判定世界坐标 (x, y, z) 处的石头应嵌成哪种矿石（无矿返回 Stone）。
        ///
        /// 四种矿各自占一个互不嵌套的余数类（%400==3、%120==2、%90==1、%60==0）而不是共用 ==0：
        /// 若都用 ==0，倍数嵌套会让深层矿偷走粗铁的命中——60 的倍数里一半是 120 的倍数、
        /// 三分之一是 90 的倍数，深层粗铁实际退化到 1/180，全世界粗铁总量会跟金打平（实测比值仅 1.04）。
        /// 错开余数后重叠概率只有 1/lcm 量级（&lt;3%），各矿密度即名义值 1/N。
        /// </summary>
        public static ushort OreAt(int seed, int x, int y, int z)
        {
            uint h = Hash(seed, x, y, z);
            if (y < MachineEssenceMaxY && h % MachineEssenceDenominator == 3) return BlockIds.MachineEssenceOre;
            if (y < SummerAlloyMaxY && h % SummerAlloyDenominator == 2) return BlockIds.SummerAlloyOre;
            if (y < GoldMaxY && h % GoldDenominator == 1) return BlockIds.GoldOre;
            if (y < RawIronMaxY && h % RawIronDenominator == 0) return BlockIds.RawIronOre;
            return BlockIds.Stone;
        }

        /// <summary>
        /// FNV-1a（32 位）混入 seed 与三维世界坐标（逐 32 位字步进，等价于按小端字节流做标准 FNV-1a），
        /// 末尾接 murmur3 终结雪崩，保证取模所用的低位也均匀分布。
        /// </summary>
        private static uint Hash(int seed, int x, int y, int z)
        {
            unchecked
            {
                const uint fnvOffsetBasis = 2166136261u;
                const uint fnvPrime = 16777619u;

                uint h = fnvOffsetBasis;
                h = (h ^ (uint)seed) * fnvPrime;
                h = (h ^ (uint)x) * fnvPrime;
                h = (h ^ (uint)y) * fnvPrime;
                h = (h ^ (uint)z) * fnvPrime;

                h ^= h >> 16;
                h *= 0x45d9f3bu;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
