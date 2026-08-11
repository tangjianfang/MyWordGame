# 里程碑-3 gameplay 层设计

> **For agentic workers:** 实施计划见 `docs/superpowers/plans/2026-08-11-milestone-3.md`
> （含 3a / 3b / 3c 三段）。本 spec 是三段共同的"做成什么样"。

**目标：** 把"渲染世界但玩不起来"变成一个能挖 / 能放 / 能合成的沙盒，含树、家具、动物、战斗、水域与汤类食物。

**范围：** A 全屏与第一人称显示 · B 树与水域生成 · C 交互修复 ·
D 物品栏与多档合成网格 · E 家具与食物方块 ·
F 拉杆 / 铁门 / 按钮机制 · G 动物实体（含敌对生物与战斗） ·
H 剑 / 工具伤害系统 · I 食物 / 汤剂系统。

**硬约束：** 仓库内文档、注释、测试断言一律中文；新方块走 `Assets/StreamingAssets/blocks/*.json` + 贴图路径；
Core 层禁止 `UnityEngine.*`；新增 Gameplay 层不依赖 UnityEditor。

---

## 1. 架构

### 1.1 三层职责

| 层 | 位置 | 状态 | 本里程碑新增 |
| --- | --- | --- | --- |
| `MyWorld.Core` | `Assets/Scripts/Core` | 已有 | `Inventory` / `CraftingRecipe` / `Mob` / `MobAI` / `TreePlacer` / `WaterFeatures` / `PlayerHealth` / `CombatMath` |
| `MyWorld.Unity` | `Assets/Scripts/Unity` | 已有 | 修 `PlayerController.Awake` Eye 自动绑、修 `BlockInteraction` 早返回 |
| `MyWorld.Gameplay` | `Assets/Scripts/Gameplay` | **新建** | 显示层（手 / 工具 / 全屏）、UI（hotbar / 合成 / 血条）、实体（mob / 剑挥）、战斗（伤害 / 死亡 / 掉落）、水域渲染 |

`MyWorld.Gameplay.asmdef` 设 `"references": ["MyWorld.Core", "MyWorld.Unity"]`、`"includePlatforms": []`（运行时可用），不引用 `UnityEditor`。

### 1.2 不属于本里程碑

- 真正红石线（红石粉、信号传播、邻居通知）——留 3d
- 敌对生物种类拓展（骷髅 / 蜘蛛 / 苦力怕）——只做僵尸一种；3d 再扩
- 村民与交易系统
- 经验 / 附魔 / 工具耐久度
- 玩家第三人称模型
- 多人 / 存档扩展（RegionFile 仍只覆盖地形方块）
- 状态方块的存档持久化（chunk _blockStates 不进 RegionFile）
- 食物的饱食度（minecraft 有 6 排鸡腿图标）——3a 用"回血数值"代替饱食度

---

## 2. 子系统 A — 显示层

### 2.1 全屏（`Assets/Scripts/Gameplay/Display/FullscreenController.cs`）

- MonoBehaviour，`[DisallowMultipleComponent]`
- `Awake`：从 `PlayerPrefs.GetInt("MyWordGame.Fullscreen", 1)` 读上次状态；`1 = 全屏` / `0 = 窗口化`
- 全屏启动：`Screen.fullScreenMode = FullScreenMode.FullScreenWindow; Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, true);`
- 窗口化：`-1` 让 Unity 用 `Screen.currentResolution` 的子集，记下 width/height 持久化
- `Update`：监听 `Input.GetKeyDown(KeyCode.F11)` 切换
- **不要**支持多分辨率选择器，只保留默认 / 上次的尺寸

### 2.2 第一人称手（`Assets/Scripts/Gameplay/Display/FirstPersonHand.cs`）

- `PrimitiveType.Cube` 占位（无贴图，用 URP/Lit 默认材质 + emissive 调色）
- `localPosition = (0.32f, -0.28f, 0.55f)`、`localRotation = Quaternion.Euler(8f, -8f, 35f)`、`localScale = (0.12f, 0.12f, 0.32f)`（长方体模拟握柄）
- 挂在 `Camera.main.transform` 下
- `Swing(ToolAction)` 方法触发 0.25s 摆动：
  - **dig**：`Mathf.PingPong(t / 0.25f * 2, 1) * 75f - 22.5f` 给本地 rotation.x
  - **place**：`Mathf.PingPong(t / 0.25f * 2, 1) * -50f + 12.5f` 给本地 rotation.x
  - 用 `Update` 里读 `Time.time - swingStartTime` 算 t，到 0.25 重置回静止 pose
- 工具切换（后续可扩展，目前只有一种占位）

### 2.3 Eye 修复（`Assets/Scripts/Unity/Player/PlayerController.cs`）

`Awake()` 末尾加：

```csharp
if (eye == null) { eye = Camera.main != null ? Camera.main.transform : transform; }
```

`BlockInteraction.Update` 去掉 `_player.Eye == null` 的早返回条件（既然 Awake 一定绑了）。

### 2.4 工具挖放接口（`Assets/Scripts/Gameplay/Display/ToolSwingBridge.cs`）

- `WorldBootstrap.Awake` 末尾 `GetComponent<ToolSwingBridge>() ?? AddComponent<ToolSwingBridge>()`
- 持有 `FirstPersonHand` 引用、`BlockInteraction` 引用
- 监听挖（左键）与放（右键）：在 `BlockInteraction.Update` 之前判断 `Input.GetMouseButtonDown(0/1)`，调 `hand.Swing(Dig)` / `hand.Swing(Place)`
- **3a 决策**：不重构 `BlockInteraction.Update`，挖放判断保留在那里；ToolSwingBridge 通过订阅 Input 事件触发 hand 摆动，避免和 BlockInteraction.Update 抢输入顺序

