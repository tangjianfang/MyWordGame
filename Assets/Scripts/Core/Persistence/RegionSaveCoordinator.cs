using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Persistence
{
    /// <summary>
    /// 方块改动态的落盘/恢复协调器（milestone-4 spec）。
    /// dirty 区块按 32×32 region 分组写 <c>r.X.Z.mwr</c>；overlay 用「生成后覆盖」
    /// （<see cref="World.AddChunk"/> 对同位置区块是替换语义，直接挂入即可）。
    /// 单个 region 写失败：该批 chunk 保持 dirty 下轮重试，不影响其它 region。
    /// </summary>
    public static class RegionSaveCoordinator
    {
        /// <summary>把 world 的全部脏区块分组写入 regionsDir。返回成功保存的 chunk 数。
        /// 主线程同步路径（ChunkStreamer 卸载前保存 / 退出保存）沿用本重载：写成功即清脏。</summary>
        public static int SaveDirty(World world, string regionsDir)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            return SaveDirtyCore(
                world.DirtyChunks,
                pos => world.TryGetChunk(pos, out ChunkColumn column) ? column : null,
                regionsDir,
                (groupChunks, _) =>
                {
                    // 卸载区块的 stale 标记也一并清掉，避免永远卡在待保存
                    foreach (ChunkPos chunk in groupChunks) world.ClearDirty(chunk);
                });
        }

        /// <summary>后台线程版（m5 C3）：只读主线程冻结的脏区块快照，<b>不触碰 world 的任何可变状态
        /// （不清脏）</b>。成功落盘的区块位置收进 <paramref name="savedOut"/>，由主线程在确认写盘
        /// 完成后按 <see cref="World.ClearDirtyIfUnchanged"/> 清脏（版本守卫防丢新改动）。
        /// savedOut 由调用方每轮新建（线程封闭，无共享）。</summary>
        public static int SaveDirty(IReadOnlyDictionary<ChunkPos, ChunkColumn> chunkSnapshot, string regionsDir,
            List<ChunkPos> savedOut)
        {
            if (chunkSnapshot == null) throw new ArgumentNullException(nameof(chunkSnapshot));
            if (savedOut == null) throw new ArgumentNullException(nameof(savedOut));
            return SaveDirtyCore(
                chunkSnapshot.Keys,
                pos => chunkSnapshot.TryGetValue(pos, out ChunkColumn column) ? column : null,
                regionsDir,
                (_, persistedChunks) => savedOut.AddRange(persistedChunks));
        }

        /// <summary>两个公开重载共用的写盘主体。onRegionPersisted(整组区块, 真正落盘的区块)
        /// 在每个 region 文件原子写成功之后回调：同步版清整组脏（含卸载 stale），快照版只登记落盘区块。</summary>
        private static int SaveDirtyCore(
            IEnumerable<ChunkPos> dirtyChunks,
            Func<ChunkPos, ChunkColumn> resolveChunk,
            string regionsDir,
            Action<List<ChunkPos>, List<ChunkPos>> onRegionPersisted)
        {
            Directory.CreateDirectory(regionsDir);
            int saved = 0;
            foreach (var group in GroupByRegion(dirtyChunks))
            {
                try
                {
                    var region = LoadOrCreate(group.Key, regionsDir);
                    var persistedChunks = new List<ChunkPos>();
                    foreach (ChunkPos chunk in group.Value)
                    {
                        ChunkColumn column = resolveChunk(chunk);
                        if (column != null)
                        {
                            region.StoreChunk(chunk, column);
                            persistedChunks.Add(chunk); // 只统计真正进 region 的块；无数据的（已卸载）不计
                        }
                    }
                    AtomicWrite(Path.Combine(regionsDir, FileName(group.Key)), region);
                    // saved 计数与回调都必须在文件成功落盘之后：写失败则保持 dirty 下轮重试，且不能虚报 saved
                    onRegionPersisted(group.Value, persistedChunks);
                    saved += persistedChunks.Count;
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException)
                {
                    // 写失败（含旧 region 文件损坏读不回来）：这批 chunk 保持 dirty，下轮保存重试
                }
            }
            return saved;
        }

        /// <summary>
        /// 查 region 存档并用其覆盖 world 内区块。region 缺失 / 区块未存 / 文件损坏
        /// 一律返回 false（调用方保留 seed 生成结果）——读容忍。
        /// </summary>
        public static bool TryLoadChunk(World world, ChunkPos pos, string regionsDir)
        {
            ChunkPos regionPos = RegionFile.RegionFor(pos);
            string path = Path.Combine(regionsDir ?? string.Empty, FileName(regionPos));
            if (!File.Exists(path)) return false;
            try
            {
                RegionFile region;
                using (var fs = File.OpenRead(path))
                {
                    region = RegionFile.Load(fs);
                }
                if (!region.TryGetChunk(pos, out var column)) return false;
                world.AddChunk(pos, column); // AddChunk 替换同位置区块，实现存档 overlay
                return true;
            }
            catch (Exception)
            {
                return false; // 魔数错/版本错/半截文件——按未存处理
            }
        }

        private static Dictionary<ChunkPos, List<ChunkPos>> GroupByRegion(IEnumerable<ChunkPos> chunks)
        {
            var groups = new Dictionary<ChunkPos, List<ChunkPos>>();
            foreach (ChunkPos chunk in chunks)
            {
                ChunkPos region = RegionFile.RegionFor(chunk);
                if (!groups.TryGetValue(region, out var list))
                {
                    list = new List<ChunkPos>();
                    groups[region] = list;
                }
                list.Add(chunk);
            }
            return groups;
        }

        /// <summary>
        /// 原子写（与 <see cref="LevelDataCodec.Save"/> 同款）：先写 .tmp 再顶替。
        /// 不能 File.Create 直接覆写目标——写到一半崩溃会把合并来的旧记录
        /// （同 region 其它区块）也写坏，违反「合并不丢旧记录」约束。
        /// </summary>
        private static void AtomicWrite(string path, RegionFile region)
        {
            string tmp = path + ".tmp";
            using (var fs = File.Create(tmp))
            {
                region.Save(fs);
            }
            // netstandard2.1 没有 File.Move(src, dst, overwrite) 重载（.NET Core 3.0 才加入）。
            // 目标已存在时用 File.Replace（底层 Win32 ReplaceFile，原子顶替，不留空窗）；
            // 首次写入目标不存在，直接改名即可。
            if (File.Exists(path))
            {
                File.Replace(tmp, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        private static RegionFile LoadOrCreate(ChunkPos regionPos, string regionsDir)
        {
            string path = Path.Combine(regionsDir, FileName(regionPos));
            if (File.Exists(path))
            {
                using (var fs = File.OpenRead(path))
                {
                    return RegionFile.Load(fs); // 合并旧记录：Load 出来的 RegionFile 已含旧 chunk
                }
            }
            return new RegionFile(regionPos);
        }

        private static string FileName(ChunkPos regionPos) => $"r.{regionPos.X}.{regionPos.Z}.mwr";
    }
}
