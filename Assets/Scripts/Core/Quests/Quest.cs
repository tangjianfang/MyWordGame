namespace MyWorld.Core.Quests
{
    /// <summary>
    /// 任务系统的事件类型。C2 的 <c>QuestEventBus</c> 把游戏内动作翻译成这几类事件喂给
    /// <see cref="QuestSystem"/>：拾取/挖到（ObtainItem）、合成产出（CraftItem）、
    /// 熔炉取出（SmeltItem）、跨过日出（SurviveNight）。
    /// </summary>
    public enum QuestEventType
    {
        /// <summary>获得物品。事件里的 <see cref="QuestEvent.Count"/> 语义是「背包现存量」，见 <see cref="QuestCondition"/>。</summary>
        ObtainItem = 0,
        /// <summary>合成产出物品。<see cref="QuestEvent.Count"/> 为本次产出数量。</summary>
        CraftItem = 1,
        /// <summary>熔炉烧炼产出物品。<see cref="QuestEvent.Count"/> 为本次产出数量。</summary>
        SmeltItem = 2,
        /// <summary>跨过一次日出。无条件参数，事件到达即算。</summary>
        SurviveNight = 3,
    }

    /// <summary>
    /// 一次任务事件。游戏逻辑不感知任务系统，只往总线丢这个结构；
    /// SurviveNight 事件不带条件参数，<see cref="ItemId"/>/<see cref="Count"/> 保持默认 0。
    /// <para>
    /// 注意是普通可变 struct 而非 <c>readonly struct</c>：调用方统一用
    /// <c>new QuestEvent { Type = ..., ItemId = ..., Count = ... }</c> 对象初始化器构造，
    /// readonly 字段不允许这样赋值（CS0191）。按值传递，可变性不会外泄。
    /// </para>
    /// </summary>
    public struct QuestEvent
    {
        public QuestEventType Type;
        public int ItemId;
        public int Count;
    }

    /// <summary>
    /// 任务完成条件的类型。与 <see cref="QuestEventType"/> 成员一一对应（同名成员底层值相同），
    /// <see cref="QuestSystem"/> 判定时直接显式转换比较。
    /// </summary>
    public enum ConditionType
    {
        /// <summary>背包拥有 N 个 X。事件到达时「背包现存量 ≥ RequiredCount」即完成——Core 不持有背包，只比较事件里的 Count。</summary>
        ObtainItem = 0,
        /// <summary>累计合成产出过足够的 X。</summary>
        CraftItem = 1,
        /// <summary>累计烧炼产出过足够的 X。</summary>
        SmeltItem = 2,
        /// <summary>跨过一次日出，无条件参数。</summary>
        SurviveNight = 3,
    }

    /// <summary>
    /// 单个任务的完成条件。物品类条件要求事件的类型、物品 id、数量三者都匹配；
    /// <see cref="ConditionType.SurviveNight"/> 只看事件类型，<see cref="ItemId"/> 固定 0、
    /// <see cref="RequiredCount"/> 固定 1（供 HUD 显示 0/1 进度，不参与判定）。
    /// </summary>
    public sealed class QuestCondition
    {
        public ConditionType Type { get; set; }
        public int ItemId { get; set; }
        public int RequiredCount { get; set; }
    }

    /// <summary>
    /// 单个任务：id / 名称 / 描述 / 完成条件 / 奖励经验。纯数据，从
    /// <c>Assets/StreamingAssets/quests/chapter1.json</c> 加载（schema 见同目录 _format.md）。
    /// </summary>
    public sealed class Quest
    {
        /// <summary>任务唯一标识。存档里记的是它（<see cref="QuestState.CurrentQuestId"/>），定了就别改。</summary>
        public string Id { get; set; }
        /// <summary>HUD 显示的任务名（中文）。</summary>
        public string Name { get; set; }
        /// <summary>给玩家的操作指引（中文）。</summary>
        public string Desc { get; set; }
        /// <summary>完成条件。</summary>
        public QuestCondition Condition { get; set; }
        /// <summary>完成时入账的经验值，由 Unity 侧 Experience 系统消费。</summary>
        public int RewardExp { get; set; }
    }

    /// <summary>
    /// 任务链存档快照（m6 C4 进 level.dat 新字段）。
    /// <see cref="CurrentQuestId"/> 为 null 表示「全新开始」或「全链完成」，由
    /// <see cref="QuestSystem.Restore"/> 按 <see cref="CompletedCount"/> 区分。
    /// 旧档缺此字段时 Newtonsoft 反序列化得 null，调用方跳过恢复即全新开始。
    /// </summary>
    public sealed class QuestState
    {
        public string CurrentQuestId;
        public int CompletedCount;
    }
}