---

## 3. 子系统 B — 世界内容

### 3.1 树生成（`Assets/Scripts/Core/WorldGen/TreePlacer.cs`）

```
namespace MyWorld.Core.WorldGen;
public static class TreePlacer {
  public static void Place(ChunkColumn column, int worldX, int worldZ, ValueNoise2D placementNoise);
}
```

- 在 `WorldGenerator.Generate` 末尾遍历 `(localX, localZ)`，对每个地表格：
  - 第二次采样噪声 `placementNoise.Sample(worldX*0.13, worldZ*0.13)` > 0.78 → 视为"该格上方要种树"
  - **确定性**与跨区块接缝：树占 5×5 叶子冠 + 1×1 × N 树干；用「中心格」作为种子格，把整棵树放到中心格所在的 `ChunkColumn`
  - 树干 4–7 格高（用第三次采样定高）
  - 叶子 5×5 顶层 + 4×4 第二层，顶角 25% 概率去掉形成不规则形状
- 跨区块的树：在每个生成阶段只往中心格 column 写方块；其他 column 调到下一帧再做（这是性能优化点；本里程碑 3a 暂不实现跨区块 chunk-loading 协调，由 ChunkStreamer 多调几次 Generate 兜底）
- **3b 实现**：真正跨 chunk 的树要做"延后写入 + 邻居回调"。3a 只放同 chunk 内的树

### 3.2 水域生成（`Assets/Scripts/Core/WorldGen/WaterFeatures.cs`）

参照《我的世界》三种水域：

```
namespace MyWorld.Core.WorldGen;
public static class WaterFeatures {
  // 海：WorldGenerator 已有的海平面（y ≤ 62）维持，作为无限海洋
  // 湖：低洼地带积水
  // 水洼：地表上凹陷（雨水残留）
  public static void FillLakes(ChunkColumn column, ValueNoise2D moistureNoise);
  public static void FillPuddles(ChunkColumn column, ValueNoise2D moistureNoise, int seed);
}
```

**湖（lakes）**：
- 用 `moistureNoise` 第二次采样：`moisture > 0.62 && surfaceY <= SeaLevel + 4` → 该格地表下挖 3×3×2 形成浅坑，灌水
- 坑大小 3–7 格（按第三次采样定尺寸），统一到 chunk 内；跨 chunk 的湖留 3c

**水洼（puddles）**：
- 在高于 SeaLevel 的地表，按 `moistureNoise > 0.85` 的概率随机挖 1×1×1 凹陷并灌水
- 体现"雨后积水"的视觉细节

**海（ocean）**：
- 已有的 `SeaLevel = 62` 在 WorldGenerator 维持——大片低于此高度的区域就是海
- 3a 不加新行为，沿用现状

**确定性**：所有水域用同一 `moistureNoise`，与地形噪声同 seed，保证可复现。

### 3.3 新增方块 ID（`Assets/Scripts/Core/Voxel/BlockIds.cs`）

保留现有 0–6，新增 7–26：

```csharp
public const ushort Log         = 7;
public const ushort Leaves      = 8;
public const ushort Planks      = 9;
public const ushort Glass       = 10;
public const ushort Cobblestone = 11;
public const ushort IronDoor    = 12;  // 状态方块
public const ushort Lever       = 13;  // 状态方块
public const ushort Button      = 14;  // 状态方块
public const ushort Table       = 15;
public const ushort Chair       = 16;
public const ushort Desk        = 17;
public const ushort Notebook    = 18;
public const ushort Keyboard    = 19;
public const ushort Mouse       = 20;
public const ushort Computer    = 21;
public const ushort CraftingTable = 22;
public const ushort Bowl        = 23;  // 食物方块占位
public const ushort Globe       = 24;  // 装饰方块
public const ushort Stone       = 1;   // 已有，但剑合成需要
public const ushort DiamondOre  = 25;  // 稀有的地底矿石
public const ushort BedrockSwordBlock = 26;  // 占位方块，仅用于合成公式里"基岩"标识
```

### 3.4 新方块 JSON

每方块一份 `Assets/StreamingAssets/blocks/<id>.json`：

- `log`: `opaque=true, solid=true`，top/bottom = `log-top`，side = `log-side`
- `leaves`: `opaque=false, solid=true`
- `planks`: 标准方块
- `glass`: `opaque=false, solid=true`
- `cobblestone`: 标准方块
- `diamond_ore`: `opaque=true, solid=true`，贴图 `diamond-ore`，亮度 `lightEmission=4`（微弱发光）
- `iron_door`: `solid=true, opaque=true`，stateful
- `lever`: `solid=false, opaque=true`，stateful
- `button`: `solid=false, opaque=true`，stateful
- `crafting_table`: `solid=true, opaque=true`，top 贴图 `crafting-top`、side 贴图 `crafting-side`
- `bowl`: `solid=false, opaque=true`，单独方块（右键使用 → 吃）
- `globe`: `solid=true, opaque=true`，装饰
- `table/chair/desk/notebook/keyboard/mouse/computer`: 标准方块
- `bedrock_sword_block`: 不生成在世界、仅作为配方标识（数值 id 占位，避免合成系统崩）

`BlockDefinitionFilesTests` 须校验：贴图文件名出现在 `art/requests/blocks/<texture>.md`（占位 OK）。**3a 放宽校验**：占位文档存在即可，文件名大小写不敏感。

### 3.5 贴图

程序生成 32×32 PNG，用 System.Drawing：

