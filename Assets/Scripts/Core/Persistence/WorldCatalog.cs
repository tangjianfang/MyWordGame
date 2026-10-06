using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MyWorld.Core.Persistence
{
    /// <summary>
    /// 世界目录编目（m12 P1）——扫存档根目录列出全部世界，支撑主菜单的
    /// 「继续上次 / 新世界 / 世界列表」三态。
    /// <para>
    /// 纯 System.IO（零 UnityEngine），dotnet / EditMode 双链同源可测；
    /// 磁盘结构照 <c>SaveLoadService</c> 既有约定：<c>&lt;saveRoot&gt;/&lt;seed&gt;/level.dat</c>
    /// + <c>regions/</c>。目录名即种子（十进制 long，与 WorldBootstrap 的
    /// <c>Path.Combine(saveRoot, seed.ToString())</c> 同源）。
    /// </para>
    /// <para>
    /// 删除走回收站式改名（<c>&lt;名&gt;.deleted-&lt;时间戳&gt;</c>），不真删目录——
    /// 防手滑误删孩子的世界；列表扫描天然跳过 .deleted*。
    /// </para>
    /// </summary>
    public static class WorldCatalog
    {
        /// <summary>被回收（改名删除）的世界目录名后缀前缀。</summary>
        public const string DeletedMarker = ".deleted";

        /// <summary>一个已存在的世界。</summary>
        public sealed class Entry
        {
            /// <summary>种子（目录名解析）。</summary>
            public long Seed;

            /// <summary>目录名（== seed.ToString()）。</summary>
            public string DirectoryName;

            /// <summary>最后游玩时间（level.dat 的最后写入时间，UTC）。</summary>
            public DateTime LastPlayedUtc;

            /// <summary>世界目录完整路径。</summary>
            public string DirPath;
        }

        /// <summary>
        /// 扫 saveRoot 下的一级子目录：名字能解析成 long 且带 level.dat（或 regions/，
        /// 允许还没写过 level.dat 的新世界）才算世界；<c>.deleted</c> 的跳过。
        /// 按最后游玩时间降序（最近玩的排最前）。
        /// </summary>
        public static List<Entry> List(string saveRoot)
        {
            var result = new List<Entry>();
            if (string.IsNullOrEmpty(saveRoot) || !Directory.Exists(saveRoot))
            {
                return result;
            }

            foreach (string dir in Directory.GetDirectories(saveRoot))
            {
                string name = Path.GetFileName(dir);
                if (name.Contains(DeletedMarker))
                {
                    continue; // 回收站式删除的目录不出现在列表
                }

                if (!TryParseSeed(name, out long seed))
                {
                    continue; // 非种子命名的目录与本项目无关
                }

                if (!File.Exists(Path.Combine(dir, "level.dat"))
                    && !Directory.Exists(Path.Combine(dir, "regions")))
                {
                    continue; // 空目录不是世界
                }

                // 最后游玩时间：优先 level.dat 的写入时刻；还没写过 level.dat 的新世界
                // （只有 regions/）用目录本身的时间
                string levelPath = Path.Combine(dir, "level.dat");
                DateTime lastPlayed = File.Exists(levelPath)
                    ? File.GetLastWriteTimeUtc(levelPath)
                    : Directory.GetLastWriteTimeUtc(dir);
                result.Add(new Entry
                {
                    Seed = seed,
                    DirectoryName = name,
                    LastPlayedUtc = lastPlayed,
                    DirPath = dir,
                });
            }

            result.Sort((a, b) => b.LastPlayedUtc.CompareTo(a.LastPlayedUtc));
            return result;
        }

        /// <summary>最近玩的世界（level.dat mtime 最大）；无世界返回 null。</summary>
        public static Entry Latest(string saveRoot)
        {
            List<Entry> worlds = List(saveRoot);
            return worlds.Count > 0 ? worlds[0] : null;
        }

        /// <summary>
        /// 回收站式删除：目录改名 <c>&lt;名&gt;.deleted-&lt;yyyyMMddHHmmss&gt;</c>，
        /// 不真删文件。找不到该种子的世界返回 false。
        /// </summary>
        public static bool TryDelete(string saveRoot, long seed, out string renamedTo)
        {
            renamedTo = null;
            if (string.IsNullOrEmpty(saveRoot))
            {
                return false;
            }

            string dir = Path.Combine(saveRoot, seed.ToString(CultureInfo.InvariantCulture));
            if (!Directory.Exists(dir))
            {
                return false;
            }

            // 时间戳后缀防同名二删撞名（同一秒内连删两次极端场景再加一位随机段）
            string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
            renamedTo = dir + DeletedMarker + "-" + suffix;
            try
            {
                Directory.Move(dir, renamedTo);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 评审 05 T-B2：目录被杀毒/备份/同步工具占用时 Move 抛 IOException——
                // 此前无兜底，异常会在 OnGUI 每帧冒泡（删除按钮永久失效且无提示）。
                // 失败回 false + 清空输出参数，让 UI 走「删除失败」提示分支
                renamedTo = null;
                return false;
            }
        }

        /// <summary>
        /// 种子输入校验：十进制 long（允许负号），去空白；空 / 非数字 / 溢出都拒。
        /// 主菜单「新世界」的输入框与 EditMode 校验测试共用同一条规则。
        /// </summary>
        public static bool TryParseSeed(string text, out long seed)
        {
            seed = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return long.TryParse(text.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out seed);
        }
    }
}
