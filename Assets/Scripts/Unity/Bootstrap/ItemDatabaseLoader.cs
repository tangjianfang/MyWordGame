using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Items;
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>从 StreamingAssets/items 读物品定义。同 BlockRegistryLoader 套路。</summary>
    public static class ItemDatabaseLoader
    {
        public static string ItemDirectory => Path.Combine(Application.streamingAssetsPath, "items");
        public static string TextureDirectory => Path.Combine(Application.streamingAssetsPath, "items", "textures");

        public static ItemDatabase Load()
        {
            string directory = ItemDirectory;
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
            return ItemDatabase.FromJson(documents);
        }

        public static RecipeDatabase LoadRecipes(ItemDatabase items)
        {
            string directory = Path.Combine(Application.streamingAssetsPath, "recipes");
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
            return RecipeDatabase.FromJson(documents, items);
        }
    }
}
