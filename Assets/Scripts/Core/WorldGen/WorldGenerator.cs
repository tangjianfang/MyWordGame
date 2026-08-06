using MyWorld.Core.Voxel;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 世界生成入口。所有阶段只依赖 seed 与世界坐标，不依赖生成顺序，保证跨区块地形连续且可复现。
    /// </summary>
    public sealed class WorldGenerator
    {
        private const float TerrainScale = 0.008f;
        private const int SeaLevel = 62;
        private const int BaseHeight = 68;
        private const int HeightAmplitude = 40;
        private const int DirtDepth = 4;

        private readonly ValueNoise2D _terrainNoise;

        public WorldGenerator(int seed)
        {
            _terrainNoise = new ValueNoise2D(seed);
        }

        public ChunkColumn Generate(ChunkPos pos)
        {
            var column = new ChunkColumn();

            int originX = pos.X * VoxelCoords.ChunkSize;
            int originZ = pos.Z * VoxelCoords.ChunkSize;

            for (var localZ = 0; localZ < VoxelCoords.ChunkSize; localZ++)
            {
                for (var localX = 0; localX < VoxelCoords.ChunkSize; localX++)
                {
                    // 以世界坐标而非区块内坐标采样，接缝两侧自然对齐
                    int surfaceY = SurfaceHeightAt(originX + localX, originZ + localZ);
                    FillColumn(column, localX, localZ, surfaceY);
                }
            }

            return column;
        }

        public int SurfaceHeightAt(int worldX, int worldZ)
        {
            float noise = _terrainNoise.SampleFbm(worldX * TerrainScale, worldZ * TerrainScale,
                octaves: 4, lacunarity: 2f, gain: 0.5f);
            return BaseHeight + (int)(noise * HeightAmplitude);
        }

        private static void FillColumn(ChunkColumn column, int localX, int localZ, int surfaceY)
        {
            column.SetBlock(localX, VoxelCoords.MinY, localZ, BlockIds.Bedrock);

            for (int y = VoxelCoords.MinY + 1; y <= surfaceY; y++)
            {
                ushort block;
                if (y == surfaceY)
                {
                    block = surfaceY <= SeaLevel ? BlockIds.Sand : BlockIds.Grass;
                }
                else if (y > surfaceY - DirtDepth)
                {
                    block = BlockIds.Dirt;
                }
                else
                {
                    block = BlockIds.Stone;
                }

                column.SetBlock(localX, y, localZ, block);
            }

            for (int y = surfaceY + 1; y <= SeaLevel; y++)
            {
                column.SetBlock(localX, y, localZ, BlockIds.Water);
            }
        }
    }
}
