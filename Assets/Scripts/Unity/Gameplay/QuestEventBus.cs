using MyWorld.Core.Entities;
using MyWorld.Core.Quests;
using MyWorld.Core.Time;
using UnityEngine;

namespace MyWorld.Unity.Gameplay
{
    /// <summary>
    /// 任务事件总线（m6 C2）：游戏内动作 → <see cref="QuestEvent"/> → 任务系统的
    /// 唯一转发点。游戏逻辑不感知任务系统，只在产出 / 拾取 / 取料 / 跨夜的缝里
    /// <c>QuestEventBus.Instance?.Raise(new QuestEvent{...})</c> 一行了事——
    /// Instance 为 null（早期场景 / 纯逻辑测试没有总线）时 <c>?.</c> 天然安全。
    /// <para>
    /// 职责刻意保持「薄」：转发事件；当前任务命中完成的瞬间先经
    /// <see cref="PlayerContext.Experience"/>.Add 入账奖励经验，再触发
    /// <see cref="OnQuestCompleted"/> 钩子（C3 的 QuestHudUi 订阅它弹「完成」提示）。
    /// 条件判定 / 链推进 / 进度计账全在 Core（单一真源），本类不重复任何比较逻辑。
    /// </para>
    /// <para>
    /// m11 W2-4 起持**多章节任务书** <see cref="Campaign"/>（章节顺序解锁：一章
    /// 完成才开下一章，完成瞬间触发 <see cref="OnChapterCompleted"/> 供 UI 弹开章提示）；
    /// <see cref="Quests"/> 始终指向**当前活动章**，旧消费方（HUD / 存档 / 帮助菜单）
    /// 不改一行照常工作。此外总线侧自行观察两类玩法状态（不进玩法文件）：
    /// ①时间从夜里被拉回早晨（Core 床系统 BedSystem.Sleep 把时刻置 0 的唯一外部可见副作用）→ SleepInBed；
    /// ②订阅 <see cref="CombatEvents.OnEntityDied"/>（玩家近战击杀的既有事件出口）→ KillKind
    /// （生物种类经 <see cref="MyWorld.Unity.Combat.MobManager"/> 反查，武器按伤害来源
    /// Projectile=弓判定）。
    /// </para>
    /// <para>
    /// 挂在 PlayerContext 同一物体上（WorldBootstrap 装配）；昼夜观察在
    /// <see cref="WatchNightCrossing"/>——由 Update 每帧调用，EditMode 测试手动步进。
    /// </para>
    /// </summary>
    public sealed class QuestEventBus : MonoBehaviour
    {
        /// <summary>全局单例（与 PlayerContext.Instance 同款约定）。EditMode 下 AddComponent 不触发 Awake，<see cref="Bind"/> 兜底设置。</summary>
        public static QuestEventBus Instance { get; private set; }

        /// <summary>绑定的多章节任务书。链文件缺失 / 加载失败时为 null——Raise 变 no-op，游戏照常玩。C3 HUD 与 C4 存档也从这里拿引用。</summary>
        public QuestCampaign Campaign { get; private set; }

        /// <summary>
        /// 当前活动章的任务系统（章节顺序解锁：前章未完成时永远指向前章）。
        /// 旧签名消费方（HUD「x/8」、存档 SaveState、帮助菜单 8 格）继续用它，无需感知多章节。
        /// 无任务书时为 null。
        /// </summary>
        public QuestSystem Quests => Campaign?.Active;

        /// <summary>任务完成钩子：参数是刚完成的任务（经验已由总线入账，订阅方只管显示）。</summary>
        public event System.Action<Quest> OnQuestCompleted;

        /// <summary>章节完成钩子（m11 W2-4）：参数是刚完成的章节下标（0 起）。
        /// 触发时活动章已前移——除非那是最后一章（<see cref="Quests"/>.Current 为 null = 全部完成）。
        /// HUD 订阅它弹「第 N 章完成 / 下一章已解锁」。</summary>
        public event System.Action<int> OnChapterCompleted;

        /// <summary>章节总数（无任务书为 0）。HUD 据此区分「首章完成」与「全部章节完成」文案。</summary>
        public int ChapterCount => Campaign?.ChapterCount ?? 0;

        /// <summary>当前活动章下标（0 起；无任务书为 0）。</summary>
        public int ActiveChapterIndex => Campaign?.ActiveChapterIndex ?? 0;

        private PlayerContext _ctx;

