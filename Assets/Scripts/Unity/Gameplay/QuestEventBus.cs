using MyWorld.Core.Quests;
using MyWorld.Core.Time;
using UnityEngine;

namespace MyWorld.Unity.Gameplay
{
    /// <summary>
    /// 任务事件总线（m6 C2）：游戏内动作 → <see cref="QuestEvent"/> → <see cref="QuestSystem"/>
    /// 的唯一转发点。游戏逻辑不感知任务系统，只在产出 / 拾取 / 取料 / 跨夜的缝里
    /// <c>QuestEventBus.Instance?.Raise(new QuestEvent{...})</c> 一行了事——
    /// Instance 为 null（早期场景 / 纯逻辑测试没有总线）时 <c>?.</c> 天然安全。
    /// <para>
    /// 职责刻意保持「薄」：转发事件；当前任务命中完成的瞬间先经
    /// <see cref="PlayerContext.Experience"/>.Add 入账奖励经验，再触发
    /// <see cref="OnQuestCompleted"/> 钩子（C3 的 QuestHudUi 订阅它弹「完成」提示）。
    /// 条件判定 / 链推进 / 进度计账全在 Core 的 QuestSystem（单一真源），本类不重复任何比较逻辑。
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

        /// <summary>绑定的任务系统。链文件缺失 / 加载失败时为 null——Raise 变 no-op，游戏照常玩。C3 HUD 与 C4 存档也从这里拿引用。</summary>
        public QuestSystem Quests { get; private set; }

        /// <summary>任务完成钩子：参数是刚完成的任务（经验已由总线入账，订阅方只管显示）。</summary>
        public event System.Action<Quest> OnQuestCompleted;

        private PlayerContext _ctx;

        /// <summary>上一帧观察到的世界时刻（tick）。跨夜判定用，见 <see cref="WatchNightCrossing"/>。</summary>
        private float _lastNightWatchTick = -1f;

        /// <summary>绑定玩家上下文与任务链。system 允许传 null（无任务链 = 事件转发 no-op）。</summary>
        public void Bind(PlayerContext ctx, QuestSystem system)
        {
            _ctx = ctx;
            Quests = system;
            Instance = this; // EditMode 下 Awake 不会跑，这里兜底；运行时与 Awake 双保险
            // 进度分子不在这里清：fix1 起归 Core 计账（含 C4 的 Restore 恢复），总线只读
            ResetNightBaseline();
        }

        /// <summary>
        /// 当前任务进度查询（m6 C3 HUD 消费）：返回 (分子, 分母)。
        /// 分母取当前任务条件的 <see cref="QuestCondition.RequiredCount"/>；
        /// 分子直接读 Core 的 <see cref="QuestSystem.CurrentProgress"/>（单一真源，fix1 起总线不再
        /// 重复计账）——口径随条件类型：ObtainItem = 最近一次匹配事件的背包现存量（覆盖不累计）、
        /// CraftItem/SmeltItem = 任务内匹配事件 Count 的累计、SurviveNight 恒 0。
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
        /// 其余情况（无链 / 条件不匹配 / 全链完成）静默返回。
        /// </summary>
        public void Raise(QuestEvent evt)
        {
            if (Quests == null) return;

            // TryComplete 成功后 Current 已前移，先取住「即将完成的任务」供钩子与经验入账用。
            // 进度累计也发生在 TryComplete 里（Core 单一真源），完成即清零切下一任务
            Quest completing = Quests.Current;
            if (!Quests.TryComplete(evt)) return;

            // 经验走 PlayerContext.Experience（struct 字段，经 _ctx 引用原地改，
            // ExperienceBarUi / 存档读的同一份）。_ctx 缺失（纯逻辑测试）时只跳过入账。
            if (_ctx != null)
            {
                _ctx.Experience.Add(completing.RewardExp);
            }
            OnQuestCompleted?.Invoke(completing);
        }

        /// <summary>
        /// 跨夜观察：世界时刻跨过 <see cref="TimeOfDay.NightEndTick"/>（23000，日出）的
        /// 那一帧发一次 SurviveNight。日间推进、回绕帧（curr &lt; prev，跨过 24000 归零）
        /// 与基线未初始化（-1）都不判。Time 的推进由 DayNightCycle 负责，本方法只读。
        /// </summary>
        public void WatchNightCrossing()
        {
            TimeOfDay time = _ctx != null ? _ctx.Time : null;
            if (time == null || Quests == null) return;

            float curr = time.CurrentTick;
            if (_lastNightWatchTick >= 0f
                && _lastNightWatchTick < TimeOfDay.NightEndTick
                && curr >= TimeOfDay.NightEndTick
                && curr >= _lastNightWatchTick)
            {
                Raise(new QuestEvent { Type = QuestEventType.SurviveNight });
            }
            _lastNightWatchTick = curr;
        }

        /// <summary>
        /// 重置跨夜观察基线为当前时刻。读档恢复时间后由 WorldBootstrap 调用——
        /// 否则「存档时正午 → 读档后 23500」的第一帧会被误判成跨过日出，白发一次 SurviveNight。
        /// </summary>
        public void ResetNightBaseline()
        {
            _lastNightWatchTick = _ctx != null && _ctx.Time != null ? _ctx.Time.CurrentTick : -1f;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            WatchNightCrossing();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
