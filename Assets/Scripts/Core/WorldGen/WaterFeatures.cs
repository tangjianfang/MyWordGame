using System.Collections.Generic;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.WorldGen
{
    /// <summary>
    /// 在 <see cref="WorldGenerator"/> 之后追加水域。
    /// 思路：用 per-chunk 哈希决定这片区域要放湖/海/水洼，再覆盖地表之上到水面。
    /// </summary>
    public static class WaterFeatures
    {
        public const int SeaRadius = 24;     // 大海半径
        public const int LakeRadius = 6;     // 湖半径
        public const int PuddleRadius = 2;   // 水洼半径

        public const float SeaChance = 0.04f;
        public const float LakeChance = 0.18f;
        public const float PuddleChance = 0.35f;

        public const int SeaLevel = 62;

        public static void Generate(World world, int seed, int chunkX, int chunkZ)
        {
            // 决定本 chunk 是否被某个水体覆盖
            int centerX = chunkX * 16 + 8;
            int centerZ = chunkZ * 16 + 8;
            var (cx, cz, radius) = FindWaterBody(seed, centerX, centerZ);
            if (cx < 0) return;

            int dx = centerX - cx;
            int dz = centerZ - cz;
            int distSq = dx * dx + dz * dz;
            int radiusSq = radius * radius;
            if (distSq > radiusSq) return;

            // 取一个公共水面高度
            int waterTop = SeaLevel;

            // 取得现存的 chunk column，没有就不动（流式加载阶段会从生成器拿到）
            if (!world.TryGetChunk(new ChunkPos(chunkX, chunkZ), out var column)) return;

            for (int lx = 0; lx < 16; lx++)
            for (int lz = 0; lz < 16; lz++)
            {
                int wx = chunkX * 16 + lx;
                int wz = chunkZ * 16 + lz;
                int ddx = wx - cx;
                int ddz = wz - cz;
                int ddSq = ddx * ddx + ddz * ddz;
                if (ddSq > radiusSq) continue;

                // 找当前最高非空气方块
                int topY = -1;
                for (int y = 255; y >= 0; y--)
                {
                    var id = column.GetBlock(lx, y, lz);
                    if (id != 0) { topY = y; break; }
                }
                if (topY < 0) continue;

                // 水面要高于地表：把 [topY+1, waterTop] 都填成水
                for (int y = topY + 1; y <= waterTop; y++)
                {
                    column.SetBlock(lx, y, lz, BlockIds.Water);
                }
            }
        }

        private static (int cx, int cz, int radius) FindWaterBody(int seed, int centerX, int centerZ)
        {
            // 用 chunk 坐标哈希决定最近的水体中心与半径
            // 简化：每 8 个 chunk 检查一次（grid 8x8 = 128x128 格）
            int gridSize = 8;
            int nearestX = 0, nearestZ = 0, nearestR = 0;
            int nearestDistSq = int.MaxValue;
            for (int gx = -2; gx <= 2; gx++)
            for (int gz = -2; gz <= 2; gz++)
            {
                int wx = (centerX / gridSize + gx) * gridSize + gridSize / 2;
                int wz = (centerZ / gridSize + gz) * gridSize + gridSize / 2;
                float h = Hash2D(seed, wx, wz);
                int radius = 0;
                if (h < SeaChance) radius = SeaRadius + (int)((h / SeaChance) * 6);
                else if (h < SeaChance + LakeChance) radius = LakeRadius + (int)(((h - SeaChance) / LakeChance) * 3);
                else if (h < SeaChance + LakeChance + PuddleChance) radius = PuddleRadius;
                else continue;
                int dx = wx - centerX;
                int dz = wz - centerZ;
                int dSq = dx * dx + dz * dz;
                if (dSq < nearestDistSq) { nearestDistSq = dSq; nearestX = wx; nearestZ = wz; nearestR = radius; }
            }
            if (nearestR == 0) return (-1, -1, 0);
            return (nearestX, nearestZ, nearestR);
        }

        private static float Hash2D(int seed, int x, int z)
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ x;
                h = (h * 397) ^ z;
                h ^= h >> 13;
                h *= 0x5BD1E995;
                h ^= h >> 15;
                // 映射到 [0, 1)
                return (h & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }
    }
}
