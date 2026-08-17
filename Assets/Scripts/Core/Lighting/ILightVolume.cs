namespace MyWorld.Core.Lighting
{
    /// <summary>光照传播作用的体积。实现方决定数据如何存储（测试用数组、运行时用区块）。</summary>
    public interface ILightVolume
    {
        int SizeX { get; }

        int SizeY { get; }

        int SizeZ { get; }

        bool Contains(int x, int y, int z);

        bool IsOpaque(int x, int y, int z);

        /// <summary>
        /// 该格方块的静态自发光强度（blocks/*.json 的 lightEmission，火把=14）。
        /// m11 W1-4：方块光全量传播（<see cref="LightPropagator.PropagateBlockLight"/>）的种子来源，
        /// 与 <see cref="IsOpaque"/> 平行的数据通道——由体积的构造方从方块注册表填进来。
        /// </summary>
        byte GetLightEmission(int x, int y, int z);

        byte GetLight(int x, int y, int z);

        void SetLight(int x, int y, int z, byte level);
    }
}
