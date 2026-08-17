// m11 W1-1（战斗）：战斗内容数据文件的守卫——bow/shield 物品、三配方、三敌对掉落条目。
// 模式抄 RecipeFilesTests：直接加载仓库真实 JSON（不走 Unity API，dotnet 链可跑），
// 数据写错在这里红，不等到 WorldBootstrap.Awake 蓝屏。
using System;
using System.IO;
using System.Linq;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Combat
{
    [TestFixture]
    public class CombatContentFilesTests
    {
        private static ItemDatabase LoadRealItems()
        {
            return ItemDatabase.FromJson(Directory.GetFiles(LocateDirectory("items"), "*.json").Select(File.ReadAllText));
        }

        [Test]
        public void 真实物品表_bow与shield已注册且字段照卡片()
        {
            var db = LoadRealItems();

            var bow = db.GetById("bow");
            Assert.That(bow.AttackDamage, Is.EqualTo(1f), "弓 attackDamage = 1（蓄力伤害另计）");
            Assert.That(bow.MaxStack, Is.EqualTo(1), "弓不可堆叠");
            Assert.That(bow.Texture, Is.EqualTo("bow"), "贴图引用 A2 已立的 bow 需求名");

            var shield = db.GetById("shield");
            Assert.That(shield.MaxStack, Is.EqualTo(1), "盾不可堆叠");
            Assert.That(shield.MaxDurability, Is.GreaterThan(0), "盾必须声明耐久（TakeDamage 挨打 -1）");
            Assert.That(shield.MaxDurability, Is.LessThanOrEqualTo(255), "Metadata 8 位上限 255");
            Assert.That(shield.Texture, Is.EqualTo("shield"));
        }

        [Test]
        public void 真实配方表_弓箭盾三配方可解析且输出正确()
        {
            var items = LoadRealItems();
            var docs = Directory.GetFiles(LocateDirectory("recipes"), "*.json").Select(File.ReadAllText);
            var db = RecipeDatabase.FromJson(docs, items);

            var bow = db.All.FirstOrDefault(r => r.Id == "bow_recipe");
            Assert.That(bow, Is.Not.Null, "缺 bow_recipe.json");
            Assert.That(bow.Output.ItemId, Is.EqualTo(items.GetById("bow").NumericId));

            var arrow = db.All.FirstOrDefault(r => r.Id == "arrow_recipe");
            Assert.That(arrow, Is.Not.Null, "缺 arrow_recipe.json");
            Assert.That(arrow.Output.ItemId, Is.EqualTo(items.GetById("arrow").NumericId));
            Assert.That(arrow.Output.Count, Is.EqualTo(4), "木棍+圆石 → 箭 ×4（卡片数值）");

            var shield = db.All.FirstOrDefault(r => r.Id == "shield_recipe");
            Assert.That(shield, Is.Not.Null, "缺 shield_recipe.json");
            Assert.That(shield.Output.ItemId, Is.EqualTo(items.GetById("shield").NumericId));
        }

        [Test]
        public void 真实掉落表_三敌对条目可解析且物品正确()
        {
            // drop_tables.json 新增 Skeleton/Spider/Creeper 条目——ParseKind 不认识会抛
            // ArgumentException（Load 阶段就炸），条目里的 itemId 必须真实可掉出来
            var table = MobDropTable.Load(LocateFile("mobs", "drop_tables.json"));

            AssertDropContains(table, MobKind.Skeleton, 1503, "骷髅应掉骨头（bone=1503）");
            AssertDropContains(table, MobKind.Skeleton, 1300, "骷髅应掉箭（arrow=1300）");
            AssertDropContains(table, MobKind.Spider, 1501, "蜘蛛应掉线（string_=1501）");
            AssertDropContains(table, MobKind.Creeper, 1502, "苦力怕应掉火药（gunpowder=1502）");
            // m11 W1-2（集成点②合并）：9 被动里只有羊有掉落（wool=1009），其余动物
            // v1 不掉（对应物品未注册，等后续波次）
            AssertDropContains(table, MobKind.Sheep, 1009, "羊应掉羊毛（wool=1009，1-2 张）");
        }

        /// <summary>多条目 + 概率掷骰：扫 200 个 seed，只要任一 seed 掷出该物品即算条目存在。</summary>
        private static void AssertDropContains(MobDropTable table, MobKind kind, int itemId, string because)
        {
            for (int seed = 0; seed < 200; seed++)
            {
                foreach (var stack in table.RollAll(kind, seed))
                {
                    if (stack.ItemId == itemId && stack.Count > 0)
                    {
                        return; // 命中过一次即可
                    }
                }
            }
            Assert.Fail($"{kind} 掉落表应含 {because}，200 个 seed 内一次都没掷出");
        }

        // ─── 目录定位（模式照 RecipeFilesTests.LocateDirectory） ─────────────

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

        private static string LocateFile(params string[] parts)
        {
            return Path.Combine(LocateDirectory(parts[0]), parts[1]);
        }
    }
}
