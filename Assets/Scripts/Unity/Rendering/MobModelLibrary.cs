using System;
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Entities;
using Newtonsoft.Json;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// 造型 JSON 的单个部位 DTO（<c>Assets/StreamingAssets/mobs/models/*.json</c> 的 parts 数组元素）。
    /// 属性名与 JSON 键大小写不敏感匹配（同 <see cref="MyWorld.Core.Entities.MobSpawnRules"/>
    /// 的 camelCase 键约定），未知键按解析失败处理（防拼错静默取默认值）。
    /// </summary>
    public sealed class MobModelPart
    {
        /// <summary>部位名（"body"/"head"/"legFL"...，MobAssembly 按名建 GameObject，不能撞名）。</summary>
        public string Name { get; set; }

        /// <summary>部位尺寸 [x, y, z]（格），必须恰三元。</summary>
        public float[] Size { get; set; }

        /// <summary>局部位置 [x, y, z]：脚底中心为原点、面朝 +Z。</summary>
        public float[] Position { get; set; }

        /// <summary>色值 "#RRGGBB"（非法不抛异常——报错返品红立刻暴露，见 <see cref="MobModelLibrary.Hex"/>）。</summary>
        public string Color { get; set; }

        /// <summary>腿：参与行走摆动。</summary>
        public bool IsLeg { get; set; }

        /// <summary>摆动相位（对角步态 0/π）。</summary>
        public float LegPhase { get; set; }
    }

    /// <summary>整份造型文件的 DTO：kind 自校验 + 部位数组。</summary>
    public sealed class MobModelFile
    {
        /// <summary>生物类型名（"Pig"/"Cow"/...，须与按 kind 推断的文件内容一致）。</summary>
        public string Kind { get; set; }

        /// <summary>部位表。</summary>
        public List<MobModelPart> Parts { get; set; }
    }

    /// <summary>
    /// 五生物部位表 JSON 库（m11 I1：真值从 <see cref="MobModels"/> 的 C# 常量表外置到
    /// <c>Assets/StreamingAssets/mobs/models/{pig,cow,chicken,zombie,villager}.json</c>，
    /// 数值逐字照抄旧表——迁移是搬真值不是重设计，守恒由 MobModelLibraryTests 守卫）。
    /// <para>
    /// 加载模式沿用 <see cref="MyWorld.Unity.Bootstrap.MobSpawnRulesLoader"/> 的惯例：
    /// <see cref="Application.streamingAssetsPath"/> 前缀定位 + Newtonsoft.Json 解析。
    /// 按 kind 缓存解析后的模板（每文件只读一次），<see cref="Load"/> 每次返回模板的
    /// 新数组副本（<see cref="MobPart"/> 是 readonly struct，副本即深拷贝），调用方可安全修改。
    /// </para>
    /// <para>
    /// 解析失败一律抛 <see cref="InvalidDataException"/> 且消息带文件名（数据表坏了
    /// 要在启动第一眼炸出来，不能静默回退成隐形/错形的生物）；唯色值非法不抛——
    /// 沿用 m8 评审 I-2 配方：报错 + 返品红占位。
    /// </para>
    /// </summary>
    public static class MobModelLibrary
    {
        /// <summary>按 kind 缓存的部位表模板（Load 返回其副本，模板本体不外泄）。</summary>
        private static readonly Dictionary<MobKind, MobPart[]> Cache =
            new Dictionary<MobKind, MobPart[]>();

        /// <summary>kind 对应的模型 JSON 路径（StreamingAssets/mobs/models/&lt;kind 小写&gt;.json）。</summary>
        public static string ModelPath(MobKind kind)
        {
            return Path.Combine(Application.streamingAssetsPath, "mobs", "models", FileNameOf(kind));
        }

        /// <summary>
        /// 加载指定生物的部位表：文件缺失/解析失败抛异常（消息带文件名）；
        /// 成功则缓存模板并返回其新数组副本（每调用返回新数组，语义与旧
        /// <see cref="MobModels.Build"/> 一致）。
        /// </summary>
        public static MobPart[] Load(MobKind kind)
        {
            if (!Cache.TryGetValue(kind, out var template))
            {
                string path = ModelPath(kind);
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        "找不到生物造型 JSON: " + path + "（m11 I1 起五生物部位表外置于 StreamingAssets/mobs/models/）", path);
                }
                template = Parse(File.ReadAllText(path), path, kind);
                Cache[kind] = template;
            }
            return (MobPart[])template.Clone();
        }

        /// <summary>
        /// 解析一份造型 JSON 为部位表（供 <see cref="Load"/> 与测试直接喂字面量）。
        /// <paramref name="sourceName"/> 只用于报错定位（通常是文件路径）。
        /// 结构坏/kind 不匹配/parts 空/数组非三元/未知字段均抛
        /// <see cref="InvalidDataException"/> 且消息带 sourceName。
        /// </summary>
        public static MobPart[] Parse(string json, string sourceName, MobKind expectedKind)
        {
            MobModelFile file;
            try
            {
                // 未知键（拼错的 legPhase 等）直接按失败拦下：静默取默认 0 相位会让步态瘫掉
                var settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                };
                file = JsonConvert.DeserializeObject<MobModelFile>(json, settings);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    "生物造型 JSON 解析失败: " + sourceName + "（" + ex.Message + "）", ex);
            }
            if (file == null)
            {
                throw new InvalidDataException("生物造型 JSON 解析为空: " + sourceName);
            }
            if (string.IsNullOrEmpty(file.Kind))
            {
                throw new InvalidDataException("生物造型 JSON 缺 kind 字段: " + sourceName);
            }
            if (!Enum.TryParse(file.Kind, out MobKind kindInFile) || kindInFile != expectedKind)
            {
                throw new InvalidDataException(
                    "生物造型 JSON kind 不匹配: " + sourceName + " 声明 " + file.Kind +
                    "，期望 " + expectedKind + "（文件放错目录或 kind 写错）");
            }
            if (file.Parts == null || file.Parts.Count == 0)
            {
                throw new InvalidDataException("生物造型 JSON parts 为空: " + sourceName);
            }

            var parts = new MobPart[file.Parts.Count];
            for (int i = 0; i < file.Parts.Count; i++)
            {
                var p = file.Parts[i];
                if (p == null || string.IsNullOrEmpty(p.Name))
                {
                    throw new InvalidDataException(
                        "生物造型 JSON 部位名缺失: " + sourceName + " 第 " + i + " 个部位");
                }
                parts[i] = new MobPart(p.Name,
                    ToVector3(p.Size, sourceName, p.Name, "size"),
                    ToVector3(p.Position, sourceName, p.Name, "position"),
                    Hex(p.Color), p.IsLeg, p.LegPhase);
            }
            return parts;
        }

        /// <summary>
        /// 色值解析（m8 评审 I-2 配方，m11 I1 从 MobModels 迁来）：非法字面量不能静默透明
        /// （out 参数是 0,0,0,0，部位会整个隐形）——报错 + 返品红，进游戏第一眼就暴露。
        /// internal 供 <see cref="MobModels"/> 旧三类保底表的字面量共用同一条检查路径。
        /// </summary>
        internal static Color Hex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString(hex, out var c))
            {
                Debug.LogError("MobModelLibrary.Hex 颜色字面量非法: " + hex + "（应为 #RRGGBB 格式，检查造型 JSON 的 color 字段）");
                return Color.magenta;
            }
            return c;
        }

        private static string FileNameOf(MobKind kind)
        {
            switch (kind)
            {
                case MobKind.Pig: return "pig.json";
                case MobKind.Cow: return "cow.json";
                case MobKind.Chicken: return "chicken.json";
                case MobKind.Zombie: return "zombie.json";
                case MobKind.Villager: return "villager.json";
                // m11 W1-1：三敌对造型（骷髅细长灰白持弓 / 蜘蛛八脚横体红眼 / 苦力怕四短腿立柱），
                // 配色与 art/requests/entities/{skeleton,spider,creeper}.md 的调色板同源
                case MobKind.Skeleton: return "skeleton.json";
                case MobKind.Spider: return "spider.json";
                case MobKind.Creeper: return "creeper.json";
                // m11 W1-2（集成点②合并）：9 被动造型 JSON 已入库，文件名 = kind 小写——
                // FileNameOf 补映射后 Load/Build 门面即可加载（守卫测试已断言部位表本身）
                case MobKind.Sheep: return "sheep.json";
                case MobKind.Rabbit: return "rabbit.json";
                case MobKind.Fox: return "fox.json";
                case MobKind.Deer: return "deer.json";
                case MobKind.Panda: return "panda.json";
                case MobKind.Penguin: return "penguin.json";
                case MobKind.Goat: return "goat.json";
                case MobKind.Raccoon: return "raccoon.json";
                case MobKind.Hamster: return "hamster.json";
                default:
                    // 旧三类（Passive/Hostile/Neutral）没有独立造型 JSON，保底表在 MobModels 内
                    throw new ArgumentException(
                        "MobKind." + kind + " 无独立造型 JSON（旧三类走 MobModels 保底表）", nameof(kind));
            }
        }

        private static Vector3 ToVector3(float[] values, string sourceName, string partName, string field)
        {
            if (values == null || values.Length != 3)
            {
                throw new InvalidDataException(
                    "生物造型 JSON 部位 " + partName + " 的 " + field + " 应为 [x, y, z] 三元数组: " + sourceName);
            }
            return new Vector3(values[0], values[1], values[2]);
        }
    }
}