- 纯色背景 + System.Drawing 居中写方块 id 文字（10pt 字体）
- 输出到 `Assets/StreamingAssets/textures/<textureName>.png`
- 一次性 CLI 入口：`tools/scripts/generate-placeholder-textures.sh` 调 `dotnet run --project tools/dotnet/MyWorld.Preview -- textures`

> **风险点**：Unity 的 BuildPlayer 不会自动 include `Assets/StreamingAssets/textures/` ——它会，但 `BlockMaterialLibrary.Load` 走 `File.ReadAllBytes(textureDirectory + textureName + ".png")`，这是 Application.streamingAssetsPath，**3a 先验证**：在 build 里读 StreamingAssets 是否走对路径（Windows Standalone 是 `Builds/Windows/MyWordGame_Data/StreamingAssets/`）。

---

## 4. 子系统 C — 交互修复

### 4.1 PlayerController

- 加 `[RequireComponent(typeof(Camera))]` 让 Camera 与 PlayerController 同 GameObject（PlayerController 已经在 Player root，Camera 在子物体上，**不改 RequireComponent**，走 Awake 里 `Camera.main` 兜底）
- `Awake` 自动绑 `eye = Camera.main.transform`
- 加 `public Transform Eye { get; private set; }` 公开访问器（已有），保证 `BlockInteraction.Update` 取到的 Eye 非 null

### 4.2 BlockInteraction

- 去掉 `if (_world == null || _player.Eye == null) return;` 中的 Eye 条件
- 去掉 `if (Cursor.lockState != CursorLockMode.Locked) return;` 早返回（**保留**鼠标锁定语义——但允许鼠标未锁定时也能切换 hotbar）
- 增加：`if (Input.GetKeyDown(KeyCode.E)) ToggleCrafting();` 与 WorldBootstrap 提供的 `CraftingUI` 桥接

### 4.3 Hotbar（`Assets/Scripts/Gameplay/UI/HotbarUI.cs`）

- OnGUI（**用 IMGUI**，URP 下 Canvas + RawImage 一致性反而麻烦；3a 用 IMGUI）
- 屏幕底部居中，9 槽，每槽 64×64 pixel，相邻 4 pixel
- `selectedIndex` 0–8，监听 1–9 数字键与滚轮（`Input.GetAxis("Mouse ScrollWheel")`）
- 槽内显示方块图标：从 `BlockMaterialLibrary` 拿贴图 → GUI.DrawTexture
- 数量文本：右下方 `GUI.Label`，字体 14pt
- 选中槽高亮：边框 2 pixel 黄色

### 4.4 状态通知

`BlockInteraction` 的 `placeBlockId` 改成读 hotbar：`ushort placeBlockId = hotbar.SelectedBlockId();`
改 `BlockInteraction` 不持有 `placeBlockId` 字段，改为 `BlockInteraction.Bind` 多接收 `Func<ushort> placeBlockProvider`。

---

## 5. 子系统 D — 物品栏与多档合成网格

### 5.1 Inventory（`Assets/Scripts/Core/Inventory/Inventory.cs`）

```
public sealed class Inventory {
  private readonly InventorySlot[] _slots;     // 36 个
  public int Capacity => 36;
  public InventorySlot Get(int index);
  public int Add(ItemStack stack);             // 返回剩余数量
  public int RemoveAt(int index, int count);   // 返回实际移除数量
  public void Compact();                        // 合并同 id 零散栈
}

public readonly struct InventorySlot {
  public ushort ItemId;     // 0 = 空
  public ushort Count;
}

public static class Items {
  public const ushort None         = 0;
  public const ushort Log          = 1;
  public const ushort Plank        = 2;
  public const ushort Stick        = 3;
  public const ushort Cobblestone  = 4;
  public const ushort IronIngot    = 5;     // 由 iron_ore 烧炼——3a 用挖出即得，3b 加炉子
  public const ushort Diamond      = 6;
  public const ushort NetheriteIngot = 7;    // 3a 用一个假 netherite 矿代替
  public const ushort BedrockShard = 8;     // 基岩剑合成素材
  public const ushort WoodenSword  = 100;
  public const ushort StoneSword   = 101;
  public const ushort IronSword    = 102;
  public const ushort DiamondSword = 103;
  public const ushort NetheriteSword = 104;
  public const ushort BedrockSword = 105;
  public const ushort Bowl         = 200;
  public const ushort BeetSoup     = 201;
  public const ushort MungBeanSoup = 202;
  public const ushort Globe        = 300;
}
```

**两套 id** 是关键：方块是 `BlockIds.Log = 7`，物品栏里是 `Items.Log = 1`。`ItemRegistry` 维护 `ItemId → BlockId` 的映射，剑 / 汤 / 地球仪等"非方块物品"映射为 `BlockId = 0`（不可被 place 成方块）。

### 5.2 合成网格分级（参照《我的世界》）

```
public enum CraftingTier {
  Pocket,     // 1×1，玩家随时可用，单格配方（1 log → 4 planks）
  Inventory,  // 2×2，玩家打开 inventory 自带，4 格配方（4 planks → 1 crafting_table）
  Workbench   // 3×3，必须右键放置 crafting_table 后站在方块旁才解锁，9 格配方
}
```

- **Pocket（1×1）**：始终在 hotbar 旁边显示一个单独"output slot"；把任意一个物品丢进 pocket 区域 → 匹配 1×1 配方 → 出来结果
- **Inventory（2×2）**：按 E 打开 inventory 时自带一个 2×2 小合成区，位于 inventory 右上角
- **Workbench（3×3）**：右键放置 `crafting_table` 方块后，对着方块按 E → 打开 3×3 合成 UI

### 5.3 合成（`Assets/Scripts/Crafting/CraftingRecipe.cs` + `CraftingGrid.cs`）

