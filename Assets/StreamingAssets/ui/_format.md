# ui/

IMGUI 用 UI 贴图（程序生成的 PNG，64×64、像素风）。放 StreamingAssets 是因为
`HotbarUI.LoadUiTextureOrFallback` 运行时按 `Application.streamingAssetsPath/ui/` 读盘，
编辑器与 standalone build 同路径（旧代码走 `dataPath/../Assets` 是编辑器专用，build 必 fallback）。

| 文件 | 用途 | 规格 |
| --- | --- | --- |
| `hotbar-slot.png` | hotbar 槽位底 | 64×64，半透明深灰底 RGB(30,30,34) A160 + 2px 浅灰边框 RGB(90,90,96) A200 |
| `hotbar-select.png` | 选中槽边框 | 64×64，内部全透明 + 4px 亮白黄 #F5D76E 边框（必须是边框，实心会盖住物品图标） |
| `heart-full.png` | 血条满心 | 24×24 红心（m11 W2-3 从 Assets/Art/UI 挪入——build 读不到 Assets 目录；`HealthBarUI` 加载，缺文件退程序生成心形） |
| `heart-half.png` | 血条半心 | 24×24 左半红右半暗（同上） |
| `heart-empty.png` | 血条空心 | 24×24 暗灰心形轮廓（同上） |
