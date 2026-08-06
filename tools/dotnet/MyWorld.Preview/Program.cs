using System;
using System.Text;
using MyWorld.Core.Meshing;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;

namespace MyWorld.Preview
{
    /// <summary>
    /// 在没有 Unity 的情况下，用文本预览世界生成结果。这是本项目"用文本表达视觉"的主要工具。
    /// </summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            int seed = args.Length > 0 && int.TryParse(args[0], out int parsed) ? parsed : 20260806;
            var generator = new WorldGenerator(seed);

            Console.WriteLine($"种子: {seed}");
            Console.WriteLine();

            PrintCrossSection(generator);
            Console.WriteLine();
            PrintHeightMap(generator);
            Console.WriteLine();
            PrintMeshStats(generator);
        }

        /// <summary>纵向剖面：直观展示地表起伏、土层厚度与水面。</summary>
        private static void PrintCrossSection(WorldGenerator generator)
        {
            const int width = 110;
            const int top = 100;
            const int bottom = 40;

            Console.WriteLine($"── 纵向剖面 (z=0, x=0..{width - 1}, y={bottom}..{top}) ──");

            var world = new World();
            LoadChunksForRow(generator, world, width);

            var line = new StringBuilder(width);
            for (int y = top; y >= bottom; y--)
            {
                line.Clear();
                for (var x = 0; x < width; x++)
                {
                    line.Append(Glyph(world.GetBlock(x, y, 0)));
                }

                Console.WriteLine($"{y,4} |{line}");
            }

            Console.WriteLine("     +" + new string('-', width));
            Console.WriteLine("     图例: '\"'草 '.'土 '#'石 ':'沙 '~'水 '_'基岩 ' '空气");
        }

        /// <summary>俯视高度图：用字符深浅表达海拔，检查大尺度地形是否自然。</summary>
        private static void PrintHeightMap(WorldGenerator generator)
        {
            const int size = 60;
            const string ramp = " .:-=+*#%@";

            Console.WriteLine($"── 俯视高度图 ({size}×{size} 方块, 每字符 4 方块) ──");

            var line = new StringBuilder(size);
            for (var z = 0; z < size; z++)
            {
                line.Clear();
                for (var x = 0; x < size; x++)
                {
                    int height = generator.SurfaceHeightAt(x * 4, z * 4);
                    int level = Math.Clamp((height - 40) * ramp.Length / 60, 0, ramp.Length - 1);
                    line.Append(ramp[level]);
                }

                Console.WriteLine("     " + line);
            }
        }

        /// <summary>用真实区块数据验证贪心合并的压缩效果。</summary>
        private static void PrintMeshStats(WorldGenerator generator)
        {
            ChunkColumn column = generator.Generate(new ChunkPos(0, 0));

            // 选取跨越地表的那一段，避免全实心或全空气的段落给出失真的压缩比
            int surface = generator.SurfaceHeightAt(8, 8);
            int sectionIndex = VoxelCoords.SectionIndexForY(surface);
            int sectionBaseY = VoxelCoords.MinY + sectionIndex * ChunkSection.Size;

            Console.WriteLine($"── 贪心网格压缩效果 (区块 0,0, 地表段 y={sectionBaseY}..{sectionBaseY + 15}) ──");

            var mesh = new MeshBuffer();
            var source = new ColumnSectionSource(column, sectionBaseY);

            GreedyMesher.Build(source, mesh);

            int naiveQuads = CountNaiveQuads(source);
            Console.WriteLine($"     朴素逐面: {naiveQuads} 个四边形");
            Console.WriteLine($"     贪心合并: {mesh.QuadCount} 个四边形");

            if (mesh.QuadCount > 0)
            {
                Console.WriteLine($"     压缩比:   {naiveQuads / (float)mesh.QuadCount:F2}×");
            }
        }

        private static int CountNaiveQuads(ColumnSectionSource source)
        {
            var count = 0;
            for (var y = 0; y < ChunkSection.Size; y++)
            for (var z = 0; z < ChunkSection.Size; z++)
            for (var x = 0; x < ChunkSection.Size; x++)
            {
                if (!source.IsSolid(source.GetBlock(x, y, z)))
                {
                    continue;
                }

                if (!source.IsSolid(source.GetBlock(x + 1, y, z))) count++;
                if (!source.IsSolid(source.GetBlock(x - 1, y, z))) count++;
                if (!source.IsSolid(source.GetBlock(x, y + 1, z))) count++;
                if (!source.IsSolid(source.GetBlock(x, y - 1, z))) count++;
                if (!source.IsSolid(source.GetBlock(x, y, z + 1))) count++;
                if (!source.IsSolid(source.GetBlock(x, y, z - 1))) count++;
            }

            return count;
        }

        private static void LoadChunksForRow(WorldGenerator generator, World world, int width)
        {
            int chunkCount = width / VoxelCoords.ChunkSize + 1;
            for (var chunkX = 0; chunkX < chunkCount; chunkX++)
            {
                ChunkColumn column = generator.Generate(new ChunkPos(chunkX, 0));
                for (int y = VoxelCoords.MinY; y < VoxelCoords.MaxY; y++)
                {
                    for (var localX = 0; localX < VoxelCoords.ChunkSize; localX++)
                    {
                        ushort block = column.GetBlock(localX, y, 0);
                        if (block != BlockIds.Air)
                        {
                            world.SetBlock(chunkX * VoxelCoords.ChunkSize + localX, y, 0, block);
                        }
                    }
                }
            }
        }

        private static char Glyph(ushort blockId)
        {
            switch (blockId)
            {
                case BlockIds.Grass: return '"';
                case BlockIds.Dirt: return '.';
                case BlockIds.Stone: return '#';
                case BlockIds.Sand: return ':';
                case BlockIds.Water: return '~';
                case BlockIds.Bedrock: return '_';
                default: return ' ';
            }
        }
    }

    /// <summary>把区块列的某一段包装成网格生成所需的方块来源。</summary>
    internal readonly struct ColumnSectionSource : IBlockSource
    {
        private readonly ChunkColumn _column;
        private readonly int _baseY;

        public ColumnSectionSource(ChunkColumn column, int sectionBaseY)
        {
            _column = column;
            _baseY = sectionBaseY;
        }

        public ushort GetBlock(int x, int y, int z)
        {
            if (x < 0 || x >= ChunkSection.Size || z < 0 || z >= ChunkSection.Size)
            {
                return BlockIds.Air;
            }

            return _column.GetBlock(x, _baseY + y, z);
        }

        public bool IsSolid(ushort blockId) => blockId != BlockIds.Air && blockId != BlockIds.Water;
    }
}
