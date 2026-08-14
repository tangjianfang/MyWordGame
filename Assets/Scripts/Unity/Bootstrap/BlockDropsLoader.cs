using System.IO;
using MyWorld.Core.Blocks;
using MyWorld.Core.Items;
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 从 <c>StreamingAssets/blocks/drops/block_drops.json</c> 读方块→物品掉落表。
    /// 与 <see cref="MobSpawnRulesLoader"/> 风格一致：找不到 / 解析失败抛异常，
    /// WorldBootstrap 在 try-catch 里包一层，不阻塞游戏启动。
    /// <para>
    /// 路径单独放在 <c>blocks/drops/</c> 而不是 <c>blocks/</c> 顶层，避免被
    /// <see cref="BlockRegistryLoader"/> 当成方块定义误解析（它的 loader 只取
    /// <c>*.json</c>，而本文件是顶层数组、不是单个方块对象）。
    /// </para>
    /// </summary>
    public static class BlockDropsLoader
    {
        public static string BlockDropsPath
            => Path.Combine(Application.streamingAssetsPath, "blocks", "drops", "block_drops.json");

        public static BlockDrops Load(ItemDatabase items)
        {
            string path = BlockDropsPath;
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"未找到 block_drops.json：{path}");
            }
            return BlockDrops.FromJson(new[] { File.ReadAllText(path) }, items);
        }
    }
}
