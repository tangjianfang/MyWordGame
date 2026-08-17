using MyWorld.Core.Entities;

namespace MyWorld.Core.Quests
{
    /// <summary>
    /// 任务系统的事件类型。C2 的 <c>QuestEventBus</c> 把游戏内动作翻译成这几类事件喂给
    /// <see cref="QuestSystem"/>：拾取/挖到（ObtainItem）、合成产出（CraftItem）、
    /// 熔炉取出（SmeltItem）、跨过日出（SurviveNight）。
    /// <para>
    /// m11 W2-4 起第二章追加 8 类（4-11）：睡觉（SleepInBed）、锄地（TillSoil）、
    /// 播种（SowSeed）、收获（HarvestCrop）、喂动物（FeedAnimal）、穿齐盔甲
    /// （EquipArmorFull）、附魔（EnchantItem）、击杀指定生物（KillKind）。
    /// </para>
    /// </summary>
    public enum QuestEventType
    {
        /// <summary>获得物品。事件里的 <see cref="QuestEvent.Count"/> 语义是「背包现存量」，见 <see cref="QuestCondition"/>。</summary>
        ObtainItem = 0,
        /// <summary>合成产出物品。<see cref="QuestEvent.Count"/> 为本次产出数量，任务内**累计**判定。</summary>
        CraftItem = 1,
        /// <summary>熔炉烧炼产出物品。<see cref="QuestEvent.Count"/> 为本次产出数量，任务内**累计**判定。</summary>
        SmeltItem = 2,
        /// <summary>跨过一次日出。无条件参数，事件到达即算。</summary>
        SurviveNight = 3,
        /// <summary>夜里在床上睡觉（时间直接跳到早晨）。无条件参数，事件到达即算（m11 W2-4）。</summary>
        SleepInBed = 4,
        /// <summary>锄头把草/泥翻成耕地。无条件参数，事件到达即算（m11 W2-4）。</summary>
        TillSoil = 5,
        /// <summary>往耕地里播种。<see cref="QuestEvent.ItemId"/> 是种子物品 id，Count 为本次播种数，任务内**累计**判定（m11 W2-4）。</summary>
        SowSeed = 6,
        /// <summary>收获一株成熟作物。不区分作物种类（ItemId 恒 0），Count 为本次收获株数，任务内**累计**判定（m11 W2-4）。</summary>
        HarvestCrop = 7,
        /// <summary>喂了一只动物。Count 为本次喂食只数，任务内**累计**判定；
        /// <see cref="QuestEvent.Kind"/> 带被喂的生物（条件可选用 kind 限定只认某种，m11 W2-4）。</summary>
        FeedAnimal = 8,
        /// <summary>四件盔甲全部穿齐（同一材料成套）。无条件参数，事件到达即算（m11 W2-4）。</summary>
        EquipArmorFull = 9,
        /// <summary>完成了一次附魔。无条件参数，事件到达即算（m11 W2-4）。</summary>
        EnchantItem = 10,
        /// <summary>击杀指定生物。<see cref="QuestEvent.Kind"/> 必带（条件里 kind 必填），
        /// <see cref="QuestEvent.Weapon"/> 非空表示限定武器（当前只有 "bow"=箭）；Count 为本次击杀数，任务内**累计**判定（m11 W2-4）。</summary>
        KillKind = 11,
    }

    /// <summary>
    /// 一次任务事件。游戏逻辑不感知任务系统，只往总线丢这个结构；
    /// SurviveNight 等无条件类型的事件不带条件参数，<see cref="ItemId"/>/<see cref="Count"/> 保持默认 0。
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
        /// <summary>
        /// 事件涉及的生物（m11 W2-4）：KillKind 必带击杀对象的 <see cref="MobKind"/>、
        /// FeedAnimal 带被喂的动物。不涉及生物的事件保持 null；条件侧
        /// <see cref="QuestCondition.Kind"/> 为 null 表示不限生物。
        /// </summary>
        public MobKind? Kind;
        /// <summary>
        /// 事件涉及的武器 itemId（m11 W2-4）：目前只有 "bow"（箭击杀，由 DamageSource.Projectile
        /// 翻译而来）；近战击杀不带武器（null = 任意武器都算）。条件侧
        /// <see cref="QuestCondition.Weapon"/> 为空表示不限武器。
        /// </summary>
        public string Weapon;
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
        /// <summary>夜里在床上睡过一次，无条件参数（m11 W2-4）。</summary>
        SleepInBed = 4,
        /// <summary>锄地累计 N 次，无条件参数（m11 W2-4）。</summary>
        TillSoil = 5,
        /// <summary>累计播下 N 粒指定种子（m11 W2-4）。</summary>
        SowSeed = 6,
        /// <summary>累计收获 N 株作物，不限种类（m11 W2-4）。</summary>
        HarvestCrop = 7,
        /// <summary>累计喂 N 只动物，可选用 <see cref="QuestCondition.Kind"/> 限定物种（m11 W2-4）。</summary>
        FeedAnimal = 8,
        /// <summary>四件盔甲穿齐一次，无条件参数（m11 W2-4）。</summary>
        EquipArmorFull = 9,
        /// <summary>完成一次附魔，无条件参数（m11 W2-4）。</summary>
        EnchantItem = 10,
        /// <summary>累计击杀 N 只指定生物，<see cref="QuestCondition.Kind"/> 必填、
        /// <see cref="QuestCondition.Weapon"/> 可选限定武器（m11 W2-4）。</summary>
        KillKind = 11,
    }

    /// <summary>
    /// 单个任务的完成条件。物品类条件要求事件的类型、物品 id 匹配；数量口径分两类
    /// （fix1 起与文档对齐）：<b>ObtainItem</b> 单笔比较（事件 Count 是背包现存量）、
    /// <b>CraftItem/SmeltItem/SowSeed/HarvestCrop/FeedAnimal/KillKind</b> 任务内累计
    /// （事件 Count 是本次数量，累计值 ≥ <see cref="RequiredCount"/> 才完成——
    /// 「需求量 &gt; 单次批量」的任务分多笔凑满）。
    /// <see cref="ConditionType.SurviveNight"/>/<b>SleepInBed</b>/<b>TillSoil</b>/
    /// <b>EquipArmorFull</b>/<b>EnchantItem</b> 只看事件类型，<see cref="ItemId"/> 固定 0、
    /// <see cref="RequiredCount"/> 固定 1（供 HUD 显示 0/1 进度，不参与判定）。
    /// </summary>
    public sealed class QuestCondition
    {
        public ConditionType Type { get; set; }
        public int ItemId { get; set; }
        public int RequiredCount { get; set; }
        /// <summary>
        /// 生物限定（m11 W2-4）：KillKind 必填（加载时校验）、FeedAnimal 可选、其余类型忽略。
        /// null 表示不限生物；事件侧 <see cref="QuestEvent.Kind"/> 为 null 时不匹配任何非空限定。
        /// </summary>
        public MobKind? Kind { get; set; }
        /// <summary>
        /// 武器限定（m11 W2-4）：仅 KillKind 使用（如 "bow"=必须箭击杀），其余类型忽略。
        /// null/空 表示不限武器。
        /// </summary>
        public string Weapon { get; set; }
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
        /// <summary>
        /// 当前任务内累计的进度分子（fix1 新增）：CraftItem/SmeltItem 是任务激活以来匹配事件的
        /// Count 累计，ObtainItem 是最近一次匹配事件携带的背包现存量。
        /// 旧档缺此字段 = 0（Newtonsoft 默认值，自然兼容——最多把一个未完成任务的进度归零重攒）。
        /// </summary>
        public int Progress;
    }
}
