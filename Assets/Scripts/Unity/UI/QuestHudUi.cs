using System;
using MyWorld.Core.Quests;
using MyWorld.Unity.Gameplay;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 右上角当前目标卡（m6 C3）：让孩子「始终知道下一步」。卡片常驻显示
    /// 「当前目标：任务名 + 进度 n/m + 本章序号 x/y」；任务完成的瞬间变绿打勾
    /// 停留 <see cref="CompletedHoldSeconds"/> 后切下一任务；全链完成后显示
    /// 「首章完成 ✓」停留 <see cref="ChapterDoneHoldSeconds"/> 再隐藏。
    /// <para>
    /// 文本计算全部收在 <see cref="GetHudText"/>（不碰 GUI 上下文，EditMode 可直接断言）；
    /// 打勾/完成卡的停留计时用可注入的 <c>Func&lt;float&gt;</c> 时钟（默认 <c>Time.time</c>），
    /// 过期判定惰性发生在 GetHudText 里——OnGUI 每帧调用它，即等于每帧步进，无需 Update。
    /// 进度数字经 <see cref="QuestEventBus.QuestProgress"/> 每帧现读（ObtainItem = 背包现存量、
    /// CraftItem/SmeltItem = 事件累计），挖到 / 合成的同一帧就能看到数字变化。
    /// </para>
    /// </summary>
    public sealed class QuestHudUi : MonoBehaviour
    {
        // ─── 卡片几何与停留时长（全局硬约束：260×64 右上角） ──────────────────
        public const float CardWidth = 260f;
        public const float CardHeight = 64f;
        /// <summary>卡片与屏幕右/上边缘的留白（px）。</summary>
        public const float EdgeMargin = 16f;
        /// <summary>任务完成打勾卡的停留时长（s），到点切下一任务。</summary>
        public const float CompletedHoldSeconds = 1f;
        /// <summary>全链完成「首章完成 ✓」卡的停留时长（s），到点隐藏。</summary>
        public const float ChapterDoneHoldSeconds = 5f;

        private QuestEventBus _bus;
        /// <summary>可注入时钟：默认游戏时间。EditMode 测试用它步进 1s / 5s 停留窗口。</summary>
        private Func<float> _timeProvider = DefaultTime;

        /// <summary>打勾停留中的已完成任务（null = 不在停留期）。完成钩子写入，过期惰性清除。</summary>
        private Quest _completedQuest;
        private float _completedUntil;

        /// <summary>全链完成卡是否在 5s 停留期内。链走完的瞬间写入。</summary>
        private bool _chapterDone;
        private float _chapterDoneUntil;

        private static float DefaultTime()
        {
            return Time.time;
        }

        /// <summary>
        /// 挂总线并订阅 <see cref="QuestEventBus.OnQuestCompleted"/>。WorldBootstrap 装配时调用；
        /// 重复 Bind 先摘旧订阅再挂新的（幂等），bus 传 null = 只解绑（无链场景不显示卡片）。
        /// </summary>
        public void Bind(QuestEventBus bus)
        {
            if (_bus != null)
            {
                _bus.OnQuestCompleted -= HandleQuestCompleted;
            }
            _bus = bus;
            if (bus != null)
            {
                bus.OnQuestCompleted += HandleQuestCompleted;
            }
        }

        /// <summary>
        /// 测试注入：换总线的同时替换时间源（EditMode 不跑 Update，用可控时钟步进停留窗口）。
        /// </summary>
        public void BindForTest(QuestEventBus bus, Func<float> timeProvider)
        {
            Bind(bus);
            _timeProvider = timeProvider ?? DefaultTime;
        }

        /// <summary>
        /// 当前目标卡的完整文本（两行，'\n' 分隔）；不该显示卡片时返回 null。
        /// 状态优先级：首章完成卡 &gt; 任务完成打勾卡 &gt; 普通目标卡 &gt; 不显示。
        /// 停留窗口的过期判定在这里惰性执行（OnGUI 每帧调用 = 每帧步进），幂等可重入。
        /// </summary>
        public string GetHudText()
        {
            // 全链完成卡优先：上一张打勾卡还没过期链就走完时，直接盖过去显示完成卡
            if (_chapterDone)
            {
                if (_timeProvider() < _chapterDoneUntil)
                {
                    return "首章完成 ✓";
                }
                _chapterDone = false; // 5s 到：隐藏（链已走完，下面 Current 也是 null）
            }

            if (_completedQuest != null)
            {
                if (_timeProvider() < _completedUntil)
                {
                    int total = _bus != null && _bus.Quests != null ? _bus.Quests.TotalCount : 0;
                    // CompletedCount 此时已含刚完成的这个任务（钩子在链推进后触发）
                    int done = _bus != null && _bus.Quests != null ? _bus.Quests.CompletedCount : 0;
                    return $"✓ {_completedQuest.Name}\n任务 {done}/{total} 完成";
                }
                _completedQuest = null; // 1s 到：切下一任务（落到下面的普通目标卡）
            }

            QuestSystem quests = _bus != null ? _bus.Quests : null;
            Quest current = quests != null ? quests.Current : null;
            if (current == null)
            {
                return null; // 无总线 / 无链 / 全链完成且完成卡已过期
            }

            var (progress, required) = _bus.QuestProgress();
            return $"当前目标：{current.Name}\n{progress}/{required}　已完成 {quests.CompletedCount}/{quests.TotalCount}";
        }

        /// <summary>
        /// 完成钩子：非链尾 → 打勾卡停留 1s；链尾（完成后 <see cref="QuestSystem.Current"/> 为 null）
        /// → 首章完成卡停留 5s。
        /// </summary>
        private void HandleQuestCompleted(Quest quest)
        {
            float now = _timeProvider();
            QuestSystem quests = _bus != null ? _bus.Quests : null;
            if (quests != null && quests.Current == null)
            {
                _chapterDone = true;
                _chapterDoneUntil = now + ChapterDoneHoldSeconds;
            }
            else
            {
                _completedQuest = quest;
                _completedUntil = now + CompletedHoldSeconds;
            }
        }

        // ─── 绘制 ─────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            string text = GetHudText();
            if (text == null)
            {
                return;
            }

            // GetHudText 已先做过期清理，这里读到的是最终停留态
            bool completedCard = _chapterDone || _completedQuest != null;

            var rect = new Rect(Screen.width - CardWidth - EdgeMargin, EdgeMargin, CardWidth, CardHeight);
            var prevColor = GUI.color;
            if (completedCard)
            {
                // 完成瞬间卡片变绿（连文字一起染），普通态保持背包的深色半透明
                GUI.color = new Color(0.65f, 1f, 0.65f, 1f);
            }
            // 半透明深色背景，同背包（GUI.Box 默认皮肤）+ 白字缓存 GUIStyle（ItemSlotDrawer 共享）
            GUI.Box(rect, GUIContent.none);
            string[] lines = text.Split('\n');
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 24f), lines[0],
                ItemSlotDrawer.WhiteStyle());
            if (lines.Length > 1)
            {
                GUI.Label(new Rect(rect.x + 12f, rect.y + 34f, rect.width - 24f, 22f), lines[1],
                    ItemSlotDrawer.WhiteStyle());
            }
            GUI.color = prevColor;
        }

        private void OnDestroy()
        {
            // 总线活得比 HUD 长时必须摘订阅，否则完成钩子回调打到已销毁组件
            if (_bus != null)
            {
                _bus.OnQuestCompleted -= HandleQuestCompleted;
            }
        }
    }
}