```
public sealed class CraftingRecipe {
  public ushort ResultItem;
  public ushort ResultCount;
  public CraftingSlot[] Inputs;   // 长度 1/4/9
  public CraftingTier Tier;       // 该配方适用的网格档位

  // 关键算法：判断当前 grid 是否匹配此配方
  public bool Matches(CraftingSlot[] grid, int gridSize, out CraftingSlot[] consumed);
}

public sealed class CraftingGrid {
  private CraftingSlot[] _slots;
  public int Size { get; }      // 1/4/9
  public CraftingRecipe Match();  // 返回首个匹配或 null
  public void TakeConsumed();     // 扣掉 consumed 格子
}
```

#### 5.3.1 起步配方（plan-3a 全部实现）

| 配方 | Tier | inputs | result |
| --- | --- | --- | --- |
| 原木 → 4 木板 | Pocket | `(Log, 1)` | `(Plank, 4)` |
| 4 木板 → 工作台 | Inventory | 2×2 `(Plank, 4)` | `(CraftingTable, 1)` |
| 2 木板 → 4 木棍 | Inventory | `(Plank, 2)` | `(Stick, 4)` |
| 木剑 | Workbench | 3×3 倒数第 2 排中间=木板、上下=木棍 | `(WoodenSword, 1)` |
| 石剑 | Workbench | 同上但中间=圆石 | `(StoneSword, 1)` |
| 铁剑 | Workbench | 同上但中间=铁锭 | `(IronSword, 1)` |
| 钻石剑 | Workbench | 同上但中间=钻石 | `(DiamondSword, 1)` |
| 下界合金剑 | Workbench | 同上但中间=下界合金锭 | `(NetheriteSword, 1)` |
| 基岩剑 | Workbench | 3×3 全是 bedrock_shard | `(BedrockSword, 1)` |
| 圆石 + 玻璃 → 碗 | Workbench | `(Cobblestone, 4)` 2x2 中心 | `(Bowl, 1)` |
| 碗 + 红菜头 → 红菜头汤 | Workbench | 碗围一圈、中心 1 红菜头 | `(BeetSoup, 1)` |
| 碗 + 绿豆 → 绿豆汤 | Workbench | 碗围一圈、中心 1 绿豆 | `(MungBeanSoup, 1)` |
| 4 木板 + 4 圆石 → 地球仪 | Workbench | 2×2 上下圆石、上下木板 | `(Globe, 1)` |

### 5.4 Crafting UI（`Assets/Scripts/Gameplay/UI/CraftingUI.cs`）

- 按 E 切换显示：
  - Pocket 始终在屏幕右下角一个小窗（hotbar 左边），不需要开 inventory
  - 按 E → 打开 inventory 窗口（含 2×2 crafting 区）
  - 对 crafting_table 按 E → 打开 3×3 workbench 窗口
- IMGUI 绘制；点击格子 cycle ItemId（debug 用，未来可换成槽位拖拽）
- Shift+点击 result → 把结果放进 hotbar 第一个有空位的槽
- 关闭 UI（再按 E）：丢弃 grid 内容（3a 不做"关闭时归还"）

### 5.5 工作台方块

- `crafting_table` 是 1×1×1 方块，无状态
- 玩家距离 ≤ 5 格、`Input.GetKeyDown(KeyCode.E)` 且视线命中 → 打开 3×3 workbench UI（区别于 inventory 的 2×2）
- 玩家移出 5 格范围 → UI 自动关闭
- 行为由 `CraftingStation`（MonoBehaviour）实现，每帧检测玩家与已放置工作台的距离

---

## 6. 子系统 H — 剑 / 工具伤害系统

### 6.1 剑作为物品

剑在 `Items` 里是 6 个独立 id（WoodenSword = 100 等），都映射到 `BlockId = 0`（**不可放置成方块**）：

| 物品 id | 名字 | 攻击伤害 | 攻击速度（次/秒） | 击退 |
| --- | --- | --- | --- | --- |
| WoodenSword | 木剑 | 4 | 1.6 | 弱 |
| StoneSword | 石剑 | 5–6 | 1.6 | 弱 |
| IronSword | 铁剑 | 10 | 1.6 | 中 |
| DiamondSword | 钻石剑 | 19 | 1.6 | 中 |
| NetheriteSword | 下界合金剑 | 24 | 1.6 | 强 |
| BedrockSword | 基岩剑 | 50 | 1.6 | 极强 |

数据来源：`Assets/Scripts/Core/Combat/ItemCombatStats.cs`，静态字典 `ItemId -> {Damage, AttackSpeed, Knockback}`。

> **说明**：基岩剑 50 伤害超过 minecraft 的 netherite 24，参考 spec 6.1 表。

### 6.2 持剑动画

- 玩家主手持剑时（hotbar 选中剑 item），`FirstPersonHand.Swing(SwingType.Attack)` 触发更大幅度的挥砍动画：
  - 持续 0.35s（比挖放的 0.25s 长）
  - 旋转幅度 ±60°
  - 沿 Y 轴整体下沉 0.05 单位（模拟"砍下去"）
- 击中 mob 时暂停 0.05s 模拟"顿挫感"
- 攻击节流：`AttackCooldown = 1f / AttackSpeed`，期间内重复点击无效

### 6.3 玩家血量（`Assets/Scripts/Core/Combat/PlayerHealth.cs`）

```
public sealed class PlayerHealth {
  public float Current = 20f;       // 10 颗心
  public float Max = 20f;
  public float LastDamageTime;

  public void Damage(float amount);
  public void Heal(float amount);   // 不能超过 Max
  public float RegenRate = 1f;      // 1 hp/秒，未受伤 5 秒后开始回血
}
```

