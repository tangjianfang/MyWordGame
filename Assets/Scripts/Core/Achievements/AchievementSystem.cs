using System;
using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Quests;
using Newtonsoft.Json.Linq;

namespace MyWorld.Core.Achievements
{
    /// <summary>一条成就定义（m12 W1，数据驱动：StreamingAssets/achievements.json）。</summary>
    public sealed class Achievement
    {
        public string Id;

        /// <summary>UI 显示名（中文）。</summary>
        public string Name;

        public string Desc;

        /// <summary>徽章贴图名（StreamingAssets/ui/codex/&lt;Icon&gt;.png，不带扩展名）。</summary>
        public string Icon;

        /// <summary>判定事件（复用任务总线同一套 12 类词汇）。</summary>
        public QuestEventType Event;

        /// <summary>物品限定（ObtainItem/CraftItem/SmeltItem 用；0 = 任意物品）。</summary>
        public int ItemId;

        /// <summary>达成所需次数。</summary>
        public int RequiredCount;

        /// <summary>生物限定（KillKind/FeedAnimal 用；null = 任意生物）。</summary>
        public MobKind? Kind;

        /// <summary>武器限定（KillKind 用；"bow" = 弩箭击杀；null = 不限）。</summary>
        public string Weapon;

        /// <summary>特殊判定："allOthers" = 其余成就全部达成后解锁。</summary>
        public string Special;
    }

    /// <summary>achievements.json 解析（缺字段走默认，坏结构抛异常由调用方降级）。</summary>
    public static class AchievementDatabase
    {
        public static List<Achievement> FromJson(string json)
        {
            var result = new List<Achievement>();
            var root = JObject.Parse(json);
            foreach (var node in root["achievements"])
            {
                var a = new Achievement
                {
                    Id = (string)node["id"],
                    Name = (string)node["name"] ?? (string)node["id"],
                    Desc = (string)node["desc"] ?? "",
                    Icon = (string)node["icon"] ?? "",
                    RequiredCount = (int?)node["count"] ?? 1,
                    ItemId = (int?)node["itemId"] ?? 0,
                    Special = (string)node["special"],
                };
                string evt = (string)node["event"];
                if (!string.IsNullOrEmpty(evt))
                {
                    a.Event = (QuestEventType)System.Enum.Parse(typeof(QuestEventType), evt, true);
                }

                string kind = (string)node["kind"];
                if (!string.IsNullOrEmpty(kind))
                {
                    a.Kind = (MobKind)System.Enum.Parse(typeof(MobKind), kind, true);
                }

                a.Weapon = (string)node["weapon"];
                if (a.RequiredCount <= 0) a.RequiredCount = 1;
                result.Add(a);
            }

            return result;
        }
    }

    /// <summary>
    /// 成就系统（m12 W1）：订阅任务总线同源的 12 类事件累计进度，达成即解锁。
    /// <para>
    /// 纯 Core：进度存内存字典，经 <see cref="ExportStats"/>/<see cref="ImportStats"/>
    /// 与 <c>LevelData.Stats</c> 往返（键 <c>ach:&lt;id&gt;</c> = 当前进度值）。
    /// 计数口径照 <see cref="QuestSystem"/>：ObtainItem 是覆盖式（事件携带背包现存量，
    /// 取 max），其余累计式（事件 Count 逐次累加）。解锁判定 = 进度 ≥ RequiredCount。
    /// </para>
    /// </summary>
    public sealed class AchievementSystem
    {
        private readonly List<Achievement> _all;
        private readonly Dictionary<string, int> _progress = new Dictionary<string, int>();

        /// <summary>成就达成瞬间发出（Unity 侧接飘字 / 弹窗；Core 不感知 UI）。</summary>
        public event Action<Achievement> Unlocked;

        public AchievementSystem(List<Achievement> definitions)
        {
            _all = definitions ?? new List<Achievement>();
        }

        public IReadOnlyList<Achievement> All => _all;

        public int UnlockedCount
        {
            get
            {
                int n = 0;
                foreach (var a in _all)
                {
                    if (IsUnlocked(a)) n++;
                }

                return n;
            }
        }

        public int ProgressOf(Achievement a)
            => _progress.TryGetValue(a.Id, out int v) ? v : 0;

        public bool IsUnlocked(Achievement a)
            => a.Special == "allOthers"
                ? _all.TrueForAll(x => x.Special == "allOthers" || IsUnlocked(x))
                : ProgressOf(a) >= a.RequiredCount;

        /// <summary>喂一个任务总线事件（与 <c>QuestEventBus.Raise</c> 同源转发）。</summary>
        public void OnEvent(QuestEvent evt)
        {
            foreach (var a in _all)
            {
                if (a.Special == "allOthers" || a.Event != evt.Type) continue;
                if (a.ItemId != 0 && evt.ItemId != a.ItemId) continue;
                if (a.Kind.HasValue && evt.Kind != a.Kind) continue;
                if (a.Weapon != null && !string.Equals(evt.Weapon, a.Weapon, System.StringComparison.OrdinalIgnoreCase)) continue;

                bool wasUnlocked = IsUnlocked(a);
                int current = ProgressOf(a);
                int next = evt.Type == QuestEventType.ObtainItem
                    ? System.Math.Max(current, evt.Count) // 覆盖式：事件 Count 是背包现存量
                    : current + System.Math.Max(evt.Count, 1); // 累计式：事件本身算一次，没带 Count 按 1 计
                _progress[a.Id] = next;
                if (!wasUnlocked && IsUnlocked(a))
                {
                    Unlocked?.Invoke(a);
                }
            }

            // completionist：任一成就解锁后复查一次 allOthers 边沿
            foreach (var a in _all)
            {
                if (a.Special != "allOthers") continue;
                bool was = _progress.TryGetValue(a.Id, out int v) && v >= 1;
                if (!was && IsUnlocked(a))
                {
                    _progress[a.Id] = 1;
                    Unlocked?.Invoke(a);
                }
            }
        }

        /// <summary>进度导出到 LevelData.Stats（键 ach:&lt;id&gt;；只写非零值省空间）。</summary>
        public void ExportStats(Dictionary<string, int> sink)
        {
            foreach (var a in _all)
            {
                int v = ProgressOf(a);
                if (v > 0)
                {
                    sink["ach:" + a.Id] = v;
                }
            }
        }

        /// <summary>旧档导入（缺键 = 零进度全新开始；不认识的键忽略）。</summary>
        public void ImportStats(Dictionary<string, int> source)
        {
            if (source == null) return;
            foreach (var a in _all)
            {
                if (source.TryGetValue("ach:" + a.Id, out int v) && v > 0)
                {
                    _progress[a.Id] = v;
                }
            }
        }
    }
}
