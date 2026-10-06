// UI 延迟审查微基准（只读仓库源码 + scratch 输出，不碰用户存档/注册表）。
// 用途：量化「主线程周期性重活」的真实耗时与托管分配——光照体积 2s 重建、
// 区块列生成、单段贪心网格、脏区块同步落盘（RegionSaveCoordinator 主线程路径）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MyWorld.Core.Blocks;
using MyWorld.Core.Lighting;
using MyWorld.Core.Math;
using MyWorld.Core.Meshing;
using MyWorld.Core.Persistence;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;

internal static class Program
{
    private static BlockRegistry _registry;

    private static int Main(string[] args)
    {
        _registry = LoadBlockRegistry();
        Console.WriteLine($"方块注册表：{_registry.TextureNames.Count} 贴图槽");

        int seed = 42;
        var generator = new WorldGenerator(seed);
        var world = new World();

        // 与实机同规模：玩家周围 5×5 区块（光照体积 3×3 脚印的父集，mesh 需要 4 邻居）
        const int radius = 3;
        var swTotal = Stopwatch.StartNew();
        for (int dx = -radius; dx <= radius; dx++)
        for (int dz = -radius; dz <= radius; dz++)
        {
            world.AddChunk(new ChunkPos(dx, dz), generator.Generate(new ChunkPos(dx, dz)));
        }
        swTotal.Stop();
        Console.WriteLine($"生成 {(2 * radius + 1) * (2 * radius + 1)} 列：{swTotal.ElapsedMilliseconds}ms 总，"
            + $"{swTotal.ElapsedMilliseconds / ((2 * radius + 1) * (2 * radius + 1)):F2}ms/列");

        BenchLightRebuild(world);
        BenchGenerateOne(generator);
        BenchMeshSection(world);
        BenchRegionSave(world, generator);
        return 0;
    }

    private static void BenchLightRebuild(World world)
    {
        // ChunkLightSystem.RebuildNow 的精确等价：Build + BuildBlockLight，48×96×48
        const int size = 48, height = 96;
        int originX = -24, originY = 40, originZ = -24;

        // 预热（JIT）
        WorldLightVolumeBuilder.Build(world, _registry, originX, originY, originZ, size, height, size);
        WorldLightVolumeBuilder.BuildBlockLight(world, _registry, originX, originY, originZ, size, height, size);

        const int runs = 10;
        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
        {
            var a = WorldLightVolumeBuilder.Build(world, _registry, originX, originY, originZ, size, height, size);
            var b = WorldLightVolumeBuilder.BuildBlockLight(world, _registry, originX, originY, originZ, size, height, size);
            GC.KeepAlive(a);
            GC.KeepAlive(b);
        }
        sw.Stop();
        long alloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        Console.WriteLine($"[光照体积] 一次 RebuildNow（Build+BuildBlockLight）："
            + $"{sw.Elapsed.TotalMilliseconds / runs:F2}ms，托管分配 {alloc / (double)runs / 1024:F0}KB/次");
    }

    private static void BenchGenerateOne(WorldGenerator generator)
    {
        var pos = new ChunkPos(999, 999); // 未生成过的远端列
        generator.Generate(pos); // 预热
        const int runs = 20;
        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
        {
            var col = generator.Generate(new ChunkPos(1000 + i, 1000));
            GC.KeepAlive(col);
        }
        sw.Stop();
        long alloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        Console.WriteLine($"[区块列生成] {sw.Elapsed.TotalMilliseconds / runs:F2}ms/列，"
            + $"托管分配 {alloc / (double)runs / 1024:F0}KB/列");
    }