- 受伤时记 `LastDamageTime = Time.time`
- 每帧检查：`if (Time.time - LastDamageTime > 5f) Current = Math.Min(Current + RegenRate * dt, Max);`
- `Damage` 限制 `Current >= 0`：归零后由 Gameplay 层决定是否死亡（3a 不做死亡惩罚，玩家卡死即可）

### 6.4 玩家 UI

- 屏幕底部 hotbar 上方一行画 10 颗心：
  - 满血 = 红心，残血 = 空心 + 红点（5 hp 切换）
  - 受击瞬间红屏闪烁 0.2s
- 实现：`HealthUI.cs`，OnGUI 画 hearts（10 个 16×16 区域）

---

## 7. 子系统 I — 食物 / 汤剂系统

### 7.1 食物作为物品

汤类是可堆叠物品（`Count` 字段生效），右键使用一次扣 1、`Heal(value)`：

| 物品 id | 名字 | 治疗量 | 备注 |
| --- | --- | --- | --- |
| Bowl | 空碗 | 0 | 单纯是合成素材 |
| BeetSoup | 红菜头汤 | 6 hp（3 颗心） | minecraft 原版 |
| MungBeanSoup | 绿豆汤 | 8 hp（4 颗心） | 用户新增，比甜菜汤略强 |

### 7.2 食物使用接口（`Assets/Scripts/Core/Items/FoodItem.cs`）

```
public static class FoodItem {
  public static float HealValue(ushort itemId);
  public static bool IsFood(ushort itemId);   // 包含汤类 + 未来更多
}
```

### 7.3 食物消费

- `BlockInteraction.Update`：当视线未命中任何方块且右键 → `ConsumeFood(hotbar.SelectedItemId)`
- 流程：
  1. 校验 `IsFood(itemId)`
  2. 校验 `PlayerHealth.Current < Max`（满血时右键食物无效）
  3. `PlayerHealth.Heal(HealValue(itemId))`
  4. `Inventory.RemoveAt(hotbar.SelectedIndex, 1)`
- 不做饱食度（spec 1.2 已声明）

### 7.4 食物配方中新增原料

合成甜菜汤 / 绿豆汤需要 `beet` / `mung_bean` 物品：

- 3a 简化：从 `world.GetBlock(beet_farm_position)` 直接取；3b 加独立的 beet 作物生成
- 3a 临时方案：`beet` 物品只在玩家挖掉地表 `grass` 时 5% 概率掉落 + `mung_bean` 物品由合成台从 `plank + cobblestone` 给 1 个

> 3a 不做真正的甜菜 / 绿豆作物方块

---

## 8. 子系统 E — 家具方块

每个家具就是 1×1×1 标准方块，单面贴图，placeholder PNG 写文字。

| 物品 | 数值 id | texture 名 | 状态 |
| --- | --- | --- | --- |
| 桌子 (table) | 15 | `table` | 3a |
| 椅子 (chair) | 16 | `chair` | 3a |
| 办公桌 (desk) | 17 | `desk` | 3a |
| 笔记本 (notebook) | 18 | `notebook` | 3b |
| 键盘 (keyboard) | 19 | `keyboard` | 3b |
| 鼠标 (mouse) | 20 | `mouse` | 3b |
| 黑客电脑 (computer) | 21 | `computer` | 3a（合成表里需要） |

> **限制**：3a 里 3b 的方块 JSON 也会先建好、ID 占位、贴图占位，**但不进合成表**——3a 不强求玩家能造所有家具。
>
> **不做**：右键椅子坐下（交互状态机）、右键电脑启动（功能脚本）。

---

## 9. 子系统 F — 机制（拉杆 / 铁门 / 按钮）

### 9.1 状态化方块（`Assets/Scripts/Core/Voxel/ChunkSection.cs` 扩展）

加一个 `byte[] _blockStates` 与方块 id 平行的数组（位宽同样走"按需分配"——但 3a 简化为总是 8 bit/格，节省复杂度）：

```
public byte GetState(int localX, int localY, int localZ);   // 返回原始 byte
public void SetState(int localX, int localY, int localZ, byte state);
public byte GetChannel(int localX, int localY, int localZ) => (byte)(GetState(...) >> 4);
public byte GetFlags(int localX, int localY, int localZ)   => (byte)(GetState(...) & 0x0F);
```

- `byte` 高 4 位 = channel id（0–15），低 4 位 = 状态位（open=1 / closed=0 / pressed=1 / released=0 / on=1 / off=0）
- 序列化：3a 不做存档（spec 1.2 已排除）
- 重建网格：当 state 变化调 `_views.MarkBlockChanged`，沿用现有脏段逻辑

### 9.2 行为

- **IronDoor**：`flags == 0` → closed（有 mesh）；`flags == 1` → open（mesh 移除顶部一格；3a 用 `MarkBlockChanged` 让整根门重生成以模拟"开"——不做专门的"开门 mesh"）
- **Lever**：右键 → `flags ^= 0x01`；调 `World.NotifyChannel(channel)` 通知同 channel 的 door
- **Button**：右键 → `flags = 0x01`，0.5 秒后自动回 `0x00`（由 `BlockInteraction.Update` 每帧检查 timer）
- **同 channel 联动**：`World.NotifyChannel(byte channel)` 遍历 World 里所有 ChunkColumn 上 channel 匹配的门，翻它们的 flags；每次翻转再调 `_views.MarkBlockChanged` 重建网格
- 性能注：3a 暂不考虑 O(n) 全遍历成本，1 万根门以内 OK

### 9.3 触发

`BlockInteraction.Update` 加：`else if (Input.GetMouseButtonDown(1) && IsStateful(hit.BlockId)) TriggerState(hit.X, hit.Y, hit.Z);`

