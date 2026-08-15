# milestone-6 设计：UI 修复 + 帮助菜单 + 引导任务链

> 日期：2026-08-15
> 状态：已通过设计评审（父子共同开发项目）
> 前置：milestone-5（视觉与体验打磨）已完成，dotnet 440 / EditMode 588 全绿
> 诊断报告：`.superpowers/m6-ui-diagnosis.md`（UI 问题全部有 file:line 证据）

## 背景与问题

父子实机试玩反馈：UI「有框但内容空」——背包/工作台/血条/hotbar 的物品图标没正常显示。
诊断结论（三个根因，均非 m5 回归）：

| 根因 | 证据 |
| --- | --- |
| HotbarUI 槽底/选中框贴图走编辑器专用路径 `dataPath/../Assets/Art/UI`，standalone build 必 fallback；选中框 fallback 是不透明纯白 1×1，整块盖住默认选中 slot 0 的图标 | `HotbarUI.cs:34-37/50-64/148-151`；build 目录下实测无该路径 |
| 背包/工作台/口袋/交易 UI 从未实现图标——`DrawSlot` 只有黑字 Label，叠深色 Box 不可读；且布局越界（背景框 240 高装 360 的内容） | `CraftingInventoryUi.cs:82-125`、`CraftingWorkbenchUi.cs:48-64` |
| 14/60 物品贴图缺失（dirt/log/beef/chicken 等前期必得品）显示品红占位；48px 图标/18px 心形在原生分辨率全屏下偏小 | items/*.json texture 字段 vs textures/*.png 的 comm 比对 |

**验证盲区根源**：全部 UI 是 IMGUI（OnGUI），visual-smoke 走 `Camera.Render` 拍不到 IMGUI——UI 从未被视觉验证过，编辑器正常 build 全坏的 bug 无处暴露。

另一个流程问题：游戏没有「现在该干什么」——孩子进世界后面对无目标沙盒，需要引导。

## 目标

1. UI 内容真正显示：图标、数量、文本在实机 build 里清晰可见
2. UI 有视觉回归守卫（截图管线覆盖 IMGUI）
3. H 键帮助菜单：怎么玩 + 设置
4. 引导任务链：8 步首章，孩子始终知道下一步

## 非目标（YAGNI）

- 成就系统、任务链多章节（首章 8 步打样，后续里程碑扩展）
- UI 美术重制（保持 IMGUI + 像素风，只修内容与可读性）
- IMGUI 整体分辨率适配矩阵（GUI.matrix 缩放另立任务；本里程碑只确保默认分辨率下可读）
- 手柄/键位重映射

## 1. UI 修复三连

### 1a. HotbarUI 路径迁移

- 槽底/选中框贴图从 `dataPath/../Assets/Art/UI/` 迁到 `Assets/StreamingAssets/ui/`（hotbar-slot.png / hotbar-select.png 程序生成或复制现有），加载路径与 items/textures 同构（`streamingAssetsPath` 前缀，build 与编辑器同路径）
- fallback 语义修正：槽底缺失 → 半透明黑 1×1（现状可接受）；**选中框缺失 → 4px 亮色边框描边（GUI.Box 边框风格），绝不整块纯白**
- 既有 `ItemTextureFileTests` 模式补 `UiTextureFileTests`：`StreamingAssets/ui/` 下引用的 PNG 必须存在

### 1b. 公共 DrawItemSlot（图标管线复用到全部 UI）

新建 `Assets/Scripts/Unity/UI/ItemSlotDrawer.cs`：

- `static void Draw(Rect slot, ItemStack stack, ItemDatabase items, bool selected)`——画图标（复用 HotbarUI 的 LoadImage + Point 过滤管线，纹理缓存共享）+ 右下数量角标（白字粗体）+ 选中高亮
- 改造 `CraftingInventoryUi` / `CraftingWorkbenchUi` / `CraftingPocketUi` / `CraftingFurnaceUi` / `TradeUi`：删黑字 Label 路径，全部走 `DrawItemSlot`；名称文字用白字样式（缓存 GUIStyle，沿用 HotbarUI 模式）
- **修布局越界**：背景 Box 尺寸按内容重新计算（背包框高 ≥ 360、工作台宽度 ≥ 420），所有格子必须在框内；悬停 tooltip 显示物品全名（可选，格子里放不下全名时兜底）

### 1c. 补 14 张物品贴图

- 优先级：dirt/log/beef/chicken（前期必得）→ arrow → 其余 10 个（chair/globe/hacker_pc/keyboard/laptop/mouse/notebook/office_desk/table）
- 程序生成占位（同 m2 snow.png 模式：确定性哈希铺色、16×16、ItemTextureFileTests 校验）；art/requests/ 立需求待正式美术替换
- block 类物品（dirt/log）短期可复用方块贴图，二选一以实现简繁定

## 2. UI 截图验证管线

- 新建 `Assets/Scripts/Unity/UiScreenshotOnArg.cs`（运行时组件）：读 `Environment.GetCommandLineArgs()`，见 `--ui-shot` 时：
  1. 等 60 帧（纹理加载 + OnGUI 跑过）
  2. `ScreenCapture.CaptureScreenshot("ui-hotbar.png")`（能拍下 IMGUI）
  3. 反射/公开开关打开背包（E 等效）→ 再等 10 帧 → 截 `ui-inventory.png`
  4. 打开工作台 → 截 `ui-workbench.png`
  5. 写 `.done` 标记文件后 `Application.Quit()`
- `WorldBootstrap` 无条件挂载（无参数时零开销：启动读一次 args 即返回）
- `visual-smoke.sh` 加一步：build 后以 `-screen-width 1280 -screen-height 720 --ui-shot` 启动 exe，轮询 3 张 PNG + `.done` 落盘（超时 60s）
- EditMode 像素断言（读 PNG）：hotbar 第 0 格中心非纯白、背包区域非纯背景色（防止「框在内容空」回归）

## 3. 帮助菜单（H 键）

`Assets/Scripts/Unity/UI/HelpMenuUi.cs`：

- **怎么玩页**：按键表（WASD 移动 / 空格跳 / 鼠标左右键 挖放 / 滚轮+1-9 切槽 / E 背包 / P 工作台 / B 口袋合成 / V 交易 / X 附魔 / H 帮助 / F11 全屏）+ 四步玩法（挖→捡→合→用）+ 当前任务目标（读 QuestSystem）
- **设置页**：鼠标灵敏度（0.5×–2×）、主音量（0–100）、FOV（60–90）三个滑条；写 `PlayerPrefs`，启动时读回
- 样式与背包 UI 统一（半透明深色 + 白字）；H 或 Esc 关闭；打开时锁鼠标操作（不锁移动，简单起见：打开期间忽略挖/放输入）

## 4. 引导任务链

### Core 层（零 UnityEngine）

`Assets/Scripts/Core/Quests/QuestSystem.cs` + `QuestChain.cs`：

```csharp
public sealed class QuestSystem
{
    public Quest Current { get; }            // 当前任务（链式，完成才解锁下一个）
    public int CompletedCount { get; }
    public bool TryComplete(QuestEvent evt); // 事件驱动推进
    public QuestState SaveState();           // 进 m4 level.dat 新字段
    public void Restore(QuestState state);
}
public readonly struct QuestEvent { public QuestEventType Type; public int ItemId; public int Count; public string Flag; }
```

- 完成条件类型（首章够用）：`ObtainItem`（背包拥有 N 个 X）、`CraftItem`（合成产出 X）、`SmeltItem`（熔炉产出 X）、`SurviveNight`（跨过一次日出）
- 数据驱动：`Assets/StreamingAssets/quests/chapter1.json`（id/名称/描述/条件/奖励经验）

### 首章 8 步

| # | 任务 | 条件 | 奖励 |
| --- | --- | --- | --- |
| 1 | 挖一根原木 | ObtainItem log ×1 | 5 |
| 2 | 合成木板 | CraftItem plank ×4 | 5 |
| 3 | 造工作台 | CraftItem crafting_table ×1 | 10 |
| 4 | 做木镐 | CraftItem wooden_pickaxe ×1 | 10 |
| 5 | 挖三块石头 | ObtainItem cobblestone ×3 | 10 |
| 6 | 造熔炉 | CraftItem furnace_block（或既有合成路径里最近的）×1 | 15 |
| 7 | 炼一根铁锭 | SmeltItem iron_ingot ×1 | 20 |
| 8 | 活过一夜 | SurviveNight | 30 |

（任务 6 的熔炉物品 id 以 items/*.json 实际注册为准，实现时核对。）

### 事件接线（Unity 侧，既有系统的缝里插）

- 挖方块/拾取 → `PlayerController.PickupNearbyDrops` 与 `BlockInteraction.BreakAt` 发 `ObtainItem`
- 合成 → `CraftingInventoryUi/WorkbenchUi` 的产出路径发 `CraftItem`
- 熔炉 → `FurnaceSystem.TakeOutput` 发 `SmeltItem`
- 昼夜 → `TimeOfDay` tick 跨过 NightEndTick 发 `SurviveNight`
- 全部经一个薄的 `QuestEventBus`（PlayerContext 上的组件）转发给 `QuestSystem`，游戏逻辑不感知任务系统

### UI

- `QuestHudUi`：右上角小卡片「当前目标：合成木板 0/4」，条件满足瞬间打勾 + 经验入账提示（复用 Experience）
- 完成后 1s 自动切下一任务
- H 帮助菜单「怎么玩」页显示全链进度（8 格小图标，完成的亮）

### 存档

`LevelData` 加 `QuestState Quest` 字段（当前任务 id + 完成计数），`SaveLoadService` 收集/恢复；旧档无此字段 = 全新开始任务链（Newtonsoft 缺字段默认 null，自然兼容）

## 5. 测试与验收

**自动化**：
- dotnet：QuestSystem 状态机（链式解锁/条件判定/Save-Restore round-trip/事件类型分派）、quests.json 加载校验（引用的 itemId 必须在 ItemDatabase 注册——同 block_drops 的悬空引用守卫）
- EditMode：ItemSlotDrawer 参数化绘制（EditMode 可测 GUI 参数与纹理缓存）、UiTextureFileTests、路径迁移后 HotbarUI 加载成功、帮助菜单按键与 PlayerPrefs round-trip
- **UI 截图管线**：visual-smoke 含 3 张 UI 截图 + 像素断言
- 基线：dotnet 440 / EditMode 588 只增不减

**实机验收剧本**：
1. 进游戏：hotbar 第 0 格可见木板图标（非白块）
2. 挖泥土 → 图标（非品红）入包，数量角标正确
3. E 开背包：图标 + 数量清晰、格子在框内；P 工作台同
4. 右上角「当前目标：挖一根原木」，挖到即打勾给经验、自动下一目标
5. H 帮助菜单：按键表可读，音量/灵敏度/FOV 滑条即时生效且重进保留
6. 按链走到「活过一夜」，退出重进任务进度还在（m4 回归兼容）

## 与既有约束的关系

- Core 零 UnityEngine（QuestSystem 纯 C#，JSON 数据驱动，确定性：无 Random）
- 注释/断言中文；测试双链；StreamingAssets 数据表模式与 blocks/items/quests 一致（`_format.md` 说明 schema）
- m4 存档语义不变（LevelData 加字段向后兼容，旧档自然全新开始任务链）
