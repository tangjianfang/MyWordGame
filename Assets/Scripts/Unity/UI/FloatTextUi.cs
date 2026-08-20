using System.Collections.Generic;
using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// 世界内飘字（m11 W2-3）：经验 +N 绿字（击杀经验入账处）、受伤 -N 红字（战斗事件），
    /// 1 秒上浮 + 线性淡出（m9 spec 的「+N 飘字」欠账）。
    /// <para>
    /// <b>挂载方式（WorldBootstrap 本波禁改）</b>：本组件**自挂**——两个静态入口
    /// （<see cref="ShowExperience"/> / <see cref="ShowDamage"/>）首次被调用时按需建一个
    /// 隐藏 GameObject 挂自己，之后复用；触发点在 MobManager（击杀经验入账
    /// <see cref="Combat.MobManager.GrantKillExperience"/> 与战斗伤害转发
    /// <see cref="Combat.MobManager.HandleDamageTaken"/>——「既有事件挂载点」），
    /// 不新增任何装配步骤。
    /// </para>
    /// <para>
    /// 同屏多条同色飘字按 <see cref="LineSpacing"/> 逐条错开不叠字；条目本身随时间上浮
    /// （<see cref="RiseOffsetAt"/>）与淡出（<see cref="AlphaAt"/>），两条曲线是纯静态，
    /// EditMode 测试直接断言。计时用 <see cref="Time.time"/>（真暂停下 timeScale=0
    /// 飘字同步冻结，与世界一致）。
    /// </para>
    /// </summary>
    public sealed class FloatTextUi : MonoBehaviour
    {
        /// <summary>单条飘字的存活时长（秒）：1s 上浮淡出（任务卡定值）。</summary>
        public const float DurationSeconds = 1f;

        /// <summary>整个存活期总共上浮的像素距离。</summary>
        public const float RisePixels = 40f;

        /// <summary>同屏多条同色飘字的行距（像素），新的一条排在更靠下（更晚消失）的位置。</summary>
        public const float LineSpacing = 24f;

        /// <summary>同屏最多保留的飘字条数（超过时最旧的当场过期，防止极端刷屏）。</summary>
        public const int MaxEntries = 8;

        /// <summary>经验飘字颜色（绿，与经验条同色系）。</summary>
        public static readonly Color ExperienceColor = new Color(0.45f, 1f, 0.5f);

        /// <summary>伤害飘字颜色（红，与血心同色系）。</summary>
        public static readonly Color DamageColor = new Color(1f, 0.35f, 0.3f);

        /// <summary>飘字种类：决定文案颜色与锚点（经验在经验条上方 / 伤害在准星下方）。</summary>
        public enum FloatKind
        {
            Experience,
            Damage,
        }

        /// <summary>一条在场的飘字（不可变值对象）。</summary>
        public readonly struct Entry
        {
            public readonly string Text;
            public readonly FloatKind Kind;
            public readonly float StartTime;

            public Entry(string text, FloatKind kind, float startTime)
            {
                Text = text;
                Kind = kind;
                StartTime = startTime;
            }
        }

        private static FloatTextUi _instance;

        private readonly List<Entry> _entries = new List<Entry>();
        private GUIStyle _style;

        /// <summary>当前挂载的实例（可能为 null——从未触发过飘字就没建）。测试断言挂载用。</summary>
        public static FloatTextUi Instance => _instance;

        /// <summary>当前在场的飘字条目（只读视图；顺序 = 入场顺序，最旧在前）。</summary>
        public IReadOnlyList<Entry> ActiveEntries => _entries;

        /// <summary>当前在场条数（测试断言队列用）。</summary>
        public int EntryCount => _entries.Count;

        /// <summary>
        /// 按需取/建全局实例：场景里已有就复用（含场景重载后 _instance 失效但对象还在的情形），
        /// 没有就自建一个同名空 GameObject 挂自己（WorldBootstrap 禁改，见类注释）。
        /// </summary>
        public static FloatTextUi EnsureInstance()
        {
            if (_instance != null) return _instance;
            var existing = FindObjectOfType<FloatTextUi>();
            if (existing != null)
            {
                _instance = existing;
                return _instance;
            }
            var go = new GameObject("FloatTextUi");
            _instance = go.AddComponent<FloatTextUi>();
            return _instance;
        }

        /// <summary>
        /// 经验 +N 绿字。击杀经验入账处（<see cref="Combat.MobManager.GrantKillExperience"/>）
        /// 调用；amount ≤ 0 不出字（0 经验的生物不该飘 +0）。
        /// </summary>
        public static void ShowExperience(int amount)
        {
            if (amount <= 0) return;
            EnsureInstance().Add("+" + amount, FloatKind.Experience);
        }

        /// <summary>
        /// 受伤 -N 红字。战斗伤害事件（<see cref="Combat.MobManager.HandleDamageTaken"/> 转发
        /// <see cref="MyWorld.Core.Entities.CombatEvents.OnDamageTaken"/>）处调用；amount ≤ 0 不出字。
        /// 显示值按四舍五入取整（0.5 的碎块伤显示 1）。
        /// </summary>
        public static void ShowDamage(float amount)
        {
            if (amount <= 0f) return;
            int shown = Mathf.Max(1, Mathf.RoundToInt(amount));
            EnsureInstance().Add("-" + shown, FloatKind.Damage);
        }

        /// <summary>自定义文本飘字（m12 W1：成就达成提示）。空串不出字。</summary>
        public static void ShowText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            EnsureInstance().Add(text, FloatKind.Experience);
        }

        /// <summary>入队一条飘字（核心入口；OnGUI/Update/测试共用）。超过上限时最旧的先过期。</summary>
        public Entry Add(string text, FloatKind kind)
        {
            var entry = new Entry(text, kind, Time.time);
            _entries.Add(entry);
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }
            return entry;
        }

        /// <summary>淡出曲线：elapsed=0 → 1（不透明），≥<see cref="DurationSeconds"/> → 0（完全消失）。</summary>
        public static float AlphaAt(float elapsed)
            => Mathf.Clamp01(1f - elapsed / DurationSeconds);

        /// <summary>上浮曲线：elapsed=0 → 0px，=<see cref="DurationSeconds"/> → <see cref="RisePixels"/>px。</summary>
        public static float RiseOffsetAt(float elapsed)
            => RisePixels * Mathf.Clamp01(elapsed / DurationSeconds);

        /// <summary>
        /// 清掉已过期的条目（elapsed ≥ <see cref="DurationSeconds"/>），返回清除数。
        /// Update 每帧调；EditMode 测试直接传虚拟时刻驱动（照 FoodBarUI.TickForTest 的思路）。
        /// </summary>
        public int ExpireBefore(float now)
        {
            int removed = 0;
            // 容差 1e-4：EditMode 的 Time.time 是真实大数值，(t0+d)-t0 的浮点损耗会
            // 把整 1s 算成 0.9999…，不加容差会在长批次里漏过期（m12 第 1 波实测）
            const float epsilon = 1e-4f;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (now - _entries[i].StartTime >= DurationSeconds - epsilon)
                {
                    _entries.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        private void Update() => ExpireBefore(Time.time);

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnGUI()
        {
            if (_entries.Count == 0) return;
            if (_style == null)
            {
                // 首帧构造一次（GUI.skin 只在 OnGUI 内可用），之后逐帧复用（m5 C1 的 GC 纪律）
                _style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 18,
                    fontStyle = FontStyle.Bold,
                };
            }

            // 同色多条按入场顺序错开行：最新的排最靠下（后消失），先来的往上顶
            int xpSeen = 0, dmgSeen = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                float elapsed = Mathf.Max(0f, Time.time - e.StartTime);
                float alpha = AlphaAt(elapsed);
                if (alpha <= 0f) continue;

                float lineIndex = e.Kind == FloatKind.Experience ? xpSeen++ : dmgSeen++;
                Rect rect = AnchorRect(e.Kind, lineIndex);
                rect.y -= RiseOffsetAt(elapsed);

                _style.normal.textColor = Tint(e.Kind, alpha);
                GUI.Label(rect, e.Text, _style);
            }
        }

        /// <summary>某条飘字的锚定矩形（未含上浮位移）：经验在经验条上方居中，伤害在准星下方居中。</summary>
        private static Rect AnchorRect(FloatKind kind, float lineIndex)
        {
            const float width = 140f;
            if (kind == FloatKind.Damage)
            {
                // 准星下方 60px 起，多条再往下排（不挡准星、不挡弓蓄力条）
                return new Rect(
                    (Screen.width - width) / 2f,
                    Screen.height / 2f + 60f + lineIndex * LineSpacing,
                    width, 22f);
            }
            // 经验飘字锚在心行上方——按**两行心**的极端高度让位（m11 W2-3 起上限可到
            // >10 心排两行），持 +maxHealth 装备时也不与第二行心叠字；多条再往上排
            return new Rect(
                (Screen.width - width) / 2f,
                Screen.height - 64f - HotbarUI.SlotSize - 18f
                    - 2f * (HealthBarUI.HeartSize + 2f) - 8f - lineIndex * LineSpacing,
                width, 22f);
        }

        private static Color Tint(FloatKind kind, float alpha)
        {
            Color c = kind == FloatKind.Experience ? ExperienceColor : DamageColor;
            c.a = alpha;
            return c;
        }
    }
}
