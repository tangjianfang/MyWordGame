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
        /// <summary>
        /// 当前任务条件的进度分子（fix1 起累计在 Core，Unity 侧 HUD 只读不重复计账）。
        /// 口径随条件类型：ObtainItem = 最近一次匹配事件携带的背包现存量（覆盖不累计）；
        /// CraftItem/SmeltItem = 任务激活以来匹配事件 Count 的累计；SurviveNight 恒 0。
        /// 任务完成切换 / Restore 时重置。
        /// </summary>
        private int _progress;

        private QuestSystem(Quest[] chain)
        {
            _chain = chain;
        }

        /// <summary>当前任务；全链完成后为 null。</summary>
        public Quest Current => _index < _chain.Length ? _chain[_index] : null;

        /// <summary>已完成的任务数（0..链长）。</summary>
        public int CompletedCount { get; private set; }

        /// <summary>
        /// 当前任务的进度分子（HUD 的 n/m 取 n 的**单一真源**）。任务未完成期间恒 ≤
        /// <see cref="QuestCondition.RequiredCount"/>（完成瞬间清零切任务，超界的累计值不外泄）。
        /// 全链完成后为 0。
        /// </summary>
        public int CurrentProgress => _progress;

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
        /// 判定规则（fix1 起两类口径分开）：
        /// <b>ObtainItem</b>——事件类型、物品 id 相同且事件的 <see cref="QuestEvent.Count"/>
        /// （背包现存量）≥ <see cref="QuestCondition.RequiredCount"/>，单笔比较；
        /// <b>CraftItem/SmeltItem</b>——事件类型、物品 id 相同即先累计
        /// （<see cref="QuestEvent.Count"/> 是本次产出数量），累计值 ≥ Required 才完成
        /// （「需求量 &gt; 单次批量」的任务分多笔凑满）；
        /// <b>SurviveNight</b>——只看事件类型，无条件参数直接完成。
        /// 类型/物品不匹配的事件不计进度也不判定。当前为 null（全链完成）或条件不满足时
        /// 返回 false，不抛异常。
        /// </para>
        /// </summary>
        public bool TryComplete(QuestEvent evt)
        {
            Quest current = Current;
            if (current == null)
            {
                return false;
            }
            if (!TrackAndCheck(current.Condition, evt))
            {
                return false;
            }

            CompletedCount++;
            _index++;
            _progress = 0; // 切换任务：分子重新累计
            return true;
        }

        /// <summary>
        /// 用一次事件更新当前条件的进度分子并判定是否达成。不匹配的事件原样返回 false、不动进度。
        /// </summary>
        private bool TrackAndCheck(QuestCondition condition, QuestEvent evt)
        {
            // 无条件参数组（SurviveNight + m11 W2-4 的睡觉/锄地/穿甲/附魔）：事件类型到达即满足，无中间进度
            if (condition.Type == ConditionType.SurviveNight || condition.Type == ConditionType.SleepInBed
                || condition.Type == ConditionType.TillSoil || condition.Type == ConditionType.EquipArmorFull
                || condition.Type == ConditionType.EnchantItem)
            {
                return evt.Type == (QuestEventType)condition.Type;
            }

            // 生物限定组（KillKind/FeedAnimal，m11 W2-4）：类型 + 可选 kind/weapon 匹配后累计 Count。
            // KillKind 的 kind 在加载侧必填；FeedAnimal 可空 = 不限物种。事件没带 Kind 时
            // 只匹配「不限物种」的条件（带限定的一律不认——近战击杀不会冒充「用弓击杀」）。
            if (condition.Type == ConditionType.KillKind || condition.Type == ConditionType.FeedAnimal)
            {
                if (evt.Type != (QuestEventType)condition.Type)
                {
                    return false;
                }
                if (condition.Kind.HasValue && evt.Kind != condition.Kind)
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(condition.Weapon) && evt.Weapon != condition.Weapon)
                {
                    return false;
                }
                _progress += evt.Count;
                return _progress >= condition.RequiredCount;
            }

            // ConditionType 与 QuestEventType 同名成员底层值相同（见 Quest.cs 注释），显式转换安全
            if (evt.Type != (QuestEventType)condition.Type || evt.ItemId != condition.ItemId)
            {
                return false;
            }

            if (condition.Type == ConditionType.ObtainItem)
            {
                // 现存量口径：事件 Count 就是背包现存量，覆盖式更新 + 单笔比较
                // （掉物品后现存量回落也如实反映，所以不累计）
                _progress = evt.Count;
                return evt.Count >= condition.RequiredCount;
            }

            // 产出口径（CraftItem/SmeltItem/SowSeed/HarvestCrop）：事件 Count 是本次数量，任务内累计后比较——
            // 单笔永远凑不满的任务（炼 3 根铁锭、一次只取 1）靠多笔累计完成
            _progress += evt.Count;
            return _progress >= condition.RequiredCount;
        }

        /// <summary>导出存档快照：当前任务 id（全链完成为 null）+ 完成计数 + 当前进度分子。</summary>
        public QuestState SaveState()
        {
            return new QuestState
            {
                CurrentQuestId = Current?.Id,
                CompletedCount = CompletedCount,
                Progress = _progress,
            };
        }

        /// <summary>
        /// 从存档快照恢复进度（C4 在读档链路调用）。
        /// <paramref name="state"/> 为 null 时跳过（旧档兼容 = 全新开始）；
        /// <see cref="QuestState.CurrentQuestId"/> 为 null/空时按完成计数区分全新开始（0）与全链完成（= 链长）；
        /// 任务 id 不属于本章任务链抛 <see cref="ArgumentException"/>（写严格，调用方按层捕获跳过）。
        /// 累计进度取 <see cref="QuestState.Progress"/>（旧档缺字段 = 0，自然兼容）并夹非负。
        /// </summary>
        public void Restore(QuestState state)
        {
            if (state == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(state.CurrentQuestId))
            {
                // 无当前任务：完成计数打到链长 = 全链完成态，否则视为全新开始；两种都无进度可言
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
                _progress = 0;
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
                    // 进度分子夹非负（负值坏档不合法但也不值得炸读档链路）
                    _progress = System.Math.Max(0, state.Progress);
                    return;
                }
            }
            throw new ArgumentException(
                $"存档里的任务 id 不属于本章任务链：{state.CurrentQuestId}（链内共 {_chain.Length} 个任务）");
        }
    }
}
