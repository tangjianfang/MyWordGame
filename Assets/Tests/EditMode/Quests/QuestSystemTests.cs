using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Quests;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Quests
{
    /// <summary>
    /// m6 C1：任务链状态机。链式解锁（完成才下一个）、四类条件、存档 round-trip、
    /// 加载写严格（坏 JSON / id 重复 / condition.type 非法抛异常）。
    /// </summary>
    [TestFixture]
    public class QuestSystemTests
    {
        /// <summary>WriteChapter 产生的临时文件，TearDown 统一清理。</summary>
        private static readonly List<string> TempFiles = new List<string>();

        private static string WriteChapter(string json)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, json);
            TempFiles.Add(path);
            return path;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string path in TempFiles)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            TempFiles.Clear();
        }

        [Test]
        public void Chain_UnlocksSequentially()
        {
            string path = WriteChapter(@"[
                { ""id"": ""q1"", ""name"": ""挖一根原木"", ""desc"": ""对着树干按左键"",
                  ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1001, ""count"": 1 }, ""rewardExp"": 5 },
                { ""id"": ""q2"", ""name"": ""合成木板"", ""desc"": ""按 E 打开背包"",
                  ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1000, ""count"": 4 }, ""rewardExp"": 5 } ]");
            var sys = QuestSystem.LoadChapter(path);
            Assert.That(sys.Current.Id, Is.EqualTo("q1"));
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1000, Count = 4 }),
                Is.False, "没完成 q1 前，q2 的条件事件不推进");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1001, Count = 1 }),
                Is.True, "q1 条件满足应完成");
            Assert.That(sys.Current.Id, Is.EqualTo("q2"), "完成 q1 解锁 q2");
            Assert.That(sys.CompletedCount, Is.EqualTo(1));
        }

        [Test]
        public void Condition_CraftItem_TypeOrItemMismatchOrCountShort_DoesNotComplete()
        {
            string path = WriteChapter(@"[
                { ""id"": ""c1"", ""name"": ""合成木板"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 4 }, ""rewardExp"": 5 } ]");
            var sys = QuestSystem.LoadChapter(path);

            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1001, Count = 8 }),
                Is.False, "ObtainItem 事件不能推进 CraftItem 条件——类型必须精确匹配");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1003, Count = 8 }),
                Is.False, "物品 id 不同不能推进");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 3 }),
                Is.False, "count 3 < 要求 4，不满足");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 }),
                Is.True, "类型 + 物品 + 数量都满足才完成");
            Assert.That(sys.CompletedCount, Is.EqualTo(1), "完成后计数 +1");
        }

        [Test]
        public void Condition_SmeltItem_Completes_OnMatchingEvent()
        {
            string path = WriteChapter(@"[
                { ""id"": ""s1"", ""name"": ""炼一根铁锭"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SmeltItem"", ""itemId"": 1004, ""count"": 1 }, ""rewardExp"": 20 } ]");
            var sys = QuestSystem.LoadChapter(path);

            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1004, Count = 1 }),
                Is.False, "CraftItem 事件不能推进 SmeltItem 条件");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SmeltItem, ItemId = 1004, Count = 1 }),
                Is.True, "SmeltItem 事件命中应完成");
            Assert.That(sys.Current, Is.Null, "单任务链完成后 Current 为 null");
        }

        [Test]
        public void Condition_SurviveNight_IgnoresItemFields_Completes()
        {
            string path = WriteChapter(@"[
                { ""id"": ""n1"", ""name"": ""活过一夜"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 30 } ]");
            var sys = QuestSystem.LoadChapter(path);

            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 99 }),
                Is.False, "物品事件不能推进 SurviveNight 条件");
            // SurviveNight 事件不带条件参数：ItemId/Count 是遗留值（默认 0）也应直接完成
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SurviveNight }),
                Is.True, "SurviveNight 事件无条件参数，到达即完成");
        }

        [Test]
        public void FullChain_AfterLast_CurrentIsNull_TryCompleteSafe()
        {
            string path = WriteChapter(@"[
                { ""id"": ""q1"", ""name"": ""一"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 1 },
                { ""id"": ""q2"", ""name"": ""二"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 2 } ]");
            var sys = QuestSystem.LoadChapter(path);

            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SurviveNight }), Is.True);
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SurviveNight }), Is.True);
            Assert.That(sys.Current, Is.Null, "全链完成后 Current 为 null");
            Assert.That(sys.CompletedCount, Is.EqualTo(2), "完成计数等于链长");
            // 全链完成后再来事件不炸、不推进计数
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SurviveNight }),
                Is.False, "全链完成后 TryComplete 返回 false");
            Assert.That(sys.CompletedCount, Is.EqualTo(2), "完成后计数不再增长");
        }

        [Test]
        public void SaveRestore_RoundTrip()
        {
            string path = WriteChapter(@"[
                { ""id"": ""q1"", ""name"": ""一"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 1 },
                { ""id"": ""q2"", ""name"": ""二"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 2 },
                { ""id"": ""q3"", ""name"": ""三"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 3 } ]");
            var sys = QuestSystem.LoadChapter(path);
            sys.TryComplete(new QuestEvent { Type = QuestEventType.SurviveNight });

            // 中途存档 → 新实例恢复：Current 与 CompletedCount 一致
            QuestState state = sys.SaveState();
            var restored = QuestSystem.LoadChapter(path);
            restored.Restore(state);
            Assert.That(restored.Current.Id, Is.EqualTo("q2"), "恢复后应停在存档时的当前任务");
            Assert.That(restored.CompletedCount, Is.EqualTo(1), "恢复后完成计数一致");

            // 从恢复点继续走完，再存再恢复：全链完成态（Current == null）也应 round-trip
            restored.TryComplete(new QuestEvent { Type = QuestEventType.SurviveNight });
            restored.TryComplete(new QuestEvent { Type = QuestEventType.SurviveNight });
            QuestState doneState = restored.SaveState();
            var restoredDone = QuestSystem.LoadChapter(path);
            restoredDone.Restore(doneState);
            Assert.That(restoredDone.Current, Is.Null, "全链完成态恢复后 Current 仍为 null");
            Assert.That(restoredDone.CompletedCount, Is.EqualTo(3), "全链完成态恢复后计数一致");
        }

        [Test]
        public void LoadChapter_UnregisteredItemId_StillLoads()
        {
            // Core 不查 ItemDatabase——悬空 itemId 引用是 C2 侧集成守卫的任务。
            // 这里锁定：Loader 只做 JSON 解析与链校验，物品 id 原样放行。
            string path = WriteChapter(@"[
                { ""id"": ""q1"", ""name"": ""神秘物品"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 999999, ""count"": 1 }, ""rewardExp"": 5 } ]");
            var sys = QuestSystem.LoadChapter(path);
            Assert.That(sys.Current.Condition.ItemId, Is.EqualTo(999999), "Loader 不校验 ItemDatabase，id 原样保留");
        }

        [Test]
        public void LoadChapter_BadJson_Throws()
        {
            string path = WriteChapter("{ 这不是合法 JSON");
            Assert.That(() => QuestSystem.LoadChapter(path),
                Throws.TypeOf<System.IO.InvalidDataException>(), "坏 JSON 必须抛 InvalidDataException（写严格）");

            string emptyPath = WriteChapter("null");
            Assert.That(() => QuestSystem.LoadChapter(emptyPath),
                Throws.TypeOf<System.IO.InvalidDataException>(), "解析为 null 同样抛 InvalidDataException");
        }

        [Test]
        public void LoadChapter_DuplicateId_Throws()
        {
            string path = WriteChapter(@"[
                { ""id"": ""q1"", ""name"": ""一"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 1 },
                { ""id"": ""q1"", ""name"": ""重复"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 2 } ]");
            Assert.That(() => QuestSystem.LoadChapter(path),
                Throws.TypeOf<System.IO.InvalidDataException>(), "任务 id 重复必须抛异常（链校验）");
        }

        [Test]
        public void LoadChapter_UnknownConditionType_Throws()
        {
            string path = WriteChapter(@"[
                { ""id"": ""q1"", ""name"": ""一"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""DefeatBoss"", ""itemId"": 1, ""count"": 1 }, ""rewardExp"": 1 } ]");
            Assert.That(() => QuestSystem.LoadChapter(path),
                Throws.TypeOf<System.ArgumentException>(), "condition.type 非法必须抛 ArgumentException（写严格）");
        }

        /// <summary>
        /// 真实首章数据集成测试：chapter1.json 能加载、共 8 个任务、
        /// 首任务是「挖一根原木」（ObtainItem log=1000 ×1）、末任务是「活过一夜」（SurviveNight）。
        /// </summary>
        [Test]
        public void Chapter1File_LoadsEightQuestChain()
        {
            var sys = QuestSystem.LoadChapter(Chapter1Path());
            Assert.That(sys.CompletedCount, Is.EqualTo(0), "新加载的任务链完成数为 0");
            int total = 0;
            Quest current = sys.Current;
            while (current != null)
            {
                total++;
                Assert.That(current.RewardExp, Is.GreaterThan(0), $"任务 {current.Id} 的奖励经验应 > 0");
                Assert.That(current.Condition, Is.Not.Null, $"任务 {current.Id} 必须有条件");
                Assert.That(sys.TryComplete(MakeSatisfyingEvent(current.Condition)),
                    Is.True, $"按条件构造的事件应能完成任务 {current.Id}");
                current = sys.Current;
            }
            Assert.That(total, Is.EqualTo(8), "首章应有 8 个任务");
        }

        /// <summary>按条件反推一个必然满足的 QuestEvent（SurviveNight 直发，物品条件给足数量）。</summary>
        private static QuestEvent MakeSatisfyingEvent(QuestCondition condition)
        {
            // ConditionType 与 QuestEventType 成员一一对应（ObtainItem/CraftItem/SmeltItem/SurviveNight），
            // 同名成员底层值相同，显式转换安全。
            return new QuestEvent
            {
                Type = (QuestEventType)condition.Type,
                ItemId = condition.ItemId,
                Count = condition.RequiredCount,
            };
        }

        private static string Chapter1Path()
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "quests", "chapter1.json");
#else
            var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "quests", "chapter1.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException("未能找到 Assets/StreamingAssets/quests/chapter1.json。");
#endif
        }
    }
}
