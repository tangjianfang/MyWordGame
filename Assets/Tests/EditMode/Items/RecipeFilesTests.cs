using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Items
{
    /// <summary>
    /// 校验仓库中真实的配方定义文件。任何 JSON 字段缺失、pattern 长度不对、引用未注册物品，
    /// 都会在这里立刻失败——避免「进游戏才抛 InvalidDataException → WorldBootstrap.Awake 中断
    /// → 只剩 skybox 蓝屏」这种回归。
    /// </summary>
    [TestFixture]
    public class RecipeFilesTests
    {
        private ItemDatabase _items;
        private List<string> _recipeDocs;

        [OneTimeSetUp]
        public void LoadRealDefinitions()
        {
            string itemsDir = LocateDirectory("items");
            string recipesDir = LocateDirectory("recipes");

            var itemDocs = Directory.GetFiles(itemsDir, "*.json").Select(File.ReadAllText);
            _items = ItemDatabase.FromJson(itemDocs);

            _recipeDocs = Directory.GetFiles(recipesDir, "*.json")
                .Select(File.ReadAllText)
                .ToList();
        }

        [Test]
        public void AllRecipeFiles_ParseSuccessfully()
        {
            var db = RecipeDatabase.FromJson(_recipeDocs, _items);
            Assert.That(db.Count, Is.GreaterThan(0), "没读到任何配方——目录路径错了？");
            Assert.That(db.Count, Is.EqualTo(_recipeDocs.Count),
                "有一份配方解析失败被吞了，应改为抛异常");
        }

        [Test]
        public void EveryRecipe_PatternLength_MatchesWidthTimesHeight()
        {
            int bad = 0;
            foreach (string doc in _recipeDocs)
            {
                try
                {
                    int w = JsonIntField(doc, "width") ?? 1;
                    int h = JsonIntField(doc, "height") ?? w;
                    int patLen = CountPatternStrings(doc);
                    if (patLen != w * h)
                    {
                        bad++;
                        string id = JsonStringField(doc, "id") ?? "?";
                        TestContext.Out.WriteLine($"  坏配方：{id} pattern={patLen} != {w}*{h}");
                    }
                }
                catch
                {
                    bad++;
                }
            }
            Assert.That(bad, Is.EqualTo(0),
                "所有配方的 pattern 长度必须等于 width*height，否则 RecipeDatabase.FromJson 抛异常导致 WorldBootstrap.Awake 中断");
        }

        [Test]
        public void EveryRecipe_OutputItem_IsRegistered()
        {
            int bad = 0;
            foreach (string doc in _recipeDocs)
            {
                string outId = ExtractOutputItemId(doc);
                if (string.IsNullOrEmpty(outId) || !_items.TryGetById(outId, out _))
                {
                    bad++;
                    string id = JsonStringField(doc, "id") ?? "?";
                    TestContext.Out.WriteLine($"  未注册输出：{id} → {outId}");
                }
            }
            Assert.That(bad, Is.EqualTo(0), "配方引用了未注册的输出物品");
        }

        [Test]
        public void EveryRecipe_PatternSymbol_IsRegistered()
        {
            int bad = 0;
            foreach (string doc in _recipeDocs)
            {
                foreach (string s in ExtractPatternSymbols(doc))
                {
                    if (string.IsNullOrEmpty(s) || s == "_" || s == "0") continue;
                    if (!_items.TryGetById(s, out _))
                    {
                        bad++;
                        string id = JsonStringField(doc, "id") ?? "?";
                        TestContext.Out.WriteLine($"  未注册符号：{id} 含 {s}");
                    }
                }
            }
            Assert.That(bad, Is.EqualTo(0), "配方 pattern 引用了未注册的物品 id");
        }

        // --- 轻量 JSON 扫描（不依赖 Newtonsoft）---

        private static readonly Regex IntFieldRegex = new Regex(
            "\"\\s*(?<name>width|height)\\s*\"\\s*:\\s*(?<num>-?\\d+)", RegexOptions.Compiled);

        private static readonly Regex StringFieldRegex = new Regex(
            "\"\\s*(?<name>id|item)\\s*\"\\s*:\\s*\"(?<val>[^\"]*)\"", RegexOptions.Compiled);

        private static int? JsonIntField(string doc, string name)
        {
            foreach (Match m in IntFieldRegex.Matches(doc))
            {
                if (m.Groups["name"].Value == name)
                    return int.Parse(m.Groups["num"].Value);
            }
            return null;
        }

        private static string JsonStringField(string doc, string name)
        {
            foreach (Match m in StringFieldRegex.Matches(doc))
            {
                if (m.Groups["name"].Value == name)
                    return m.Groups["val"].Value;
            }
            return null;
        }

        private static readonly Regex PatternArrayRegex = new Regex(
            "\"\\s*pattern\\s*\"\\s*:\\s*\\[(?<body>[^\\]]*)\\]", RegexOptions.Compiled | RegexOptions.Singleline);

        private static int CountPatternStrings(string doc)
        {
            Match m = PatternArrayRegex.Match(doc);
            if (!m.Success) return -1;
            return Regex.Matches(m.Groups["body"].Value, "\"(?:[^\"\\\\]|\\\\.)*\"").Count;
        }

        private static IEnumerable<string> ExtractPatternSymbols(string doc)
        {
            Match m = PatternArrayRegex.Match(doc);
            if (!m.Success) yield break;
            foreach (Match s in Regex.Matches(m.Groups["body"].Value, "\"(?:[^\"\\\\]|\\\\.)*\""))
                yield return s.Value.Trim('"');
        }

        // 输出块在另一层，单独匹配
        private static readonly Regex OutputItemRegex = new Regex(
            "\"\\s*output\\s*\"\\s*:\\s*\\{[^{}]*?\"\\s*item\\s*\"\\s*:\\s*\"(?<val>[^\"]*)\"",
            RegexOptions.Compiled);

        private static string ExtractOutputItemId(string doc)
        {
            Match m = OutputItemRegex.Match(doc);
            return m.Success ? m.Groups["val"].Value : null;
        }

        private static string LocateDirectory(string subdir)
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, subdir);
#else
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", subdir);
                if (Directory.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException($"未能从测试输出目录向上找到 Assets/StreamingAssets/{subdir}。");
#endif
        }
    }
}