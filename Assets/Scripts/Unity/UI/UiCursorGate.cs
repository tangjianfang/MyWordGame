using UnityEngine;

namespace MyWorld.Unity.UI
{
    /// <summary>
    /// m6 终审修 C1：模态 UI 的统一指针门。
    /// <para>
    /// 六个模态 UI（背包 E / 工作台 P / 口袋 B / 熔炉 F / 交易 V / 帮助 H）的开关统一登记到这里：
    /// 门从 0 → 1 的那一刻把指针切成 <c>None + visible</c>——指针锁定时 IMGUI 点击只命中屏幕中心，
    /// 不解锁就没法点格子、拉滑条、按「买」；全部关闭（1 → 0）时恢复 <c>Locked + invisible</c>。
    /// </para>
    /// <para>
    /// 消费方两处，都在 Update 开头早退：<see cref="Player.PlayerController.UpdateLook"/>
    /// （门开着时不转视角、不因点击重锁指针——否则每点一格都要重新解锁一次）与
    /// <see cref="Player.BlockInteraction.Update"/>（门开着时不挖/放——点 UI 格子不该
    /// 顺手挖掉准星后的方块）。
    /// </para>
    /// <para>
    /// 计数制（而不是单布尔）：多个 UI 叠开时，先关的那个不会把另一个还开着的指针提前锁回去。
    /// </para>
    /// </summary>
    public static class UiCursorGate
    {
        private static int _openCount;

        /// <summary>是否有任一模态 UI 开着（指针应处于解锁可见状态）。</summary>
        public static bool IsOpen => _openCount > 0;

        /// <summary>当前登记打开的 UI 个数（EditMode 断言用）。</summary>
        public static int OpenCount => _openCount;

        /// <summary>
        /// 门最近一次向引擎申请的指针锁定态（None = 解锁 / Locked = 锁定）。
        /// EditMode 批处理下 <c>Cursor.lockState = Locked</c> 写进去读不回来（引擎不回读），
        /// 这个记录让「全关后恢复锁定」在测试里可断言——记录与真正的 Cursor 写入在同一行代码里维护。
        /// </summary>
        public static CursorLockMode AppliedLockState { get; private set; } = CursorLockMode.Locked;

        /// <summary>门最近一次向引擎申请的指针可见性（同 <see cref="AppliedLockState"/>）。</summary>
        public static bool AppliedVisible { get; private set; }

        /// <summary>UI 打开时调用。第一个打开者负责把指针切成 None + visible。</summary>
        public static void Open()
        {
            _openCount++;
            if (_openCount == 1)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                AppliedLockState = CursorLockMode.None;
                AppliedVisible = true;
            }
        }

        // ─── 评审 04 R-5/R-6 + 07#3：级联关闭注册表 + 同帧关闭痕迹 ──────────────
        private static readonly System.Collections.Generic.List<System.Action> _closeCallbacks =
            new System.Collections.Generic.List<System.Action>();

        private static int _lastCloseFrame = -1;

        /// <summary>最近一次「有关闭中的模态」的 Close() 所在帧号（-1 = 尚无）。
        /// PauseMenuUi 据此做同帧让位：Esc 先关了别的模态时本帧不再开暂停
        /// （评审 04 R-5——HelpMenuUi 的 Update 轮询与暂停的 OnGUI 事件是两条通道，
        /// evt.Use() 抑制不了 Input 轮询，必须显式让位）。</summary>
        public static int LastCloseFrame => _lastCloseFrame;

        /// <summary>模态 UI 注册「一键关闭」回调（OnEnable 挂 / OnDisable 摘，方法组保证
        /// 可等值移除）。PauseMenuUi 打开时经 <see cref="CloseAllRegistered"/> 级联关闭
        /// 全部已开模态（评审 04 R-6/07#3：此前只级联 HelpMenuUi，背包/箱子等叠开）。</summary>
        public static void RegisterClose(System.Action close)
        {
            if (close != null) _closeCallbacks.Add(close);
        }

        /// <summary>摘除级联关闭回调（UI 关闭/销毁时——表里不许留死引用）。</summary>
        public static void UnregisterClose(System.Action close) => _closeCallbacks.Remove(close);

        /// <summary>级联关闭全部已登记模态（倒序遍历；回调幂等——已关的再调是 no-op，
        /// 各 UI 的 SetOpen(false)/Close() 都有早退或计数防御）。</summary>
        public static void CloseAllRegistered()
        {
            for (int i = _closeCallbacks.Count - 1; i >= 0; i--) _closeCallbacks[i]?.Invoke();
        }

        /// <summary>UI 关闭时调用。最后一个关闭者把指针恢复 Locked + invisible。</summary>
        public static void Close()
        {
            // 评审 04 R-5：记录「本帧有关闭中的模态」（计数尚 >0 时才记——真关而非多余关）
            if (_openCount > 0) _lastCloseFrame = Time.frameCount;
            // 防御：多余的 Close 不允许把计数打成负（负数会让 IsOpen 恒 false，门形同虚设）
            _openCount = Mathf.Max(0, _openCount - 1);
            if (_openCount == 0)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                AppliedLockState = CursorLockMode.Locked;
                AppliedVisible = false;
            }
        }

        /// <summary>测试 / 场景重载复位。只清计数不动指针——调用方自己保证屏幕状态一致。</summary>
        public static void Reset()
        {
            _lastCloseFrame = -1; // 评审 04 R-5：场景重载清同帧痕迹（回调表由各 UI OnDisable 自摘，不清）
            _openCount = 0;
            AppliedLockState = CursorLockMode.Locked;
            AppliedVisible = false;
        }
    }
}
