#if UNITY_EDITOR
// m6 C3：QuestHudUi——右上角当前目标卡的文本状态机 + 总线的进度查询接口。
// 依赖 UnityEngine（MonoBehaviour / GameObject / Time），#if UNITY_EDITOR 包裹只跑 EditMode 链；
// GetHudText 是纯文本计算（不碰 GUI 上下文），测试在 OnGUI 之外直接断言。
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MyWorld.Core.Entities;
using MyWorld.Core.Player;
using MyWorld.Core.Quests;
using MyWorld.Core.Time;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyWorld.Core.Tests.Quests
{
    /// <summary>
    /// 覆盖三组行为：
    /// 1) <see cref="QuestEventBus.QuestProgress"/>：ObtainItem = 最近事件现存量（覆盖不累计）、
    ///    CraftItem/SmeltItem = 事件 Count 累计、非匹配事件不动进度、完成后进度清零；
    /// 2) <see cref="QuestHudUi.GetHudText"/>：无链 / 无任务返 null、任务中含任务名与「分子/分母」；
    /// 3) 完成切换时序：完成瞬间打勾（含刚完成的任务名）、注入时间推过 1s 切下一任务、
    ///    全链完成显示「首章完成 ✓」、5s 后隐藏。
    /// </summary>
    [TestFixture]
    public class QuestHudUiTests
    {
        private static readonly List<string> TempFiles = new List<string>();

        private GameObject _host;
        private QuestEventBus _bus;
        private QuestHudUi _hud;
        private float _now; // 注入给 HUD 的时钟（EditMode 不跑 Update，测试手动步进）

        /// <summary>默认测试链：q1 拾取原木 → q2 合成木板。</summary>
        private const string DefaultChain = @"[
            { ""id"": ""q1"", ""name"": ""挖一根原木"", ""desc"": ""..."",
              ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1000, ""count"": 1 }, ""rewardExp"": 5 },
            { ""id"": ""q2"", ""name"": ""合成木板"", ""desc"": ""..."",
              ""condition"": { ""type"": ""CraftItem"", ""itemId"": 1001, ""count"": 4 }, ""rewardExp"": 5 } ]";

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("QuestHudHost");
            var ctx = _host.AddComponent<PlayerContext>();
            ctx.Inventory = new PlayerInventory();
            ctx.Health = new Health(20f);
            ctx.Time = new TimeOfDay();
            ctx.Experience = new Experience();
            _bus = _host.AddComponent<QuestEventBus>();
            _bus.Bind(ctx, QuestSystem.LoadChapter(WriteChapter(DefaultChain)));

            _now = 100f;
            _hud = _host.AddComponent<QuestHudUi>();
            _hud.BindForTest(_bus, () => _now);
        }

        [TearDown]
        public void TearDown()
        {
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

        // ─── 1) QuestProgress：进度查询接口 ─────────────────────────────────

        [Test]
        public void 进度_未发事件时为0_分母取条件RequiredCount()
        {
            Assert.That(_bus.QuestProgress().Progress, Is.EqualTo(0), "还没拾取过，分子应为 0");
            Assert.That(_bus.QuestProgress().Required, Is.EqualTo(1), "q1 要 1 根原木，分母应为 1");
        }

        [Test]
        public void 进度_ObtainItem按现存量覆盖_不累计()
        {
            // 要求 5 根（事件 1 → 3 都完不成）：覆盖口径应显示 3，若误按累计会变成 4
            string path = WriteChapter(@"[
                { ""id"": ""q5"", ""name"": ""囤五根原木"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1000, ""count"": 5 }, ""rewardExp"": 5 } ]");
            _bus.Bind(null, QuestSystem.LoadChapter(path));

            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 });
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 3 });

            Assert.That(_bus.QuestProgress().Progress, Is.EqualTo(3),
                "ObtainItem 口径是「背包现存量」，最近一次事件的 Count 直接覆盖（不累计）");
        }

        [Test]
        public void 进度_CraftItem按事件Count累计()
        {
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 }); // 完成 q1，切 q2

            _bus.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 1 });
            _bus.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 2 });

            Assert.That(_bus.QuestProgress().Required, Is.EqualTo(4), "q2 要 4 块木板");
            Assert.That(_bus.QuestProgress().Progress, Is.EqualTo(3), "CraftItem 是「本次产出数量」，累计 1+2=3");
        }

        [Test]
        public void 进度_物品或类型不匹配的事件不动进度()
        {
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 2000, Count = 9 }); // 别的物品
            _bus.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1000, Count = 8 });  // 类型不对

            Assert.That(_bus.QuestProgress().Progress, Is.EqualTo(0), "与当前条件不匹配的事件不应计入进度");
        }

        [Test]
        public void 进度_任务完成后清零_为下一任务重新累计()
        {
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 5 }); // 完成 q1
            Assert.That(_bus.Quests.Current.Id, Is.EqualTo("q2"), "前置：q1 已完成");

            Assert.That(_bus.QuestProgress().Progress, Is.EqualTo(0), "切到 q2 后进度应从 0 重新累计");
            Assert.That(_bus.QuestProgress().Required, Is.EqualTo(4), "分母应换成 q2 的 4");
        }

        [Test]
        public void 进度_无链或全链完成时返回零()
        {
            // 强转消歧（CS0121）：null 显式走 Bind(PlayerContext, QuestCampaign) 重载
            _bus.Bind(null, (QuestCampaign)null);
            Assert.That(_bus.QuestProgress().Progress, Is.EqualTo(0), "无链（Quests=null）时安全返回 0");
            Assert.That(_bus.QuestProgress().Required, Is.EqualTo(0), "无链时分母也是 0");

            _bus.Bind(null, QuestSystem.LoadChapter(WriteChapter(DefaultChain)));
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 }); // q1
            _bus.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 }); // q2 → 全链完成
            Assert.That(_bus.Quests.Current, Is.Null, "前置：链走完");
            Assert.That(_bus.QuestProgress().Progress, Is.EqualTo(0), "全链完成后无当前任务，进度归零");
        }

        [Test]
        public void 进度_SurviveNight任务_分母为1分子恒0()
        {
            string path = WriteChapter(@"[
                { ""id"": ""n1"", ""name"": ""活过一夜"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 30 } ]");
            _bus.Bind(null, QuestSystem.LoadChapter(path));

            Assert.That(_bus.QuestProgress().Required, Is.EqualTo(1),
                "SurviveNight 的 RequiredCount 由加载器规范化为 1（HUD 显示 0/1）");
            Assert.That(_bus.QuestProgress().Progress, Is.EqualTo(0), "跨夜事件到达即完成，没有中间进度");
        }

        // ─── 2) GetHudText：各状态的文本 ────────────────────────────────────

        [Test]
        public void 文本_未绑定总线_返回null()
        {
            var orphan = new GameObject("OrphanHud").AddComponent<QuestHudUi>();
            try
            {
                Assert.That(orphan.GetHudText(), Is.Null, "没挂总线（早期场景）时不应显示目标卡");
            }
            finally
            {
                Object.DestroyImmediate(orphan.gameObject);
            }
        }

        [Test]
        public void 文本_总线无链_返回null()
        {
            _bus.Bind(null, (QuestCampaign)null); // 链文件缺失 / 加载失败的形态（强转消歧 CS0121）
            Assert.That(_hud.GetHudText(), Is.Null, "无任务链时右上角不画卡");
        }

        [Test]
        public void 文本_任务中_含当前目标前缀任务名与进度分母()
        {
            string text = _hud.GetHudText();

            Assert.That(text, Is.Not.Null, "任务中应有目标卡文本");
            Assert.That(text, Does.Contain("当前目标"), "固定前缀「当前目标：」让孩子一眼知道这是目标卡");
            Assert.That(text, Does.Contain("挖一根原木"), "应显示当前任务名");
            Assert.That(text, Does.Contain("0/1"), "应显示「分子/分母」进度（还没拾取 = 0/1）");
        }

        [Test]
        public void 文本_任务中_含本章已完成数x分之n()
        {
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 }); // 完成 q1
            _now += QuestHudUi.CompletedHoldSeconds; // 打勾卡过期，切到 q2 的普通目标卡

            string text = _hud.GetHudText();
            Assert.That(text, Does.Contain("合成木板"), "q1 完成切换后应显示 q2 任务名");
            Assert.That(text, Does.Contain("已完成 1/2"), "本章共 2 个任务已完成 1 个 → 显示「已完成 1/2」");
            Assert.That(text, Does.Contain("0/4"), "q2 进度从 0/4 重新累计");
        }

        [Test]
        public void 文本_进度随事件刷新()
        {
            string path = WriteChapter(@"[
                { ""id"": ""q5"", ""name"": ""挖五根原木"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1000, ""count"": 5 }, ""rewardExp"": 5 } ]");
            _bus.Bind(null, QuestSystem.LoadChapter(path));

            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 2 });
            Assert.That(_hud.GetHudText(), Does.Contain("2/5"), "现存量 2 应显示 2/5");

            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 4 });
            Assert.That(_hud.GetHudText(), Does.Contain("4/5"), "现存量涨到 4 应显示 4/5（还没到 5 不完成）");
        }

        // ─── 3) 完成切换时序（注入时钟步进） ────────────────────────────────

        [Test]
        public void 完成_瞬间打勾显示刚完成的任务名_1s内不切换()
        {
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 }); // 完成 q1
            _now += 0.5f; // 完成后 0.5s：还在打勾停留窗口内

            string text = _hud.GetHudText();
            Assert.That(text, Is.Not.Null, "完成瞬间目标卡仍在");
            Assert.That(text, Does.Contain("✓"), "完成瞬间应打勾");
            Assert.That(text, Does.Contain("挖一根原木"), "打勾期间显示的是刚完成的任务（不是下一个）");
            Assert.That(text, Does.Not.Contain("当前目标"), "打勾卡不是普通目标卡，不带「当前目标」前缀");
        }

        [Test]
        public void 完成_推过1s后切换显示下一任务()
        {
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 }); // 完成 q1

            _now += QuestHudUi.CompletedHoldSeconds; // 推过停留时长

            string text = _hud.GetHudText();
            Assert.That(text, Does.Contain("当前目标"), "1s 后回到普通目标卡形态");
            Assert.That(text, Does.Contain("合成木板"), "应切到下一任务 q2");
            Assert.That(text, Does.Not.Contain("✓"), "打勾应消失");
        }

        [Test]
        public void 完成_打勾卡未过期时下一任务又完成_新打勾覆盖旧打勾()
        {
            // 链上三个任务都设 SurviveNight：第二夜完成时链还没走完（还有 n3），
            // 第一张打勾卡也没过期——新打勾卡应接管显示
            string path = WriteChapter(@"[
                { ""id"": ""n1"", ""name"": ""一夜一"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 5 },
                { ""id"": ""n2"", ""name"": ""一夜二"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 5 },
                { ""id"": ""n3"", ""name"": ""一夜三"", ""desc"": ""..."",
                  ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 5 } ]");
            _bus.Bind(null, QuestSystem.LoadChapter(path));

            _bus.Raise(new QuestEvent { Type = QuestEventType.SurviveNight });
            _now += 0.2f; // 第一张打勾卡仍在窗口内
            _bus.Raise(new QuestEvent { Type = QuestEventType.SurviveNight });

            string text = _hud.GetHudText();
            Assert.That(text, Does.Contain("一夜二"), "第二次完成应显示新完成的任务名");
            Assert.That(text, Does.Contain("✓"), "仍是打勾卡形态（链没走完，不是首章完成卡）");
        }

        [Test]
        public void 全链完成_显示首章完成卡_5s内不隐藏()
        {
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 }); // q1
            _now += QuestHudUi.CompletedHoldSeconds; // q1 打勾过期
            _bus.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 }); // q2 → 全链完

            _now += 2f; // 首章完成卡 5s 窗口内
            string text = _hud.GetHudText();
            Assert.That(text, Does.Contain("首章完成"), "全链完成后应显示「首章完成」卡片");
            Assert.That(text, Does.Contain("✓"), "完成卡带勾");
        }

        [Test]
        public void 全链完成_推过5s后隐藏_文本返回null()
        {
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 });
            _now += QuestHudUi.CompletedHoldSeconds;
            _bus.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 }); // 全链完

            _now += QuestHudUi.ChapterDoneHoldSeconds; // 推过 5s

            Assert.That(_hud.GetHudText(), Is.Null, "首章完成卡 5s 后应隐藏（无当前任务 = null）");
        }

        [Test]
        public void 全链完成_上一张打勾卡还在窗口内时链就走完_直接进完成卡()
        {
            // q1 完成后不到 1s 就完成 q2（打勾卡未过期）：全链完成态优先，显示首章完成卡
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 });
            _bus.Raise(new QuestEvent { Type = QuestEventType.CraftItem, ItemId = 1001, Count = 4 });

            Assert.That(_hud.GetHudText(), Does.Contain("首章完成"), "链走完时应优先显示首章完成卡（盖过未过期的打勾卡）");
        }

        [Test]
        public void 销毁组件_摘掉完成钩子订阅_总线照常推进()
        {
            var field = typeof(QuestEventBus).GetField("OnQuestCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "QuestEventBus 应有 OnQuestCompleted 事件");
            Assert.That(SubscriberCount(field, _bus), Is.EqualTo(1), "Bind 后 HUD 应订阅完成钩子");

            // EditMode 下没挂 [ExecuteAlways] 的组件 DestroyImmediate 不回调 OnDestroy，
            // 与 Awake 同款处理：反射显式调一次（运行时由 Unity 在销毁/换场景时自动调）
            var onDestroy = typeof(QuestHudUi).GetMethod("OnDestroy",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onDestroy, Is.Not.Null, "QuestHudUi 应有私有 OnDestroy");
            onDestroy.Invoke(_hud, null);
            Object.DestroyImmediate(_hud);

            Assert.That(SubscriberCount(field, _bus), Is.EqualTo(0),
                "HUD 销毁时必须摘掉订阅（否则总线持有死引用，切换场景时回调打到已销毁组件）");
            Assert.DoesNotThrow(() => _bus.Raise(
                new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1000, Count = 1 }),
                "HUD 销毁后总线完成事件不应再打到它");
            Assert.That(_bus.Quests.CompletedCount, Is.EqualTo(1), "总线推进不受 HUD 生死影响");
        }

        private static int SubscriberCount(FieldInfo field, QuestEventBus bus)
        {
            var del = (System.Action<Quest>)field.GetValue(bus);
            return del == null ? 0 : del.GetInvocationList().Length;
        }
    }
}
#endif
