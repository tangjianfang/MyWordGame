using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Blocks;
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 从 StreamingAssets 读方块定义。桌面平台上 StreamingAssets 就是普通目录，可以直接文件 IO；
    /// 将来要出 Android 版再换成 UnityWebRequest，不影响调用方。
    /// </summary>
    public static class BlockRegistryLoader
    {
        public static string BlockDirectory => Path.Combine(Application.streamingAssetsPath, "blocks");

        public static string TextureDirectory => Path.Combine(BlockDirectory, "textures");

        public static BlockRegistry Load()
        {
            string directory = BlockDirectory;
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"未找到方块定义目录：{directory}");
            }

            var documents = new List<string>();
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                documents.Add(File.ReadAllText(path));
            }

            if (documents.Count == 0)
            {
                throw new InvalidDataException($"{directory} 下没有任何方块定义文件。");
            }

            return BlockRegistry.FromJson(documents);
        }
    }
}
