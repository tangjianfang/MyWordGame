#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MyWorld.Core.Blocks;
using MyWorld.Core.Entities;
using MyWorld.Core.Persistence;
using MyWorld.Core.Player;
using MyWorld.Core.Quests;
using MyWorld.Core.Time;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Persistence;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m6 B3：H 键帮助菜单的开关 / 按键路由 / 保存退出契约。
    /// InputLocked 静态门由 <see cref="HelpMenuUi"/> 的开关维护，
    /// <see cref="BlockInteraction.Update"/> 开头早退——这里测门的翻转，不拉起完整交互链路。
    /// <para>
    /// m8 B1：PlayerPrefs 三键 round-trip / 默认值 / Awake 读回的用例已随三滑条
    /// 平移进 <see cref="SettingsPanelUiTests"/>（面板抽成公共组件，断言原样照跑），
    /// 这里换上一条「设置页嵌入公共面板」的接线守卫。
    /// </para>
    /// <para>
    /// m6 C5 追加：<see cref="HelpMenuUi.GetProgressSummary"/> 纯函数各状态——
    /// 「怎么玩」页的当前目标全文 + 全链 8 格进度条全部从这一个快照取数，
    /// 不自己记账（单一真源是总线挂的 <see cref="QuestSystem"/>）。
    /// </para>
    /// <para>
    /// m7 A4 追加：「保存并退出」按钮契约——点击即 <c>SaveNow(async:false)</c> 同步落盘
    /// （注入真实 SaveLoadService 断言 level.dat 立即存在）、半秒停留窗后才触发退出
    /// （<see cref="HelpMenuUi.QuitRequested"/> 注入计数器、时钟注入步进，不真退测试进程）。
    /// </para>
    /// <para>
    /// m7 A4 fix1 追加：保存失败（假保存抛异常 / 真实 IO 失败使 SaveNow 返回 false）
    /// 绝不进入退出流程——按钮保持可点可重试，红字提示带原因与 Alt+F4 退路；
    /// 重试成功后清失败提示、照常半秒退出。
    /// </para>
    /// </summary>
    [TestFixture]
    public class HelpMenuUiTests
    {
        private static readonly List<string> TempFiles = new List<string>();

        /// <summary>挂任务总线用的宿主（C5 测试按需创建，无总线用例不建）。</summary>
        private GameObject _questHost;
        private QuestEventBus _bus;

        /// <summary>m7 A4 保存并退出用例的存档根目录（SetUp 建、TearDown 删，互不串档）。</summary>
        private string _saveRoot;

        /// <summary>
        /// 默认测试链（8 个任务，与真实首章等长）：q1–q3、q5–q8 是 SurviveNight
        /// （一次跨夜事件即完成，便于任意推进），q4 是 ObtainItem×5（留一个数值条件
        /// 断言「进度 x/y」的分子分母）。
        /// </summary>
        private const string Chain8 = @"[
            { ""id"": ""q1"", ""name"": ""挖一根原木"", ""desc"": ""对着树干按住左键，挖下 1 根原木"",
              ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 5 },
            { ""id"": ""q2"", ""name"": ""合成木板"", ""desc"": ""按 B 打开口袋合成，放 1 根原木进去"",
              ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 5 },
            { ""id"": ""q3"", ""name"": ""造工作台"", ""desc"": ""把 4 块木板放进合成格"",
              ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 10 },
            { ""id"": ""q4"", ""name"": ""挖三块石头"", ""desc"": ""拿着木镐去挖石头"",
              ""condition"": { ""type"": ""ObtainItem"", ""itemId"": 1003, ""count"": 5 }, ""rewardExp"": 10 },
            { ""id"": ""q5"", ""name"": ""造石镐"", ""desc"": ""3 块圆石 + 2 根木棍"",
              ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 15 },
            { ""id"": ""q6"", ""name"": ""炼一根铁锭"", ""desc"": ""投入圆石和煤，烧出铁锭"",
              ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 20 },
            { ""id"": ""q7"", ""name"": ""做火把"", ""desc"": ""照亮夜路"",
              ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 10 },
            { ""id"": ""q8"", ""name"": ""活过一夜"", ""desc"": ""撑到日出"",
              ""condition"": { ""type"": ""SurviveNight"" }, ""rewardExp"": 30 } ]";

        /// <summary>EditMode 下 AddComponent 不会自动触发 MonoBehaviour.Awake
        /// （Unity 仅在 PlayMode / 场景加载时回调），用反射显式调用私有 Awake。</summary>
        private static void InvokeAwake(MonoBehaviour mb)
        {
            var method = mb.GetType().GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 Awake");
            method.Invoke(mb, null);
        }

        /// <summary>建一个挂了总线 + 指定任务链的宿主（Bind 会把
        /// <see cref="QuestEventBus.Instance"/> 指过去，HelpMenuUi 从 Instance 读）。</summary>
        private void BindChain(string json)
        {
            _questHost = new GameObject("HelpMenuQuestBus");
            _bus = _questHost.AddComponent<QuestEventBus>();
            string path = Path.GetTempFileName();
            File.WriteAllText(path, json);
            TempFiles.Add(path);
            _bus.Bind(null, QuestSystem.LoadChapter(path));
        }

        private static void RaiseNight(QuestEventBus bus)
        {
            bus.Raise(new QuestEvent { Type = QuestEventType.SurviveNight });
        }

        private static HelpMenuUi NewMenu()
        {
            var go = new GameObject("HelpMenu");
            var ui = go.AddComponent<HelpMenuUi>();
            InvokeAwake(ui);
            return ui;
        }

        [SetUp]
        public void SetUp()
        {
            // 三滑条 / PlayerPrefs 三键的用例 m8 B1 平移进 SettingsPanelUiTests（键也归它清），
            // 本夹具只测菜单开关 / 任务进度 / 保存退出
            BlockInteraction.InputLocked = false;
            // m6 终审修 C1：Toggle/HandleKey 现在也登记 UiCursorGate，静态门跨夹具清一次
            UiCursorGate.Reset();
            _saveRoot = Path.Combine(Application.temporaryCachePath,
                $"helpmenu-quit-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            BlockInteraction.InputLocked = false;
            UiCursorGate.Reset();
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
            // EditMode 下 DestroyImmediate 不回调 OnDestroy（无 [ExecuteAlways]），
            // Instance 会残留指向已销毁组件的引用——GetProgressSummary 的 Unity 判空兜底，但别污染别的夹具
            if (_questHost != null) Object.DestroyImmediate(_questHost);
            foreach (string path in TempFiles)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            TempFiles.Clear();
        }

        [Test]
        public void Toggle_FlipsOpenStateAndInputLocked()
        {
            var ui = NewMenu();
            try
            {
                Assert.That(ui.IsOpen, Is.False, "初始应为关闭");
                Assert.That(BlockInteraction.InputLocked, Is.False, "关闭时不应锁挖/放输入");

                ui.Toggle();
                Assert.That(ui.IsOpen, Is.True, "Toggle 一次后打开");
                Assert.That(BlockInteraction.InputLocked, Is.True, "打开时必须锁住挖/放输入");

                ui.Toggle();
                Assert.That(ui.IsOpen, Is.False, "再 Toggle 回到关闭");
                Assert.That(BlockInteraction.InputLocked, Is.False, "关闭时解锁挖/放输入");
            }
            finally
            {
                Object.DestroyImmediate(ui.gameObject);
            }
        }

        [Test]
        public void HandleKey_H_Toggles_EscapeClosesOnlyWhenOpen()
        {
            var ui = NewMenu();
            try
            {
                ui.HandleKey(KeyCode.H);
                Assert.That(ui.IsOpen, Is.True, "H 应打开帮助菜单");

                ui.HandleKey(KeyCode.Escape);
                Assert.That(ui.IsOpen, Is.False, "打开状态下 Esc 应关闭");

                ui.HandleKey(KeyCode.Escape);
                Assert.That(ui.IsOpen, Is.False, "关闭状态下 Esc 不应打开（不误触）");

                ui.HandleKey(KeyCode.H);
                Assert.That(ui.IsOpen, Is.True, "H 再按一次重新打开");
                ui.HandleKey(KeyCode.H);
                Assert.That(ui.IsOpen, Is.False, "H 也能关闭（H 或 Esc 关）");
            }
            finally
            {
                Object.DestroyImmediate(ui.gameObject);
            }
        }

        [Test]
        public void 设置页_嵌入公共面板_Awake后同物体挂SettingsPanelUi()
        {
            // m8 B1：三滑条抽到公共 SettingsPanelUi，HelpMenuUi 改嵌入不再内联——
            // Awake 必须保证面板就位（同物体懒挂），否则设置页 OnGUI 会空引用
            var ui = NewMenu();
            try
            {
                Assert.That(ui.SettingsPanel, Is.Not.Null,
                    "HelpMenuUi 应在 Awake 嵌入公共设置面板（m8 B1）");
                Assert.That(ui.SettingsPanel.transform, Is.EqualTo(ui.transform),
                    "面板挂同一物体——旧场景里已存的 HelpMenuUi 无需重存");
            }
            finally
            {
                Object.DestroyImmediate(ui.gameObject);
            }
        }

        // ─── m6 C5：GetProgressSummary 纯函数（进度页取数单一入口） ─────────

        [Test]
        public void 进度_全新链_计数0of8_当前任务全文可读()
        {
            BindChain(Chain8);

            QuestProgressSummary s = HelpMenuUi.GetProgressSummary();

            Assert.That(s.HasChain, Is.True, "挂了链应有任务数据");
            Assert.That(s.CountText, Is.EqualTo("0/8"), "全新链计数 0/8");
            Assert.That(s.CompletedIds, Is.Empty, "还没完成任何任务");
            Assert.That(s.CurrentQuestId, Is.EqualTo("q1"), "链式解锁从第一个开始");
            Assert.That(s.CurrentQuestName, Is.EqualTo("挖一根原木"), "「怎么玩」页要显示任务名");
            Assert.That(s.CurrentQuestDesc, Does.Contain("左键"), "要显示任务描述全文（操作指引）");
            Assert.That(s.CurrentProgress, Is.EqualTo(0), "还没发过事件，分子 0");
            Assert.That(s.CurrentRequired, Is.EqualTo(1), "SurviveNight 分母由加载器规范化为 1");
            Assert.That(s.ChainComplete, Is.False);

            Assert.That(s.GetSlotState(0), Is.EqualTo(QuestSlotState.Current), "第 0 格是当前任务（亮白描边）");
            for (int i = 1; i < 8; i++)
            {
                Assert.That(s.GetSlotState(i), Is.EqualTo(QuestSlotState.Locked),
                    "第 " + i + " 格还没轮到，应为暗灰锁定态");
            }
        }

        [Test]
        public void 进度_完成3个_计数3of8_完成id集合与当前进度分子()
        {
            BindChain(Chain8);
            RaiseNight(_bus); // q1
            RaiseNight(_bus); // q2
            RaiseNight(_bus); // q3
            // q4 是数值条件：背包现存量 2 / 要求 5（覆盖口径）
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1003, Count = 2 });

            QuestProgressSummary s = HelpMenuUi.GetProgressSummary();

            Assert.That(s.CountText, Is.EqualTo("3/8"), "完成 3 个应显示 3/8");
            Assert.That(s.CompletedIds, Is.EqualTo(new[] { "q1", "q2", "q3" }),
                "完成 id 集合按解锁顺序收集（前 3 格亮金）");
            Assert.That(s.CurrentQuestId, Is.EqualTo("q4"), "当前切到第 4 个任务");
            Assert.That(s.CurrentQuestName, Is.EqualTo("挖三块石头"));
            Assert.That(s.CurrentProgress, Is.EqualTo(2), "进度分子来自 QuestSystem 单一真源");
            Assert.That(s.CurrentRequired, Is.EqualTo(5), "分母取条件 RequiredCount");
            Assert.That(s.ChainComplete, Is.False);

            for (int i = 0; i < 3; i++)
            {
                Assert.That(s.GetSlotState(i), Is.EqualTo(QuestSlotState.Completed),
                    "第 " + i + " 格已完成，应亮金");
            }
            Assert.That(s.GetSlotState(3), Is.EqualTo(QuestSlotState.Current), "第 3 格是当前任务");
            for (int i = 4; i < 8; i++)
            {
                Assert.That(s.GetSlotState(i), Is.EqualTo(QuestSlotState.Locked),
                    "第 " + i + " 格未解锁，应暗灰");
            }
        }

        [Test]
        public void 进度_全链完成_8of8_无当前任务全格亮金()
        {
            BindChain(Chain8);
            RaiseNight(_bus); // q1
            RaiseNight(_bus); // q2
            RaiseNight(_bus); // q3
            _bus.Raise(new QuestEvent { Type = QuestEventType.ObtainItem, ItemId = 1003, Count = 5 }); // q4
            RaiseNight(_bus); // q5
            RaiseNight(_bus); // q6
            RaiseNight(_bus); // q7
            RaiseNight(_bus); // q8 → 全链完成

            QuestProgressSummary s = HelpMenuUi.GetProgressSummary();

            Assert.That(s.CountText, Is.EqualTo("8/8"), "全链完成计数 8/8");
            Assert.That(s.CompletedIds.Count, Is.EqualTo(8), "8 个 id 全部进完成集合");
            Assert.That(s.CompletedIds[7], Is.EqualTo("q8"));
            Assert.That(s.CurrentQuestId, Is.Null, "全链完成后无当前任务");
            Assert.That(s.CurrentQuestName, Is.Null);
            Assert.That(s.CurrentQuestDesc, Is.Null);
            Assert.That(s.CurrentProgress, Is.EqualTo(0), "全链完成后进度归零");
            Assert.That(s.CurrentRequired, Is.EqualTo(0));
            Assert.That(s.ChainComplete, Is.True, "ChainComplete 供进度页显示「首章完成」");
            for (int i = 0; i < 8; i++)
            {
                Assert.That(s.GetSlotState(i), Is.EqualTo(QuestSlotState.Completed),
                    "第 " + i + " 格全链完成后都应亮金");
            }
        }

        [Test]
        public void 进度_无总线_安全返回空快照不抛异常()
        {
            // 先建再销毁：模拟场景卸载后 Instance 残留指向已销毁组件的形态
            // （EditMode 下 DestroyImmediate 不回调 OnDestroy 清 Instance）
            BindChain(Chain8);
            Object.DestroyImmediate(_questHost);
            _questHost = null;

            QuestProgressSummary s = HelpMenuUi.GetProgressSummary();

            Assert.That(s, Is.Not.Null, "无总线必须返回空快照而不是抛异常（早期场景 H 键照常能开）");
            Assert.That(s.HasChain, Is.False);
            Assert.That(s.TotalCount, Is.EqualTo(0));
            Assert.That(s.CountText, Is.EqualTo("0/0"));
            Assert.That(s.CompletedIds, Is.Empty);
            Assert.That(s.CurrentQuestId, Is.Null);
            Assert.That(s.CurrentQuestName, Is.Null);
            Assert.That(s.ChainComplete, Is.False, "无链 ≠ 全链完成，进度页应显示占位而不是「首章完成」");
        }

        [Test]
        public void 进度_总线无链_链文件缺失形态_安全返回空快照()
        {
            BindChain(Chain8);
            _bus.Bind(null, null); // 链文件缺失 / 加载失败：总线在但 Quests 为 null（C2 语义）

            QuestProgressSummary s = HelpMenuUi.GetProgressSummary();

            Assert.That(s.HasChain, Is.False, "总线活着但没链，同样返回空快照");
            Assert.That(s.CountText, Is.EqualTo("0/0"));
            Assert.That(s.CompletedIds, Is.Empty);
            Assert.That(s.CurrentQuestId, Is.Null);
            Assert.That(s.ChainComplete, Is.False);
        }

        [Test]
        public void 进度_真实chapter1链_共8格_首任务是挖一根原木()
        {
            BindChain(File.ReadAllText(Path.Combine(
                Application.streamingAssetsPath, "quests", "chapter1.json")));

            QuestProgressSummary s = HelpMenuUi.GetProgressSummary();

            Assert.That(s.TotalCount, Is.EqualTo(8), "设计定的首章 8 步 → 进度页画 8 格");
            Assert.That(s.CountText, Is.EqualTo("0/8"));
            Assert.That(s.CurrentQuestId, Is.EqualTo("ch1_01_punch_log"));
            Assert.That(s.CurrentQuestName, Is.EqualTo("挖一根原木"));
            Assert.That(s.CurrentQuestDesc, Does.Contain("左键"), "真实 desc 要能全文进「怎么玩」页");
            Assert.That(s.CurrentRequired, Is.EqualTo(1), "首任务条件 ObtainItem×1");
        }

        [Test]
        public void 格子状态_下标越界抛异常_写严格()
        {
            BindChain(Chain8);
            QuestProgressSummary s = HelpMenuUi.GetProgressSummary();

            Assert.That(() => s.GetSlotState(-1),
                Throws.TypeOf<System.ArgumentOutOfRangeException>(), "负下标写严格");
            Assert.That(() => s.GetSlotState(8),
                Throws.TypeOf<System.ArgumentOutOfRangeException>(), "8 格链的下标 8 越界");
        }

        // ─── m7 A4：保存并退出（同步落盘 → 半秒停留 → 退出） ───────────────

        /// <summary>建一棵最小可保存的树（PlayerContext + PlayerController + SaveLoadService，
        /// 档落 _saveRoot）。「保存并退出」按钮注入的存档服务就挂这棵树上，
        /// 用真实 SaveNow(async:false) 路径断言，不造假存档。</summary>
        private SaveLoadService BuildSaveHost(out PlayerContext ctx)
        {
            var host = new GameObject("HelpMenuQuitSaveHost");
            ctx = host.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不回调 Awake：PlayerContext 各系统显式赋值
            //（同 SaveLoadServiceSaveTests 的注释，防单例残留形态）
            ctx.Inventory = new PlayerInventory();
            ctx.Health = new Health(20f);
            ctx.Time = new TimeOfDay();
            var player = host.AddComponent<PlayerController>();
            var service = host.AddComponent<SaveLoadService>();
            service.Bind(new World(), ctx, player, seed: 42, saveRoot: _saveRoot);
            return service;
        }

        [Test]
        public void 保存并退出_同步落盘_半秒后触发一次退出()
        {
            SaveLoadService service = BuildSaveHost(out PlayerContext ctx);
            try
            {
                // 同步路径不得走后台执行器：一旦被调即失败（退出保存必须内联落盘）
                service.WriteExecutor = a => Assert.Fail("退出保存必须同步落盘，不得调度后台执行器");

                var ui = NewMenu();
                try
                {
                    ui.SaveService = service; // 注入存档服务，懒查找留给生产路径
                    float now = 0f;
                    ui.QuitClock = () => now; // 注入时钟：EditMode 不跑 Update，步进 0.5s 停留窗
                    int quitCount = 0;
                    ui.QuitRequested = () => quitCount++; // 退出动作注入：不真退出测试进程

                    Assert.That(ui.QuitStatusText, Is.Null, "未点击时应显示按钮而不是确认文本");

                    ui.RequestSaveAndQuit();

                    Assert.That(File.Exists(service.LevelDataPath), Is.True,
                        "点击即同步保存：SaveNow(async:false) 返回时 level.dat 已落盘");
                    Assert.That(ui.QuitStatusText, Is.EqualTo("已保存，正在退出…"),
                        "保存到退出之间要显示确认文本（半秒停留窗就是为让它被看见）");
                    Assert.That(quitCount, Is.EqualTo(0), "刚保存完不得立刻退出——停留窗还没走完");

                    now = 0.25f;
                    ui.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(0), "0.25s < 0.5s：停留窗内不退");

                    now = 0.5f;
                    ui.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(1), "满 0.5s 应触发一次退出");

                    ui.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(1), "退出动作只触发一次，不得每帧重复调");
                }
                finally
                {
                    Object.DestroyImmediate(ui.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void 保存失败_假保存抛异常_不退出可重试()
        {
            // m7 A4 fix1：保存抛异常（快照收集段没有兜底的形态）绝不进入退出流程——
            // 档可能还没写全就退，等于丢档。注入抛异常的假保存隔离验证 UI 侧契约
            SaveLoadService service = BuildSaveHost(out PlayerContext ctx);
            try
            {
                var ui = NewMenu();
                try
                {
                    ui.SaveService = service;
                    ui.SaveNowSync = s => throw new IOException("磁盘空间不足"); // 假保存：抛异常
                    float now = 0f;
                    ui.QuitClock = () => now;
                    int quitCount = 0;
                    ui.QuitRequested = () => quitCount++;

                    ui.RequestSaveAndQuit();

                    Assert.That(ui.QuitStatusText, Is.Null,
                        "保存失败不得进入退出流程——按钮必须还在，玩家才能重试");
                    Assert.That(ui.QuitErrorText, Does.Contain("磁盘空间不足"), "失败提示要带原因");
                    Assert.That(ui.QuitErrorText, Does.Contain("Alt+F4"), "要给 Alt+F4 退路指引");
                    now = 10f; // 时钟推到远超 0.5s
                    ui.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(0), "保存失败绝不退出——先救人家的档");

                    // 重试：换回真实保存，应清掉失败提示、照常进入退出流程
                    ui.SaveNowSync = s => s.SaveNow(async: false);
                    ui.RequestSaveAndQuit();
                    Assert.That(File.Exists(service.LevelDataPath), Is.True, "重试应真的落盘");
                    Assert.That(ui.QuitErrorText, Is.Null, "新一轮尝试要先清上轮失败提示");
                    Assert.That(ui.QuitStatusText, Is.EqualTo("已保存，正在退出…"), "成功后照常进退出流程");
                    now = 10.5f;
                    ui.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(1), "重试成功后半秒照常退出");
                }
                finally
                {
                    Object.DestroyImmediate(ui.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void 保存失败_SaveNow返回false_不退出可重试()
        {
            // m7 A4 fix1 的另一半：SaveNow 对 IO 错误是捕获后记录（log-and-continue）不抛，
            // 靠返回 false 反馈。真实失败注入——把 <saveRoot>/<seed> 占成文件，
            // 建目录即炸，走的就是玩家磁盘满 / 路径不可写时的真链路
            SaveLoadService service = BuildSaveHost(out PlayerContext ctx);
            try
            {
                string worldDir = Path.Combine(_saveRoot, "42");
                File.WriteAllText(worldDir, "not a directory");

                var ui = NewMenu();
                try
                {
                    ui.SaveService = service;
                    float now = 0f;
                    ui.QuitClock = () => now;
                    int quitCount = 0;
                    ui.QuitRequested = () => quitCount++;

                    // 真实 SaveNow 会把两层错误打进错误日志（ApplyPendingClears 补发），按序声明
                    LogAssert.Expect(LogType.Error, new Regex("level\\.dat 保存失败"));
                    LogAssert.Expect(LogType.Error, new Regex("region 保存失败"));

                    ui.RequestSaveAndQuit();

                    Assert.That(ui.QuitStatusText, Is.Null,
                        "SaveNow 返回 false 不得进入退出流程");
                    Assert.That(ui.QuitErrorText, Does.Contain("level.dat"),
                        "失败提示要带真实原因（来自 SaveLoadService.LastSaveError）");
                    now = 10f;
                    ui.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(0), "保存失败绝不退出");

                    // 重试：障碍清除后同一按钮应能存成并退出（可重试不是嘴上说说）
                    File.Delete(worldDir);
                    ui.RequestSaveAndQuit();
                    Assert.That(File.Exists(service.LevelDataPath), Is.True, "障碍清除后重试应落盘");
                    now = 10.5f;
                    ui.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(1), "重试成功后半秒照常退出");
                }
                finally
                {
                    Object.DestroyImmediate(ui.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void 保存并退出_手抖双击_只保存一次()
        {
            SaveLoadService service = BuildSaveHost(out PlayerContext ctx);
            try
            {
                ctx.Time.CurrentTick = 1234f;

                var ui = NewMenu();
                try
                {
                    ui.SaveService = service;
                    ui.QuitClock = () => 0f;
                    ui.QuitRequested = () => { };

                    ui.RequestSaveAndQuit();
                    ctx.Time.CurrentTick = 9999f; // 两次点击之间世界时间变了
                    ui.RequestSaveAndQuit();      // 第二次点击应整轮忽略

                    var loaded = LevelDataCodec.Load(service.LevelDataPath);
                    Assert.That(loaded.TimeTick, Is.EqualTo(1234f),
                        "第二次点击不得重复保存——档里应是首次点击时刻的世界时间");
                }
                finally
                {
                    Object.DestroyImmediate(ui.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void 保存并退出_无存档服务_不抛异常照常退出()
        {
            // 早期场景（没挂 WorldBootstrap / SaveLoadService）形态：点退出只退不存，不得炸
            var ui = NewMenu();
            try
            {
                float now = 0f;
                ui.QuitClock = () => now;
                int quitCount = 0;
                ui.QuitRequested = () => quitCount++;

                Assert.DoesNotThrow(() => ui.RequestSaveAndQuit(),
                    "场景里找不到 SaveLoadService 不得抛异常");
                Assert.That(ui.QuitStatusText, Is.EqualTo("正在退出…"),
                    "没存过档不得谎报「已保存」");
                now = 0.5f;
                ui.TickQuit();
                Assert.That(quitCount, Is.EqualTo(1), "无存档服务也应正常退出");
            }
            finally
            {
                Object.DestroyImmediate(ui.gameObject);
            }
        }

        [Test]
        public void 按键表_AltF4行_直接退出自动存档()
        {
            // m7 A4：右栏补第 9 行 Alt+F4（两栏自此不等长，绘制按较长者遍历）。
            // 顺着渲染行数扫一遍，确认 Alt+F4 行真的落在会被画出来的范围内——
            // 循环若仍只按左栏 8 行走，右栏末行会被静默截掉。
            string desc = null;
            int altRow = -1;
            for (int row = 0; row < HelpMenuUi.KeyTableRowCount; row++)
            {
                var (key, d) = HelpMenuUi.GetRightColumnRow(row);
                if (key == "Alt+F4")
                {
                    altRow = row;
                    desc = d;
                    break;
                }
            }
            Assert.That(altRow, Is.GreaterThanOrEqualTo(0),
                "按键表应有 Alt+F4 行，且落在渲染行数范围内（左栏 8 行截不住右栏第 9 行）");
            Assert.That(desc, Is.EqualTo("直接退出（自动存档）"),
                "说明要写明自动存档——Alt+F4 走 OnApplicationQuit 同步落盘，是既有行为");
        }

        [Test]
        public void 挖矿提示行_镐门槛链与方块表一致()
        {
            // m10 C3（spec §4 遗留）：「怎么玩」按键表正下方的挖矿门槛提示行——
            // 文案里的材料链必须与真实 blocks/*.json 的 minToolTier 阶梯同向，
            // 单边改动（改门槛数据不改文案 / 改文案不改数据）都会被这里抓住
            Assert.That(HelpMenuUi.MiningTierHint, Does.Contain("挖到不同矿石需要更好的镐"),
                "提示行主句要与 spec §4 一致（孩子问「为什么挖不动」时的答案）");
            Assert.That(HelpMenuUi.MiningTierHint, Does.Contain("石→铁→金/合金→机元"),
                "门槛链按递增顺序列四种材料（石=木镐→铁=石镐→金/合金=铁镐→机元=钻石镐）");

            // 真数据对照：按文案顺序读各矿 minToolTier，阶梯必须与链一致
            string dir = Path.Combine(Application.streamingAssetsPath, "blocks");
            var registry = BlockRegistry.FromJson(
                Directory.GetFiles(dir, "*.json").Select(File.ReadAllText));
            Assert.That(registry.GetById("stone").MinToolTier, Is.EqualTo(1),
                "石头门槛 1=木镐（徒手挖得掉但 4s 且无掉落）");
            Assert.That(registry.GetById("raw_iron_ore").MinToolTier, Is.EqualTo(2),
                "粗铁门槛 2=石镐");
            Assert.That(registry.GetById("gold_ore").MinToolTier, Is.EqualTo(3),
                "金矿门槛 3=铁镐");
            Assert.That(registry.GetById("summer_alloy_ore").MinToolTier, Is.EqualTo(3),
                "夏季合金门槛 3=铁镐（与金同档，文案并列「金/合金」）");
            Assert.That(registry.GetById("machine_essence_ore").MinToolTier, Is.EqualTo(4),
                "机元门槛 4=钻石镐（链上最深一层）");
        }
    }
}
#endif
