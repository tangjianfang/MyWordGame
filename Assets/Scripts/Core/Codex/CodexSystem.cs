using System.Collections.Generic;
using MyWorld.Core.Entities;
using MyWorld.Core.Quests;

namespace MyWorld.Core.Codex
{
    /// <summary>
    /// 图鉴收集系统（m12 W2）：三类条目「首次遭遇 / 首次挖到 / 首次获得」解锁。
    /// <para>
    /// 纯 Core：解锁集经 <see cref="ExportStats"/>/<see cref="ImportStats"/> 与
    /// <c>LevelData.Stats</c> 往返（键 <c>codex:mob:&lt;kind&gt;</c> /
    /// <c>codex:block:&lt;blockId&gt;</c> / <c>codex:item:diamond</c>，值恒 1）。
    /// 解锁喂点（Unity 侧）：击杀或命中生物（CombatEvents / 总线 KillKind）、
    /// 挖方块（<c>BlockInteraction.BlockBroken</c>）、获得钻石（总线 ObtainItem）。
    /// </para>
    /// <para>
    /// 条目表硬编码（18 生物 / 4 矿石方块 + 1 钻石物品 / 3 植物）——图鉴条目随玩法
    /// 演进变动频率低于数据表，且需要与 MobKind/方块 id/卡牌贴图名三方对齐，
    /// 放代码里由测试钉死比 JSON 更稳（取舍注释）。
    /// </para>
    /// </summary>
    public sealed class CodexSystem
    {
        /// <summary>图鉴生物条目：MobKind → 中文名（18 种，与 mobs/models/*.json 对齐）。</summary>
        public static readonly (MobKind Kind, string Name)[] MobEntries =
        {
            (MobKind.Pig, "猪"), (MobKind.Cow, "牛"), (MobKind.Chicken, "鸡"),
            (MobKind.Zombie, "僵尸"), (MobKind.Villager, "村民"), (MobKind.Sheep, "羊"),
            (MobKind.Rabbit, "兔子"), (MobKind.Fox, "狐狸"), (MobKind.Deer, "鹿"),
            (MobKind.Panda, "熊猫"), (MobKind.Penguin, "企鹅"), (MobKind.Goat, "山羊"),
            (MobKind.Raccoon, "浣熊"), (MobKind.Hamster, "仓鼠"), (MobKind.Skeleton, "骷髅"),
            (MobKind.Spider, "蜘蛛"), (MobKind.Creeper, "苦力怕"), (MobKind.MachineGuardian, "机元守卫"),
        };

        /// <summary>图鉴矿石/植物条目：方块 id →（卡牌贴图名, 中文名）。
        /// 钻石没有方块（是物品），单列 <see cref="DiamondItemId"/>。</summary>
        public static readonly (string BlockId, string Card, string Name)[] BlockEntries =
        {
            ("gold_ore", "card-ore-gold", "金矿石"),
            ("raw_iron_ore", "card-ore-iron", "粗铁矿石"),
            ("summer_alloy_ore", "card-ore-alloy", "夏季合金矿石"),
            ("machine_essence_ore", "card-ore-essence", "机元矿石"),
            ("flower_sunflower", "card-plant-sunflower", "向日葵"),
            ("fern", "card-plant-fern", "蕨"),
            ("cherry_log", "card-plant-cherry", "樱花树"),
        };

        /// <summary>钻石（物品 numericId，items/diamond.json）。</summary>
        public const int DiamondItemId = 1005;

        /// <summary>钻石卡牌贴图名（矿石第 5 张——没有对应方块，走物品获得解锁）。</summary>
        public const string DiamondCard = "card-ore-diamond";

        private readonly HashSet<string> _unlocked = new HashSet<string>();

        public int UnlockedCount => _unlocked.Count;

        public int TotalCount => MobEntries.Length + BlockEntries.Length + 1; // +1 钻石

        public bool IsMobUnlocked(MobKind kind) => _unlocked.Contains("codex:mob:" + kind);

        public bool IsBlockUnlocked(string blockId) => _unlocked.Contains("codex:block:" + blockId);

        public bool IsDiamondUnlocked => _unlocked.Contains("codex:item:diamond");

        /// <summary>首次遭遇生物（命中或击杀都算"见过"）。返回 true 表示本次是新解锁。</summary>
        public bool SeeMob(MobKind kind)
        {
            return _unlocked.Add("codex:mob:" + kind);
        }

        /// <summary>首次挖到方块（只认 <see cref="BlockEntries"/> 里的 7 个 id，其余忽略）。</summary>
        public bool UnlockBlock(string blockId)
        {
            for (int i = 0; i < BlockEntries.Length; i++)
            {
                if (BlockEntries[i].BlockId == blockId)
                {
                    return _unlocked.Add("codex:block:" + blockId);
                }
            }

            return false;
        }

        /// <summary>首次获得关键物品（目前只有钻石）。</summary>
        public bool UnlockItem(int itemId)
        {
            if (itemId == DiamondItemId)
            {
                return _unlocked.Add("codex:item:diamond");
            }

            return false;
        }

        /// <summary>总线事件喂点（KillKind 解锁对应生物；ObtainItem 解锁钻石）。</summary>
        public void OnQuestEvent(QuestEvent evt)
        {
            if (evt.Type == QuestEventType.KillKind && evt.Kind.HasValue)
            {
                SeeMob(evt.Kind.Value);
            }
            else if (evt.Type == QuestEventType.ObtainItem && evt.ItemId == DiamondItemId)
            {
                UnlockItem(evt.ItemId);
            }
        }

        public void ExportStats(Dictionary<string, int> sink)
        {
            foreach (string key in _unlocked)
            {
                sink[key] = 1;
            }
        }

        public void ImportStats(Dictionary<string, int> source)
        {
            if (source == null) return;
            foreach (var pair in source)
            {
                if (pair.Value > 0 && pair.Key.StartsWith("codex:", System.StringComparison.Ordinal))
                {
                    _unlocked.Add(pair.Key);
                }
            }
        }
    }
}
