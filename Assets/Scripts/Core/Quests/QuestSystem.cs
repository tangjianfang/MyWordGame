using System;
using System.Collections.Generic;

namespace MyWorld.Core.Quests
{
    /// <summary>
    /// 引导任务链状态机（m6 C1）。链式解锁：同一时刻只有 <see cref="Current"/> 一个任务，
    /// <see cref="TryComplete"/> 只对它判定条件，命中即完成并解锁下一个；全链完成后
    /// <see cref="Current"/> 为 null，再来事件安全返回 false。
    /// <para>
    /// Core 不持有背包、不感知 Unity：<see cref="QuestEvent.Count"/> 由 C2 的事件接线赋值
    /// （ObtainItem 语义是「背包现存量」，CraftItem/SmeltItem 是「本次产出数量」），本类只做比较。
    /// </para>
    /// <para>
    /// 存档走 <see cref="SaveState"/>/<see cref="Restore"/>（C4 挂进 level.dat）；
    /// 旧档无 QuestState 字段时 Newtonsoft 反序列化得 null，<see cref="Restore"/> 对 null
    /// 直接跳过 = 全新开始任务链。
    /// </para>
    /// </summary>
    public sealed class QuestSystem
    {
        private readonly Quest[] _chain;
        /// <summary>当前任务下标；等于 <c>_chain.Length</c> 表示全链完成。</summary>
        private int _index;

        private QuestSystem(Quest[] chain)
        {
            _chain = chain;
        }

        /// <summary>当前任务；全链完成后为 null。</summary>
        public Quest Current => _index < _chain.Length ? _chain[_index] : null;

        /// <summary>已完成的任务数（0..链长）。</summary>
        public int CompletedCount { get; private set; }

        /// <summary>本章任务总数（供 C3 HUD「x/8」进度显示）。</summary>
        public int TotalCount => _chain.Length;

        /// <summary>本章全部任务（顺序即解锁顺序；供 C3 帮助菜单画全链 8 格图标）。不暴露可变引用——数组本身不写。</summary>
        public IReadOnlyList<Quest> Quests => _chain;

        /// <summary>
        /// 从章节 JSON 文件加载任务链。文件不存在抛 <see cref="System.IO.FileNotFoundException"/>；
        /// 坏 JSON / id 重复 / condition 非法等数据问题抛异常（写严格，校验细节见 <see cref="QuestChainLoader"/>）。
        /// </summary>
        public static QuestSystem LoadChapter(string jsonPath)
        {
            return new QuestSystem(QuestChainLoader.Load(jsonPath));
        }

        /// <summary>
        /// 用一个事件尝试推进当前任务。返回 true 当且仅当这次事件使当前任务完成
        /// （随后 <see cref="Current"/> 已指向下一个任务或为 null）。
        /// <para>
        /// 判定规则：物品类条件（ObtainItem/CraftItem/SmeltItem）要求事件类型、物品 id
        /// 都相同且 <see cref="QuestEvent.Count"/> ≥ <see cref="QuestCondition.RequiredCount"/>；
        /// SurviveNight 条件只看事件类型，无条件参数直接完成。
        /// 当前为 null（全链完成）或条件不满足时返回 false，不抛异常。
        /// </para>
        /// </summary>
        public bool TryComplete(QuestEvent evt)
        {
            Quest current = Current;
            if (current == null)
            {
                return false;
            }
            if (!Matches(current.Condition, evt))
            {
                return false;
            }

            CompletedCount++;
            _index++;
            return true;
        }

        /// <summary>导出存档快照：当前任务 id（全链完成为 null）+ 完成计数。</summary>
        public QuestState SaveState()
        {
            return new QuestState
            {
                CurrentQuestId = Current?.Id,
                CompletedCount = CompletedCount,
            };
        }

        /// <summary>
        /// 从存档快照恢复进度（C4 在读档链路调用）。
        /// <paramref name="state"/> 为 null 时跳过（旧档兼容 = 全新开始）；
        /// <see cref="QuestState.CurrentQuestId"/> 为 null/空时按完成计数区分全新开始（0）与全链完成（= 链长）；
        /// 任务 id 不属于本章任务链抛 <see cref="ArgumentException"/>（写严格，调用方按层捕获跳过）。
        /// </summary>
        public void Restore(QuestState state)
        {
            if (state == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(state.CurrentQuestId))
            {
                // 无当前任务：完成计数打到链长 = 全链完成态，否则视为全新开始
                if (state.CompletedCount >= _chain.Length)
                {
                    _index = _chain.Length;
                    CompletedCount = _chain.Length;
                }
                else
                {
                    _index = 0;
                    CompletedCount = 0;
                }
                return;
            }

            for (int i = 0; i < _chain.Length; i++)
            {
                if (_chain[i].Id == state.CurrentQuestId)
                {
                    _index = i;
                    // 完成计数夹回 [0, 链长]，坏档不至于把 HUD 的 x/8 挤爆
                    // 注意写全 System.Math——Core 里有个 MyWorld.Core.Math 命名空间会抢解析
                    CompletedCount = System.Math.Max(0, System.Math.Min(state.CompletedCount, _chain.Length));
                    return;
                }
            }
            throw new ArgumentException(
                $"存档里的任务 id 不属于本章任务链：{state.CurrentQuestId}（链内共 {_chain.Length} 个任务）");
        }

        private static bool Matches(QuestCondition condition, QuestEvent evt)
        {
            if (condition.Type == ConditionType.SurviveNight)
            {
                // SurviveNight 无条件参数：事件到达即满足
                return evt.Type == QuestEventType.SurviveNight;
            }

            // ConditionType 与 QuestEventType 同名成员底层值相同（见 Quest.cs 注释），显式转换安全
            return evt.Type == (QuestEventType)condition.Type
                && evt.ItemId == condition.ItemId
                && evt.Count >= condition.RequiredCount;
        }
    }
}
