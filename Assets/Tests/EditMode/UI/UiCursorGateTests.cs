#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using MyWorld.Unity.Player;
using MyWorld.Unity.UI;

namespace MyWorld.Core.Tests.UI
{
    /// <summary>
    /// m6 终审修 C1：模态 UI 指针门（<see cref="UiCursorGate"/>）的行为契约。
    /// <para>
    /// 六个模态 UI（背包 E / 工作台 P / 口袋 B / 熔炉 F / 交易 V / 帮助 H）的开关统一登记到门：
    /// 任一打开 → 指针解锁且可见（指针锁定时 IMGUI 点击只命中屏幕中心，格子点不到）；
    /// 全部关闭 → 恢复 Locked + 隐藏。<see cref="PlayerController.UpdateLook"/> 在门开着时
    /// 早退（<see cref="PlayerController.ShouldSkipLook"/>）——不转视角、不因点击重锁指针。
    /// </para>
    /// <para>
    /// EditMode 下 <c>Cursor.lockState = None</c> 可直接断言；写 Locked 引擎不回读，
    /// 恢复方向断言 <see cref="UiCursorGate.AppliedLockState"/>（与真正的 Cursor 写入同处维护）。
    /// 静态门状态跨夹具残留，SetUp / TearDown 都 <see cref="UiCursorGate.Reset"/>。
    /// </para>
    /// </summary>
    [TestFixture]
    public class UiCursorGateTests
    {
        [SetUp]
        public void SetUp()
        {
            UiCursorGate.Reset();
            BlockInteraction.InputLocked = false;
        }

        [TearDown]
        public void TearDown()
        {
            UiCursorGate.Reset();
            BlockInteraction.InputLocked = false;
        }

        [Test]
        public void 门计数_Open递增Close递减_多余Close钳在零()
        {
            Assert.That(UiCursorGate.IsOpen, Is.False, "复位后门应为关闭");
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(0));

            UiCursorGate.Open();
            UiCursorGate.Open();
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(2), "两个 UI 叠开计数应为 2");
            Assert.That(UiCursorGate.IsOpen, Is.True);

            UiCursorGate.Close();
            Assert.That(UiCursorGate.IsOpen, Is.True, "还剩一个 UI 开着，门必须保持打开");

            UiCursorGate.Close();
            Assert.That(UiCursorGate.IsOpen, Is.False, "全部关闭后门关闭");

