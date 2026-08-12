#if UNITY_EDITOR
// 本 fixture 依赖 UnityEngine（Application.streamingAssetsPath），
// 整个文件用 #if UNITY_EDITOR ... #endif 包裹：dotnet csproj 链跑纯 Core 测试时跳过，
// Unity EditMode 链跑这条。视觉/AI 角色测试统一走 Unity EditMode。
using System.IO;
using System.Linq;
using MyWorld.Core.Items;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Items
{
    /// <summary>
    /// 校验所有「任务 A4 brief 列出」的物品的 texture 字段是否都有对应 PNG。
    /// 任务是按 brief 范围覆盖的，brief 没列的物品（如 arrow、chair、globe 等）
    /// 不在本测试的检查范围内——它们的贴图留给后续任务。
    ///
    /// 真正的 PNG 像素约束（大小、Alpha、调色板）由 postprocess_art.py 的 verify
    /// 在产物落盘那一刻做，没通过就退出码非 0，根本进不到这一步。
    /// </summary>
    [TestFixture]
    public class ItemTextureFileTests
    {
        // 任务 A4 brief 列出的物品（与 art/requests/items/*.md 一一对应）
        private static readonly string[] CoveredItems = {
            // 矿石与锭
            "diamond", "iron_ingot", "netherite_ingot", "coal", "emerald",
            "lapis", "redstone", "redstone_dust",
            // 原始方块
            "bedrock", "cobblestone",
            // 木材
            "plank", "stick",
            // 食物
            "raw_porkchop", "rotten_flesh", "beet", "mung_bean", "bowl",
            "bowl_of_water", "beet_soup", "mung_bean_soup",
            // 剑
            "wooden_sword", "stone_sword", "iron_sword", "diamond_sword",
            "netherite_sword", "bedrock_sword",
            // 镐
            "wooden_pickaxe", "stone_pickaxe", "iron_pickaxe", "diamond_pickaxe",
            "netherite_pickaxe", "bedrock_pickaxe",
            // 斧
            "wooden_axe", "stone_axe", "iron_axe",
            // 锹
            "wooden_shovel", "stone_shovel", "iron_shovel",
            // 杂项（string_ 是 id，因为 string 是 C# 关键字；texture 名仍是 string）
            "wool", "book", "enchanted_book", "string_", "gunpowder", "bone",
            "skull", "crafting_table",
        };

        [Test]
        public void EveryBriefItem_TextureFile_Exists()
        {
            string itemsDir = Path.Combine(Application.streamingAssetsPath, "items");
            var itemDocs = Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText);
            var db = ItemDatabase.FromJson(itemDocs);

            string texDir = Path.Combine(itemsDir, "textures");
            int missing = 0;
            foreach (string id in CoveredItems)
            {
                Assert.That(db.TryGetById(id, out var def), Is.True,
                    $"brief 列出 {id}，但物品注册表里没找到");

                string path = Path.Combine(texDir, def.Texture + ".png");
                if (!File.Exists(path))
                {
                    missing++;
                    TestContext.Out.WriteLine($"  缺贴图：{id} -> {def.Texture}（{path}）");
                }
            }
            Assert.That(missing, Is.EqualTo(0),
                $"任务 A4 brief 范围内 ({CoveredItems.Length} 个物品) 必须全部有贴图，否则 HotbarUI 会显示 missingTex 棕色。缺 {missing} 个。");
        }

        [Test]
        public void AllItems_AreRegistered_NoDuplicates()
        {
            // 副带验证：ItemDatabase.FromJson 解析所有 JSON 不抛异常（id 不重复、
            // numericId 不冲突）。这与 RecipeFilesTests 的部分覆盖重叠，但独立
            // 失败更易定位。
            string itemsDir = Path.Combine(Application.streamingAssetsPath, "items");
            var itemDocs = Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText);
            Assert.DoesNotThrow(() => ItemDatabase.FromJson(itemDocs),
                "物品 JSON 解析失败——大概率是 id 或 numericId 冲突");
        }
    }
}
#endif
