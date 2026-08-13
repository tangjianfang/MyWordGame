using System.IO;
using MyWorld.Core.Entities;
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 从 StreamingAssets/mobs 读刷怪规则 JSON。找不到时不抛异常，
    /// 返回 null —— MobManager 检测到 null 会回退到旧硬编码概率路径。
    /// </summary>
    public static class MobSpawnRulesLoader
    {
        public static string SpawnRulesPath => Path.Combine(Application.streamingAssetsPath, "mobs", "spawn_rules.json");

        /// <summary>
        /// 尝试加载 <see cref="MobSpawnRules"/>；JSON 缺失或解析失败返回 null。
        /// </summary>
        public static MobSpawnRules TryLoad()
        {
            string path = SpawnRulesPath;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[MobSpawnRules] 未找到 {path}，运行时刷怪走旧硬编码概率路径");
                return null;
            }

            try
            {
                return MobSpawnRules.Load(path);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MobSpawnRules] 加载 {path} 失败：{ex.Message}，回退到旧路径");
                return null;
            }
        }
    }
}