        /// <summary>
        /// 击杀事件反查生物种类的宿主（m11 W2-4）。懒解析、命中即缓存（照 ProjectileManager 的
        /// 模式）；被销毁后 Unity fake-null 令 <c>== null</c> 成立、下个事件自动重解析。
        /// </summary>
        private MyWorld.Unity.Combat.MobManager _mobManager;

        /// <summary>上一帧观察到的世界时刻（tick）。跨夜 / 睡觉判定用，见 <see cref="WatchNightCrossing"/>。</summary>
        private float _lastNightWatchTick = -1f;

        /// <summary>绑定玩家上下文与任务书。campaign 允许传 null（无任务书 = 事件转发 no-op）。</summary>
        public void Bind(PlayerContext ctx, QuestCampaign campaign)
        {
            _ctx = ctx;
            Campaign = campaign;
            Instance = this; // EditMode 下 Awake 不会跑，这里兜底；运行时与 Awake 双保险
            // 进度分子不在这里清：fix1 起归 Core 计账（含 C4 的 Restore 恢复），总线只读
            ResetNightBaseline();
        }

        /// <summary>
        /// 单章绑定（m6 旧签名，测试 / WorldBootstrap 兼容）：内部包成单章任务书，
        /// 行为与旧版完全一致（<see cref="Quests"/> 就是传入的系统本身）。
        /// system 为 null = 无任务书。
        /// </summary>
        public void Bind(PlayerContext ctx, QuestSystem system)
        {
            Bind(ctx, system == null ? null : new QuestCampaign(new[] { system }));
        }

        /// <summary>
        /// 当前任务进度查询（m6 C3 HUD 消费）：返回 (分子, 分母)。
        /// 分母取当前任务条件的 <see cref="QuestCondition.RequiredCount"/>；
        /// 分子直接读 Core 的 <see cref="QuestSystem.CurrentProgress"/>（单一真源，fix1 起总线不再
        /// 重复计账）——口径随条件类型：ObtainItem = 最近一次匹配事件的背包现存量（覆盖不累计）、
        /// CraftItem/SmeltItem 等产出与动作类 = 任务内匹配事件 Count 的累计、无条件参数类恒 0。
        /// 无链或全链完成（<see cref="QuestSystem.Current"/> 为 null）时返回 (0, 0)——HUD 拿到后不画进度。
        /// </summary>
        public (int Progress, int Required) QuestProgress()
        {
            Quest current = Quests != null ? Quests.Current : null;
            if (current == null)
            {
                return (0, 0);
            }
            return (Quests.CurrentProgress, current.Condition.RequiredCount);
        }

        /// <summary>
        /// 单入口：把一个游戏事件转发给任务系统。命中「当前任务完成」时入账经验并触发钩子；
        /// 章节全链完成的瞬间再触发 <see cref="OnChapterCompleted"/>（此时活动章已切到下一章，
        /// 订阅方经 <see cref="Quests"/> 读到的就是新章首任务）；其余情况
        /// （无链 / 条件不匹配 / 全部完成）静默返回。
        /// </summary>
        public void Raise(QuestEvent evt)
        {
            // m12 第 1 波：成就 / 图鉴与任务链同源吃事件——**先于 Campaign 判空**转发，
            // 任务书缺失时成就照常工作（两者解耦，成就不依赖章节推进）
            if (_ctx != null)
            {
                _ctx.Achievements?.OnEvent(evt);
                _ctx.Codex?.OnQuestEvent(evt);
            }

            if (Campaign == null) return;

            // TryComplete 成功后活动章可能已前移，先经 out 参数取住「完成的任务与章节」
            // 供钩子与经验入账用。进度累计也发生在 Core（单一真源），完成即清零切任务
            if (!Campaign.TryComplete(evt, out Quest completing, out int chapterIndex, out bool chapterFinished))
            {
                return;
            }

            // 经验走 PlayerContext.Experience（struct 字段，经 _ctx 引用原地改，
            // ExperienceBarUi / 存档读的同一份）。_ctx 缺失（纯逻辑测试）时只跳过入账。
            if (_ctx != null)
            {
                _ctx.Experience.Add(completing.RewardExp);
            }
            OnQuestCompleted?.Invoke(completing);
            if (chapterFinished)
            {
                OnChapterCompleted?.Invoke(chapterIndex);
            }
        }

