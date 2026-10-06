# -*- coding: utf-8 -*-
# patch_esc 续跑：第 3 节（修计数）+ 第 4 节（10 个模态 UI 接线）
import io

def patch(path, subs):
    s = io.open(path, encoding='utf-8').read()
    for old, new, cnt in subs:
        assert s.count(old) == cnt, f"{path}: {old[:48]!r} x{s.count(old)} != {cnt}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(s)
    print('PATCHED', path)

# 3) TitleScreenUi：OverlayVisible 静态镜像（4 处 IsVisible 赋值点同步：16 空格 false ×1、
#    12 空格 true ×1、12 空格 false ×2——两个 false 都要挂镜像）
p = 'Assets/Scripts/Unity/UI/TitleScreenUi.cs'
s = io.open(p, encoding='utf-8').read()
anchor = "        public bool IsVisible { get; private set; } = true;"
assert s.count(anchor) == 1
s = s.replace(anchor, anchor + """

        /// <summary>主菜单遮罩可见性的静态镜像（评审 07#7：PauseMenuUi 的 Esc 让位判定——
        /// 标题画面开着时 Esc 不该弹暂停）。与 <see cref="IsVisible"/> 同点维护。</summary>
        public static bool OverlayVisible { get; private set; } = false;""")
subs = [("                IsVisible = false;", 1), ("            IsVisible = true;", 1),
        ("            IsVisible = false;", 2)]
for site, cnt in subs:
    assert s.count(site) == cnt, (site, s.count(site))
    s = s.replace(site, site + "\n            OverlayVisible = IsVisible;")
io.open(p, 'w', encoding='utf-8', newline='\n').write(s)
print('PATCHED', p)

# 4a. 三个合成 UI OnEnable 扩块 + ChestUi 同款
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

# 4c. 4a 的 4 个文件 OnDisable 首行补摘除
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
