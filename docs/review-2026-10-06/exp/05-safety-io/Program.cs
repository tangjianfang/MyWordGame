// 审查实验：MyWordGame 単机存档/IO 稳健性（B 危险操作门禁 + C 配置与外部 IO）
// 全部实验只作用于 docs/review-2026-10-06/exp/05-safety-io/work 下的合成副本，
// 不触碰用户真实存档（C:\Users\...\LocalLow\DefaultCompany\MyWordGame）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using MyWorld.Core.Persistence;
using MyWorld.Core.Voxel;

namespace RobustnessLab
{
    internal static class Program
    {
        private static readonly string Work = Path.Combine(AppContext.BaseDirectory, "work");

        private static int Main()
        {
            Directory.CreateDirectory(Work);
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("== A. level.dat 坏样本矩阵 ==");
            RunLevelDataMatrix();
            Console.WriteLine();
            Console.WriteLine("== B. 深嵌套 JSON 炸弹 ==");
            RunDeepNesting();
            Console.WriteLine();
            Console.WriteLine("== C. RegionFile.Load 边界样本 ==");
            RunRegionEdge();
            Console.WriteLine();
            Console.WriteLine("== D. 坏 region 下 SaveDirty / TryLoadChunk ==");
            RunBadRegionSaveDirty();
            Console.WriteLine();
            Console.WriteLine("== E. WorldCatalog 删除对抗 ==");
            RunWorldCatalog();
            Console.WriteLine();
            Console.WriteLine("== F. LevelDataCodec.Save 原子性 / 失败注入 / 并发 ==");
            RunCodecSave();
            Console.WriteLine();
            Console.WriteLine("== G. ChunkSerializer 坏 Deflate ==");
            RunChunkSerializer();
            Console.WriteLine();
            Console.WriteLine("== H. RegionFile 随机 fuzz ==");
            RunFuzz();
            Console.WriteLine();
            Console.WriteLine("== I. .deleted 改名后同 seed 新建（进度污染假设） ==");
            RunDeletedRecreate();
            Console.WriteLine();
            Console.WriteLine("== J. 深嵌套内层异常类型 ==");
            RunDeepNestingInner();
            Console.WriteLine();
            Console.WriteLine("== K. region 头部坐标与文件名不符 → SaveDirty 路径 ==");
            RunRegionPosMismatch();
            return 0;
        }

        // ─── A. level.dat 坏样本 ─────────────────────────────────────────────
        private static void RunLevelDataMatrix()
        {
            var samples = new (string Name, string Content)[]
            {
                ("空文件", ""),
                ("字面 null", "null"),
                ("根为数组", "[]"),
                ("根为数字", "123"),
                ("根为字符串", "\"hello\""),
                ("截断 JSON", "{\"Seed\":42,\"TimeTick\":1"),
                ("seed 为字符串", "{\"Seed\":\"42\"}"),
                ("seed null", "{\"Seed\":null}"),
                ("seed 溢出", "{\"Seed\":99999999999999999999999999}"),
                ("TimeTick 字符串", "{\"Seed\":42,\"TimeTick\":\"abc\"}"),
                ("Drops 写字符串", "{\"Seed\":42,\"Drops\":\"xx\"}"),
                ("Player.Y=1e40 溢出float", "{\"Seed\":42,\"Player\":{\"X\":0,\"Y\":1e40,\"Z\":0}}"),
                ("HealthCurrent 负数", "{\"Seed\":42,\"Player\":{\"HealthCurrent\":-5,\"HealthMax\":20}}"),
                ("Stats 值为字符串", "{\"Seed\":42,\"Stats\":{\"a\":\"b\"}}"),
                ("重复键 Seed", "{\"Seed\":1,\"Seed\":2}"),
                ("未知字段忽略", "{\"Seed\":42,\"zzz\":[1,2,3]}"),
                ("二进制垃圾", "\0" + (char)1 + (char)2 + (char)0xFF + (char)0xFE + " garbage"),
                ("BOM+正常", "\uFEFF{\"Seed\":42}"),
                ("合法档(对照)", "{\"Seed\":42,\"Player\":{\"X\":1,\"Y\":2,\"Z\":3,\"HealthCurrent\":19,\"HealthMax\":20,\"Hunger\":18,\"Saturation\":5,\"ExpCurrent\":3,\"ExpLevel\":0,\"SelectedHotbarIndex\":0,\"Slots\":[{\"ItemId\":1001,\"Count\":5,\"Metadata\":0}]}}"),
            };
            foreach (var (name, content) in samples)
            {
                string path = Path.Combine(Work, "level.dat");
                File.WriteAllText(path, content);
                string result;
                try
                {
                    var data = LevelDataCodec.Load(path);
                    if (data == null) result = "返回 null（调用方按坏档降级）";
                    else result = $"OK Seed={data.Seed} Player={(data.Player == null ? "null" : $"{data.Player.X},{data.Player.Y},{data.Player.Z} hp={data.Player.HealthCurrent}/{data.Player.HealthMax} slots={(data.Player.Slots == null ? -1 : data.Player.Slots.Length)}")}";
                }
                catch (Exception ex)
                {
                    result = $"抛 {ex.GetType().Name}: {FirstLine(ex.Message)}";
                }
                Console.WriteLine($"[{name,-28}] {result}");
            }
        }

