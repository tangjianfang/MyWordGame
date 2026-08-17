using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Math;
using Newtonsoft.Json;

namespace MyWorld.Core.Persistence
{
    /// <summary>
    /// level.dat 读写。写走「tmp + 原子改名」，半写的文件永远不会覆盖上一次好档；
    /// 读失败抛 <see cref="InvalidDataException"/>，降级策略由调用方决定（读容忍原则）。
    /// </summary>
    public static class LevelDataCodec
    {
        /// <summary>原子保存：先写 path.tmp 再改名顶替。</summary>
        public static void Save(LevelData data, string path)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            string tmp = path + ".tmp";
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(tmp, json);
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

        /// <summary>加载。文件不存在抛 <see cref="FileNotFoundException"/>；解析失败抛 <see cref="InvalidDataException"/>。</summary>
        public static LevelData Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("level.dat 不存在", path);
            string json = File.ReadAllText(path);
            try
            {
                LevelData data = JsonConvert.DeserializeObject<LevelData>(json);
                NormalizeNewCollections(data);
                return data;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"level.dat 解析失败：{path}", ex);
            }
        }

        /// <summary>m11 I3 追加的五组可空集合字段做「缺键 → 空集合」归一：
        /// 旧档 JSON 没有对应键时 Newtonsoft 会留 null，这里统一补成空集合，
        /// 后续波次直接拿来用不会 NPE（沿用 quests「旧档 = 全新开始」的兼容策略）。
        /// 字段类型给错（如 Stats 给字符串）在反序列化阶段就抛错，本方法不做任何静默吞错。
        /// level.dat 本身没有版本号字段（FormatVersion 是 regions 二进制格式的概念），无需 bump。</summary>
        private static void NormalizeNewCollections(LevelData data)
        {
            if (data == null) return; // 文件内容为 "null" 的空档保持原语义（调用方按坏档降级处理）
            data.ChestContents ??= new Dictionary<string, List<DropSnapshot>>();
            data.BedSpawnPoints ??= new List<Float3>();
            data.PlayerEnchantments ??= new Dictionary<string, string>();
            data.Stats ??= new Dictionary<string, int>();
            data.FarmStates ??= new Dictionary<string, string>();
        }
    }
}
