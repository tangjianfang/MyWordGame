using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Items;
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>从 StreamingAssets/items 读物品定义。同 BlockRegistryLoader 套路。
    /// 评审 04 R-8：解析失败不再打断 <see cref="WorldBootstrap"/>.Awake（启动黑屏）——
    /// 与 BlockDropsLoader 同款降级：错误日志 + 空表，游戏能起、缺物品可诊断。</summary>
    public static class ItemDatabaseLoader
    {
        public static string ItemDirectory => Path.Combine(Application.streamingAssetsPath, "items");
        public static string TextureDirectory => Path.Combine(Application.streamingAssetsPath, "items", "textures");

        public static ItemDatabase Load() => Load(ItemDirectory);

        /// <summary>目录可注入版（EditMode 测试喂坏 JSON 用）。目录缺失 / 解析失败一律
        /// 降级空表 + 日志，绝不抛——items 是主数据源，炸 Awake 比「缺物品」伤害大得多。</summary>
        internal static ItemDatabase Load(string directory)
        {
            if (!Directory.Exists(directory))
            {
                Debug.LogWarning($"[ItemDatabase] 未找到物品目录 {directory}，使用空表");
                return ItemDatabase.FromJson(new string[0]);
            }

            var documents = new List<string>();
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                documents.Add(File.ReadAllText(path));
            }
            try
            {
                return ItemDatabase.FromJson(documents);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemDatabase] 物品表解析失败，降级为空表：{ex.Message}");
                return ItemDatabase.FromJson(new string[0]);
            }
        }

        public static RecipeDatabase LoadRecipes(ItemDatabase items) => LoadRecipes(
            Path.Combine(Application.streamingAssetsPath, "recipes"), items);

        /// <summary>目录可注入版（EditMode 测试用），降级语义与 <see cref="Load(string)"/> 一致。</summary>
        internal static RecipeDatabase LoadRecipes(string directory, ItemDatabase items)
        {
            if (!Directory.Exists(directory))
            {
                Debug.LogWarning($"[RecipeDatabase] 未找到配方目录 {directory}，使用空表");
                return RecipeDatabase.FromJson(new string[0], items);
            }
            var documents = new List<string>();
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                documents.Add(File.ReadAllText(path));
            }
            try
            {
                return RecipeDatabase.FromJson(documents, items);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecipeDatabase] 配方表解析失败，降级为空表：{ex.Message}");
                return RecipeDatabase.FromJson(new string[0], items);
            }
        }
    }
}
