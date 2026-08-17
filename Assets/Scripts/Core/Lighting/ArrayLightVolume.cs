namespace MyWorld.Core.Lighting
{
    /// <summary>
    /// 基于连续数组的有界光照体积。
    /// m11 W1-4：新增 <see cref="_emission"/> 通道（方块自发光，火把=14），
    /// 与 <see cref="_opaque"/> 同款「构造后由调用方逐格填」的写法。
    /// </summary>
    public sealed class ArrayLightVolume : ILightVolume
    {
        private readonly byte[] _light;
        private readonly bool[] _opaque;
        private readonly byte[] _emission;

        public ArrayLightVolume(int sizeX, int sizeY, int sizeZ)
        {
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            _light = new byte[sizeX * sizeY * sizeZ];
            _opaque = new bool[sizeX * sizeY * sizeZ];
            _emission = new byte[sizeX * sizeY * sizeZ];
        }

        public int SizeX { get; }

        public int SizeY { get; }

        public int SizeZ { get; }

        public bool Contains(int x, int y, int z)
            => x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;

        public bool IsOpaque(int x, int y, int z) => _opaque[Index(x, y, z)];

        public void SetOpaque(int x, int y, int z, bool opaque) => _opaque[Index(x, y, z)] = opaque;

        /// <summary>方块自发光通道（m11 W1-4）：火把等 lightEmission&gt;0 的方块在此登记。</summary>
        public byte GetLightEmission(int x, int y, int z) => _emission[Index(x, y, z)];

        public void SetLightEmission(int x, int y, int z, byte emission) => _emission[Index(x, y, z)] = emission;

        public byte GetLight(int x, int y, int z) => _light[Index(x, y, z)];

        public void SetLight(int x, int y, int z, byte level) => _light[Index(x, y, z)] = level;

        private int Index(int x, int y, int z) => (y * SizeZ + z) * SizeX + x;
    }
}
