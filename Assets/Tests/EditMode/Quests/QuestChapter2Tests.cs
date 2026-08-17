using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Items;
using MyWorld.Core.Quests;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Quests
{
    /// <summary>
    /// m11 W2-4：第二章新条件类型（SleepInBed/TillSoil/SowSeed/HarvestCrop/FeedAnimal/
    /// EquipArmorFull/EnchantItem/KillKind）的加载解析与判定语义 + 真实 chapter2.json
    /// 集成守卫（全链事件驱动 / 悬空 itemId / kind 合法性）。纯 Core，双链同跑。
    /// </summary>
    [TestFixture]
    public class QuestChapter2Tests
    {
        private static readonly List<string> TempFiles = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (string path in TempFiles)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            TempFiles.Clear();
        }

        /// <summary>单任务链加载助手：写临时 JSON → LoadChapter。</summary>
        private static QuestSystem LoadSingle(string conditionJson)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, $@"[
                {{ ""id"": ""t"", ""name"": ""测试任务"", ""desc"": ""..."",
                  ""condition"": {conditionJson}, ""rewardExp"": 5 }} ]");
            TempFiles.Add(path);
            return QuestSystem.LoadChapter(path);
        }

        // ─── 加载解析：新类型逐个落对字段 ───────────────────────────────────

        [Test]
        public void 解析_无条件参数类_itemId写了也被忽略_规范化为零和一()
        {
            // 五个无条件参数类型共用 SurviveNight 的规范化策略（HUD 显示 0/1）
            string[] types = { "SleepInBed", "TillSoil", "EquipArmorFull", "EnchantItem" };
            foreach (string type in types)
            {
                QuestSystem sys = LoadSingle(
                    $@"{{ ""type"": ""{type}"", ""itemId"": 999, ""count"": 9, ""kind"": ""Pig"" }}");
                QuestCondition condition = sys.Current.Condition;
                Assert.That(condition.Type.ToString(), Is.EqualTo(type), type + " 应解析成功");
                Assert.That(condition.ItemId, Is.EqualTo(0), type + " 的 itemId 写了也忽略");
                Assert.That(condition.RequiredCount, Is.EqualTo(1), type + " 的 count 固定 1");
                Assert.That(condition.Kind, Is.Null, type + " 不使用 kind");
            }
        }

        [Test]
        public void 解析_SowSeed_物品类条件_itemId必填()
        {
            QuestSystem sys = LoadSingle(@"{ ""type"": ""SowSeed"", ""itemId"": 1019, ""count"": 3 }");
            Assert.That(sys.Current.Condition.Type, Is.EqualTo(ConditionType.SowSeed));
            Assert.That(sys.Current.Condition.ItemId, Is.EqualTo(1019), "种子物品 id 原样保留");
            Assert.That(sys.Current.Condition.RequiredCount, Is.EqualTo(3));

            Assert.That(() => LoadSingle(@"{ ""type"": ""SowSeed"", ""count"": 1 }"),
                Throws.TypeOf<System.IO.InvalidDataException>(), "SowSeed 缺 itemId 必须抛（写严格）");
        }

        [Test]
        public void 解析_HarvestCrop_itemId忽略_count必填()
        {
            QuestSystem sys = LoadSingle(@"{ ""type"": ""HarvestCrop"", ""itemId"": 1018, ""count"": 2 }");
            Assert.That(sys.Current.Condition.ItemId, Is.EqualTo(0), "收获不区分作物：itemId 恒 0");
            Assert.That(sys.Current.Condition.RequiredCount, Is.EqualTo(2));

            Assert.That(() => LoadSingle(@"{ ""type"": ""HarvestCrop"", ""count"": 0 }"),
                Throws.TypeOf<System.IO.InvalidDataException>(), "count=0 必须抛（写严格）");
        }

        [Test]
        public void 解析_FeedAnimal_kind可选_非法kind抛()
        {
            QuestSystem sys = LoadSingle(@"{ ""type"": ""FeedAnimal"", ""kind"": ""Sheep"", ""count"": 2 }");
            Assert.That(sys.Current.Condition.Kind, Is.EqualTo(MobKind.Sheep), "kind 解析成 MobKind 枚举");
            Assert.That(sys.Current.Condition.ItemId, Is.EqualTo(0), "喂食不看物品 id");

            QuestSystem any = LoadSingle(@"{ ""type"": ""FeedAnimal"", ""count"": 1 }");
            Assert.That(any.Current.Condition.Kind, Is.Null, "不写 kind = 不限物种");

            Assert.That(() => LoadSingle(@"{ ""type"": ""FeedAnimal"", ""kind"": ""Boss"", ""count"": 1 }"),
                Throws.TypeOf<System.ArgumentException>(), "非法生物名必须抛（写严格）");
        }

        [Test]
        public void 解析_KillKind_kind必填_weapon可选()
        {
            QuestSystem sys = LoadSingle(
                @"{ ""type"": ""KillKind"", ""kind"": ""Skeleton"", ""weapon"": ""bow"", ""count"": 1 }");
            Assert.That(sys.Current.Condition.Kind, Is.EqualTo(MobKind.Skeleton));
            Assert.That(sys.Current.Condition.Weapon, Is.EqualTo("bow"), "武器限定原样保留");

            QuestSystem noWeapon = LoadSingle(@"{ ""type"": ""KillKind"", ""kind"": ""Creeper"", ""count"": 1 }");
            Assert.That(noWeapon.Current.Condition.Weapon, Is.Null, "不写 weapon = 任意武器都算");

            Assert.That(() => LoadSingle(@"{ ""type"": ""KillKind"", ""count"": 1 }"),
                Throws.TypeOf<System.IO.InvalidDataException>(), "KillKind 缺 kind 必须抛（写严格）");
            Assert.That(() => LoadSingle(@"{ ""type"": ""KillKind"", ""kind"": ""NotAMob"", ""count"": 1 }"),
                Throws.TypeOf<System.ArgumentException>(), "非法 kind 必须抛");
        }

        [Test]
        public void 解析_未知类型_抛ArgumentException并列出新类型()
        {
            System.ArgumentException ex = Assert.Throws<System.ArgumentException>(
                () => LoadSingle(@"{ ""type"": ""DefeatBoss"", ""count"": 1 }"));
            StringAssert.Contains("KillKind", ex.Message, "报错信息应列出全部合法类型（含 KillKind）");
        }

        // ─── 判定语义 ────────────────────────────────────────────────────

        [Test]
        public void 语义_SleepInBed_事件到达即完成_不发其它类型()
        {
            QuestSystem sys = LoadSingle(@"{ ""type"": ""SleepInBed"" }");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SurviveNight }),
                Is.False, "活过夜不能顶替睡觉");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SleepInBed }),
                Is.True, "睡觉事件到达即完成");
        }

        [Test]
        public void 语义_SowSeed_物品不匹配不计_累计凑满()
        {
            QuestSystem sys = LoadSingle(@"{ ""type"": ""SowSeed"", ""itemId"": 1019, ""count"": 3 }");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SowSeed, ItemId = 1028, Count = 9 }),
                Is.False, "播的是别的种子（甜菜 1028）不算");
            Assert.That(sys.CurrentProgress, Is.EqualTo(0), "不匹配事件不污染累计");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1019, Count = 9 }),
                Is.False, "获得种子 ≠ 播种");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SowSeed, ItemId = 1019, Count = 1 }),
                Is.False, "1/3 不够");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SowSeed, ItemId = 1019, Count = 2 }),
                Is.True, "1+2=3 累计凑满（多笔播种）");
        }

        [Test]
        public void 语义_HarvestCrop_不认物品id_只数收获株数()
        {
            QuestSystem sys = LoadSingle(@"{ ""type"": ""HarvestCrop"", ""count"": 2 }");
            // 事件的 ItemId 恒 0（收口接线只带 Count）：两种作物的收获都进同一累计
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.HarvestCrop, ItemId = 0, Count = 1 }),
                Is.False, "1/2 不够");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.HarvestCrop, ItemId = 0, Count = 1 }),
                Is.True, "第二株凑满 2/2");
        }

        [Test]
        public void 语义_FeedAnimal_kind限定_喂错动物不算_两只凑满()
        {
            QuestSystem sys = LoadSingle(@"{ ""type"": ""FeedAnimal"", ""kind"": ""Sheep"", ""count"": 2 }");
            Assert.That(sys.TryComplete(
                    new QuestEvent { Type = QuestEventType.FeedAnimal, Kind = MobKind.Pig, Count = 1 }),
                Is.False, "喂猪不算（任务要羊）");
            Assert.That(sys.TryComplete(
                    new QuestEvent { Type = QuestEventType.FeedAnimal, Kind = null, Count = 9 }),
                Is.False, "不带 kind 的喂食事件满足不了限定羊的条件");
            Assert.That(sys.CurrentProgress, Is.EqualTo(0));
            Assert.That(sys.TryComplete(
                    new QuestEvent { Type = QuestEventType.FeedAnimal, Kind = MobKind.Sheep, Count = 1 }),
                Is.False, "第一只羊 1/2");
            Assert.That(sys.CurrentProgress, Is.EqualTo(1), "HUD 此刻显示 1/2");
            Assert.That(sys.TryComplete(
                    new QuestEvent { Type = QuestEventType.FeedAnimal, Kind = MobKind.Sheep, Count = 1 }),
                Is.True, "第二只羊凑满（繁殖需要两只各喂一次）");
        }

        [Test]
        public void 语义_KillKind_武器限定_近战不能冒充弓杀_计数累计()
        {
            QuestSystem bow = LoadSingle(
                @"{ ""type"": ""KillKind"", ""kind"": ""Skeleton"", ""weapon"": ""bow"", ""count"": 1 }");
            Assert.That(bow.TryComplete(
                    new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Skeleton, Weapon = null, Count = 1 }),
                Is.False, "近战（不带武器）杀骷髅不算「用弓击败」");
            Assert.That(bow.TryComplete(
                    new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Zombie, Weapon = "bow", Count = 1 }),
                Is.False, "弓杀僵尸不算（任务要骷髅）");
            Assert.That(bow.TryComplete(
                    new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Skeleton, Weapon = "bow", Count = 1 }),
                Is.True, "弓杀骷髅命中");

            QuestSystem anyWeapon = LoadSingle(
                @"{ ""type"": ""KillKind"", ""kind"": ""Creeper"", ""count"": 2 }");
            Assert.That(anyWeapon.TryComplete(
                    new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Creeper, Weapon = null, Count = 1 }),
                Is.False, "不限武器：第一只 1/2");
            Assert.That(anyWeapon.TryComplete(
                    new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Creeper, Weapon = "bow", Count = 1 }),
                Is.True, "第二只（弓杀也行）凑满 2/2");
        }

        // ─── 真实 chapter2.json 集成守卫 ─────────────────────────────────

        [Test]
        public void 真实chapter2链_八步事件驱动全解锁_类型与顺序正确()
        {
            var sys = QuestSystem.LoadChapter(ChapterPath());
            string[] expectedOrder =
            {
                "ch2_01_sleep_in_bed", "ch2_02_sow_wheat", "ch2_03_first_harvest",
                "ch2_04_feed_two_sheep", "ch2_05_full_iron_armor", "ch2_06_first_enchant",
                "ch2_07_bow_kill_skeleton", "ch2_08_defeat_creeper",
            };
            ConditionType[] expectedTypes =
            {
                ConditionType.SleepInBed, ConditionType.SowSeed, ConditionType.HarvestCrop,
                ConditionType.FeedAnimal, ConditionType.EquipArmorFull, ConditionType.EnchantItem,
                ConditionType.KillKind, ConditionType.KillKind,
            };

            int index = 0;
            while (sys.Current != null)
            {
                Assert.That(sys.Current.Id, Is.EqualTo(expectedOrder[index]),
                    $"第 {index + 1} 步的 id 应按链式顺序（存档记 id，定了别改）");
                Assert.That(sys.Current.Condition.Type, Is.EqualTo(expectedTypes[index]),
                    $"第 {index + 1} 步的条件类型");
                Assert.That(sys.TryComplete(MakeSatisfyingEvent(sys.Current.Condition)), Is.True,
                    $"按条件构造的事件应能完成 {expectedOrder[index]}");
                index++;
            }
            Assert.That(index, Is.EqualTo(8), "第二章应有 8 个任务且全部事件驱动走完");
            Assert.That(sys.Current, Is.Null, "全链完成后 Current 为 null");
        }

        [Test]
        public void 真实chapter2链_SowSeed的itemId_在真实物品表已注册()
        {
            // _format.md 的约定：悬空 itemId 由集成守卫测试负责——SowSeed 引用的
            // 麦种 1019 必须真实存在于 items/*.json，否则任务永远无法完成
            var sys = QuestSystem.LoadChapter(ChapterPath());
            QuestCondition sow = null;
            while (sys.Current != null)
            {
                if (sys.Current.Condition.Type == ConditionType.SowSeed)
                {
                    sow = sys.Current.Condition;
                    break;
                }
                Assert.That(sys.TryComplete(MakeSatisfyingEvent(sys.Current.Condition)), Is.True, "前置推进");
            }
            Assert.That(sow, Is.Not.Null, "chapter2 应含 SowSeed 条件（ch2_02）");

            ItemDatabase items = LoadRealItems();
            Assert.That(items.TryGetByNumericId(sow.ItemId, out ItemDefinition def), Is.True,
                $"SowSeed 引用的 itemId={sow.ItemId} 未在 items/*.json 注册（悬空引用）");
            Assert.That(def.Id, Is.EqualTo("seeds_wheat"), "第二章锄地播种的对象应是小麦种子");
        }

        [Test]
        public void 真实chapter2链_奖励经验全为正_合计不低于第一章()
        {
            var sys = QuestSystem.LoadChapter(ChapterPath());
            int total = 0;
            while (sys.Current != null)
            {
                Assert.That(sys.Current.RewardExp, Is.GreaterThan(0), "第二章每步都应给正经验");
                total += sys.Current.RewardExp;
                Assert.That(sys.TryComplete(MakeSatisfyingEvent(sys.Current.Condition)), Is.True);
            }
            Assert.That(total, Is.EqualTo(170), "第二章合计 170 经验（10+10+15+15+25+25+30+40），档位高于第一章 105");
        }

        /// <summary>按条件反推必然满足的事件（含 kind/weapon 维度）。</summary>
        private static QuestEvent MakeSatisfyingEvent(QuestCondition condition)
        {
            return new QuestEvent
            {
                Type = (QuestEventType)condition.Type,
                ItemId = condition.ItemId,
                Count = condition.RequiredCount,
                Kind = condition.Kind,
                Weapon = condition.Weapon,
            };
        }

        /// <summary>加载真实物品表（dotnet 链向上找仓库根；EditMode 用 streamingAssetsPath）。</summary>
        private static ItemDatabase LoadRealItems()
        {
            string directory = ItemsDirectory();
            var documents = new List<string>();
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                documents.Add(File.ReadAllText(path));
            }
            return ItemDatabase.FromJson(documents);
        }

        private static string ItemsDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "items");
#else
            var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "items");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("未能找到 Assets/StreamingAssets/items。");
#endif
        }

        private static string ChapterPath()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "quests", "chapter2.json");
#else
            var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "quests", "chapter2.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException("未能找到 Assets/StreamingAssets/quests/chapter2.json。");
#endif
        }
    }
}