        /// <summary>
        /// 跨夜与睡觉观察（m11 W2-4 起两件事共用同一条基线，由 Update 每帧调用）：
        /// <para>
        /// <b>跨夜</b>——世界时刻自然推进跨过 <see cref="TimeOfDay.NightEndTick"/>（23000，日出）
        /// 的那一帧发一次 SurviveNight。日间推进、回绕帧（curr &lt; prev，跨过 24000 归零）
        /// 与基线未初始化（-1）都不判。
        /// </para>
        /// <para>
        /// <b>睡觉</b>——夜里（[NightStartTick, NightEndTick)）时刻被**整段拉回**早晨
        /// （curr &lt; prev 且落回夜里之前）：这是 <c>BedSystem.Sleep</c> 把时间直接置 0 的
        /// 唯一外部可见副作用，总线据此发 SleepInBed——**不需要玩法文件加事件出口**。
        /// 自然回绕不误判：回绕前必经 23000+（已出夜），不满足「上一帧还在夜里」。
        /// </para>
        /// </summary>
        public void WatchNightCrossing()
        {
            TimeOfDay time = _ctx != null ? _ctx.Time : null;
            if (time == null || Campaign == null) return;

            float curr = time.CurrentTick;
            if (_lastNightWatchTick >= 0f)
            {
                if (curr >= _lastNightWatchTick)
                {
                    // 自然推进：跨过日出刻的那一帧发 SurviveNight（回绕帧 curr < prev 不进这里）。
                    // 判定与 m6 C2 完全一致——白天大步推进跨过 23000 也算「活过夜」
                    if (_lastNightWatchTick < TimeOfDay.NightEndTick && curr >= TimeOfDay.NightEndTick)
                    {
                        Raise(new QuestEvent { Type = QuestEventType.SurviveNight });
                    }
                }
                else
                {
                    // 时刻被拉回（curr < prev）：上一帧还在夜里且落回夜里之前 = 床睡觉跳夜
                    bool wasNight = _lastNightWatchTick >= TimeOfDay.NightStartTick
                                    && _lastNightWatchTick < TimeOfDay.NightEndTick;
                    if (wasNight && curr < TimeOfDay.NightStartTick)
                    {
                        Raise(new QuestEvent { Type = QuestEventType.SleepInBed });
                    }
                }
            }
            _lastNightWatchTick = curr;
        }

        /// <summary>
        /// 重置跨夜观察基线为当前时刻。读档恢复时间后由 WorldBootstrap 调用——
        /// 否则「存档时正午 → 读档后 23500」的第一帧会被误判成跨过日出，白发一次 SurviveNight；
        /// 同理防止「存档深夜 → 读档正午」被误判成睡觉。
        /// </summary>
        public void ResetNightBaseline()
        {
            _lastNightWatchTick = _ctx != null && _ctx.Time != null ? _ctx.Time.CurrentTick : -1f;
        }

        /// <summary>
        /// 玩家击杀 mob 的死亡事件转发（m11 W2-4）。订阅 <see cref="CombatEvents.OnEntityDied"/>
        /// （CombatController 近战致死一击的既有出口；EditMode 不回调 OnEnable，
        /// 测试直接调本方法 = 同一条翻译逻辑）。只认「玩家（attacker=0）杀死 mob（victim≠0）」；
        /// 生物种类按 victim 的 EntityId 反查 <see cref="MyWorld.Unity.Combat.MobManager"/>
        /// （Dying 状态的 mob 还在列表里，反查必然命中）；武器按伤害来源翻译——
        /// <see cref="DamageSource.Projectile"/> = 箭 = "bow"，近战不带武器（null = 任意）。
        /// </summary>
        public void HandleEntityDied(DamageEvent ev)
        {
            if (ev.VictimEntityId == 0) return; // 死的是玩家：不是击杀任务的事
            if (ev.AttackerEntityId != 0) return; // 非玩家击杀（未来 mob 互殴）
            if (Campaign == null) return;

            if (_mobManager == null)
            {
                _mobManager = FindObjectOfType<MyWorld.Unity.Combat.MobManager>();
                if (_mobManager == null) return; // 没有宿主就解不出种类：安全跳过
            }
            if (!_mobManager.TryGetMobById(ev.VictimEntityId, out var mob)) return; // 实体已不在（防御）

            Raise(new QuestEvent
            {
                Type = QuestEventType.KillKind,
                Kind = mob.Kind,
                Weapon = ev.Source == DamageSource.Projectile ? "bow" : null,
                Count = 1,
            });
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            CombatEvents.OnEntityDied += HandleEntityDied;
        }

        private void OnDisable()
        {
            CombatEvents.OnEntityDied -= HandleEntityDied;
        }

        private void Update()
        {
            WatchNightCrossing();
        }

        private void OnDestroy()
        {
            CombatEvents.OnEntityDied -= HandleEntityDied; // 静态事件必须摘，否则悬垂订阅
            if (Instance == this) Instance = null;
        }
    }
}
