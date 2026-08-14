using System;
using System.IO;
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
                return JsonConvert.DeserializeObject<LevelData>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"level.dat 解析失败：{path}", ex);
            }
        }
    }
}