### 9.4 不做

红石粉、信号衰减、邻居传播——3c。本里程碑只支持"同 channel 的 door/lever/button 全部联动"。

---

## 10. 子系统 G — 动物实体（友好 + 敌对）

### 10.1 数据（`Assets/Scripts/Core/Entities/Mob.cs`）

```
public sealed class Mob {
  public int MobTypeId;            // 1=pig, 2=sheep, 3=zombie
  public Float3 Position;
  public Float3 Velocity;
  public Aabb Body;
  public MobState State;          // Idle / Wander / Scared / Chasing / FleeingFromAttacker / Dying
  public float StateTimer;
  public Float3 WanderTarget;
  public float Health;             // pig=10, sheep=8, zombie=20
  public float MaxHealth;
  public float AttackDamage;       // zombie=2，其它 0
  public float LastAttackTime;
  public float HitFlashTimer;
  public MobKind Kind;             // Passive / Hostile
  public Float3 LastAttackerPos;
}

public enum MobState { Idle, Wander, Scared, Chasing, FleeingFromAttacker, Dying }
public enum MobKind  { Passive, Hostile }
```

### 10.2 友好动物 AI（`Assets/Scripts/Core/Entities/MobAI.cs`）

```
public static class MobAI {
  public static void Tick(Mob mob, Float3 playerPos, World world, float dt) {
    if (mob.Health <= 0) return;    // Dying 状态由外部清理
    
    float distSq = DistanceSquared(playerPos, mob.Position);
    
    if (mob.Kind == Passive) {
      // 友好生物：玩家靠近就害怕
      if (distSq < 8*8 && mob.State != FleeingFromAttacker) mob.State = Scared;
      else if (mob.State == Scared && distSq > 16*16) mob.State = Idle;
    } else { // Hostile
      // 敌对生物：玩家距离 16 格内、夜里 → 追击
      bool night = TimeOfDay.IsNight();
      if (night && distSq < 16*16 && mob.State != FleeingFromAttacker) {
        mob.State = Chasing;
        mob.WanderTarget = playerPos;
      } else if (mob.State == Chasing && (distSq > 24*24 || !night)) {
        mob.State = Idle;
      }
    }
    
    switch (mob.State) {
      case Idle:
        mob.StateTimer -= dt;
        if (mob.StateTimer <= 0) StartWander(mob);
        break;
      case Wander:
        mob.Velocity = TowardTarget(mob);
        if (arrived(mob) || mob.StateTimer <= 0) StartIdle(mob);
        break;
      case Scared:
        mob.Velocity = AwayFromPlayer(mob, playerPos) * 6f;
        break;
      case Chasing:
        mob.Velocity = TowardTarget(mob, playerPos) * 4f;
        if (distSq < 1.4f * 1.4f && mob.LastAttackTime + 1.0f < TimeOfDay.Seconds) {
          // 通过事件回调给 Gameplay 层（避免 Core 直接引用 UnityEvent）
          CombatEvents.PlayerAttacked(mob, mob.AttackDamage);
          mob.LastAttackTime = TimeOfDay.Seconds;
        }
        break;
      case FleeingFromAttacker:
        mob.StateTimer -= dt;
        if (mob.StateTimer <= 0) mob.State = Scared;
        mob.Velocity = (mob.Position - mob.LastAttackerPos) * 5f;
        break;
      case Dying:
        // 外部 0.5s 后销毁
        break;
    }
    
    MoveWithCollision(mob, world, dt);   // 体素碰撞（沿用 WorldSolidSource）
    mob.HitFlashTimer = System.Math.Max(0, mob.HitFlashTimer - dt);
  }
}
```

> Core 层不直接 `UnityEvent`——`CombatEvents` 是一个静态的 `event Action<Mob, float>` 钩子，Gameplay 层订阅后接到回调再调 `PlayerHealth.Damage`。这样 Core 仍无 Unity 依赖。

### 10.3 战斗入口（`Assets/Scripts/Core/Combat/CombatSystem.cs`）

```
public static class CombatSystem {
  public static void AttackMob(Mob mob, float damage, Float3 attackerPos);
}

public static class CombatEvents {
  public static event Action<Mob, float> MobHit;       // mob 受伤（用于渲染红闪）
  public static event Action<Mob> MobDied;             // mob 死亡（用于渲染 + 清理）
  public static event Action<Mob, float> PlayerAttackedByMob;  // 玩家被 mob 打
  public static event Action<ushort, int> ItemDropped; // 物品掉落到世界（itemId, count）
}
```

- `AttackMob`：
  1. `mob.Health -= damage`
  2. `mob.HitFlashTimer = 0.2f`
  3. `mob.LastAttackerPos = attackerPos`
  4. `mob.Velocity = KnockbackDirection(attackerPos, mob.Position) * KnockbackFactor(mob)`
  5. `mob.State = FleeingFromAttacker; mob.StateTimer = 5f;`
  6. 触发 `CombatEvents.MobHit(mob, damage)`
  7. 若 `mob.Health <= 0`：`mob.State = Dying;` + `CombatEvents.MobDied(mob)` + 触发掉落

### 10.4 掉落

- 简单掉落：调 `Inventory.Add(ItemStack(lootItemId, count))`——直接进玩家背包（3a 不做掉落物实体飞行）
- 默认规则：
  - pig → MungBeanSoup × 1
  - sheep → BeetSoup × 1
  - zombie → 无
- 3b 再扩成"实体飞行 + 1–3 个随机数量"

### 10.5 玩家攻击入口

- `PlayerController`/`BlockInteraction` 扩 `TryAttackMob()`：
  1. 当左键 + 主手持剑 + 视线命中 mob → 命中
  2. 调 `CombatSystem.AttackMob(mob, ItemCombatStats[selectedItem].Damage, playerPos)`
