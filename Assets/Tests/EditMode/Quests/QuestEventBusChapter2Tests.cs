#if UNITY_EDITOR
// m11 W2-4：QuestEventBus 第二章接线——睡觉观察（时刻被拉回早晨）、多章节任务书
// （章节顺序解锁 / OnChapterCompleted / Quests 指向活动章）、击杀事件翻译
// （CombatEvents.OnEntityDied → KillKind，生物种类经 MobManager 反查、武器按伤害来源）。
// 依赖 UnityEngine，#if UNITY_EDITOR 包裹只跑 EditMode 链；纯逻辑判定由
// QuestChapter2Tests / QuestCampaignTests 在双链覆盖。
using System.Collections.Generic;
using System.IO;
using MyWorld.Core.Entities;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Quests;
using MyWorld.Core.Time;
using MyWorld.Unity.Combat;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Quests
{
    [TestFixture]
    public class QuestEventBusChapter2Tests
    {
        private static readonly List<string> TempFiles = new List<string>();

        private GameObject _host;
        private PlayerContext _ctx;
        private QuestEventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("QuestBusCh2Host");
            _ctx = _host.AddComponent<PlayerContext>();
            InvokeAwake(_ctx); // EditMode 下 AddComponent 不触发 Awake，手动补一次让单例就位
            _ctx.Inventory = new PlayerInventory();
            _ctx.Health = new Health(20f);
            _ctx.Time = new TimeOfDay();
            _ctx.Experience = new Experience();
            _bus = _host.AddComponent<QuestEventBus>();
        }

        [TearDown]
        public void TearDown()
        {
            // 击杀测试可能经 MobManager 刷出带 MobView 的实体（独立 GameObject），
            // 这里统一清掉，避免 EditMode 场景残留
            foreach (var view in Object.FindObjectsOfType<MobView>())
            {
                Object.DestroyImmediate(view.gameObject);
            }
            foreach (var manager in Object.FindObjectsOfType<MobManager>())
            {
                Object.DestroyImmediate(manager.gameObject);
            }
            if (_host != null) Object.DestroyImmediate(_host);
            foreach (string path in TempFiles)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            TempFiles.Clear();
        }

        private static string WriteChapter(string json)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, json);
            TempFiles.Add(path);
            return path;
        }

        private void BindSingle(string conditionJson)
        {
            _bus.Bind(_ctx, QuestSystem.LoadChapter(WriteChapter($@"[
                {{ ""id"": ""t"", ""name"": ""测试任务"", ""desc"": ""..."",
                  ""condition"": {conditionJson}, ""rewardExp"": 5 }} ]")));
        }

        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        // ─── 睡觉观察：夜里时刻被拉回早晨 = SleepInBed ──────────────────────

        [Test]
        public void 睡觉_夜里跳回早晨_发SleepInBed_且不发SurviveNight()
        {
            BindSingle(@"{ ""type"": ""SleepInBed"" }");

            _ctx.Time.CurrentTick = 15000f; // 入夜
            _bus.WatchNightCrossing();
            _ctx.Time.CurrentTick = 0f; // BedSystem.Sleep 把时间直接置 0
            _bus.WatchNightCrossing();

            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "夜里时刻被拉回早晨应发 SleepInBed 完成任务");
        }

        [Test]
        public void 睡觉跳夜_不会误发SurviveNight()
        {
            BindSingle(@"{ ""type"": ""SurviveNight"" }");

            _ctx.Time.CurrentTick = 22000f;
            _bus.WatchNightCrossing();
            _ctx.Time.CurrentTick = 0f; // 睡觉：curr < prev，跨夜观察按回绕帧不判
            _bus.WatchNightCrossing();

            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0),
                "睡觉跳夜没有自然跨过 23000，不得白发 SurviveNight（活过夜要靠真撑到日出）");
        }

        [Test]
        public void 自然回绕帧_不是夜里起点_不发SleepInBed()
        {
            BindSingle(@"{ ""type"": ""SleepInBed"" }");

            _ctx.Time.CurrentTick = 23999f; // 23000..24000 是清晨：回绕的合法起点
            _bus.WatchNightCrossing();
            _ctx.Time.CurrentTick = 10f; // 24000 回绕归零的自然帧
            _bus.WatchNightCrossing();

            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0), "自然回绕不得误判成睡觉");
        }

        [Test]
        public void 白天推进与倒退_不发SleepInBed()
        {
            BindSingle(@"{ ""type"": ""SleepInBed"" }");

            _ctx.Time.CurrentTick = 6000f;
            _bus.WatchNightCrossing();
            _ctx.Time.CurrentTick = 6500f; // 白天正常推进
            _bus.WatchNightCrossing();
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0));

            _ctx.Time.CurrentTick = 3000f; // 白天时刻倒退（非夜里起点）
            _bus.WatchNightCrossing();
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0), "不在夜里的倒退不是睡觉");
        }

        [Test]
        public void 读档重置基线后_深夜拉回白天不发()
        {
            BindSingle(@"{ ""type"": ""SleepInBed"" }");

            // WorldBootstrap 的真实时序：读档时 ApplyTime 直接赋值跳变时刻 →
            // **跳变之后**才 ResetNightBaseline（基线 = 恢复后的时刻，见其文档
            // 「读档恢复时间后由 WorldBootstrap 调用」）→ 之后每帧 Watch 从恢复值
            // 起步比较，跳变帧对观察者不可见
            _ctx.Time.CurrentTick = 20000f; // 读档前世界已走到深夜
            _ctx.Time.CurrentTick = 6000f;  // TryRestore 的 ApplyTime 跳回存档值（白天）
            _bus.ResetNightBaseline();      // WorldBootstrap 读档后的动作
            _bus.WatchNightCrossing();

            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0), "读档的时间跳变不得误判成睡觉");
        }

        // ─── 多章节任务书：顺序解锁 / 章节钩子 / Quests 指向活动章 ──────────

        /// <summary>两章各一任务：第一章活过夜 → 第二章睡觉。</summary>
        private QuestCampaign BindTwoChapters()
        {
            var campaign = QuestCampaign.Load(
                WriteChapter(@"[
                    { ""id"": ""c1"", ""name"": ""活过一夜"", ""desc"": ""..."",
                      ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 30 } ]"),
                WriteChapter(@"[
                    { ""id"": ""c2"", ""name"": ""在床上睡到天亮"", ""desc"": ""..."",
                      ""condition"": { ""type"": ""SleepInBed"" }, ""rewardExp"": 10 } ]"));
            _bus.Bind(_ctx, campaign);
            return campaign;
        }

        [Test]
        public void 多章节_前章未完成_后章事件Raise无效()
        {
            BindTwoChapters();

            _bus.Raise(new QuestEvent { Type = QuestEventType.SleepInBed });

            Assert.That(_bus.Quests.Current.Id, Is.EqualTo("c1"), "活动章仍是第一章首任务");
            Assert.That(_bus.ActiveChapterIndex, Is.EqualTo(0));
            Assert.That(_bus.ChapterCount, Is.EqualTo(2));
        }

        [Test]
        public void 多章节_前章完成_自动切章_钩子与经验按任务入账()
        {
            BindTwoChapters();

            Quest completed = null;
            int? chapterHook = null;
            _bus.OnQuestCompleted += q => completed = q;
            _bus.OnChapterCompleted += c => chapterHook = c;

            _bus.Raise(new QuestEvent { Type = QuestEventType.SleepInBed });
            Assert.That(completed, Is.Null, "后章事件在前章未完成时无效");
            Assert.That(chapterHook, Is.Null);

            _bus.Raise(new QuestEvent { Type = QuestEventType.SurviveNight }); // 完成 c1 = 第一章全链
            Assert.That(completed, Is.Not.Null);
            Assert.That(completed.Id, Is.EqualTo("c1"));
            Assert.That(chapterHook, Is.EqualTo(0), "第一章完成应触发 OnChapterCompleted(0)");
            Assert.That(_bus.ActiveChapterIndex, Is.EqualTo(1), "活动章切到第二章");
            Assert.That(_bus.Quests.Current.Id, Is.EqualTo("c2"), "Quests 指向新章首任务（HUD 自动跟章）");
            Assert.That(_ctx.Experience.Current, Is.EqualTo(30), "c1 的 30 经验入账");

            _bus.Raise(new QuestEvent { Type = QuestEventType.SleepInBed }); // 新章任务正常推进
            Assert.That(_bus.Quests.Current, Is.Null, "两章走完 Current 为 null");
            Assert.That(chapterHook, Is.EqualTo(1), "末章完成也触发章节钩子（下标 1）");
        }

        // ─── 击杀事件翻译：CombatEvents.OnEntityDied → KillKind ─────────────

        /// <summary>挂一个 MobManager 并刷一只指定生物（击杀反查的数据源）。</summary>
        private Mob SpawnMob(MobKind kind)
        {
            var managerHost = new GameObject("MobManagerHost");
            var manager = managerHost.AddComponent<MobManager>();
            return manager.SpawnMobAt(kind, new Float3(0f, 70f, 0f));
        }

        [Test]
        public void 击杀_近战死亡事件_按生物种类完成KillKind任务()
        {
            BindSingle(@"{ ""type"": ""KillKind"", ""kind"": ""Creeper"", ""count"": 1 }");
            Mob creeper = SpawnMob(MobKind.Creeper);

            // CombatController 近战致死一击发的既有事件（attacker=0 = 玩家）
            _bus.HandleEntityDied(new DamageEvent(
                DamageSource.Melee, 9f, attacker: 0, victim: creeper.EntityId, hit: new Float3(0f, 0f, 0f)));

            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "玩家近战击杀苦力怕应完成 KillKind:Creeper");
        }

        [Test]
        public void 击杀_箭死亡事件算bow_近战满足不了弓限定()
        {
            BindSingle(@"{ ""type"": ""KillKind"", ""kind"": ""Skeleton"", ""weapon"": ""bow"", ""count"": 1 }");
            Mob skeleton = SpawnMob(MobKind.Skeleton);

            _bus.HandleEntityDied(new DamageEvent(
                DamageSource.Melee, 4f, attacker: 0, victim: skeleton.EntityId, hit: new Float3(0f, 0f, 0f)));
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0), "近战击杀不带武器，满足不了 weapon=bow");

            _bus.HandleEntityDied(new DamageEvent(
                DamageSource.Projectile, 4f, attacker: 0, victim: skeleton.EntityId, hit: new Float3(0f, 0f, 0f)));
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "箭（Projectile）击杀翻译成 weapon=bow，应完成");
        }

        [Test]
        public void 击杀_玩家死亡与非玩家击杀_都不翻译()
        {
            BindSingle(@"{ ""type"": ""KillKind"", ""kind"": ""Zombie"", ""count"": 1 }");
            Mob zombie = SpawnMob(MobKind.Zombie);

            _bus.HandleEntityDied(new DamageEvent(
                DamageSource.Melee, 5f, attacker: 3, victim: 0, hit: new Float3(0f, 0f, 0f))); // mob 打死玩家
            _bus.HandleEntityDied(new DamageEvent(
                DamageSource.Melee, 5f, attacker: 3, victim: zombie.EntityId, hit: new Float3(0f, 0f, 0f))); // mob 互殴
            _bus.HandleEntityDied(new DamageEvent(
                DamageSource.Melee, 5f, attacker: 0, victim: 999, hit: new Float3(0f, 0f, 0f))); // victim 不存在

            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(0), "非玩家击杀 / 玩家死亡 / 未知实体都不算");
        }

        // ─── HUD：章节解锁卡与全章完成文案 ────────────────────────────────

        [Test]
        public void HUD_非末章完成_章节解锁卡停留后切新章目标()
        {
            BindTwoChapters();
            float clock = 0f;
            var hud = _host.AddComponent<QuestHudUi>();
            hud.BindForTest(_bus, () => clock);

            _bus.Raise(new QuestEvent { Type = QuestEventType.SurviveNight }); // 完成 c1 = 第一章

            Assert.That(hud.GetHudText(), Is.EqualTo("第 1 章完成 ✓\n第 2 章已解锁"),
                "章节完成的瞬间应弹开章提示卡（覆盖普通打勾卡）");

            clock += 5.1f; // 停留窗口过期
            Assert.That(hud.GetHudText(), Does.StartWith("当前目标：在床上睡到天亮"),
                "5s 后落到新章首任务的普通目标卡");
        }

        [Test]
        public void HUD_末章完成_全部章节文案_单章任务书仍是首章完成()
        {
            BindTwoChapters();
            float clock = 0f;
            var hud = _host.AddComponent<QuestHudUi>();
            hud.BindForTest(_bus, () => clock);

            _bus.Raise(new QuestEvent { Type = QuestEventType.SurviveNight }); // 第一章
            _bus.Raise(new QuestEvent { Type = QuestEventType.SleepInBed }); // 末章（第二章）走完

            Assert.That(hud.GetHudText(), Is.EqualTo("全部章节完成 ✓"),
                "多章节任务书的最后一章完成 = 整本通关");

            // 单章任务书（m6 形态）文案不许漂移——QuestHudUiTests 锁的就是它
            _bus.Bind(_ctx, QuestSystem.LoadChapter(WriteChapter(@"[
                { ""id"": ""only"", ""name"": ""一"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 5 } ]")));
            hud.BindForTest(_bus, () => clock);
            _bus.Raise(new QuestEvent { Type = QuestEventType.SurviveNight });
            Assert.That(hud.GetHudText(), Is.EqualTo("首章完成 ✓"), "单章任务书保持 m6 的既有文案");
        }
    }
}
#endif
