#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using MyWorld.Core.Entities;
using MyWorld.Core.Player;
using MyWorld.Core.Time;
using MyWorld.Unity.Gameplay;
using MyWorld.Unity.Persistence;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m8 B2：Esc 真暂停菜单的开关 / 真暂停 / Esc 路由 / 复用契约。
    /// <para>
    /// 真暂停 = 打开即 <c>UnityEngine.Time.timeScale = 0</c>、关闭恢复 1——EditMode 下 timeScale
    /// 只是普通浮点字段（不影响编辑器时钟），读写回环可断言。保存退出流程由
    /// <see cref="HelpMenuUi"/> 的 m7 A4 状态机承担（本菜单只委托），
    /// 注入缝（<c>QuitClock</c> / <c>QuitRequested</c> / <c>SaveService</c>）
    /// 也在帮助菜单那一侧，这里照 <see cref="HelpMenuUiTests"/> 的用法注入。
    /// </para>
    /// <para>
    /// Esc 路由优先级：死亡画面可见 → Esc 让位不弹暂停；暂停开着 → Esc 关闭
    /// （退出流程挂起时连关闭也挡住——保存退出期间保持 timeScale=0）。
    /// </para>
    /// </summary>
    [TestFixture]
    public class PauseMenuTests
    {
        /// <summary>死亡让位用例的 PlayerContext 宿主（TearDown 销毁）。</summary>
        private GameObject _ctxHost;

        /// <summary>保存退出用例的存档根目录（SetUp 建、TearDown 删，互不串档）。</summary>
        private string _saveRoot;

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

        /// <summary>反射调用私有无参方法（EditMode 下 DestroyImmediate 不回调
        /// OnDisable，禁用复位用例只能这样直接驱动）。</summary>
        private static void InvokeNoArgPrivate(MonoBehaviour mb, string name)
        {
            var method = mb.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, mb.GetType().Name + " 应有私有 " + name);
            method.Invoke(mb, null);
        }

        /// <summary>建一个暂停菜单并显式走 Awake（懒挂 HelpMenuUi + SettingsPanelUi）。</summary>
        private static PauseMenuUi NewPauseMenu()
        {
            var go = new GameObject("PauseMenu");
            var ui = go.AddComponent<PauseMenuUi>();
            InvokeAwake(ui);
            return ui;
        }

        /// <summary>建一棵最小可保存的树（PlayerContext + PlayerController + SaveLoadService，
        /// 档落 _saveRoot）——与 HelpMenuUiTests 同款，用真实 SaveNow(async:false) 路径。</summary>
        private SaveLoadService BuildSaveHost()
        {
            var host = new GameObject("PauseMenuSaveHost");
            var ctx = host.AddComponent<PlayerContext>();
            // EditMode 下 AddComponent 不回调 Awake：PlayerContext 各系统显式赋值
            //（同 HelpMenuUiTests 的注释，防单例残留形态）
            ctx.Inventory = new PlayerInventory();
            ctx.Health = new Health(20f);
            ctx.Time = new TimeOfDay();
            var player = host.AddComponent<PlayerController>();
            var service = host.AddComponent<SaveLoadService>();
            service.Bind(new MyWorld.Core.Voxel.World(), ctx, player, seed: 42, saveRoot: _saveRoot);
            return service;
        }

        [SetUp]
        public void SetUp()
        {
            BlockInteraction.InputLocked = false;
            UiCursorGate.Reset();
            UnityEngine.Time.timeScale = 1f; // 上个用例若把暂停开着漏关，这里兜底还原
            _saveRoot = Path.Combine(Application.temporaryCachePath,
                $"pausemenu-quit-{System.Guid.NewGuid():N}");
            Directory.CreateDirectory(_saveRoot);
        }

        [TearDown]
        public void TearDown()
        {
            BlockInteraction.InputLocked = false;
            UiCursorGate.Reset();
            UnityEngine.Time.timeScale = 1f; // 真暂停用例把 timeScale 归零后必须还原，不冻结别的夹具
            if (Directory.Exists(_saveRoot)) Directory.Delete(_saveRoot, true);
            if (_ctxHost != null) Object.DestroyImmediate(_ctxHost);
        }

        [Test]
        public void Esc开关_打开锁输入登记指针门_关闭全复位()
        {
            var pause = NewPauseMenu();
            try
            {
                Assert.That(pause.IsOpen, Is.False, "初始应为关闭");
                Assert.That(BlockInteraction.InputLocked, Is.False, "关闭时不应锁挖/放输入");
                Assert.That(UiCursorGate.OpenCount, Is.EqualTo(0), "关闭时指针门无登记");

                pause.HandleKey(KeyCode.Escape);
                Assert.That(pause.IsOpen, Is.True, "Esc 应打开暂停菜单");
                Assert.That(BlockInteraction.InputLocked, Is.True, "暂停时必须锁住挖/放输入");
                Assert.That(UiCursorGate.OpenCount, Is.EqualTo(1), "打开应登记指针门（解锁鼠标点按钮）");

                pause.HandleKey(KeyCode.Escape);
                Assert.That(pause.IsOpen, Is.False, "再按 Esc 关闭");
                Assert.That(BlockInteraction.InputLocked, Is.False, "关闭时解锁挖/放输入");
                Assert.That(UiCursorGate.OpenCount, Is.EqualTo(0), "关闭后指针门回落");
            }
            finally
            {
                Object.DestroyImmediate(pause.gameObject);
            }
        }

        [Test]
        public void 真暂停_打开timeScale归零_关闭恢复1()
        {
            var pause = NewPauseMenu();
            try
            {
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(1f), "未暂停时时间正常流动");

                pause.HandleKey(KeyCode.Escape);
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f), "打开暂停必须 UnityEngine.Time.timeScale=0（真暂停，世界冻结）");

                pause.HandleKey(KeyCode.Escape);
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(1f), "关闭暂停恢复 UnityEngine.Time.timeScale=1");
            }
            finally
            {
                Object.DestroyImmediate(pause.gameObject);
            }
        }

        [Test]
        public void 死亡画面可见_Esc让位_不弹暂停菜单()
        {
            // 真 PlayerContext + DeathScreenUi：Awake 把死亡画面注册进 ctx.DeathScreen，
            // 暂停菜单经 PlayerContext.Instance 反查（与 PlayerController 取 Show() 同一条路）
            _ctxHost = new GameObject("PauseDeathCtx");
            var ctx = _ctxHost.AddComponent<PlayerContext>();
            InvokeAwake(ctx);
            var deathGo = new GameObject("DeathScreen");
            var death = deathGo.AddComponent<DeathScreenUi>();
            InvokeAwake(death);
            death.OnPlayerDied();
            Assert.That(death.IsVisible, Is.True, "前置：死亡画面已显示");

            var pause = NewPauseMenu();
            try
            {
                pause.HandleKey(KeyCode.Escape);
                Assert.That(pause.IsOpen, Is.False, "死亡画面可见时 Esc 让位——复活按钮才是主操作");
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(1f), "让位时不得顺带冻结世界");

                // 死亡画面没了（已销毁引用经 Unity 判空兜底）后 Esc 恢复开门
                Object.DestroyImmediate(deathGo);
                pause.HandleKey(KeyCode.Escape);
                Assert.That(pause.IsOpen, Is.True, "死亡画面收起后 Esc 应正常打开暂停菜单");
            }
            finally
            {
                Object.DestroyImmediate(pause.gameObject);
            }
        }

        [Test]
        public void 设置面板_Awake同物体懒挂_与帮助菜单共享同一实例()
        {
            // m8 B1 的公共面板只许一份：暂停菜单与帮助菜单同物体共存时，
            // 后挂的一方必须 GetComponent 复用先挂的，而不是再 AddComponent
            var go = new GameObject("PauseSharedPanel");
            var help = go.AddComponent<HelpMenuUi>();
            var pause = go.AddComponent<PauseMenuUi>();
            InvokeAwake(help);
            InvokeAwake(pause);
            try
            {
                Assert.That(pause.SettingsPanel, Is.Not.Null, "暂停菜单 Awake 应嵌入公共设置面板");
                Assert.That(pause.SettingsPanel.transform, Is.EqualTo(pause.transform), "面板挂同一物体");
                Assert.That(pause.SettingsPanel, Is.SameAs(help.SettingsPanel),
                    "暂停菜单与帮助菜单必须共享同一个 SettingsPanelUi 实例（不复制面板）");
                Assert.That(pause.HelpMenu, Is.SameAs(help), "Awake 应复用同物体的帮助菜单（保存退出状态机宿主）");
                Assert.That(go.GetComponents<SettingsPanelUi>().Length, Is.EqualTo(1),
                    "全场只建一个设置面板实例");
                Assert.That(go.GetComponents<HelpMenuUi>().Length, Is.EqualTo(1),
                    "全场只建一个帮助菜单实例");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 帮助菜单互斥_暂停打开先关帮助_指针门不叠_H不叠开()
        {
            var pause = NewPauseMenu();
            try
            {
                pause.HelpMenu.SetOpen(true);
                Assert.That(pause.HelpMenu.IsOpen, Is.True, "前置：帮助菜单先开着");
                Assert.That(UiCursorGate.OpenCount, Is.EqualTo(1));

                pause.HandleKey(KeyCode.Escape);
                Assert.That(pause.IsOpen, Is.True, "Esc 打开暂停");
                Assert.That(pause.HelpMenu.IsOpen, Is.False, "暂停打开时必须先关帮助菜单（互斥）");
                Assert.That(UiCursorGate.OpenCount, Is.EqualTo(1),
                    "帮助关 + 暂停开各登记一次，指针门计数不得叠到 2");
                Assert.That(BlockInteraction.InputLocked, Is.True,
                    "关帮助菜单会把输入锁清掉，暂停必须重新锁上");

                // 反方向也互斥：暂停开着按 H 不叠开帮助（既有门卫：别的模态 UI 开着时 H 忽略）
                pause.HelpMenu.HandleKey(KeyCode.H);
                Assert.That(pause.HelpMenu.IsOpen, Is.False, "暂停期间 H 不得叠开帮助菜单");
            }
            finally
            {
                Object.DestroyImmediate(pause.gameObject);
            }
        }

        [Test]
        public void 保存退出_委托HelpMenu状态机_暂停中同步落盘半秒后退出一次()
        {
            SaveLoadService service = BuildSaveHost();
            try
            {
                // 同步路径不得走后台执行器：一旦被调即失败（退出保存必须内联落盘）
                service.WriteExecutor = a => Assert.Fail("退出保存必须同步落盘，不得调度后台执行器");

                var pause = NewPauseMenu();
                try
                {
                    HelpMenuUi help = pause.HelpMenu;
                    Assert.That(help, Is.Not.Null, "Awake 应懒挂帮助菜单（保存退出状态机宿主）");
                    help.SaveService = service; // 注入存档服务，懒查找留给生产路径
                    float now = 0f;
                    help.QuitClock = () => now; // 注入时钟：EditMode 不跑 Update，手动步进半秒停留窗
                    int quitCount = 0;
                    help.QuitRequested = () => quitCount++; // 退出动作注入：不真退测试进程

                    pause.HandleKey(KeyCode.Escape);
                    Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f), "暂停中保存退出：全程保持 timeScale=0");
                    Assert.That(pause.QuitPending, Is.False, "未点击退出时应显示按钮");

                    pause.RequestSaveAndQuit(); // 「保存并退出」按钮路径（整体委托）

                    Assert.That(File.Exists(service.LevelDataPath), Is.True,
                        "点击即同步保存：HelpMenu 状态机的 SaveNow(async:false) 返回时 level.dat 已落盘");
                    Assert.That(pause.QuitPending, Is.True, "存成后进入半秒停留窗");
                    Assert.That(help.QuitStatusText, Is.EqualTo("已保存，正在退出…"),
                        "确认文本来自帮助菜单公开接口（同一份状态机）");
                    Assert.That(quitCount, Is.EqualTo(0), "刚存完不得立刻退出——停留窗还没走完");

                    now = 0.25f;
                    help.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(0), "0.25s < 0.5s：停留窗内不退");

                    now = 0.5f;
                    help.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(1), "满 0.5s 应触发一次退出");

                    help.TickQuit();
                    Assert.That(quitCount, Is.EqualTo(1), "退出动作只触发一次，不得每帧重复调");
                    Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f),
                        "退出保存期间保持暂停——停留窗不恢复时间流");
                }
                finally
                {
                    Object.DestroyImmediate(pause.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void 退出挂起期间_Esc不关菜单_timeScale保持0()
        {
            // 保存退出已挂起（无存档服务的早期场景形态即可进入停留窗）时，
            // Esc 不得提前关菜单恢复时间流——半秒后就退了，别在这窗口里解冻世界
            var pause = NewPauseMenu();
            try
            {
                pause.HelpMenu.QuitClock = () => 0f;
                int quitCount = 0;
                pause.HelpMenu.QuitRequested = () => quitCount++;

                pause.HandleKey(KeyCode.Escape);
                Assert.That(pause.IsOpen, Is.True);
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f));

                pause.RequestSaveAndQuit();
                Assert.That(pause.QuitPending, Is.True, "无存档服务也照常进入退出流程（只退不存）");

                pause.HandleKey(KeyCode.Escape);
                Assert.That(pause.IsOpen, Is.True, "退出挂起期间 Esc 不关菜单");
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f), "退出挂起期间 timeScale 保持 0");
                Assert.That(quitCount, Is.EqualTo(0), "本用例不步进停留窗，退出动作不应触发");

                // 程序化总阀不受挡：SetOpen(false) 是低层阀门（禁用复位也走它）
                pause.SetOpen(false);
                Assert.That(pause.IsOpen, Is.False);
                Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(pause.gameObject);
            }
        }

        [Test]
        public void 重新打开菜单_设置子面板展开态复位()
        {
            var pause = NewPauseMenu();
            try
            {
                pause.HandleKey(KeyCode.Escape);
                Assert.That(pause.SettingsShown, Is.False, "打开时从主按钮页开始");

                pause.ToggleSettings(); // 「设置」按钮路径
                Assert.That(pause.SettingsShown, Is.True, "点设置应展开公共面板");

                pause.HandleKey(KeyCode.Escape); // 关
                pause.HandleKey(KeyCode.Escape); // 再开
                Assert.That(pause.SettingsShown, Is.False, "重新打开菜单应复位子面板展开态");
            }
            finally
            {
                Object.DestroyImmediate(pause.gameObject);
            }
        }

        [Test]
        public void 禁用复位_开着时被禁用_timeScale指针门输入锁全还原()
        {
            // 暂停菜单跟着场景卸载 / 物体销毁消失时若还开着，必须把 timeScale、
            // 指针门、输入锁一并复位——漏了就是「整个游戏永久冻结」的残局。
            // EditMode 下 DestroyImmediate 不回调 OnDisable，反射直调验证逻辑本身
            var pause = NewPauseMenu();
            pause.HandleKey(KeyCode.Escape);
            Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(0f));

            InvokeNoArgPrivate(pause, "OnDisable");

            Assert.That(pause.IsOpen, Is.False, "禁用时应自动收起菜单");
            Assert.That(UnityEngine.Time.timeScale, Is.EqualTo(1f), "禁用复位必须恢复时间流");
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(0), "禁用复位必须注销指针门");
            Assert.That(BlockInteraction.InputLocked, Is.False, "禁用复位必须解锁挖/放输入");
        }
    }
}
#endif
