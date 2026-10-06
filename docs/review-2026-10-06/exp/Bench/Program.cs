// 审查实验：后台线程 / 存档与区块 IO / 流式加载 / 缓存 量化基准。
// 只读仓库源码（经 ProjectReference 链接编译），全部数据写在 %TEMP% 下的合成目录，
// 不触碰真实存档 C:\Users\...\worlds\。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MyWorld.Core.Blocks;
using MyWorld.Core.Lighting;
using MyWorld.Core.Math;
using MyWorld.Core.Meshing;
using MyWorld.Core.Persistence;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using Newtonsoft.Json;

namespace MyWorld.Bench
{
    internal static class Program
    {
        private static BlockRegistry _registry;
        private static WorldGenerator _generator;
        private static string _root; // 合成数据根目录（TEMP 下）

        private static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            _root = Path.Combine(Path.GetTempPath(), "myword-bench-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_root);

            _registry = LoadBlockRegistry();
            _generator = MakeGenerator(42);

            Console.WriteLine($"合成数据目录: {_root}");
            Console.WriteLine($"方块注册表: {_registry.Count} 项");
            Console.WriteLine();

            BenchGenerate();
            BenchMesh();
            BenchSerialize();
            BenchRegionWriteAmplify();
            BenchTryLoadChunk();
            BenchLevelDat();
            BenchLightVolume();
            BenchParallelGenerate();

            Console.WriteLine("全部实验完成。");
        }

        // ── 基建 ───────────────────────────────────────────────────────────

        private static BlockRegistry LoadBlockRegistry()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "blocks");
                if (Directory.Exists(candidate))
                {
                    return BlockRegistry.FromJson(Directory.GetFiles(candidate, "*.json").Select(File.ReadAllText));
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("未找到 Assets/StreamingAssets/blocks 目录。");
        }

        /// <summary>照游戏正式链路（WorldBootstrap.CreateGenerator）建 4 参生成器。</summary>
        private static WorldGenerator MakeGenerator(int seed)
        {
            string sa = FindStreamingAssets();
            var vegetation = VegetationTable.Load(
                File.ReadAllText(Path.Combine(sa, "vegetation", "trees.json")),
                File.ReadAllText(Path.Combine(sa, "vegetation", "flowers.json")));
            var biomes = BiomeConfigLoader.Load(Path.Combine(sa, "biomes.json"));
            return new WorldGenerator(seed, biomes, vegetation, _registry);
        }

        private static string FindStreamingAssets()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets");
                if (Directory.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("未找到 Assets/StreamingAssets。");
        }

