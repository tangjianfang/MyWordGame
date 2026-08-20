// m12 W2：图鉴系统纯逻辑契约（dotnet / EditMode 双链同源）。
// 解锁幂等 / 白名单外方块忽略 / 总数对表 / Stats 往返。
using System.Collections.Generic;
using System.Linq;
using MyWorld.Core.Codex;
using MyWorld.Core.Entities;
using MyWorld.Core.Quests;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Codex
{
    [TestFixture]
    public class CodexSystemTests
    {
        [Test]
        public void SeeMob_UnlocksOnceAndIsIdempotent()
        {
            var codex = new CodexSystem();

            Assert.That(codex.SeeMob(MobKind.Panda), Is.True, "首次遇见 = 新解锁");
            Assert.That(codex.SeeMob(MobKind.Panda), Is.False, "再见不算新解锁");
            Assert.That(codex.IsMobUnlocked(MobKind.Panda), Is.True);
            Assert.That(codex.IsMobUnlocked(MobKind.Fox), Is.False);
        }

        [Test]
        public void UnlockBlock_OnlyWhitelistedEntries()
        {
            var codex = new CodexSystem();

            Assert.That(codex.UnlockBlock("gold_ore"), Is.True);
            Assert.That(codex.UnlockBlock("stone"), Is.False, "白名单外方块（普通石头）不进图鉴");
            Assert.That(codex.UnlockBlock("dirt"), Is.False);
            Assert.That(codex.IsBlockUnlocked("gold_ore"), Is.True);
        }

        [Test]
        public void UnlockItem_DiamondOnly()
        {
            var codex = new CodexSystem();

            Assert.That(codex.UnlockItem(1005), Is.True, "钻石解锁");
            Assert.That(codex.UnlockItem(1004), Is.False, "铁锭不进图鉴");
            Assert.That(codex.IsDiamondUnlocked, Is.True);
        }

        [Test]
        public void OnQuestEvent_KillAndObtain()
        {
            var codex = new CodexSystem();
            codex.OnQuestEvent(new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Creeper });
            codex.OnQuestEvent(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1005, Count = 1 });

            Assert.That(codex.IsMobUnlocked(MobKind.Creeper), Is.True, "击杀 = 见过");
            Assert.That(codex.IsDiamondUnlocked, Is.True, "获得钻石解锁钻石卡");
        }

        [Test]
        public void TotalCount_MatchesEntryTables()
        {
            var codex = new CodexSystem();
            Assert.That(codex.TotalCount, Is.EqualTo(18 + 7 + 1),
                "18 生物 + 7 矿石植物方块 + 1 钻石物品");
        }

        [Test]
        public void Stats_RoundTrip_OnlyCodexKeys()
        {
            var codex = new CodexSystem();
            codex.SeeMob(MobKind.Pig);
            codex.UnlockBlock("fern");

            var sink = new Dictionary<string, int> { { "ach:first-night", 1 } }; // 别人的键共用字典
            codex.ExportStats(sink);

            var restored = new CodexSystem();
            restored.ImportStats(sink);

            Assert.That(restored.IsMobUnlocked(MobKind.Pig), Is.True);
            Assert.That(restored.IsBlockUnlocked("fern"), Is.True);
            Assert.That(restored.UnlockedCount, Is.EqualTo(2), "ach: 前缀键不被图鉴误收");
        }

        [Test]
        public void MobEntries_AllDistinctKinds()
        {
            var kinds = CodexSystem.MobEntries.Select(e => e.Kind).ToArray();
            Assert.That(kinds.Distinct().Count(), Is.EqualTo(kinds.Length), "生物条目不重复");
            Assert.That(CodexSystem.BlockEntries.Select(e => e.BlockId).Distinct().Count(),
                Is.EqualTo(CodexSystem.BlockEntries.Length), "方块条目不重复");
        }
    }
}
