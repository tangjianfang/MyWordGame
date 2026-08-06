using System.Collections.Generic;

namespace MyWorld.Core.Lighting
{
    /// <summary>
    /// 光照传播。方块光用 BFS 洪泛，因此能绕过拐角；移除光源采用去光 + 重传播的增量算法，
    /// 避免每次方块变更都重算整个区块。
    /// </summary>
    public static class LightPropagator
    {
        public const byte MaxLevel = 15;

        private static readonly int[] NeighbourX = { 1, -1, 0, 0, 0, 0 };
        private static readonly int[] NeighbourY = { 0, 0, 1, -1, 0, 0 };
        private static readonly int[] NeighbourZ = { 0, 0, 0, 0, 1, -1 };

        public static void AddBlockLight(ILightVolume volume, int x, int y, int z, byte level)
        {
            if (!volume.Contains(x, y, z) || level == 0)
            {
                return;
            }

            volume.SetLight(x, y, z, level);

            var queue = new Queue<(int X, int Y, int Z)>();
            queue.Enqueue((x, y, z));
            Flood(volume, queue);
        }

        public static void RemoveBlockLight(ILightVolume volume, int x, int y, int z)
        {
            if (!volume.Contains(x, y, z))
            {
                return;
            }

            byte removed = volume.GetLight(x, y, z);
            if (removed == 0)
            {
                return;
            }

            volume.SetLight(x, y, z, 0);

            var removals = new Queue<(int X, int Y, int Z, byte Level)>();
            var repropagate = new Queue<(int X, int Y, int Z)>();
            removals.Enqueue((x, y, z, removed));

            while (removals.Count > 0)
            {
                (int cx, int cy, int cz, byte level) = removals.Dequeue();

                for (var i = 0; i < 6; i++)
                {
                    int nx = cx + NeighbourX[i];
                    int ny = cy + NeighbourY[i];
                    int nz = cz + NeighbourZ[i];

                    if (!volume.Contains(nx, ny, nz))
                    {
                        continue;
                    }

                    byte neighbourLevel = volume.GetLight(nx, ny, nz);
                    if (neighbourLevel == 0)
                    {
                        continue;
                    }

                    if (neighbourLevel < level)
                    {
                        volume.SetLight(nx, ny, nz, 0);
                        removals.Enqueue((nx, ny, nz, neighbourLevel));
                    }
                    else
                    {
                        // 该邻居由其它光源供光，作为重传播的种子
                        repropagate.Enqueue((nx, ny, nz));
                    }
                }
            }

            Flood(volume, repropagate);
        }

        public static void PropagateSkyLight(ILightVolume volume)
        {
            var queue = new Queue<(int X, int Y, int Z)>();

            // 自顶向下柱状直射：未被遮挡时不衰减，一旦遇到不透光方块，其下方直射光归零
            for (var x = 0; x < volume.SizeX; x++)
            {
                for (var z = 0; z < volume.SizeZ; z++)
                {
                    byte level = MaxLevel;
                    for (int y = volume.SizeY - 1; y >= 0; y--)
                    {
                        if (volume.IsOpaque(x, y, z))
                        {
                            level = 0;
                        }

                        volume.SetLight(x, y, z, level);

                        if (level == MaxLevel)
                        {
                            queue.Enqueue((x, y, z));
                        }
                    }
                }
            }

            Flood(volume, queue);
        }

        private static void Flood(ILightVolume volume, Queue<(int X, int Y, int Z)> queue)
        {
            while (queue.Count > 0)
            {
                (int cx, int cy, int cz) = queue.Dequeue();
                byte current = volume.GetLight(cx, cy, cz);
                if (current <= 1)
                {
                    continue;
                }

                var next = (byte)(current - 1);

                for (var i = 0; i < 6; i++)
                {
                    int nx = cx + NeighbourX[i];
                    int ny = cy + NeighbourY[i];
                    int nz = cz + NeighbourZ[i];

                    if (!volume.Contains(nx, ny, nz) || volume.IsOpaque(nx, ny, nz))
                    {
                        continue;
                    }

                    if (volume.GetLight(nx, ny, nz) >= next)
                    {
                        continue;
                    }

                    volume.SetLight(nx, ny, nz, next);
                    queue.Enqueue((nx, ny, nz));
                }
            }
        }
    }
}