            UiCursorGate.Close(); // 防御路径：多余的 Close 不允许把计数打成负
            Assert.That(UiCursorGate.OpenCount, Is.EqualTo(0), "多余 Close 应钳在 0（负数会让 IsOpen 恒 false）");
        }

        [Test]
        public void 开背包_指针解锁可见_全关恢复锁定隐藏()
        {
            var go = new GameObject("GateInv");
            try
            {
                var ui = go.AddComponent<CraftingInventoryUi>();
                ui.SetOpen(true);

                Assert.That(UiCursorGate.IsOpen, Is.True, "背包打开必须登记指针门");
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None),
                    "背包开着指针必须解锁——锁定时 IMGUI 点击只命中屏幕中心，格子点不到（C1 主症状）");
                Assert.That(UiCursorGate.AppliedVisible, Is.True, "背包开着指针必须可见");

                ui.SetOpen(false);
                Assert.That(UiCursorGate.IsOpen, Is.False);
                Assert.That(UiCursorGate.AppliedLockState, Is.EqualTo(CursorLockMode.Locked),
                    "全部关闭后必须向引擎申请恢复锁定，正常挖/放流程不受影响（EditMode 批处理下 " +
                    "Cursor.lockState 写 Locked 不回读，断言门的应用记录）");
                Assert.That(UiCursorGate.AppliedVisible, Is.False, "全部关闭后指针隐藏");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 叠开两个UI_先关一个指针仍解锁_再全关才恢复锁定()
        {
            var go = new GameObject("GateTwo");
            try
            {
                var inv = go.AddComponent<CraftingInventoryUi>();
                var pocket = go.AddComponent<CraftingPocketUi>();

                inv.SetOpen(true);
                pocket.SetOpen(true);
                Assert.That(UiCursorGate.OpenCount, Is.EqualTo(2), "背包 + 口袋叠开计数为 2");

                inv.SetOpen(false);
                Assert.That(UiCursorGate.AppliedLockState, Is.EqualTo(CursorLockMode.None),
                    "口袋还开着：先关背包不能把指针提前锁回去（计数制的意义）");

                pocket.SetOpen(false);
                Assert.That(UiCursorGate.AppliedLockState, Is.EqualTo(CursorLockMode.Locked),
                    "两个都关了才恢复锁定");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 门开着时_PlayerController视角更新早退_不重锁指针()
        {
            var go = new GameObject("GateLook");
            try
            {
                go.AddComponent<PlayerController>();
                var inv = go.AddComponent<CraftingInventoryUi>();

                inv.SetOpen(true);
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "前置：背包已把指针解锁");
                // UpdateLook 的守卫判定（纯函数，Update 每帧开头调用同一个）：
                // 门开着必须为 true——即点击 UI 格子的那一帧不会把指针重锁回屏幕中心
                Assert.That(PlayerController.ShouldSkipLook(UiCursorGate.IsOpen), Is.True,
                    "门开着时 UpdateLook 必须跳过：不转视角、不因点击重锁指针（C1：Esc 关 UI 后首次点击被重锁）");

                inv.SetOpen(false);
                Assert.That(UiCursorGate.AppliedLockState, Is.EqualTo(CursorLockMode.Locked),
                    "前置：门全关后指针已申请恢复锁定");
                Assert.That(PlayerController.ShouldSkipLook(UiCursorGate.IsOpen), Is.False,
                    "门全关后恢复正常的视角控制与点击重锁语义");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 帮助菜单_SetOpen与按键同路径_同步InputLocked与指针门()
        {
            var go = new GameObject("GateHelp");
            try
            {
                var menu = go.AddComponent<HelpMenuUi>();

                menu.SetOpen(true);
                Assert.That(menu.IsOpen, Is.True);
                Assert.That(BlockInteraction.InputLocked, Is.True, "帮助菜单打开必须锁挖/放（点滑条不误挖）");
                Assert.That(UiCursorGate.IsOpen, Is.True, "帮助菜单打开必须登记指针门");
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None), "帮助菜单打开指针解锁（滑条要能拖）");

                menu.SetOpen(false);
                Assert.That(menu.IsOpen, Is.False);
                Assert.That(BlockInteraction.InputLocked, Is.False);
                Assert.That(UiCursorGate.IsOpen, Is.False);
                Assert.That(UiCursorGate.AppliedLockState, Is.EqualTo(CursorLockMode.Locked), "关闭后申请恢复锁定");

                // H 键路由与 SetOpen 同一条路径：门与输入锁不会漏维护
                menu.HandleKey(KeyCode.H);
                Assert.That(menu.IsOpen, Is.True);
                Assert.That(UiCursorGate.IsOpen, Is.True, "H 打开同样登记指针门");
                menu.HandleKey(KeyCode.H);
                Assert.That(UiCursorGate.IsOpen, Is.False, "H 关闭同样归还门位");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void 重复SetOpen同值_不重复登记门位()
        {
            var go = new GameObject("GateIdem");
            try
            {
                var wb = go.AddComponent<CraftingWorkbenchUi>();
                wb.SetOpen(true);
                wb.SetOpen(true); // 幂等：同值早退，不能把计数刷成 2
                Assert.That(UiCursorGate.OpenCount, Is.EqualTo(1), "重复 SetOpen(true) 不应重复登记");

                wb.SetOpen(false);
                wb.SetOpen(false);
                Assert.That(UiCursorGate.OpenCount, Is.EqualTo(0), "重复 SetOpen(false) 不应把计数打成负");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