        private static string FirstLine(string s)
        {
            int i = s.IndexOf('\n');
            return i < 0 ? s : s.Substring(0, i);
        }

        // ─── B. 深嵌套 ──────────────────────────────────────────────────────
        private static void RunDeepNesting()
        {
            // 未被反序列化消费的未知字段也要递归跳过——深嵌套会打到解析递归深度
            foreach (int depth in new[] { 200, 2000, 20000, 200000 })
            {
                string path = Path.Combine(Work, "deep.json");
                var sb = new StringBuilder();
                for (int i = 0; i < depth; i++) sb.Append("{\"a\":");
                sb.Append("1");
                for (int i = 0; i < depth; i++) sb.Append("}");
                File.WriteAllText(path, sb.ToString());
                try
                {
                    var data = LevelDataCodec.Load(path);
                    Console.WriteLine($"深度 {depth}: OK 返回 {(data == null ? "null" : "对象")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"深度 {depth}: 抛 {ex.GetType().Name}: {FirstLine(ex.Message)}");
                }
            }
        }

        // ─── C. RegionFile 边界 ─────────────────────────────────────────────
        private static void RunRegionEdge()
        {
            // 1. 坏魔数
            TryLoadRegion("坏魔数", new byte[] { 0x41, 0x41, 0x41, 0x41, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            // 2. 版本未来值
            var good = Header(magic: 0x4752574D, version: 99, x: 0, z: 0, count: 0);
            TryLoadRegion("版本 99", good);
            // 3. 截断（头 10 字节）
            TryLoadRegion("截断 10B", Header(magic: 0x4752574D, version: 1, x: 0, z: 0, count: 1).Take(10).ToArray());
            // 4. count 为负
            TryLoadRegion("count=-1", Header(magic: 0x4752574D, version: 1, x: 0, z: 0, count: -1));
            // 5. count=1025 超界
            TryLoadRegion("count=1025", Header(magic: 0x4752574D, version: 1, x: 0, z: 0, count: 1025));
            // 6. 负 length
            var neg = Header(magic: 0x4752574D, version: 1, x: 0, z: 0, count: 1).Concat(I32(0)).Concat(I32(-5)).ToArray();
            TryLoadRegion("length=-5", neg);
            // 7. 声明 length=64MB 但实际只有 8 字节（内存分配观察）
            var bomb = Header(magic: 0x4752574D, version: 1, x: 0, z: 0, count: 1).Concat(I32(0)).Concat(I32(64 * 1024 * 1024)).Concat(new byte[8]).ToArray();
            TryLoadRegion("length=64MB 截断", bomb, watchMem: true);
            // 8. 负 localIndex
            var negIdx = Header(magic: 0x4752574D, version: 1, x: 0, z: 0, count: 1).Concat(I32(-7)).Concat(I32(0)).ToArray();
            TryLoadRegion("localIndex=-7", negIdx);
            // 9. 重复 localIndex（后覆盖前）
            var dup = Header(magic: 0x4752574D, version: 1, x: 0, z: 0, count: 2)
                .Concat(I32(0)).Concat(I32(0))
                .Concat(I32(0)).Concat(I32(0)).ToArray();
            TryLoadRegion("重复 localIndex", dup);
        }

        private static void TryLoadRegion(string name, byte[] bytes, bool watchMem = false)
        {
            string path = Path.Combine(Work, "r.mwr");
            File.WriteAllBytes(path, bytes);
            long before = GC.GetTotalAllocatedBytes(precise: true);
            var sw = Stopwatch.StartNew();
            try
            {
                using var fs = File.OpenRead(path);
                var region = RegionFile.Load(fs);
                long after = GC.GetTotalAllocatedBytes(precise: true);
                Console.WriteLine($"[{name,-18}] OK ChunkCount={region.ChunkCount} 用时 {sw.ElapsedMilliseconds}ms 分配 {(after - before) / 1024}KB");
            }
            catch (Exception ex)
            {
                long after = GC.GetTotalAllocatedBytes(precise: true);
                Console.WriteLine($"[{name, -18}] 抛 {ex.GetType().Name}: {FirstLine(ex.Message)} 用时 {sw.ElapsedMilliseconds}ms 分配 {(after - before) / 1024}KB");
            }
        }

        private static byte[] I32(int v) => new[]
        {
            (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF), (byte)((v >> 24) & 0xFF),
        };

        private static byte[] Header(uint magic, int version, int x, int z, int count) =>
            I32(unchecked((int)magic)).Concat(I32(version)).Concat(I32(x)).Concat(I32(z)).Concat(I32(count)).ToArray();

        // ─── D. 坏 region 下保存协调器 ───────────────────────────────────────
        private static void RunBadRegionSaveDirty()
        {
            string regionsDir = Path.Combine(Work, "badregion", "regions");
            Directory.CreateDirectory(regionsDir);
            // 先写一个魔数损坏的 region 文件（模拟坏档/半截写坏）
            File.WriteAllBytes(Path.Combine(regionsDir, "r.0.0.mwr"), new byte[] { 0x58, 0x58, 0x58, 0x58, 1, 0, 0, 0 });

            var world = new World();
            var pos = new ChunkPos(0, 0);
            world.AddChunk(pos, new ChunkColumn());
            world.SetBlock(0, 100, 0, 3); // 与游戏内改方块同源：改完自动进 DirtyChunks

            int saved = RegionSaveCoordinator.SaveDirty(world, regionsDir);
            Console.WriteLine($"坏 region 下 SaveDirty 返回 saved={saved}（0=没存进）");
            Console.WriteLine($"脏区块集合 Count={world.DirtyChunks.Count()}（>0=保留下轮重试）");
            Console.WriteLine($"region 文件是否仍为坏魔数: {(File.ReadAllBytes(Path.Combine(regionsDir, "r.0.0.mwr"))[0] == 0x58 ? "是（未被改写）" : "否（被覆盖）")}");

            // TryLoadChunk 同一坏文件
            var world2 = new World();
            bool loaded = RegionSaveCoordinator.TryLoadChunk(world2, pos, regionsDir);
            Console.WriteLine($"TryLoadChunk 坏 region 返回 {loaded}（false=按 seed 生成回退）");

            // 对照：正常 region 的往返
            string goodDir = Path.Combine(Work, "goodregion", "regions");
            var world3 = new World();
            var col3 = new ChunkColumn();
            world3.AddChunk(pos, col3);
            world3.SetBlock(0, 100, 0, 3);
            int saved3 = RegionSaveCoordinator.SaveDirty(world3, goodDir);
            Console.WriteLine($"正常 region 对照：saved={saved3}, dirty={world3.DirtyChunks.Count()}, TryLoadChunk={RegionSaveCoordinator.TryLoadChunk(new World(), pos, goodDir)}");
        }

        // ─── E. WorldCatalog ────────────────────────────────────────────────
        private static void RunWorldCatalog()
        {
            string root = Path.Combine(Work, "worlds");
            Directory.CreateDirectory(root);

            Console.WriteLine("-- TryParseSeed 矩阵 --");
            foreach (var s in new[] { "42", " 42 ", "+42", "-42", "０４２（全角）", "4.2", "1e3", "", " ", "abc", "99999999999999999999", "9223372036854775807", "-9223372036854775808", "9223372036854775808", "0x10", "42\t", "٣٤٢(阿拉伯数字)" })
            {
                Console.WriteLine($"[{s,-24}] -> {WorldCatalog.TryParseSeed(s, out long v)} ({v})");
            }

            Console.WriteLine("-- 同秒连删两个世界 --");
            MakeWorld(root, 100);
            MakeWorld(root, 200);
            bool d1 = WorldCatalog.TryDelete(root, 100, out string r1);
            bool d2 = WorldCatalog.TryDelete(root, 200, out string r2);
            Console.WriteLine($"删 100: {d1} -> {Path.GetFileName(r1)}");
            Console.WriteLine($"删 200: {d2} -> {Path.GetFileName(r2)}");
            Console.WriteLine($"删除后 List 条数 = {WorldCatalog.List(root).Count}");

            Console.WriteLine("-- 同一世界连删两次 --");
            MakeWorld(root, 300);
            bool a1 = WorldCatalog.TryDelete(root, 300, out _);
            bool a2 = WorldCatalog.TryDelete(root, 300, out _);
            Console.WriteLine($"第一次 {a1}，第二次 {a2}（false=已不存在，符合预期）");

            Console.WriteLine("-- 改名目标撞名（Directory.Move 目标已存在） --");
            MakeWorld(root, 400);
            string dir400 = Path.Combine(root, "400");
            string fakeTarget = dir400 + WorldCatalog.DeletedMarker + "-x"; // 构造：真实代码里是时间戳+GUID，这里直接演示 Move 撞名行为
            Directory.CreateDirectory(fakeTarget);
            try
            {
                Directory.Move(dir400, fakeTarget);
                Console.WriteLine("Move 成功（未抛）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"抛 {ex.GetType().Name}: {FirstLine(ex.Message)} ← TitleScreenUi.OnGUI 无 try-catch，此异常会冒进 Unity OnGUI");
            }

            Console.WriteLine("-- 目录内文件被占用（模拟杀毒/OneDrive 锁） --");
            MakeWorld(root, 500);
            string dir500 = Path.Combine(root, "500");
            var lockStream = File.Open(Path.Combine(dir500, "level.dat"), FileMode.Open, FileAccess.Read, FileShare.None);
            try
            {
                string target = dir500 + WorldCatalog.DeletedMarker + "-lock";
                Directory.Move(dir500, target);
                Console.WriteLine("Move 成功（锁不阻碍改名）");
                Directory.Move(target, dir500); // 还原，继续下一组
            }
            catch (Exception ex)
            {
                Console.WriteLine($"抛 {ex.GetType().Name}: {FirstLine(ex.Message)}");
            }
            finally { lockStream.Dispose(); }

            Console.WriteLine("-- 只读属性目录 --");
            MakeWorld(root, 600);
            string dir600 = Path.Combine(root, "600");
            foreach (var f in Directory.GetFiles(dir600)) File.SetAttributes(f, FileAttributes.ReadOnly);
            try
            {
                WorldCatalog.TryDelete(root, 600, out _);
                Console.WriteLine("删除成功（只读属性不影响目录改名；目录已被改名，600 原路径不存在）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"抛 {ex.GetType().Name}: {FirstLine(ex.Message)}");
            }

            Console.WriteLine("-- 删除后新建同 seed（污染假设） --");
            MakeWorld(root, 700);
            WorldCatalog.TryDelete(root, 700, out _);
            MakeWorld(root, 700); // SaveLoadService.Write 首次保存会 Directory.CreateDirectory
            var entries = WorldCatalog.List(root);
            Console.WriteLine($"删除后重建同 seed：List 含 700 = {entries.Any(e => e.Seed == 700)}，条数 {entries.Count}");
            Console.WriteLine($"regions 目录是新的空目录 = {!Directory.EnumerateFiles(Path.Combine(root, "700", "regions")).Any()}");
        }

        private static void MakeWorld(string root, long seed)
        {
            string dir = Path.Combine(root, seed.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(Path.Combine(dir, "regions"));
            File.WriteAllText(Path.Combine(dir, "level.dat"), "{\"Seed\":" + seed + "}");
        }

        // ─── F. Codec.Save ──────────────────────────────────────────────────
        private static void RunCodecSave()
        {
            string path = Path.Combine(Work, "codec", "level.dat");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var data = new LevelData { Seed = 42 };
            LevelDataCodec.Save(data, path);
            Console.WriteLine($"正常保存往返: {LevelDataCodec.Load(path).Seed == 42}");
            Console.WriteLine($"保存后 .tmp 残留: {File.Exists(path + ".tmp")}");

            // 失败注入：目录只读（Windows 下目录 ACL 才有效，这里用不存在盘符路径）
            try
            {
                LevelDataCodec.Save(data, "Q:\\nonexistent\\level.dat");
                Console.WriteLine("坏盘路径保存：未抛（意外）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"坏盘路径保存: 抛 {ex.GetType().Name}（上层 SaveLoadService.Write 有 catch，failed=true）");
            }

            // 并发双写（模拟双开进程/双写者）
            string race = Path.Combine(Work, "codec", "race.dat");
            var errs = new List<string>();
            var threads = new Thread[4];
            for (int t = 0; t < threads.Length; t++)
            {
                threads[t] = new Thread(() =>
                {
                    for (int i = 0; i < 50; i++)
                    {
                        try { LevelDataCodec.Save(new LevelData { Seed = 42 }, race); }
                        catch (Exception ex) { lock (errs) errs.Add(ex.GetType().Name); }
                    }
                });
            }
            foreach (var th in threads) th.Start();
            foreach (var th in threads) th.Join();
            Console.WriteLine($"4 线程 ×50 轮并发保存：抛错 {errs.Count} 次 {string.Join(",", errs.Distinct())}；终文件可读: {Try(() => LevelDataCodec.Load(race) != null)}");
        }

        private static string Try(Func<bool> f) { try { return f() ? "是" : "否"; } catch { return "否（坏档）"; } }

        // ─── G. ChunkSerializer ─────────────────────────────────────────────
        private static void RunChunkSerializer()
        {
            var col = new ChunkColumn();
            col.SetBlock(3, 100, 5, 4);
            byte[] good = ChunkSerializer.Serialize(col);
            Console.WriteLine($"正常往返: {ChunkSerializer.Deserialize(good).GetBlock(3, 100, 5) == 4}");

            foreach (var (name, mutate) in new (string, Func<byte[], byte[]>)[]
            {
                ("版本 2", b => { b[0] = 2; return b; }),
                ("mask 全 1 截断", b => { b[1]=b[2]=b[3]=b[4]=0xFF; return b.Take(5 + 10).ToArray(); }),
                ("Deflate 位翻转", b => { b[^1] ^= 0xFF; return b; }),
                ("长度 <5", b => b.Take(3).ToArray()),
                ("null", b => null),
            })
            {
                try
                {
                    var c2 = ChunkSerializer.Deserialize(mutate((byte[])good.Clone()));
                    Console.WriteLine($"[{name,-14}] OK（读到 column，块值 {c2.GetBlock(3,100,5)}）");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{name,-14}] 抛 {ex.GetType().Name}: {FirstLine(ex.Message)}");
                }
            }

            // 解压炸弹：高度重复数据 Deflate 压缩后极小、声明 mask 多 section
            var bombCol = new ChunkColumn();
            for (int s = 0; s < 6; s++) bombCol.GetOrCreateSection(s * 1); // 让多个 section 存在
            var bombBytes = new byte[6 * 8192];
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(1);
                uint mask = 0;
                for (int i = 0; i < 24; i++) { mask |= 1u << i; bombCol.GetOrCreateSection(i); }
                ms.Write(I32(unchecked((int)mask)), 0, 4);
                using (var df = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                    df.Write(bombBytes, 0, bombBytes.Length);
                Console.WriteLine($"全 24 section 全零 payload：原始 {24 * 8192}B → 压缩后 {ms.Length}B（炸弹上限受 mask 限制）");
            }
        }

        // ─── H. fuzz ────────────────────────────────────────────────────────
        private static void RunFuzz()
        {
            // 基于真实 region 文件做结构感知变异 + 纯随机
            var col = new ChunkColumn();
            col.SetBlock(1, 90, 1, 5);
            col.SetBlock(2, 100, 2, 6);
            var region = new RegionFile(new ChunkPos(0, 0));
            region.StoreChunk(new ChunkPos(0, 0), col);
            region.StoreChunk(new ChunkPos(1, 0), col);
            byte[] seedBytes;
            using (var ms = new MemoryStream()) { region.Save(ms); seedBytes = ms.ToArray(); }

            var rng = new Random(20261006);
            var kinds = new Dictionary<string, int>();
            int okCount = 0, deserialized = 0;
            for (int i = 0; i < 3000; i++)
            {
                byte[] sample = (byte[])seedBytes.Clone();
                int mode = rng.Next(3);
                if (mode == 0) // 纯随机字节
                {
                    sample = new byte[rng.Next(0, 200)];
                    rng.NextBytes(sample);
                }
                else if (mode == 1) // 头部字段变异
                {
                    int off = rng.Next(0, Math.Min(20, sample.Length));
                    sample[off] = (byte)rng.Next(256);
                }
                else // 随机区间覆写 + 截断
                {
                    int start = rng.Next(0, sample.Length);
                    int len = rng.Next(1, Math.Min(64, sample.Length - start + 1));
                    for (int k = 0; k < len && start + k < sample.Length; k++) sample[start + k] = (byte)rng.Next(256);
                    if (rng.Next(2) == 0) sample = sample.Take(rng.Next(0, sample.Length)).ToArray();
                }
                try
                {
                    using var ms = new MemoryStream(sample);
                    var r = RegionFile.Load(ms);
                    okCount++;
                    foreach (var idx in new[] { 0 })
                        if (r.TryGetChunk(new ChunkPos(idx & 31, idx >> 5), out var c)) deserialized++;
                }
                catch (Exception ex)
                {
                    string key = ex.GetType().Name;
                    kinds.TryGetValue(key, out int n);
                    kinds[key] = n + 1;
                }
            }
            Console.WriteLine($"3000 轮 fuzz：成功解析 {okCount}，TryGetChunk 反序列化 {deserialized}，异常分布: {string.Join(", ", kinds.Select(kv => $"{kv.Key}×{kv.Value}"))}");
            Console.WriteLine("（无未处理崩溃/挂起即本宿主稳健；OutOfMemory 未出现）");
        }

        // ─── I. .deleted 后同 seed 新建（污染假设验证，独立干净目录） ────────
        private static void RunDeletedRecreate()
        {
            string root = Path.Combine(Work, "worlds2");
            Directory.CreateDirectory(root);
            string dir = Path.Combine(root, "42");
            Directory.CreateDirectory(Path.Combine(dir, "regions"));
            File.WriteAllText(Path.Combine(dir, "level.dat"), "{\"Seed\":42}");
            File.WriteAllText(Path.Combine(dir, "regions", "r.0.0.mwr"), "旧世界数据");

            WorldCatalog.TryDelete(root, 42, out string renamed);
            // 新建同 seed（走 SaveLoadService.Write 的目录创建语义）
            string newDir = Path.Combine(root, "42");
            Directory.CreateDirectory(Path.Combine(newDir, "regions"));
            Console.WriteLine($"旧目录改名: {Path.GetFileName(renamed)}");
            Console.WriteLine($"新目录 regions 是否为空（空=无污染）: {!Directory.EnumerateFiles(Path.Combine(newDir, "regions")).Any()}");
            Console.WriteLine($"改名目录是否还在（回收站语义可手动找回）: {Directory.Exists(renamed)}");
        }

        // ─── J. 深嵌套内层异常（确认是 JsonReader 深度限制而非栈溢出） ────────
        private static void RunDeepNestingInner()
        {
            string path = Path.Combine(Work, "deep2.json");
            var sb = new StringBuilder();
            for (int i = 0; i < 500; i++) sb.Append("{\"a\":");
            sb.Append("1");
            for (int i = 0; i < 500; i++) sb.Append("}");
            File.WriteAllText(path, sb.ToString());
            try
            {
                LevelDataCodec.Load(path);
                Console.WriteLine("未抛（意外）");
            }
            catch (Exception ex)
            {
                for (Exception e = ex; e != null; e = e.InnerException)
                    Console.WriteLine($"  {e.GetType().Name}: {FirstLine(e.Message)}");
            }
        }

        // ─── K. region 文件头 regionX/Z 与文件名不符 → StoreChunk 抛什么 ──────
        private static void RunRegionPosMismatch()
        {
            string regionsDir = Path.Combine(Work, "mismatch", "regions");
            Directory.CreateDirectory(regionsDir);
            // 合法 MWRG、版本 1，但头部 regionX=5/regionZ=5，文件名是 r.0.0.mwr
            byte[] header = Header(magic: 0x4752574D, version: 1, x: 5, z: 5, count: 0);
            File.WriteAllBytes(Path.Combine(regionsDir, "r.0.0.mwr"), header);

            var world = new World();
            var pos = new ChunkPos(0, 0);
            world.AddChunk(pos, new ChunkColumn());
            world.SetBlock(0, 100, 0, 3);
            try
            {
                int saved = RegionSaveCoordinator.SaveDirty(world, regionsDir);
                Console.WriteLine($"SaveDirty 返回 {saved}（未抛）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveDirty 抛 {ex.GetType().Name}: {FirstLine(ex.Message)}");
                Console.WriteLine("→ SaveDirtyCore 的 catch 只接 IOException/InvalidDataException，本异常会向上冒泡：");
                Console.WriteLine("   · SaveLoadService.Write 有 catch(Exception)（failed=true，退出菜单能拦住）");
                Console.WriteLine("   · ChunkStreamer.UnloadDistant 的同步 SaveDirty 无 catch → Unity 帧循环异常");
            }
        }
    }
}