- 攻击节流：`TimeOfDay.Seconds - lastSwingTime >= 1f / ItemCombatStats[selectedItem].AttackSpeed`

### 10.6 敌对生物生成（`MobSpawner.cs` 扩展）

- 现有 1–2 只 friendly（pig/sheep）逻辑保留
- 加 `SpawnHostile(int chunkSeed)`：
  - 仅在 `TimeOfDay.IsNight()` 时调用
  - 每个 chunk 0–1 只
  - 在玩家周围 24–48 格范围随机选一个地表格生成
- 渲染层 `MobRenderer` 在 chunk 加载后根据 MobSpawns 创建 GameObject

### 10.7 时间系统（`Assets/Scripts/Core/World/TimeOfDay.cs`）

```
public static class TimeOfDay {
  public static float Seconds;       // 0–24000（minecraft 一日）
  public static float Rate = 60f;    // 现实 1 秒 = 游戏 60 秒（加速 60 倍）
  public static bool IsNight();      // 18000–6000 区间
  public static void Tick(float dt);
}
```

- 真实 7 分钟 = 游戏 1 天
- 不做太阳光角度变化（光照是定向光，颜色固定）——3d

### 10.8 渲染（`Assets/Scripts/Gameplay/Entities/MobRenderer.cs`）

- `PrimitiveType.Cube`：
  - `pig = 粉色 (1, 0.6f, 0.7f)`、`sheep = 白色 (0.9f, 0.9f, 0.9f)`
  - `zombie = 绿色 (0.4f, 0.6f, 0.3f)`，1×1×2 高（比猪高一倍）
- `HitFlashTimer > 0` 时材质 `color.r` 加 0.5（变红）
- `Dying` 状态 0.5 秒后销毁 GameObject（订阅 `CombatEvents.MobDied`）

### 10.9 不做

- 骷髅 / 苦力怕 / 蜘蛛（只做僵尸一种敌对生物）
- 掉落物实体飞行
- 玩家死亡画面（玩家血归零后死亡 → 立即满血复活，简单挫败感）
- 群体 AI / 跟随羊群领袖
- 寻路 A*（简单直线 + 体素碰撞兜底）

---

## 11. 文件结构

```
Assets/Scripts/
├── Core/
│   ├── Inventory/
│   │   ├── Inventory.cs
│   │   ├── InventorySlot.cs
│   │   └── ItemRegistry.cs
│   ├── Crafting/
│   │   ├── CraftingRecipe.cs
│   │   ├── CraftingGrid.cs
│   │   └── CraftingRecipesCatalog.cs   # 13 个起步配方的静态注册表
│   ├── Entities/
│   │   ├── Mob.cs
│   │   ├── MobKind.cs
│   │   └── MobAI.cs
│   ├── Combat/
│   │   ├── PlayerHealth.cs
│   │   ├── CombatSystem.cs
│   │   ├── CombatEvents.cs              # 静态 Action 钩子
│   │   └── ItemCombatStats.cs
│   ├── Items/
│   │   └── FoodItem.cs
│   ├── World/
│   │   └── TimeOfDay.cs
│   └── WorldGen/
│       ├── TreePlacer.cs
│       ├── WaterFeatures.cs
│       └── MobSpawner.cs
├── Unity/
│   ├── Player/PlayerController.cs       (改：Awake 绑 eye + 持剑动画 hook)
│   └── Player/BlockInteraction.cs       (改：去早返回 + 接受 provider + 攻击入口)
└── Gameplay/                            (新建)
    ├── MyWorld.Gameplay.asmdef
    ├── Display/
    │   ├── FullscreenController.cs
    │   ├── FirstPersonHand.cs
    │   └── ToolSwingBridge.cs
    ├── UI/
    │   ├── HotbarUI.cs
    │   ├── CraftingUI.cs
    │   ├── HealthUI.cs                  # 10 颗心 + 红屏闪烁
    │   └── CraftingStation.cs           # 玩家靠近 crafting_table 时的 3x3 UI
    └── Entities/
        ├── MobRenderer.cs
        ├── MobSpawnRenderer.cs          # 把 MobSpawns 物化成 GameObject
        └── CombatVisualHooks.cs         # 订阅 CombatEvents → 渲染红闪 / 销毁

Assets/StreamingAssets/
├── blocks/                              (新增 18 个 JSON：log/leaves/planks/glass/cobblestone/
│                                         diamond_ore/iron_door/lever/button/crafting_table/
│                                         bowl/globe/table/chair/desk/notebook/keyboard/mouse/computer)
└── textures/                            (新增程序生成的占位 PNG)

art/requests/blocks/                     (新增对应文档占位)
├── log.md, log-top.md, log-side.md, leaves.md
├── planks.md, glass.md, cobblestone.md, diamond-ore.md
├── iron_door.md, lever.md, button.md, crafting-top.md, crafting-side.md
├── bowl.md, globe.md
├── table.md, chair.md, desk.md, notebook.md
├── keyboard.md, mouse.md, computer.md

Assets/Tests/EditMode/Gameplay/          (新建)
├── InventoryTests.cs
├── CraftingRecipeTests.cs               # 13 个配方 match / consume
├── MobAITests.cs
├── WaterFeaturesTests.cs
├── PlayerHealthTests.cs
├── CombatSystemTests.cs
├── TimeOfDayTests.cs
├── TreePlacerTests.cs
├── HotbarUITests.cs
├── CraftingUITests.cs
├── FullscreenControllerTests.cs
├── FirstPersonHandTests.cs
└── HealthUITests.cs
```

---

## 12. 测试策略

