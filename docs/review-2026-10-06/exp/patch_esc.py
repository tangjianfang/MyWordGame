# -*- coding: utf-8 -*-
# 评审 04 R-5/R-6 + 07#3：Esc 路由统一补丁（主会话一次性机械应用）
import io

def patch(path, subs):
    s = io.open(path, encoding='utf-8').read()
    for old, new, cnt in subs:
        assert s.count(old) == cnt, f"{path}: {old[:48]!r} x{s.count(old)} != {cnt}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(s)
    print('PATCHED', path)

# 1) UiCursorGate：级联注册表 + LastCloseFrame
patch('Assets/Scripts/Unity/UI/UiCursorGate.cs', [
    ("""        /// <summary>UI 关闭时调用。最后一个关闭者把指针恢复 Locked + invisible。</summary>
        public static void Close()
        {
            // 防御：多余的 Close 不允许把计数打成负（负数会让 IsOpen 恒 false，门形同虚设）
            _openCount = Mathf.Max(0, _openCount - 1);""",
     """        // ─── 评审 04 R-5/R-6 + 07#3：级联关闭注册表 + 同帧关闭痕迹 ──────────────
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
            _openCount = Mathf.Max(0, _openCount - 1);""", 1),
    ("""        public static void Reset()
        {
            _openCount = 0;""",
     """        public static void Reset()
        {
            _lastCloseFrame = -1; // 评审 04 R-5：场景重载清同帧痕迹（回调表由各 UI OnDisable 自摘，不清）
            _openCount = 0;""", 1),
])

# 2) PauseMenuUi：级联关闭 + Esc 让位/标题让位
patch('Assets/Scripts/Unity/UI/PauseMenuUi.cs', [
    ("""            if (open && _help != null && _help.IsOpen) _help.SetOpen(false);""",
     """            // 评审 04 R-6/07#3：级联关闭全部已开模态（此前只关 _help——背包/箱子/武器面板
            // 等开着按 Esc 会叠开暂停，两 UI 都画都收事件）。回调注册见 UiCursorGate.RegisterClose。
            if (open) UiCursorGate.CloseAllRegistered();""", 1),
    ("""            else if (!DeathScreenVisible())
            {
                SetOpen(true);
            }
        }""",
     """            else if (!DeathScreenVisible() && !TitleScreenVisible())
            {
                // 评审 04 R-5/R-6：还有模态开着（或本帧刚被 Esc 关掉）时 Esc 先服务关模态，
                // 暂停放行——同帧双通道（HelpMenuUi Update 轮询 + 本 OnGUI 事件）不再双响
                if (UiCursorGate.OpenCount > 0) return;
                if (UiCursorGate.LastCloseFrame == Time.frameCount) return;
                SetOpen(true);
            }
        }

        /// <summary>标题画面（主菜单遮罩）可见时 Esc 归它管——暂停不开（评审 07#7）。
        /// OverlayVisible 由 TitleScreenUi 与 IsVisible 同步维护。</summary>
        private static bool TitleScreenVisible() => TitleScreenUi.OverlayVisible;""", 1),
])

# 3) TitleScreenUi：OverlayVisible 静态镜像（3 处 IsVisible 赋值点同步）
p = 'Assets/Scripts/Unity/UI/TitleScreenUi.cs'
s = io.open(p, encoding='utf-8').read()
anchor = "        public bool IsVisible { get; private set; } = true;"
assert s.count(anchor) == 1
s = s.replace(anchor, anchor + """

        /// <summary>主菜单遮罩可见性的静态镜像（评审 07#7：PauseMenuUi 的 Esc 让位判定——
        /// 标题画面开着时 Esc 不该弹暂停）。与 <see cref="IsVisible"/> 同点维护。</summary>
        public static bool OverlayVisible { get; private set; } = false;""")
for site in ["                IsVisible = false;", "            IsVisible = true;", "            IsVisible = false;"]:
    assert s.count(site) == 1, site
    s = s.replace(site, site + "\n            OverlayVisible = IsVisible;")
io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('PATCHED', p)

# 4a. 有 Task-17 OnEnable 的 3 个合成 UI：表达式体扩块
for ui in ['CraftingInventoryUi', 'CraftingWorkbenchUi', 'CraftingPocketUi']:
    patch(f'Assets/Scripts/Unity/UI/{ui}.cs', [
        ("private void OnEnable() => CraftGridInteraction.RegisterReturnHandler(ReturnGridToPlayer);",
         """private void OnEnable()
        {
            CraftGridInteraction.RegisterReturnHandler(ReturnGridToPlayer);
            UiCursorGate.RegisterClose(CloseSelf); // 评审 04 R-6：暂停级联关闭
        }

        /// <summary>评审 04 R-6：级联关闭入口（幂等——已关再调 no-op）。</summary>
        private void CloseSelf() => SetOpen(false);""", 1),
    ])

# 4a'. ChestUi 的 OnEnable 同款扩块
patch('Assets/Scripts/Unity/UI/ChestUi.cs', [
    ("private void OnEnable() => CraftGridInteraction.RegisterReturnHandler(ReturnHeldForSave);",
     """private void OnEnable()
        {
            CraftGridInteraction.RegisterReturnHandler(ReturnHeldForSave);
            UiCursorGate.RegisterClose(CloseSelf); // 评审 04 R-6：暂停级联关闭
        }

        /// <summary>评审 04 R-6：级联关闭入口（幂等——手持归位走 Close 兜底链）。</summary>
        private void CloseSelf() => Close();""", 1),
])

# 4b. 无 OnEnable 的 6 个：插 CloseSelf + OnEnable，OnDisable 首行摘除
INS = {
    'CraftingFurnaceUi': 'SetOpen(false)',
    'ArmorSlotsUi': 'SetOpen(false)',
    'WeaponPanelUi': 'SetOpen(false)',
    'EnchantingUi': 'SetOpen(false)',
    'HelpMenuUi': 'SetOpen(false)',
    'TradeUi': 'CloseTrade()',
}
for ui, close_call in INS.items():
    p = f'Assets/Scripts/Unity/UI/{ui}.cs'
    s = io.open(p, encoding='utf-8').read()
    anchor = '        private void OnDisable()'
    assert s.count(anchor) == 1, (ui, s.count(anchor))
    block = ("        /// <summary>评审 04 R-6：级联关闭入口（幂等——已关再调 no-op）。</summary>\n"
             f"        private void CloseSelf() => {close_call};\n\n"
             "        private void OnEnable() => UiCursorGate.RegisterClose(CloseSelf);\n\n")
    s = s.replace(anchor, block + anchor, 1)
    idx = s.index(anchor)
    brace = s.index('{', idx)
    s = s[:brace+1] + '\n            UiCursorGate.UnregisterClose(CloseSelf);' + s[brace+1:]
    io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
    print('PATCHED', p)

# 4c. 4a 的 4 个文件 OnDisable 首行也补摘除
for ui in ['CraftingInventoryUi', 'CraftingWorkbenchUi', 'CraftingPocketUi', 'ChestUi']:
    p = f'Assets/Scripts/Unity/UI/{ui}.cs'
    s = io.open(p, encoding='utf-8').read()
    anchor = '        private void OnDisable()'
    assert s.count(anchor) == 1
    idx = s.index(anchor)
    brace = s.index('{', idx)
    s = s[:brace+1] + '\n            UiCursorGate.UnregisterClose(CloseSelf);' + s[brace+1:]
    io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
    print('PATCHED', p)

print('ALL OK')
