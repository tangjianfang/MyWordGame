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

        byte GetLight(int x, int y, int z);

        void SetLight(int x, int y, int z, byte level);
    }
}