    private static void BenchMeshSection(World world)
    {
        var buffer = new MeshBuffer();
        var submeshes = new List<Submesh>();
        // 找一根已生成列的接地段（y≈64 → section (64-(-64))/16=8）
        var source = new ChunkMeshSource(world, _registry, new ChunkPos(0, 0), -64 + 8 * 16);
        GreedyMesher.Build(source, buffer); // 预热
        buffer.SplitByTexture(submeshes);

        const int runs = 50;
        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
        {
            GreedyMesher.Build(source, buffer);
            buffer.SplitByTexture(submeshes);
        }
        sw.Stop();
        long alloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        Console.WriteLine($"[单段贪心网格] {sw.Elapsed.TotalMilliseconds / runs:F3}ms/段（顶点 {buffer.VertexCount}），"
            + $"托管分配 {alloc / (double)runs / 1024:F1}KB/段");
    }

    private static void BenchRegionSave(World world, WorldGenerator generator)
    {
        // 复刻 ChunkStreamer.UnloadDistant 的主线程同步落盘：改几格 → SaveDirty
        string dir = Path.Combine(AppContext.BaseDirectory, "bench-regions");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        // 让 (0,0) 与 (1,0) 两列变脏（SetBlock 挖各 8 格）
        for (int i = 0; i < 8; i++)
        {
            world.SetBlock(i, 70, i, BlockIds.Air);
        }
        for (int i = 0; i < 8; i++)
        {
            world.SetBlock(20 + i, 70, i, BlockIds.Air);
        }

        // 预热一次（建 region 文件），再测“已存在 region 的重复保存”（实机常态：越改越多）
        RegionSaveCoordinator.SaveDirty(world, dir);

        const int runs = 5;
        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
        {
            // 重新弄脏（SaveDirty 成功后清脏，需要再标脏才有可写内容）
            world.SetBlock(30 + i, 70, i, BlockIds.Air);
            RegionSaveCoordinator.SaveDirty(world, dir);
        }
        sw.Stop();
        long alloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        long bytes = 0;
        foreach (var f in Directory.GetFiles(dir)) bytes += new FileInfo(f).Length;
        Console.WriteLine($"[脏区块同步落盘] 2 脏列 → {sw.Elapsed.TotalMilliseconds / runs:F2}ms/次，"
            + $"托管分配 {alloc / (double)runs / 1024:F0}KB/次，region 目录 {bytes / 1024:F0}KB");

        // 大基地带走场景：32 根脏列（同一 region 32×32 区块格内），模拟孩子离家 8+ 区块
        string bigDir = Path.Combine(AppContext.BaseDirectory, "bench-regions-big");
        if (Directory.Exists(bigDir)) Directory.Delete(bigDir, true);
        Directory.CreateDirectory(bigDir);
        var bigWorld = new World();
        for (int dx = 0; dx < 8; dx++)
        for (int dz = 0; dz < 4; dz++)
        {
            bigWorld.AddChunk(new ChunkPos(dx, dz), generator.Generate(new ChunkPos(dx, dz)));
            for (int i = 0; i < 4; i++)
            {
                bigWorld.SetBlock(dx * 16 + i, 70, dz * 16 + i, BlockIds.Stone);
            }
        }
        RegionSaveCoordinator.SaveDirty(bigWorld, bigDir); // 预热建文件
        for (int dx = 0; dx < 8; dx++)
        for (int dz = 0; dz < 4; dz++)
        {
            bigWorld.SetBlock(dx * 16, 71, dz * 16, BlockIds.Stone); // 重新弄脏 32 列
        }
        var swBig = Stopwatch.StartNew();
        RegionSaveCoordinator.SaveDirty(bigWorld, bigDir);
        swBig.Stop();
        long bigBytes = 0;
        foreach (var f in Directory.GetFiles(bigDir)) bigBytes += new FileInfo(f).Length;
        Console.WriteLine($"[脏区块同步落盘·大] 32 脏列 → {swBig.Elapsed.TotalMilliseconds:F1}ms 单次，"
            + $"region 目录 {bigBytes / 1024:F0}KB");
    }

    private static BlockRegistry LoadBlockRegistry()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "blocks");
            if (Directory.Exists(candidate))
            {
                return BlockRegistry.FromJson(
                    Directory.GetFiles(candidate, "*.json").Select(File.ReadAllText));
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("未找到 Assets/StreamingAssets/blocks 目录。");
    }
}
