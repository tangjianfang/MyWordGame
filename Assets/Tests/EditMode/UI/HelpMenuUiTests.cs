#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MyWorld.Core.Quests;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m6 B3：H 键帮助菜单的开关 / 按键路由 / PlayerPrefs 三键 round-trip 契约。
    /// InputLocked 静态门由 <see cref="HelpMenuUi"/> 的开关维护，
    /// <see cref="BlockInteraction.Update"/> 开头早退——这里测门的翻转，不拉起完整交互链路。
    /// <para>
    /// m6 C5 追加：<see cref="HelpMenuUi.GetProgressSummary"/> 纯函数各状态——
    /// 「怎么玩」页的当前目标全文 + 全链 8 格进度条全部从这一个快照取数，
    /// 不自己记账（单一真源是总线挂的 <see cref="QuestSystem"/>）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class HelpMenuUiTests
    {
        private static readonly List<string> TempFiles = new List<string>();

        /// <summary>挂任务总线用的宿主（C5 测试按需创建，无总线用例不建）。</summary>
        private GameObject _questHost;
        private QuestEventBus _bus;

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
            // 每个测试前清掉三键，避免上个测试写入的值影响「默认值」断言
            PlayerPrefs.DeleteKey(HelpMenuUi.SensitivityKey);
            PlayerPrefs.DeleteKey(HelpMenuUi.VolumeKey);
            PlayerPrefs.DeleteKey(HelpMenuUi.FovKey);
            BlockInteraction.InputLocked = false;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(HelpMenuUi.SensitivityKey);
            PlayerPrefs.DeleteKey(HelpMenuUi.VolumeKey);
            PlayerPrefs.DeleteKey(HelpMenuUi.FovKey);
            BlockInteraction.InputLocked = false;
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
        public void Settings_ThreeKeys_RoundTripThroughPlayerPrefs()
        {
            // Save → Load 相等（三键同构：灵敏度 / 音量 / FOV）
            HelpMenuUi.SaveSensitivity(1.5f);
            HelpMenuUi.SaveVolume(42f);
            HelpMenuUi.SaveFov(85f);

            Assert.That(HelpMenuUi.LoadSensitivity(), Is.EqualTo(1.5f), "灵敏度应原样回读");
            Assert.That(HelpMenuUi.LoadVolume(), Is.EqualTo(42f), "音量应原样回读");
            Assert.That(HelpMenuUi.LoadFov(), Is.EqualTo(85f), "FOV 应原样回读");
        }

        [Test]
        public void Settings_Defaults_WhenKeysMissing()
        {
            // 没存过时必须返回默认值，而不是 0（0 灵敏度会让视角完全转不动）
            Assert.That(HelpMenuUi.LoadSensitivity(), Is.EqualTo(HelpMenuUi.SensitivityDefault),
                "灵敏度默认 1.0");
            Assert.That(HelpMenuUi.LoadVolume(), Is.EqualTo(HelpMenuUi.VolumeDefault),
                "音量默认 80");
            Assert.That(HelpMenuUi.LoadFov(), Is.EqualTo(HelpMenuUi.FovDefault),
                "FOV 默认 70");
        }

        [Test]
        public void Awake_RestoresCurrentSettings_FromPlayerPrefs()
        {
            HelpMenuUi.SaveSensitivity(1.75f);
            HelpMenuUi.SaveVolume(10f);
            HelpMenuUi.SaveFov(88f);

            var ui = NewMenu();
            try
            {
                Assert.That(ui.CurrentSensitivity, Is.EqualTo(1.75f), "Awake 应从 PlayerPrefs 读回灵敏度");
                Assert.That(ui.CurrentVolume, Is.EqualTo(10f), "Awake 应从 PlayerPrefs 读回音量");
                Assert.That(ui.CurrentFov, Is.EqualTo(88f), "Awake 应从 PlayerPrefs 读回 FOV");
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
    }
}
#endif
