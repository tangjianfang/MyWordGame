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
                Is.False, "单笔 count 3 < 要求 4 不完成（累计进度记到 3/4）");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 }),
                Is.True, "类型 + 物品 + 累计数量都满足才完成");
            Assert.That(sys.CompletedCount, Is.EqualTo(1), "完成后计数 +1");
        }

        /// <summary>
        /// m6 C1 fix1：CraftItem/SmeltItem 是**累计**语义——「需求量 &gt; 单次批量」的任务
        /// （如炼 3 根铁锭、一次只取 1）要能分多笔凑满，而不是要求单笔事件 Count ≥ Required。
        /// 与当前条件不匹配的事件（类型对物品错 / 物品对类型错）不计入累计。
        /// </summary>
        [Test]
        public void Condition_CraftItem_AccumulatesAcrossEvents_ToRequired()
        {
            string path = WriteChapter(@"[
                { ""id"": ""q1"", ""name"": ""合成木板"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 4 }, ""rewardExp"": 5 },
                { ""id"": ""q2"", ""name"": ""下一任务"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 1 } ]");
            var sys = QuestSystem.LoadChapter(path);

            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 2 }),
                Is.False, "累计 2/4 还不够");
            Assert.That(sys.CurrentProgress, Is.EqualTo(2), "进度分子应累计到 2");

            // 中途插进来的不匹配事件不许污染累计（类型对但物品错的 CraftItem）
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1002, Count = 9 }),
                Is.False, "物品 id 不同不能推进");
            Assert.That(sys.CurrentProgress, Is.EqualTo(2), "不匹配的事件不应计入累计");
            // 物品对但类型错（ObtainItem）同样不计——口径是「产出累计」不是「拥有过」
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1001, Count = 9 }),
                Is.False, "类型不同不能推进");

            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 2 }),
                Is.True, "2 + 2 累计到 4 应完成（单笔都只有 2，任何一笔单独都不满足）");
            Assert.That(sys.Current.Id, Is.EqualTo("q2"), "完成后切下一任务");
            Assert.That(sys.CurrentProgress, Is.EqualTo(0), "切换任务后累计清零，下一任务从 0 重新计");
        }

        /// <summary>
        /// m6 C1 fix1 评审给出的具体场景：炼 3 根铁锭、一次只能取 1——
        /// 三笔 SmeltItem(iron_ingot, 1) 逐笔累计到 3 才完成。
        /// </summary>
        [Test]
        public void Condition_SmeltItem_AccumulatesSingleBarDrops_ToRequired()
        {
            string path = WriteChapter(@"[
                { ""id"": ""s3"", ""name"": ""炼三根铁锭"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SmeltItem"", ""itemId"": 1004, ""count"": 3 }, ""rewardExp"": 20 } ]");
            var sys = QuestSystem.LoadChapter(path);

            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SmeltItem, ItemId = 1004, Count = 1 }),
                Is.False, "1/3 不够——单笔 1 永远 < 3，旧的单笔比较口径会让任务永不打勾");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SmeltItem, ItemId = 1004, Count = 1 }),
                Is.False, "2/3 还不够");
            Assert.That(sys.CurrentProgress, Is.EqualTo(2), "累计分子应为 2（HUD 此刻显示 2/3，不越界）");
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.SmeltItem, ItemId = 1004, Count = 1 }),
                Is.True, "第三笔凑满 3/3 应完成");
            Assert.That(sys.CompletedCount, Is.EqualTo(1), "完成后计数 +1");
        }

        /// <summary>
        /// m6 C1 fix1：进度分子边界——任务未完成期间分子恒 ≤ 分母（HUD 的 n/m 不会出现 n&gt;m），
        /// 完成的瞬间分子清零并切换任务；ObtainItem 单笔超出要求时即时完成、不留超界分子。
        /// </summary>
        [Test]
        public void CurrentProgress_NeverExceedsRequiredWhileIncomplete_AndResetsOnCompletion()
        {
            string path = WriteChapter(@"[
                { ""id"": ""a"", ""name"": ""囤木柴"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 8 }, ""rewardExp"": 5 },
                { ""id"": ""b"", ""name"": ""捡原木"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1000, ""count"": 1 }, ""rewardExp"": 5 } ]");
            var sys = QuestSystem.LoadChapter(path);

            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 5 }),
                Is.False, "5/8 不够");
            Assert.That(sys.CurrentProgress, Is.LessThanOrEqualTo(8), "未完成期间分子不得超过分母");
            // 5 + 5 = 10 ≥ 8：完成的同一笔里分子先到 10，随即完成、清零、切任务——对外只见 0
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 5 }),
                Is.True, "累计 10 ≥ 8 应完成");
            Assert.That(sys.Current.Id, Is.EqualTo("b"), "切到下一任务");
            Assert.That(sys.CurrentProgress, Is.EqualTo(0), "完成清零后下一任务从 0 起");

            // ObtainItem 单笔超界（现存量 8 > 要求 1）：即时完成，同样不留超界分子
            Assert.That(sys.TryComplete(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 8 }),
                Is.True, "现存量 8 ≥ 要求 1 应即时完成");
            Assert.That(sys.Current, Is.Null, "全链完成");
            Assert.That(sys.CurrentProgress, Is.EqualTo(0), "全链完成后分子归零");
        }

        /// <summary>
        /// m6 C1 fix1：CraftItem/SmeltItem 的累计进度进存档——中途 2/4 存档，
        /// 新实例恢复后分子还是 2，再来一笔 2 即完成（跨存档不丢口径）。
        /// </summary>
        [Test]
        public void SaveRestore_PreservesAccumulatedProgress()
        {
            string path = WriteChapter(@"[
                { ""id"": ""q1"", ""name"": ""合成木板"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 4 }, ""rewardExp"": 5 } ]");
            var sys = QuestSystem.LoadChapter(path);
            sys.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 2 });

            QuestState state = sys.SaveState();
            Assert.That(state.CurrentQuestId, Is.EqualTo("q1"));
            Assert.That(state.Progress, Is.EqualTo(2), "存档快照应带出累计进度 2");

            var restored = QuestSystem.LoadChapter(path);
            restored.Restore(state);
            Assert.That(restored.CurrentProgress, Is.EqualTo(2), "恢复后累计进度应从 2 继续（不归零重挖）");
            Assert.That(restored.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 2 }),
                Is.True, "恢复后 2 + 2 凑满 4 应完成，不必从头再合 4 块");
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