### 12.1 Core（dotnet + EditMode 双跑）

- `InventoryTests`：Add 满槽回退、Remove 空槽返回 0、Compact 合并零散同 id
- `CraftingRecipeTests`：13 个起步配方 match / 不 match / take consumed 后数量正确（Pocket 1×1、Inventory 2×2、Workbench 3×3 三档）
- `MobAITests`：玩家靠近 8 格内变 Scared、远离 16 格回 Idle、Wander 状态计时归零转 Idle
- `TreePlacerTests`：相同 seed 输出相同方块序列；地表为沙时不种树（海平面下规则）
- `WaterFeaturesTests`：湖只在 `surfaceY <= SeaLevel+4 && moisture > 0.62` 时挖；水洼只在地表上方
- `PlayerHealthTests`：Damage 不低于 0、5 秒未受伤后开始回血、Heal 不超过 Max
- `CombatSystemTests`：AttackMob 减血 + HitFlashTimer、Health<=0 转 Dying 触发 MobDied
- `TimeOfDayTests`：Tick 累加、IsNight 区间、Rate 可调
- `FoodItemTests`：BeetSoup=6、MungBeanSoup=8、IsFood 命中汤类

### 12.2 EditMode

- `HotbarUITests`：selectedIndex 数字键切换、滚轮切换越界 wrap
- `CraftingUITests`：Pocket 1 槽配方匹配、Inventory 2×2、Workbench 3×3 三档网格渲染
- `FullscreenControllerTests`：F11 触发切换、PlayerPrefs 持久化往返
- `FirstPersonHandTests`：Swing 调用 0.25s 后回到静止 pose、重复 Swing 重置计时、持剑 vs 持方块用不同动画
- `HealthUITests`：10 颗心绘制、满血 vs 残血图标区分

### 12.3 集成验证

- `MyWorld.Preview`：新增 `--show-mobs` flag 把 mob 位置打印成字符
- 完整流水线（`./tools/scripts/build-and-run.sh`）跑通后用 `PrintWindow` 截图：
  - 树木可见（log + leaves 颜色）
  - hotbar 9 槽可见、键盘 1 选中第一个槽
  - 看到至少 1 只猪（粉色 cube）和至少 1 只僵尸（绿色 cube，需等到夜里）
  - 玩家视野看到至少 1 个水域（湖 / 水洼）
- 不做"动物数量"等量化断言（3a 内视觉验证 + log 检查够用）

---

## 13. 风险与开放问题

| 项 | 风险 | 缓解 |
| --- | --- | --- |
| System.Drawing 在 Linux 上 | dotnet 9.0 默认不带 libgdiplus | 3a 只在 Windows 上跑（CLAUDE.md 标的项目当前平台）；macOS/Linux 由后续再做 |
| StreamingAssets 路径在 build | `Application.streamingAssetsPath` 在不同平台不同 | 3a 验证 Windows 路径正确即可 |
| 红石/红石粉延后到 3c | lever/button 联动粗暴 | 不实现"信号强度衰减"——只做 channel 翻 state |
| 跨区块树 | 树根在 chunk A，叶子可能延伸到 chunk B | 3a 只放同 chunk 的树（5×5 冠总能塞下，因为叶子层 < 5 格 + 树干 ≤ 7 格，全在 ±7 格内 = 一个 chunk）；3b 才做跨 chunk |
| 多个 MobRenderer 在已卸载区块 | Mob 引用被销毁 | `_mobByChunk` 字典维护，UnloadColumn 时清理 |
| `FirstPersonHand` 立方体被墙挡住 | 视觉穿模 | 接受（3c 加 culling mask） |
| Placeholder 贴图难看 | 玩家感受 | art/requests/ 文档说明未来替换；UI 里加 "PLACEHOLDER" 角标说明 |

---

## 14. 验收标准

完成 plan-3a 后：

1. `dotnet test` 全过（≈ 240+ 测试，新增 ≥ 10 个 Core 测试）
2. Unity EditMode 测试全过
3. `Builds/Windows/MyWordGame.exe` 启动后看到：
   - 默认全屏
   - 按 F11 切到窗口化、能看到 hotbar（9 槽空）+ 屏幕底部 10 颗红心
   - 按 1 选中 hotbar 第 1 槽（默认 Stone）、右键点空气放石头、看到摆动动画
   - 出生点附近至少看到 1 棵树（log + leaves）+ 1 个水域（湖或水洼）
   - Pocket UI：丢 1 个 log 进 pocket → 出现 planks × 4
   - 按 E 打开 inventory（含 2×2 crafting 区）+ 对 crafting_table 按 E 打开 3×3 workbench
   - 合成木剑 / 石剑 / 铁剑 / 钻石剑 / 下界合金剑 / 基岩剑（6 个剑至少 1 个能造出来）
   - 看到至少 1 只猪 / 羊，靠近时逃跑；夜里看到僵尸（绿色 cube）追击
   - 主手持剑左键挥砍动物 → 动物红闪 → 死亡掉物品 → 进 inventory
   - 主手持汤右键 → 玩家回血
   - lever 翻动 → 同 channel 的 iron_door 真的开关
4. 没有运行时异常（`Player.log` 干净）

完成 plan-3b 后：

1. 看到树完整生成（叶冠、树干）
2. 13 个配方全部可用
3. 玩家能放置所有 7 件家具
4. 真实红石粉 + 信号传播
5. 多敌对生物（骷髅 / 苦力怕）

完成 plan-3c 后：

1. 村民 + 交易
2. 经验 / 附魔 / 耐久
3. 第三人称 + 模型 + 动画
4. 玩家死亡画面 + 复活动画

---

> End of spec. Implementation plans live in `docs/superpowers/plans/`.