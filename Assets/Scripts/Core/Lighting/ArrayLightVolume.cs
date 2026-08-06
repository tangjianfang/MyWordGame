namespace MyWorld.Core.Lighting
{
    /// <summary>基于连续数组的有界光照体积。</summary>
    public sealed class ArrayLightVolume : ILightVolume
    {
        private readonly byte[] _light;
        private readonly bool[] _opaque;

        public ArrayLightVolume(int sizeX, int sizeY, int sizeZ)
        {
            SizeX = sizeX;
            SizeY = sizeY;
            SizeZ = sizeZ;
            _light = new byte[sizeX * sizeY * sizeZ];
            _opaque = new bool[sizeX * sizeY * sizeZ];
        }

        public int SizeX { get; }

        public int SizeY { get; }

        public int SizeZ { get; }

        public bool Contains(int x, int y, int z)
            => x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;

        public bool IsOpaque(int x, int y, int z) => _opaque[Index(x, y, z)];

        public void SetOpaque(int x, int y, int z, bool opaque) => _opaque[Index(x, y, z)] = opaque;

        public byte GetLight(int x, int y, int z) => _light[Index(x, y, z)];

        public void SetLight(int x, int y, int z, byte level) => _light[Index(x, y, z)] = level;

        private int Index(int x, int y, int z) => (y * SizeZ + z) * SizeX + x;
    }
}
