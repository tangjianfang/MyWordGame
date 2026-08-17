using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Quests;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Quests
{
    /// <summary>
    /// m11 W2-4：多章节任务书 QuestCampaign。章节顺序解锁（前章未完成后章事件无效、
    /// 前章全链完成自动切下一章）、每章独立存档快照、旧档单章字段兼容
    /// （第一章接续 / 已完成则直接解锁第二章）。纯 Core，dotnet 与 EditMode 双链同跑。
    /// </summary>
    [TestFixture]
    public class QuestCampaignTests
    {
        /// <summary>WriteChapter 产生的临时文件，TearDown 统一清理。</summary>
        private static readonly List<string> TempFiles = new List<string>();

        /// <summary>第一章（沿用 m6 的两任务形态）：q1 拾原木 → q2 合成木板 ×4。</summary>
        private const string ChapterOneJson = @"[
            { ""id"": ""q1"", ""name"": ""挖一根原木"", ""desc"": ""..."",
              ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1000, ""count"": 1 }, ""rewardExp"": 5 },
            { ""id"": ""q2"", ""name"": ""合成木板"", ""desc"": ""..."",
              ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 4 }, ""rewardExp"": 5 } ]";

        /// <summary>第二章：s1 睡觉 → s2 用弓杀骷髅（覆盖新条件类型）。</summary>
        private const string ChapterTwoJson = @"[
            { ""id"": ""s1"", ""name"": ""在床上睡到天亮"", ""desc"": ""..."",
              ""condition"": { ""type"": ""SleepInBed"" }, ""rewardExp"": 10 },
            { ""id"": ""s2"", ""name"": ""用弓击败骷髅"", ""desc"": ""..."",
              ""condition"": { ""type"": ""KillKind"", ""kind"": ""Skeleton"", ""weapon"": ""bow"", ""count"": 1 }, ""rewardExp"": 30 } ]";

        [TearDown]
        public void TearDown()
        {
            foreach (string path in TempFiles)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            TempFiles.Clear();
        }

        private static string WriteChapter(string json)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, json);
            TempFiles.Add(path);
            return path;
        }

        /// <summary>装订两章任务书（每次调用重新加载，模拟重启后从章节文件重建）。</summary>
        private static QuestCampaign BindTwoChapters()
        {
            return QuestCampaign.Load(WriteChapter(ChapterOneJson), WriteChapter(ChapterTwoJson));
        }

        /// <summary>按条件反推一个必然满足的 QuestEvent（含 m11 W2-4 的 kind/weapon 维度）。</summary>
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

        [Test]
        public void Load_两章_活动章是第一章()
        {
            QuestCampaign campaign = BindTwoChapters();

            Assert.That(campaign.ChapterCount, Is.EqualTo(2), "两章文件装订成两章任务书");
            Assert.That(campaign.ActiveChapterIndex, Is.EqualTo(0), "新任务书从第一章开始");
            Assert.That(campaign.Active.Current.Id, Is.EqualTo("q1"), "活动章首任务是第一章首任务");
            Assert.That(campaign.IsChapterComplete(0), Is.False, "第一章未完成");
        }

        [Test]
        public void 顺序解锁_前章未完成_后章事件全部无效()
        {
            QuestCampaign campaign = BindTwoChapters();

            // 第二章的睡觉 / 弓杀骷髅事件在第一章没走完时到达：一律不算数
            Assert.That(
                campaign.TryComplete(new QuestEvent { Type = QuestEventType.SleepInBed },
                    out Quest completed1, out _, out _),
                Is.False, "后章事件不得推进任务书");
            Assert.That(completed1, Is.Null);
            Assert.That(
                campaign.TryComplete(
                    new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Skeleton, Weapon = "bow", Count = 1 },
                    out Quest completed2, out _, out _),
                Is.False, "后章的击杀事件同样不得推进");
            Assert.That(completed2, Is.Null);
            Assert.That(campaign.ActiveChapterIndex, Is.EqualTo(0), "活动章仍是第一章");
        }

        [Test]
        public void 第一章全链完成_自动切第二章_末章完成停在原地()
        {
            QuestCampaign campaign = BindTwoChapters();

            // 走完第一章（q1 拾取 + q2 合成 ×4）
            bool q1Done = campaign.TryComplete(
                new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 },
                out Quest first, out int chapter1, out bool chapter1Finished);
            Assert.That(q1Done, Is.True);
            Assert.That(first.Id, Is.EqualTo("q1"));
            Assert.That(chapter1, Is.EqualTo(0));
            Assert.That(chapter1Finished, Is.False, "链中任务完成≠章节完成");

            bool q2Done = campaign.TryComplete(
                new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 },
                out Quest last, out int chapter1Again, out bool unlocked);
            Assert.That(q2Done, Is.True);
            Assert.That(last.Id, Is.EqualTo("q2"), "完成的是第一章末任务");
            Assert.That(chapter1Again, Is.EqualTo(0));
            Assert.That(unlocked, Is.True, "第一章走完的同一笔应报告章节完成");
            Assert.That(campaign.ActiveChapterIndex, Is.EqualTo(1), "活动章自动前移到第二章（顺序解锁）");
            Assert.That(campaign.Active.Current.Id, Is.EqualTo("s1"), "新章首任务立即就位");

            // 走完第二章（末章）：章节完成但活动章停在末章，Current 为 null
            campaign.TryComplete(new QuestEvent { Type = QuestEventType.SleepInBed }, out _, out _, out _);
            bool s2Done = campaign.TryComplete(
                new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Skeleton, Weapon = "bow", Count = 1 },
                out _, out int chapter2, out bool finished2);
            Assert.That(s2Done, Is.True);
            Assert.That(chapter2, Is.EqualTo(1));
            Assert.That(finished2, Is.True);
            Assert.That(campaign.ActiveChapterIndex, Is.EqualTo(1), "末章完成不再前移（没有下一章）");
            Assert.That(campaign.Active.Current, Is.Null, "全部完成后活动章 Current 为 null");
            Assert.That(campaign.IsChapterComplete(0) && campaign.IsChapterComplete(1), Is.True, "两章都完成");

            // 全部完成后再来事件安全 no-op
            Assert.That(campaign.TryComplete(new QuestEvent { Type = QuestEventType.SleepInBed },
                out Quest none, out _, out _), Is.False);
            Assert.That(none, Is.Null);
        }

        [Test]
        public void SaveAll_两章各自快照_RestoreAll_接续到存档时刻()
        {
            QuestCampaign source = BindTwoChapters();
            // 推进：第一章全链完成 + 第二章 s1 完成（停在 s2）
            source.TryComplete(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 }, out _, out _, out _);
            source.TryComplete(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 }, out _, out _, out _);
            source.TryComplete(new QuestEvent { Type = QuestEventType.SleepInBed }, out _, out _, out _);

            List<QuestState> saved = source.SaveAll();
            Assert.That(saved.Count, Is.EqualTo(2), "每章一个快照、顺序与章节一致");
            Assert.That(saved[0].CurrentQuestId, Is.Null, "第一章全链完成：无当前任务");
            Assert.That(saved[0].CompletedCount, Is.EqualTo(2));
            Assert.That(saved[1].CurrentQuestId, Is.EqualTo("s2"), "第二章停在 s2");

            // 模拟重启：全新加载同两章 → 恢复 → 状态接续
            string ch1Path = WriteChapter(ChapterOneJson);
            string ch2Path = WriteChapter(ChapterTwoJson);
            var restored = QuestCampaign.Load(ch1Path, ch2Path);
            restored.RestoreAll(saved);
            Assert.That(restored.ActiveChapterIndex, Is.EqualTo(1), "第一章已完成：活动章应落在第二章");
            Assert.That(restored.Active.Current.Id, Is.EqualTo("s2"), "第二章进度接续（不是从头 s1）");
            Assert.That(restored.IsChapterComplete(0), Is.True, "第一章完成态接续");
        }

        [Test]
        public void RestoreAll_null或短列表_缺的章节全新开始()
        {
            QuestCampaign campaign = BindTwoChapters();

            // null（更老的档）：整本全新
            campaign.RestoreAll(null);
            Assert.That(campaign.ActiveChapterIndex, Is.EqualTo(0));
            Assert.That(campaign.Active.Current.Id, Is.EqualTo("q1"));

            // 只有第一章记录的短列表（旧档没有第二章字段的形态）：
            // 第一章已完成 → 活动章直接落在全新的第二章
            campaign.RestoreAll(new List<QuestState>
            {
                new QuestState { CurrentQuestId = null, CompletedCount = 2, Progress = 0 },
            });
            Assert.That(campaign.ActiveChapterIndex, Is.EqualTo(1), "缺第二章记录 = 第二章全新，但第一章完成已解锁它");
            Assert.That(campaign.Active.Current.Id, Is.EqualTo("s1"));
            Assert.That(campaign.Active.CompletedCount, Is.EqualTo(0), "第二章全新开始");
        }

        [Test]
        public void RestoreAll_坏章id只跳过该章_其余章照常()
        {
            QuestCampaign campaign = BindTwoChapters();

            // 第一章快照里的任务 id 不属于本章（换章内容读旧档）→ 该章全新，第二章照常恢复
            campaign.RestoreAll(new List<QuestState>
            {
                new QuestState { CurrentQuestId = "不存在的任务", CompletedCount = 1, Progress = 0 },
                new QuestState { CurrentQuestId = "s2", CompletedCount = 1, Progress = 0 },
            });

            Assert.That(campaign.Active.Current.Id, Is.EqualTo("q1"), "坏掉的第一章全新开始（活动章回到第一章）");
            Assert.That(campaign.Active.CompletedCount, Is.EqualTo(0));
            Assert.That(campaign.Chapters[1].Current.Id, Is.EqualTo("s2"), "第二章照常接续，不被第一章拖垮");
        }

        [Test]
        public void RestoreLegacy_旧档单章字段_第一章接续_第二章全新()
        {
            QuestCampaign campaign = BindTwoChapters();

            campaign.RestoreLegacy(new QuestState { CurrentQuestId = "q2", CompletedCount = 1, Progress = 2 });

            Assert.That(campaign.ActiveChapterIndex, Is.EqualTo(0), "旧档第一章进行中：活动章仍是第一章");
            Assert.That(campaign.Active.Current.Id, Is.EqualTo("q2"), "第一章进度接续");
            Assert.That(campaign.Active.CurrentProgress, Is.EqualTo(2), "进度分子接续");
            Assert.That(campaign.Chapters[1].Current.Id, Is.EqualTo("s1"), "旧档没有第二章字段：全新开始");
        }

        [Test]
        public void RestoreLegacy_旧档第一章已完成_直接解锁第二章()
        {
            QuestCampaign campaign = BindTwoChapters();

            // m6 时代档的「全链完成」形态：CurrentQuestId 空 + CompletedCount = 链长
            campaign.RestoreLegacy(new QuestState { CurrentQuestId = null, CompletedCount = 2, Progress = 0 });

            Assert.That(campaign.ActiveChapterIndex, Is.EqualTo(1), "第一章完成 → 读档直接落在第二章");
            Assert.That(campaign.Active.Current.Id, Is.EqualTo("s1"), "第二章首任务就位");
            Assert.That(campaign.IsChapterComplete(0), Is.True);
        }

        [Test]
        public void 构造_空任务书或含null章_抛ArgumentException()
        {
            Assert.That(() => new QuestCampaign(new QuestSystem[0]),
                Throws.TypeOf<System.ArgumentException>(), "零章不许装订");
            Assert.That(() => new QuestCampaign(new QuestSystem[] { null }),
                Throws.TypeOf<System.ArgumentException>(), "null 章不许装订");
        }

        [Test]
        public void 真实chapter1与chapter2_装订两章_顺序解锁全程事件驱动()
        {
            string ch1 = ChapterPath("chapter1.json");
            string ch2 = ChapterPath("chapter2.json");
            Assume.That(File.Exists(ch1) && File.Exists(ch2), Is.True, "章节文件应存在（仓库内数据）");

            QuestCampaign campaign = QuestCampaign.Load(ch1, ch2);
            Assert.That(campaign.ChapterCount, Is.EqualTo(2));
            Assert.That(campaign.Active.Current.Id, Is.EqualTo("ch1_01_punch_log"), "活动章是第一章");

            int completedQuests = 0;
            int chapterFinishes = 0;
            while (campaign.Active.Current != null)
            {
                // 先取住任务 id：完成末章末任务后 Active.Current 变 null，断言消息不能现读
                string questId = campaign.Active.Current.Id;
                QuestCondition condition = campaign.Active.Current.Condition;
                bool done = campaign.TryComplete(MakeSatisfyingEvent(condition), out _, out _, out bool finished);
                Assert.That(done, Is.True, $"按条件构造的事件应能完成 {questId}");
                completedQuests++;
                if (finished) chapterFinishes++;
            }
            Assert.That(completedQuests, Is.EqualTo(16), "两章共 8+8 个任务全部事件驱动走完");
            Assert.That(chapterFinishes, Is.EqualTo(2), "两章各报告一次章节完成");
            Assert.That(campaign.ActiveChapterIndex, Is.EqualTo(1), "全部完成后停在末章");
        }

        /// <summary>定位真实章节文件（dotnet 链向上找仓库根；EditMode 用 streamingAssetsPath）。</summary>
        private static string ChapterPath(string fileName)
        {
#if UNITY_EDITOR
            return Path.Combine(UnityEngine.Application.streamingAssetsPath, "quests", fileName);
#else
            var directory = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Assets", "StreamingAssets", "quests", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            throw new FileNotFoundException($"未能找到 Assets/StreamingAssets/quests/{fileName}。");
#endif
        }
    }
}
