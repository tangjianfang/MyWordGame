// m12 W1：成就系统纯逻辑契约（dotnet / EditMode 双链同源）。
// 进度口径（ObtainItem 覆盖式 / 其余累计式）/ 解锁边沿 / allOthers / Stats 往返。
using System.Collections.Generic;
using MyWorld.Core.Achievements;
using MyWorld.Core.Entities;
using MyWorld.Core.Quests;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Achievements
{
    [TestFixture]
    public class AchievementSystemTests
    {
        private static Achievement Def(string id, QuestEventType evt, int count = 1,
            int itemId = 0, MobKind? kind = null, string weapon = null, string special = null)
            => new Achievement
            {
                Id = id, Name = id, Desc = "", Icon = "badge-" + id,
                Event = evt, RequiredCount = count, ItemId = itemId,
                Kind = kind, Weapon = weapon, Special = special,
            };

        [Test]
        public void CumulativeEvent_CountsUpAndUnlocksAtEdge()
        {
            var sys = new AchievementSystem(new List<Achievement> { Def("slayer", QuestEventType.KillKind, 3) });

            sys.OnEvent(new QuestEvent { Type = QuestEventType.KillKind, Count = 1 });
            sys.OnEvent(new QuestEvent { Type = QuestEventType.KillKind, Count = 1 });
            Assert.That(sys.UnlockedCount, Is.EqualTo(0), "2/3 未解锁");

            int fired = 0;
            sys.Unlocked += _ => fired++;
            sys.OnEvent(new QuestEvent { Type = QuestEventType.KillKind, Count = 1 });

            Assert.That(sys.UnlockedCount, Is.EqualTo(1), "3/3 解锁");
            Assert.That(fired, Is.EqualTo(1), "解锁事件恰好发一次（边沿）");
        }

        [Test]
        public void ObtainItem_IsMaxNotAccumulate()
        {
            // ObtainItem 事件 Count 携带背包现存量：覆盖式取 max（与 QuestSystem 同口径）
            var sys = new AchievementSystem(new List<Achievement> { Def("iron", QuestEventType.ObtainItem, 1, itemId: 1004) });

            sys.OnEvent(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1004, Count = 5 });
            sys.OnEvent(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1004, Count = 3 });

            Assert.That(sys.UnlockedCount, Is.EqualTo(1), "max(5,3)=5 ≥ 1：拾取后丢几个也不取消成就");
        }

        [Test]
        public void ItemIdFilter_OtherItemsIgnored()
        {
            var sys = new AchievementSystem(new List<Achievement> { Def("diamond", QuestEventType.ObtainItem, 1, itemId: 1005) });

            sys.OnEvent(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1004, Count = 99 });

            Assert.That(sys.UnlockedCount, Is.EqualTo(0), "铁锭不计入钻石成就");
        }

        [Test]
        public void KindAndWeaponFilters()
        {
            var sys = new AchievementSystem(new List<Achievement>
            {
                Def("sniper", QuestEventType.KillKind, 1, kind: MobKind.Skeleton, weapon: "bow"),
                Def("anykill", QuestEventType.KillKind, 2),
            });

            sys.OnEvent(new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Skeleton, Weapon = "bow" });
            sys.OnEvent(new QuestEvent { Type = QuestEventType.KillKind, Kind = MobKind.Zombie });

            Assert.That(sys.ProgressOf(sys.All[0]), Is.EqualTo(1), "弓杀骷髅命中狙骷髅成就");
            Assert.That(sys.ProgressOf(sys.All[1]), Is.EqualTo(2), "任意击杀累计两种");
            Assert.That(sys.UnlockedCount, Is.EqualTo(2));
        }

        [Test]
        public void AllOthers_UnlocksOnlyWhenRestComplete()
        {
            var sys = new AchievementSystem(new List<Achievement>
            {
                Def("a", QuestEventType.SurviveNight),
                Def("b", QuestEventType.SleepInBed),
                Def("done", QuestEventType.SleepInBed, 1, special: "allOthers"),
            });

            sys.OnEvent(new QuestEvent { Type = QuestEventType.SurviveNight });
            Assert.That(sys.UnlockedCount, Is.EqualTo(1), "只完成 a 时 allOthers 不解锁");

            int fired = 0;
            sys.Unlocked += _ => fired++;
            sys.OnEvent(new QuestEvent { Type = QuestEventType.SleepInBed });

            Assert.That(sys.UnlockedCount, Is.EqualTo(3), "a+b 齐 → allOthers 连锁解锁");
            Assert.That(fired, Is.EqualTo(2), "b 与 allOthers 同帧各发一次");
        }

        [Test]
        public void Stats_RoundTrip()
        {
            var sys = new AchievementSystem(new List<Achievement> { Def("x", QuestEventType.HarvestCrop, 20) });
            sys.OnEvent(new QuestEvent { Type = QuestEventType.HarvestCrop, Count = 7 });

            var sink = new Dictionary<string, int>();
            sys.ExportStats(sink);
            Assert.That(sink["ach:x"], Is.EqualTo(7));

            var restored = new AchievementSystem(new List<Achievement> { Def("x", QuestEventType.HarvestCrop, 20) });
            restored.ImportStats(sink);
            Assert.That(restored.ProgressOf(restored.All[0]), Is.EqualTo(7), "读档续上进度");
            Assert.That(restored.UnlockedCount, Is.EqualTo(0), "7/20 仍未解锁");
        }

        [Test]
        public void ImportStats_UnknownKeysIgnored()
        {
            var sys = new AchievementSystem(new List<Achievement> { Def("y", QuestEventType.SmeltItem, 5) });
            sys.ImportStats(new Dictionary<string, int> { { "ach:ghost", 9 }, { "codex:mob:Pig", 1 } });
            Assert.That(sys.UnlockedCount, Is.EqualTo(0), "不认识的键不产生成就");
        }

        [Test]
        public void Database_FromJson_ParsesFullDefinition()
        {
            const string json = @"{ ""achievements"": [
                { ""id"": ""first-night"", ""name"": ""初夜生存"", ""desc"": ""d"",
                  ""icon"": ""badge-first-night"", ""event"": ""SurviveNight"", ""count"": 1 },
                { ""id"": ""sniper"", ""event"": ""KillKind"", ""kind"": ""Skeleton"",
                  ""weapon"": ""bow"", ""count"": 2 } ] }";

            var list = AchievementDatabase.FromJson(json);

            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0].Name, Is.EqualTo("初夜生存"));
            Assert.That(list[1].Event, Is.EqualTo(QuestEventType.KillKind));
            Assert.That(list[1].Kind, Is.EqualTo(MobKind.Skeleton));
            Assert.That(list[1].Weapon, Is.EqualTo("bow"));
            Assert.That(list[1].RequiredCount, Is.EqualTo(2));
        }
    }
}
