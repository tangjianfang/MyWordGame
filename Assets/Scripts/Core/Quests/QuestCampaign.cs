using System;
using System.Collections.Generic;

namespace MyWorld.Core.Quests
{
    /// <summary>
    /// 多章节任务书（m11 W2-4）：把若干章 <see cref="QuestSystem"/> 按顺序串成一本，
    /// **章节顺序解锁**——任意时刻只有 <see cref="Active"/> 一章在判定事件，
    /// 本章全链完成后自动把活动章推进到下一章（下一章此前收到再多事件也不算数）。
    /// <para>
    /// Core 不感知 Unity：章节文件由调用方按顺序喂入（Unity 侧是
    /// <c>QuestEventBus</c>）。存档走 <see cref="SaveAll"/>/<see cref="RestoreAll"/>
    /// （每章一个 <see cref="QuestState"/>，进 level.dat 的 QuestChapters 字段）；
    /// 旧档只有单章 Quest 字段时用 <see cref="RestoreLegacy"/>——第一章恢复进度、
    /// 其余章节全新开始，若旧档第一章已全链完成则直接解锁第二章。
    /// </para>
    /// </summary>
    public sealed class QuestCampaign
    {
        private readonly QuestSystem[] _chapters;

        /// <summary>
        /// 当前活动章下标（0 起）。章节完成即前移；全部完成时停在最后一章
        /// （<see cref="Active"/>.Current 为 null，HUD 显示全章完成）。
        /// </summary>
        public int ActiveChapterIndex { get; private set; }

        /// <summary>当前活动章（永不返回 null——构造保证至少一章）。</summary>
        public QuestSystem Active => _chapters[ActiveChapterIndex];

        /// <summary>章节总数（≥ 1）。</summary>
        public int ChapterCount => _chapters.Length;

        /// <summary>全部章节（顺序即解锁顺序；供存档/HUD 遍历。不暴露可变引用）。</summary>
        public IReadOnlyList<QuestSystem> Chapters => _chapters;

        /// <summary>
        /// 按顺序装订一本任务书。写严格：章节列表为 null/空、或含 null 元素抛
        /// <see cref="ArgumentException"/>。
        /// </summary>
        public QuestCampaign(IReadOnlyList<QuestSystem> chapters)
        {
            if (chapters == null || chapters.Count == 0)
            {
                throw new ArgumentException("任务书至少要有一章", nameof(chapters));
            }
            _chapters = new QuestSystem[chapters.Count];
            for (int i = 0; i < chapters.Count; i++)
            {
                if (chapters[i] == null)
                {
                    throw new ArgumentException($"第 {i} 章是 null", nameof(chapters));
                }
                _chapters[i] = chapters[i];
            }
        }

        /// <summary>按文件顺序加载各章（每章一个 quests/*.json）。文件缺失/坏数据由
        /// <see cref="QuestSystem.LoadChapter"/> 抛异常（写严格）。</summary>
        public static QuestCampaign Load(params string[] chapterPaths)
        {
            if (chapterPaths == null || chapterPaths.Length == 0)
            {
                throw new ArgumentException("任务书至少要有一章文件", nameof(chapterPaths));
            }
            var systems = new QuestSystem[chapterPaths.Length];
            for (int i = 0; i < chapterPaths.Length; i++)
            {
                systems[i] = QuestSystem.LoadChapter(chapterPaths[i]);
            }
            return new QuestCampaign(systems);
        }

        /// <summary>某章是否已全链完成（完成计数打到链长）。</summary>
        public bool IsChapterComplete(int index)
            => index >= 0 && index < _chapters.Length
               && _chapters[index].CompletedCount >= _chapters[index].TotalCount;

        /// <summary>
        /// 用一个事件尝试推进<b>当前活动章</b>的当前任务（顺序解锁：非活动章不判定，
        /// 后续章节的事件在前章未完成时全部无效）。返回 true 当且仅当这次事件完成了
        /// 某个任务，并经 out 参数带出完成的任务、所在章下标、以及该章是否就此走完。
        /// 章走完且还有后续 → 活动章自动前移（下一章解锁）；没有后续则停在末章。
        /// </summary>
        public bool TryComplete(QuestEvent evt, out Quest completed, out int completedChapterIndex, out bool chapterFinished)
        {
            completed = null;
            completedChapterIndex = -1;
            chapterFinished = false;

            QuestSystem active = Active;
            Quest current = active.Current;
            if (current == null)
            {
                return false; // 全部章节已完成：安全 no-op
            }
            if (!active.TryComplete(evt))
            {
                return false;
            }

            completed = current;
            completedChapterIndex = ActiveChapterIndex;
            if (active.Current == null)
            {
                chapterFinished = true;
                if (ActiveChapterIndex < _chapters.Length - 1)
                {
                    ActiveChapterIndex++; // 本章走完：解锁下一章（无下一章则留在末章展示完成态）
                }
            }
            return true;
        }

        /// <summary>
        /// 导出存档快照：每章一个 <see cref="QuestState"/>（顺序与章节一致）。
        /// level.dat 的 QuestChapters 字段按此列表往返。
        /// </summary>
        public List<QuestState> SaveAll()
        {
            var states = new List<QuestState>(_chapters.Length);
            foreach (QuestSystem chapter in _chapters)
            {
                states.Add(chapter.SaveState());
            }
            return states;
        }

        /// <summary>
        /// 从存档快照逐章恢复（读容忍按层降级）：
        /// <paramref name="states"/> 为 null → 全部章节全新开始；
        /// 比 <see cref="ChapterCount"/> 短 → 缺的章节全新开始（旧档没有第二章字段的形态）；
        /// 比章节数长 → 多余条目忽略；某章的
        /// <see cref="QuestState.CurrentQuestId"/> 不属于该章（换章内容读旧档）时该章
        /// 单独跳过按全新开始，不拖垮其余章。恢复完按「第一个未完成的章」重算活动章
        /// ——前章已完成的旧档直接落在下一章。
        /// </summary>
        public void RestoreAll(IReadOnlyList<QuestState> states)
        {
            if (states != null)
            {
                for (int i = 0; i < _chapters.Length && i < states.Count; i++)
                {
                    QuestState state = states[i];
                    if (state == null)
                    {
                        continue; // 该章无记录 = 全新开始
                    }
                    try
                    {
                        _chapters[i].Restore(state);
                    }
                    catch (ArgumentException)
                    {
                        // 任务 id 不属于本章（章节内容改版）：该章全新开始，其余章照常
                    }
                }
            }
            RecomputeActiveChapter();
        }

        /// <summary>
        /// 旧档兼容（m6 时代的单章 Quest 字段）：state 只恢复进第一章，其余章节全新开始。
        /// state 为 null（更老的档 / 无链）= 整本全新开始。
        /// 第一章已全链完成的旧档 → 重算后活动章直接落在第二章。
        /// </summary>
        public void RestoreLegacy(QuestState state)
        {
            RestoreAll(state == null ? null : new[] { state });
        }

        /// <summary>
        /// 按恢复出的各章进度重算活动章：第一个「未全链完成」的章；
        /// 全部完成则停在末章（Active.Current 为 null，HUD 显示完成态）。
        /// </summary>
        private void RecomputeActiveChapter()
        {
            for (int i = 0; i < _chapters.Length; i++)
            {
                if (!IsChapterComplete(i))
                {
                    ActiveChapterIndex = i;
                    return;
                }
            }
            ActiveChapterIndex = _chapters.Length - 1;
        }
    }
}