        private static (double min, double avg, double max) Time(string label, int warmup, int iters, Action act)
        {
            for (int i = 0; i < warmup; i++) act();
            var samples = new double[iters];
            for (int i = 0; i < iters; i++)
            {
                var sw = Stopwatch.StartNew();
                act();
                samples[i] = sw.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            double avg = samples.Average();
            Console.WriteLine($"  {label,-58} min={samples[0],9:0.000} ms  avg={avg,9:0.000} ms  max={samples[^1],9:0.000} ms");
            return (samples[0], avg, samples[^1]);
        }

        /// <summary>生成以 (0,0) 为中心 side×side 的区块网格（含邻居，供网格/光照采样）。</summary>
        private static World BuildWorldArea(int side)
        {
            var world = new World();
            int half = side / 2;
            for (int cx = -half; cx <= half; cx++)
            for (int cz = -half; cz <= half; cz++)
            {
                world.AddChunk(new ChunkPos(cx, cz), _generator.Generate(new ChunkPos(cx, cz)));
            }
            return world;
        }

        // ── 1) 单列生成成本 ────────────────────────────────────────────────

        private static void BenchGenerate()
        {
            Console.WriteLine("── [G1] WorldGenerator.Generate 单列（正式链路 4 参构造，seed=42）──");
            var poses = Enumerable.Range(0, 64).Select(i => new ChunkPos(i % 8 - 4, i / 8 - 4)).ToArray();
            int k = 0;
            ChunkColumn sink = null;
            Time("单列 Generate", 4, 32, () => { sink = _generator.Generate(poses[k++ % poses.Length]); });
            GC.KeepAlive(sink);

            // 生成一列 + region overlay 读（无 region 文件时的 File.Exists 短路路径）
            Console.WriteLine();
        }

        // ── 2) 网格重建成本（挖一格最多牵连 8 段） ─────────────────────────

        private static void BenchMesh()
        {
            Console.WriteLine("── [G2] GreedyMesher.Build 单 section（地表段，MarkBlockChanged 的重建单元）──");
            var world = BuildWorldArea(5);
            int surfaceSection = VoxelCoords.SectionIndexForY(96);
            var source = new ChunkMeshSource(world, _registry, new ChunkPos(0, 0), surfaceSection * 16 + VoxelCoords.MinY);
            var buffer = new MeshBuffer();
            Time("地表段建网格（含邻居采样）", 4, 32, () => GreedyMesher.Build(source, buffer));
            Console.WriteLine($"  本段四边形数: {buffer.QuadCount}(参考)");
            Console.WriteLine();
        }

        // ── 3) ChunkSerializer 序列化/反序列化 ────────────────────────────

        private static void BenchSerialize()
        {
            Console.WriteLine("── [G3] ChunkSerializer（CompressionLevel.Optimal）──");
            var column = _generator.Generate(new ChunkPos(0, 0));
            byte[] payload = null;
            Time("地表列 Serialize（~11 个非空 section）", 3, 16, () => payload = ChunkSerializer.Serialize(column));
            Console.WriteLine($"  产物大小: {payload.Length / 1024.0:0.0} KB");
            Time("地表列 Deserialize", 3, 16, () => ChunkSerializer.Deserialize(payload));

            // 全 24 section 满的极限列
            var full = new ChunkColumn();
            for (int s = 0; s < VoxelCoords.SectionCount; s++)
            {
                var sec = full.GetOrCreateSection(s);
                for (int y = 0; y < 16; y++)
                for (int z = 0; z < 16; z++)
                for (int x = 0; x < 16; x++)
                {
                    ushort id = (ushort)(((x + y + z + s) % 7) == 0 ? 1000 : 3); // 混杂嵌矿，贴近实际
                    sec.Set(x, y, z, id);
                }
            }
            byte[] fullPayload = null;
            Time("满 24 section 列 Serialize（极限）", 2, 8, () => fullPayload = ChunkSerializer.Serialize(full));
            Console.WriteLine($"  产物大小: {fullPayload.Length / 1024.0:0.0} KB");
            Console.WriteLine();
        }

        // ── 4) region 写放大：每轮全文件读-改-写 ───────────────────────────

        private static void BenchRegionWriteAmplify()
        {
            Console.WriteLine("── [G4] RegionSaveCoordinator.SaveDirty 写放大（读-改-写整文件/轮）──");
            foreach (int n in new[] { 8, 64, 256, 1024 })
            {
                string dir = Path.Combine(_root, "amp-" + n);
                var world = new World();
                // 用同一根生成列填满 region 的前 n 个槽（写内容不同即可，借用不同坐标的列）
                int i = 0;
                for (int cx = 0; cx < 32 && i < n; cx++)
                for (int cz = 0; cz < 32 && i < n; cz++)
                {
                    var pos = new ChunkPos(cx, cz);
                    world.AddChunk(pos, _generator.Generate(pos));
                    world.SetBlock(cx * 16 + 8, 96, cz * 16 + 8, 3); // 标脏
                    i++;
                }
                var snapshot = new Dictionary<ChunkPos, ChunkColumn>();
                foreach (var pos in world.ChunkPositions) snapshot[pos] = world.TryGetChunk(pos, out var c) ? c : null;
                var savedOut = new List<ChunkPos>();
                string label0 = $"region 满 {n,4} 块·首轮全量写";
                Time(label0, 0, 1, () => RegionSaveCoordinator.SaveDirty(snapshot, dir, savedOut));

                // 增量：只改 1 块再存——量测「读-改-写」的固定成本（快照只含这 1 块）
                world.SetBlock(8, 96, 8, 3);
                var incSnapshot = new Dictionary<ChunkPos, ChunkColumn>
                {
                    [new ChunkPos(0, 0)] = snapshot[new ChunkPos(0, 0)],
                };
                savedOut.Clear();
                string label1 = $"region 满 {n,4} 块·增量 1 块再写";
                Time(label1, 0, 3, () => RegionSaveCoordinator.SaveDirty(incSnapshot, dir, savedOut));
                Console.WriteLine($"      文件大小: {new FileInfo(Path.Combine(dir, "r.0.0.mwr")).Length / 1024.0:0.0} KB");
            }
            Console.WriteLine();
        }

        // ── 5) TryLoadChunk 每块整文件解析 ─────────────────────────────────

        private static void BenchTryLoadChunk()
        {
            Console.WriteLine("── [G5] RegionSaveCoordinator.TryLoadChunk（每块=开文件+解析整个 region）──");
            foreach (int n in new[] { 16, 256, 1024 })
            {
                string dir = Path.Combine(_root, "load-" + n);
                var world = new World();
                int i = 0;
                for (int cx = 0; cx < 32 && i < n; cx++)
                for (int cz = 0; cz < 32 && i < n; cz++)
                {
                    var pos = new ChunkPos(cx, cz);
                    world.AddChunk(pos, _generator.Generate(pos));
                    world.SetBlock(cx * 16 + 8, 96, cz * 16 + 8, 3);
                    i++;
                }
                RegionSaveCoordinator.SaveDirty(world, dir);

                var reader = new World();
                var targets = Enumerable.Range(0, 8).Select(j => new ChunkPos(j, 0)).ToArray();
                int hits = 0;
                string label = $"region {n,4} 块·连续加载 8 根（每根一次整文件解析）";
                Time(label, 0, 3, () =>
                {
                    foreach (var pos in targets)
                    {
                        if (RegionSaveCoordinator.TryLoadChunk(reader, pos, dir)) hits++;
                    }
                });
                Console.WriteLine($"      命中 {hits}/8");
            }
            Console.WriteLine();
        }

        // ── 6) level.dat JSON 往返（真实结构合成） ──────────────────────────

        private static void BenchLevelDat()
        {
            Console.WriteLine("── [G6] LevelDataCodec（Formatting.Indented）合成档往返 ──");
            foreach (var (chests, drops, stats, farms) in new[]
                     {
                         (0, 0, 0, 0),        // 空档
                         (10, 100, 50, 20),   // 轻度
                         (50, 237, 200, 100), // ≈ 重档 42 现状 + 中度农场
                         (200, 500, 1000, 500), // 放大 4 倍
                     })
            {
                var data = MakeLevelData(chests, drops, stats, farms);
                string path = Path.Combine(_root, $"level-{chests}-{drops}-{stats}.dat");
                Time($"写 level.dat（箱{chests,3}·掉落{drops,3}·统计{stats,4}·田{farms,3}）", 1, 8,
                    () => LevelDataCodec.Save(data, path));
                long bytes = new FileInfo(path).Length;
                Console.WriteLine($"      文件大小: {bytes / 1024.0:0.0} KB");
                LevelData loaded = null;
                Time($"读 level.dat 同档", 1, 8, () => loaded = LevelDataCodec.Load(path));
                GC.KeepAlive(loaded);
            }
            Console.WriteLine();
        }

        private static LevelData MakeLevelData(int chests, int drops, int stats, int farms)
        {
            var rngSeed = 12345;
            var data = new LevelData
            {
                Seed = 42,
                TimeTick = 12345f,
                Player = new PlayerSnapshot
                {
                    X = 1.5f, Y = 100f, Z = 2.5f, HealthCurrent = 18, HealthMax = 20,
                    Hunger = 17, Saturation = 4f, ExpCurrent = 30, ExpLevel = 3,
                    SelectedHotbarIndex = 2,
                    Slots = MakeSlots(36, rngSeed),
                    ArmorSlots = MakeSlots(4, rngSeed),
                },
                Drops = Enumerable.Range(0, drops).Select(i => new DropSnapshot
                {
                    ItemId = 1000 + (i % 40), Count = 1 + (i % 5), Metadata = (ushort)(i % 250),
                    X = i * 0.5f, Y = 96f, Z = i * 0.25f,
                }).ToList(),
            };
            var chestDict = new Dictionary<string, List<DropSnapshot>>();
            for (int c = 0; c < chests; c++)
            {
                chestDict[$"{c * 7},{96},{c * 5}"] = Enumerable.Range(0, 27).Select(s => new DropSnapshot
                {
                    ItemId = 1000 + ((c + s) % 40), Count = 1 + (s % 12), Metadata = (ushort)(s % 250),
                }).ToList();
            }
            data.ChestContents = chestDict;
            var statDict = new Dictionary<string, int>();
            for (int s = 0; s < stats; s++) statDict[$"stat.{s / 10}.{s % 10}"] = s * 37;
            data.Stats = statDict;
            var farmDict = new Dictionary<string, string>();
            for (int f = 0; f < farms; f++) farmDict[$"{f * 3},{96},{f * 9}"] = $"wheat:{f % 7}:{f * 1.5f:0.0}";
            data.FarmStates = farmDict;
            return data;
        }

        private static SlotSnapshot[] MakeSlots(int count, int seed)
        {
            var slots = new SlotSnapshot[count];
            for (int i = 0; i < count; i++)
            {
                slots[i] = (i % 3 == 0)
                    ? new SlotSnapshot { ItemId = 1000 + (i % 40), Count = 1 + (i % 8), Metadata = (ushort)(i % 250) }
                    : new SlotSnapshot();
            }
            return slots;
        }

        // ── 7) 光照体积重建（ChunkLightSystem 每 2s 的两份体积） ────────────

        private static void BenchLightVolume()
        {
            Console.WriteLine("── [G7] WorldLightVolumeBuilder（ChunkLightSystem 每 2s 重建，48×96×48 ×2 份）──");
            var world = BuildWorldArea(5); // 覆盖 3×3 脚印 + 邻居
            int originX = -24, originZ = -24, originY = 96 - 48;
            WorldLightVolume a = null, b = null;
            Time("Build（合成体积：不透明+天光+方块光）", 1, 8,
                () => a = WorldLightVolumeBuilder.Build(world, _registry, originX, originY, originZ, 48, 96, 48));
            Time("BuildBlockLight（纯方块光体积）", 1, 8,
                () => b = WorldLightVolumeBuilder.BuildBlockLight(world, _registry, originX, originY, originZ, 48, 96, 48));
            long allocPerRound = 2L * 48 * 96 * 48 * 3; // byte+bool+byte 每体积
            Console.WriteLine($"  每轮两体积新分配 ≈ {allocPerRound / 1024.0:0.0} KB（2s 一次 → {allocPerRound * 30 / 1024 / 1024.0:0.0} MB/分钟 GC 压力）");
            GC.KeepAlive(a);
            GC.KeepAlive(b);
            Console.WriteLine();
        }

        // ── 8) 生成并行化潜力 ─────────────────────────────────────────────

        private static void BenchParallelGenerate()
        {
            Console.WriteLine("── [G8] Generate 并行化潜力（同一 WorldGenerator 实例共享，测线程安全与扩展性）──");
            var targets = Enumerable.Range(0, 256).Select(i => new ChunkPos(i % 16 - 8, i / 16 - 8)).ToArray();
            var reference = targets.Select(p => ChunkSerializer.Serialize(_generator.Generate(p))).ToArray();

            foreach (int threads in new[] { 1, 2, 4, 8 })
            {
                var columns = new ChunkColumn[targets.Length];
                long elapsed = 0;
                for (int trial = 0; trial < 2; trial++)
                {
                    var sw = Stopwatch.StartNew();
                    if (threads == 1)
                    {
                        for (int i = 0; i < targets.Length; i++) columns[i] = _generator.Generate(targets[i]);
                    }
                    else
                    {
                        int next = 0;
                        var workers = new Thread[threads];
                        for (int t = 0; t < threads; t++)
                        {
                            workers[t] = new Thread(() =>
                            {
                                while (true)
                                {
                                    int i = Interlocked.Increment(ref next) - 1;
                                    if (i >= targets.Length) break;
                                    columns[i] = _generator.Generate(targets[i]);
                                }
                            });
                            workers[t].Start();
                        }
                        foreach (var w in workers) w.Join();
                    }
                    sw.Stop();
                    elapsed = sw.ElapsedMilliseconds;
                }
                // 一致性校验：并行产物与单线程参照逐字节比对
                bool identical = true;
                for (int i = 0; i < targets.Length; i++)
                {
                    var bytes = ChunkSerializer.Serialize(columns[i]);
                    if (!bytes.AsSpan().SequenceEqual(reference[i].AsSpan())) { identical = false; break; }
                }
                Console.WriteLine($"  {threads} 线程生成 256 列: {elapsed,5} ms   与单线程产物逐字节一致: {(identical ? "是" : "否(!!)")}");
            }
            Console.WriteLine();
        }
    }
}
