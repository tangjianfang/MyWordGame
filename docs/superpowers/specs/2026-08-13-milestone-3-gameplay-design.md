# Milestone 3: Gameplay Core + World + Mob 设计

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) 或 superpowers:executing-plans 执行本设计。
> 本 spec 涵盖 4 阶段：A（清理+视觉）/ B（玩法核心）/ C（世界生成）/ D（生物+AI）。
> 每阶段独立可交付、可独立 review、可独立暂停。

---

## Context（项目背景）

MyWordGame 是自研体素沙盒（Unity 2022.3.62f3c1 + URP 14.0.11 + 纯 C# Core 层）。当前已完成：
- **milestone-2**：玩家层（PlayerController + Inventory + BlockInteraction + HandController + PlayerVisual）
- **visual-role-polish**（前一个 plan）：视觉/角色 polish + 视觉回归测试 + visual-smoke CI

**当前状态**：
- 86 commits 在本地 main（待 push，GitHub 网络不通）
- 测试基线：315/315 dotnet + 337/337 Unity EditMode + Build OK
- 数据资产：15 blocks + 57 items + 26 recipes + 9 mob icons + 12 UI art
- 代码骨架已有但未 wiring：Crafting × 3 UI / Hunger / Mob × 4 系统 / Redstone / Time / SaplingGrowth 等

**本次目标**：从「可走+可挖+可截图」升级到「可玩 voxel 沙盒循环」。

---

## Goal（一句话）

完成 **最小可玩 voxel 沙盒循环**：挖 → 合成 → 烧炼 → 饿 → 死循环 + 世界有 4 种生物群系 + 生物在动。

---

## Tech Stack

- **Unity 2022.3.62f3c1** + **URP 14.0.11**（已实机验证）
- **Core 层**：`netstandard2.1` + C# 9 + `LangVersion 9.0` + `TreatWarningsAsErrors=true` + `noEngineReferences: true`
- **Unity 层**：现有 asmdef（`MyWorld.Unity` / `MyWorld.Unity.Editor` / `MyWorld.Core.Tests`）
- **数据驱动**：JSON via Newtonsoft.Json（已验证）
- **测试**：NUnit via dotnet test + Unity Test Framework
- **CI**：现有 `tools/scripts/build-and-run.sh --with-visual`（visual-smoke 已集成）

---

## Global Constraints（项目硬约束，CLAUDE.md 沿用）

1. **Core 层禁 UnityEngine**：新增 Core 代码不能 `using UnityEngine.*`；用 `Core/Math/Float3` 而非 `Vector3`
2. **一个警告都不能留**：Core 编译时 `TreatWarningsAsErrors=true`
3. **未改既有工程文件**：不改 `MyWorld.Core.csproj` / `MyWorld.Core.Tests.csproj`（如需新增 .cs 落被链接目录即可）
4. **不新建 asmdef**：新代码归入既有 `MyWorld.Core` / `MyWorld.Unity` / `MyWorld.Unity.Editor` / `MyWorld.Core.Tests` 之一
5. **dotnet 与 Unity EditMode 双轨**：纯 Core 测试走 dotnet；Unity 相关（`UnityEngine.*`、`MonoBehaviour`、视觉）走 EditMode，必要时 `#if UNITY_EDITOR` 包裹
6. **视觉测试只 Unity EditMode 跑**：视觉相关测试文件整段 `#if UNITY_EDITOR ... #endif` 包裹，dotnet 编译为空
7. **docstring / 注释 / 测试断言消息 一律中文**
8. **deterministic 生成**：`ValueNoise2D` 整数哈希；biome/cave 只依赖 seed + 世界坐标，不依赖调用顺序
9. **读容忍、写严格**：`World.GetBlock` 越界返回空气；`SetBlock` 越界抛异常
10. **Visual regression 不退化**：每 phase 完成跑 visual-smoke，4/4 + 新增视觉断言必须 pass

---

## Phase 总览

| Phase | 名称 | 工作量 | 关键路径 |
|---|---|---|---|
| A | Cleanup + Visual | 1-2 天 | 起 |
| B | Gameplay Core | 4-5 天 | A 之后 |
| C | World Gen | 4-5 天 | A 之后（与 B 并行） |
| D | Mob + AI | 5-6 天 | B + C 之后 |

**总关键路径**: A → max(B, C) → D = ~14-19 工作日（串行 SDD 执行 ~15-19 天）

---

## Architecture（4 阶段如何整合）

### 分层复用

| 层 | 路径 | 已实现 | 本 milestone 扩展 |
|---|---|---|---|
| **Core** | `Assets/Scripts/Core`（noEngineReferences） | `PlayerInventory` / `PlayerState` / `Health` / `CraftingMatrix` / `RecipeDatabase` / `Mob` / `MobAI` / `MobKind` / `MobState` / `WorldGenerator` / `TreeFeature` / `ValueNoise2D` / `TimeOfDay` / `RedstoneNode` / `DoorController` / `Damage` / `DeathSystem` | `FurnaceSystem` / `HungerSystem` / `ItemDropEntity` / `Biome` / `CaveCarver` / `MobSpawnRules` / `ItemDropTable` |
| **Unity** | `Assets/Scripts/Unity` | `PlayerController` / `HandController` / `HotbarUI` / `HealthBarUI` / `FoodBarUI` / `CraftingInventoryUi` / `MobManager` / `MobView` / `WorldBootstrap` / `DayNightCycle` / `BlockInteraction` | UI 接线 / `PlayerAudioSystem` / mob 视觉扩展 / `CraftingFurnaceUi` |
| **Tests** | `Assets/Tests/EditMode` + `tools/dotnet` | 337 EditMode + 315 dotnet | 每 phase 新增对应测试 |
| **Data** | `Assets/StreamingAssets` | 15 blocks + 57 items + 26 recipes + 9 mob icons + 12 UI art | biome 配置 / mob 规则 / drop 表 |

### 关键架构决策

1. **状态管理**：沿用 `PlayerContext`（Unity 聚合）+ Core `PlayerState`（health/hunger/position）。Phase B 在 `PlayerState` 加 `Hunger` + `Saturation` 字段。
2. **持久化**：MVP 不引入完整 save/load。沿用 `RegionFile` 存世界方块改动。玩家状态随 session（重生时丢失，注 spec）。
3. **Mob AI**：沿用 `MobAI` state machine。新 mob 类型走 enum 扩展 + `MobAI.Update(MobKind)` switch，不引入新 AI 框架。
4. **Biome 生成**：`Biome.GetBiome(int x, int z, int seed)` 纯函数（依赖 `ValueNoise2D` 阈值）→ 与确定性约束一致。
5. **Day/Night**：`TimeOfDay.IsNight()` 提供查询，`MobSpawnRules` 按其过滤。

---

## Components（每 phase 的具体文件 + 职责）

### Phase A: Cleanup + Visual（5 items, 1-2 天）

**Cleanup（继承自 visual-role-polish Final review）**:
- `Assets/Scripts/Unity/Editor/ScreenshotCapture.cs`：4 处 docstring 订正（5 张→2 张、幂等位置、-nographics 警告、`CaptureAll` 用途）+ `Path.GetDirectoryName` NRE 防御 + 死代码 `DefaultCameraName` 删除 + 末尾 newline
- `Assets/Tests/EditMode/Blocks/BlockMaterialLibraryTests.cs`：多余 `if (m == null) continue;` 删除 + 末尾 newline
- `Assets/Scripts/Unity/UI/HotbarUI.cs`：`_selectBorder` → `_countBorder` 改名

**视觉 followups**:
- `Assets/Scripts/Unity/Player/PlayerVisual.cs`：6 cube → 双段色（jacket+skin / pants+boots，共 12 cube）+ `BootColor` / `HairColor` 启用
- `Assets/Scripts/Unity/Player/PlayerController.cs`：加 jump 逻辑（Space → 上跳）+ `WalkPhase` dt 钳位（speed 除数和 WalkPhase 累加都钳 `Max(dt, 1e-4)`）
- `Assets/Scripts/Unity/Player/HandController.cs`：jump 期间切到 idle 姿势（mid-air 持物但不挥）

**音频**:
- `Assets/Scripts/Unity/Audio/PlayerAudioSystem.cs`：单例 MonoBehaviour，`PlayFootstep()` / `PlayPlace()` / `PlayBreak()` 三个方法
- `Assets/Art/Audio/footstep.ogg` + `place.ogg` + `break.ogg`：3 个音效（procedural 生成或 free 库占位）

**测试**:
- `Assets/Tests/EditMode/Player/JumpAnimationTests.cs`：3 测试（jump 期间腿并拢 + 着地复位 + `WalkPhase` 钳位）
- `Assets/Tests/EditMode/Audio/PlayerAudioSystemTests.cs`：2 测试（PlayFootstep 触发正确 clip + 加载失败不抛）

### Phase B: Gameplay Core（6 items, 4-5 天）

**Core 新增**:
- `Assets/Scripts/Core/Items/FurnaceSystem.cs`：纯 Core smelting 逻辑（input + fuel + output + progress tick）。`Tick(dt)` 调用方传入 `WorldSolidSource` 查方块。
- `Assets/Scripts/Core/Player/HungerSystem.cs`：`Hunger` + `Saturation` 字段 + tick 衰减（每 30s Hunger--，Saturation=0 加速）+ 触发 Heal/Damage
- `Assets/Scripts/Core/Entities/ItemDropEntity.cs`：丢落物理实体（gravity + 拾取距离检测）

**Core 修改**:
- `Assets/Scripts/Core/Player/PlayerState.cs`：加 `Hunger` + `Saturation` 字段

**Unity 接线**:
- `Assets/Scripts/Unity/UI/CraftingInventoryUi.cs`：接 `RecipeDatabase` + 3x3 grid 状态机 + 输出槽
- `Assets/Scripts/Unity/UI/CraftingFurnaceUi.cs`（新）：input + fuel + output + progress bar
- `Assets/Scripts/Unity/UI/FoodBarUI.cs`：接 `HungerSystem` 显示
- `Assets/Scripts/Unity/UI/DeathScreenUi.cs`：接 `DeathSystem` + 重生按钮 → `PlayerController.Respawn`
- `Assets/Scripts/Unity/Player/PlayerController.cs`：接 Damage 来源（fall + hunger + mob attack）+ pickup 钩子
- `Assets/Scripts/Unity/WorldBootstrap.cs`：启动时注册 `FurnaceSystem` + `HungerSystem` 到 `PlayerContext`
- `Assets/Scripts/Unity/Audio/PlayerAudioSystem.cs`（Phase A 已建）：接线 `BlockInteraction.Place/Break` 调用 `PlayPlace/PlayBreak`

**测试**:
- `Assets/Tests/EditMode/Items/FurnaceSystemTests.cs`：4 测试（smelt 时间 + fuel 消耗 + 多 slot + fuel 烧完暂停）
- `Assets/Tests/EditMode/Player/HungerSystemTests.cs`：3 测试（衰减 + 饱和耗尽加速 + 饿死触发）
- `Assets/Tests/EditMode/Entities/ItemDropEntityTests.cs`：2 测试（重力 + 拾取范围）
- `Assets/Tests/EditMode/UI/CraftingInventoryUiTests.cs`：3 测试（匹配 + 不匹配 + 输出后 input 减）
- `Assets/Tests/EditMode/UI/FoodBarUiWireTests.cs`：1 测试（wire 后 UI 跟随 HungerSystem）
- `Assets/Tests/EditMode/UI/DeathScreenUiWireTests.cs`：1 测试（Health=0 → UI 显示 + Respawn 复位）

### Phase C: World Gen（5 items, 4-5 天）

**Core 新增**:
- `Assets/Scripts/Core/WorldGen/Biome.cs`：`Biome` enum（Plains / Forest / Desert / Snow）+ `GetBiome(int x, int z, int seed)` 纯函数
- `Assets/Scripts/Core/WorldGen/CaveCarver.cs`：3D `ValueNoise2D` 扩展（加 `Sample3D`）阈值剔除 → 返回 `(x,y,z)[]` 坐标集合

**Core 修改**:
- `Assets/Scripts/Core/WorldGen/WorldGenerator.cs`：加 biome 选择 + cave pass + 地表方块按 biome 选
- `Assets/Scripts/Core/WorldGen/TreeFeature.cs`：树密度按 biome 调（forest 多、desert/snow 无）
- `Assets/Scripts/Core/ValueNoise2D.cs`：加 `Sample3D(int x, int y, int z, int seed)`（如未实现；如已有 Sample3D 跳过）

**Unity 验证**:
- 截图 4 张（per biome）作为视觉基线

**测试**:
- `Assets/Tests/EditMode/WorldGen/BiomeTests.cs`：3 测试（4 种 biome 边界 + 确定性 + 阈值）
- `Assets/Tests/EditMode/WorldGen/CaveCarverTests.cs`：2 测试（噪声剔除 + 不破坏 bedrock y<5）
- `Assets/Tests/EditMode/WorldGen/TreeFeatureTests.cs`：1 测试（biome 密度差异）
- `Assets/Tests/EditMode/Visual/BiomeVisualTests.cs`：3 screenshot 断言（每 biome 截图 + 草/沙/雪色像素比）

**数据**:
- `Assets/StreamingAssets/world/biomes.json`：4 biome 配置（噪声阈值 + 地表方块 ID + 树密度 + 动物规则 link）

### Phase D: Mob + AI（6 items, 5-6 天）

**Core 新增**:
- `Assets/Scripts/Core/Entities/MobSpawnRules.cs`：`Pick(Biome, bool isNight)` → `MobKind?` 规则表
- `Assets/Scripts/Core/Entities/ItemDropTable.cs`：`Drop(MobKind)` → `ItemStack[]`

**Core 修改**:
- `Assets/Scripts/Core/Entities/MobKind.cs`：enum 加 `PIG` / `COW` / `CHICKEN` / `ZOMBIE`
- `Assets/Scripts/Core/Entities/MobAI.cs`：加 `Update(MobKind, MobState, World)` switch，type-specific 行为
  - PIG / COW / CHICKEN：Wander + 玩家靠近时 Flee + 死亡 → `ItemDropTable.Drop(kind)`
  - ZOMBIE：Wander + 玩家 32 格内 Chase + 8 格内 Attack + 死亡 → rotten_flesh + 5% ingot

**Unity 修改**:
- `Assets/Scripts/Unity/Combat/MobView.cs`：mob-specific 视觉（pig 4 腿、cow 4 腿、chicken 2 腿+喙、zombie 6 部件+绿皮肤）
- `Assets/Scripts/Unity/Combat/MobManager.cs`：接 `MobSpawnRules` + spawn tick（按 DayNightCycle）
- `Assets/Scripts/Unity/Combat/VillagerManager.cs`：验证 villager 真的会生成 + 接 TradeUi

**测试**:
- `Assets/Tests/EditMode/Entities/MobAITests.cs`：5 测试（zombie 8 格追玩家 + pig 永不变 Chase + chicken 随机跳 + 死亡触发 Drop）
- `Assets/Tests/EditMode/Entities/MobSpawnRulesTests.cs`：4 测试（白天 plains=猪牛鸡 + 夜晚 plains=zombie + desert 不生成 zombie + 边界）
- `Assets/Tests/EditMode/Entities/ItemDropTableTests.cs`：3 测试（pig=porkchop + zombie=rotten_flesh + 不存在 ID 跳过）
- `Assets/Tests/EditMode/UI/VillagerTradeTests.cs`：2 测试（trade UI 打开 + 物品库存空返回 false）
- `Assets/Tests/EditMode/Visual/MobVisualTests.cs`：2 screenshot 断言（至少 1 pig + 1 zombie 可见）

**数据**:
- `Assets/StreamingAssets/mobs/spawn_rules.json`：per biome × per time-of-day → mob type
- `Assets/StreamingAssets/mobs/drop_tables.json`：per mob kind → drop list

---

## Data Flow + Phase Dependencies

### 阶段依赖图

```
        ┌──────────┐
        │ Phase A  │  cleanup + visual + audio（独立）
        │ (1-2天)  │
        └────┬─────┘
             │
       ┌─────┴─────┐
       ↓           ↓
  ┌─────────┐  ┌─────────┐
  │Phase B  │  │Phase C  │   ← 可并行（B 改 PlayerController.cs pickup 钩子，
  │(4-5天)  │  │(4-5天)  │      C 改 BlockInteraction.cs hardness，互不重叠）
  └────┬────┘  └────┬────┘
       │            │
       └─────┬──────┘
             ↓
       ┌──────────┐
       │ Phase D  │   ← 依赖 B（ItemDropEntity）+ C（Biome）
       │ (5-6天)  │
       └──────────┘
```

### Phase 内关键数据流

#### Phase B：玩法核心循环

```
玩家攻击方块
   ↓ BlockInteraction.Break()
   ↓ damage block (按 hardness × tool multiplier)
   ↓ block 消失 → ItemDropEntity.Spawn(blockDrops, position)
   ↓
ItemDropEntity 物理下落
   ↓ 玩家走入 pickup 范围 → PlayerInventory.Add(stack) → HotbarUI refresh

HungerSystem.Tick(每 30 秒) → Hunger--, Saturation--
   ↓ Saturation=0 → Hunger 加速衰减
   ↓ Hunger=0 → 每 10s Health--, FoodBarUI 显示
   ↓ Health=0 → DeathScreenUi 显示 → 玩家点 Respawn → PlayerController.Respawn(spawnPoint) → Hunger 满 + Health 满 + 位置重置

CraftingInventoryUi (3x3 grid)
   ↓ 玩家点 Craft → CraftingMatrix.Match(recipe, inputs)
   ↓ if match: outputs → PlayerInventory.Remove(inputs) + Add(output) → UI 刷新

CraftingFurnaceUi (input + fuel + output)
   ↓ FurnaceSystem.Tick(每 20 tick) → Progress++
   ↓ Progress=完成 → output slot 进 1 → 玩家点取出 → PlayerInventory.Add(output)
```

#### Phase C：世界生成

```
WorldGenerator.Generate(chunk)
   ↓
对每个 column (x, z):
   ↓ biome = Biome.GetBiome(x, z, seed)
   ↓ heightMap = noise2D(x, z) + biome.altitudeOffset
   ↓ fillTopBlocks(biome, heightMap) → grass/sand/snow
   ↓ fillDirtLayer(biome)
   ↓ fillStoneBelow(biome)
   ↓
对每个 (x, y, z) in chunk:
   ↓ if CaveCarver.IsCave(x, y, z, seed) and y >= 5 → air
   ↓
对每个 surface block in biome:
   ↓ if TreeFeature.ShouldPlace(biome, x, z, seed) → place tree structure
```

#### Phase D：生物 + AI

```
DayNightCycle.Tick() → TimeOfDay.update()
   ↓
MobManager.SpawnTick():
   ↓ 对每个 loaded chunk:
   ↓   biome = Biome.GetBiome(chunkX, chunkZ)
   ↓   isNight = TimeOfDay.IsNight()
   ↓   for each candidate position in chunk:
   ↓     kind = MobSpawnRules.Pick(biome, isNight)
   ↓     if kind != null → Mob.Spawn(kind, position)

MobAI.Tick(each mob):
   ↓ switch (kind):
   ↓   PIG/COW/CHICKEN: Wander + 玩家靠近 → Flee + 死亡 → Drop
   ↓   ZOMBIE: Wander + 玩家 32 格 → Chase + 8 格 → Attack + 死亡 → Drop
   ↓
MobManager.OnChunkUnload(): 清空该 chunk 的 mob list + 保存 drops 到 ItemDropEntity
```

### 跨阶段数据契约（保证可串行）

| 数据 | 定义位置 | 依赖者 |
|---|---|---|
| `ItemDropEntity.Spawn(itemId, count, pos)` | Phase B | Phase D（mob death 调用） |
| `Biome.GetBiome(x, z, seed)` | Phase C | Phase D（spawn rules） |
| `TimeOfDay.IsNight()` | 已有 | Phase D（zombie 生成） |
| `PlayerAudioSystem.Play(clip)` | Phase A | Phase B（place/break/footstep） |
| `FurnaceSystem.Recipe(oreId) → (ingotId, time)` | Phase B | Phase D 可选 |
| `ItemDropTable.Drop(mobKind)` | Phase D | D 自己 |

### 风险与解耦

- **B 与 C 并行**：若同时改 `BlockInteraction.cs` → 冲突。Mitigation：B 只在 `PlayerController.cs` 加 pickup 钩子，C 只改 `BlockInteraction.cs` 的 hardness 表
- **D 同时依赖 B + C**：D 起步前 B 和 C 都必须 merged
- **测试隔离**：每 phase 跑全量测试时，其余 phase 文件不变 → 测试不冲突

---

## Error Handling（错误处理约定）

### 沿用 CLAUDE.md 已确立的模式

1. **读容忍、写严格**：`ChunkColumn.GetBlock` / `World.GetBlock` 越界或未加载 → 返回空气；`SetBlock` 越界 → 抛 `InvalidOperationException`
2. **Core 不持 UnityEngine 引用** — 错误用纯 C# 异常
3. **.NET 9 警告 = 错误** — `TreatWarningsAsErrors=true`

### 各 phase 错误处理

#### Phase A

| 场景 | 处理 |
|---|---|
| AudioClip 加载失败（路径错/文件不存在） | `PlayerAudioSystem.Awake` try/catch + `Debug.LogWarning` + 不抛（音效是 nice-to-have） |
| 音效播放被打断（Player 死亡 / scene unload） | AudioSource.Stop() + null check，no-op |
| `Path.GetDirectoryName(outputPath)` 返回 null | Cleanup item 已修：fallback 用 `Application.dataPath` |

#### Phase B

| 场景 | 处理 |
|---|---|
| Recipe 输入不匹配 | `CraftingMatrix.Match` 返回 false → 输出槽不显示，no error |
| 输出槽满（inventory full） | `CraftingInventoryUi.OnCraftClicked` 检 `PlayerInventory.CanAdd` → 失败时 warning + UI 红字提示 |
| Furnace 燃料烧完但 input 未烧完 | `FurnaceSystem.Tick` 检 fuel=0 → progress 暂停（不倒退），no error |
| Hunger 减到 0 | `HungerSystem` 不抛，转交 `DeathSystem` |
| Damage 来源为 null（mob 已 despawn） | `PlayerController.TakeDamage` 检查 attacker != null → skip |
| ItemDropEntity 拾取时 inventory 满 | `PlayerInventory.Add` 返回 false → entity 保留，提示清理背包 |
| 重生时玩家被卡在方块内 | `PlayerController.Respawn` 沿用 `FindSafeSpawn` 算法（找不到时 teleport + warning） |

#### Phase C

| 场景 | 处理 |
|---|---|
| Biome 边界（x 在 chunk 边界） | `Biome.GetBiome` 接 chunk 坐标而非 block 坐标 → 永远 in-bounds |
| CaveCarver 试图删除 bedrock | 硬约束 `y >= 5` 跳过 |
| TreeFeature 在水面/沙上放树 | `TreeFeature.Place` 检查底下方块必须是 grass/dirt，否则 skip |
| biome JSON 缺失/解析失败 | `BiomeConfigLoader.Load` 失败 → fallback 到硬编码默认值 + warning |

#### Phase D

| 场景 | 处理 |
|---|---|
| Mob 试图在 unloaded chunk spawn | `MobManager.SpawnTick` 检 chunk loaded → skip |
| Mob 在 chunk unload 时被 despawn | `MobManager.OnChunkUnload` 清空 + 保存 drops 到 `ItemDropEntity`（不丢物品） |
| Spawn rules JSON 缺/解析失败 | Fallback 到硬编码默认表 + warning |
| Drop table 给的 itemId 不存在 | `ItemDropTable.Drop` 检 `ItemDatabase.HasItem(id)` → 跳过未知 id + warning |
| Villager trade 物品库存空 | `Villager.Trade` 返回 false + UI 提示 "已售罄" |

### 全局原则

- **不抛跨层异常**：Core 异常用纯 C# 类型，Unity 异常用 `Debug.LogException` 捕获
- **Gameplay 错误用 warning + UI 提示，不阻断玩家**
- **资源加载失败 → fallback + warning**（音效/biome JSON/drop table）
- **Mob despawn 丢物品** → 不允许，必须存为 `ItemDropEntity`

---

## Testing + Acceptance Criteria

### 测试基础设施（沿用现有）

- **dotnet test** (`tools/dotnet/MyWorld.Tools.sln`): 315 测试，Core 逻辑
- **Unity EditMode** (`Assets/Tests/EditMode/`): 337 测试，Unity 侧 + 视觉
- **visual-smoke** (`tools/scripts/visual-smoke.sh`): PR gate，每次跑
- **`#if UNITY_EDITOR` 包裹**：视觉测试只 Unity 跑

### 测试增量 + 验收数

| Phase | 新增 EditMode 测试 | 累计 | 验收时间 |
|---|---|---|---|
| A | JumpAnimationTests(3) + PlayerAudioSystemTests(2) | 342 | dotnet test + Unity EditMode + Build OK + visual-smoke pass |
| B | FurnaceSystemTests(4) + HungerSystemTests(3) + ItemDropEntityTests(2) + CraftingInventoryUiTests(3) + FoodBarUiWireTests(1) + DeathScreenUiWireTests(1) | 356 | 同上 |
| C | BiomeTests(3) + CaveCarverTests(2) + TreeFeatureTests(1) + BiomeVisualTests(3) | 365 | 同上 |
| D | MobAITests(5) + MobSpawnRulesTests(4) + ItemDropTableTests(3) + VillagerTradeTests(2) + MobVisualTests(2) | 381 | 同上 |

**总验收**: milestone 完成 381/381 EditMode + 315/315 dotnet + Build OK + visual-smoke pass

### Phase A 验收

- [ ] 9 项 cleanup commit 已落地
- [ ] `PlayerVisual` 双段色四肢（截图可见 jacket + skin / pants + boots 4 色分区）
- [ ] 玩家 jump：Space 起跳、Y 速度遵循 gravity、落地复位
- [ ] jump 期间腿并拢、落地复位
- [ ] 移动播放 footstep（每 0.5s 一步）
- [ ] 放方块播 place、挖方块播 break
- [ ] `WalkPhase` dt 钳位一致
- [ ] 现有 337 测试不退化
- [ ] Build OK + visual-smoke 4/4 pass

### Phase B 验收

- [ ] 玩家死亡（饿死/摔死/被攻击死）→ DeathScreenUi → Respawn 重生 + 满饥饿
- [ ] CraftingInventoryUi：4 planks 进 2x2 → 出 1 crafting_table（消耗输入、加输出）
- [ ] 不匹配时输出槽空
- [ ] Furnace：1 coal + 1 iron_ore → 30s 后出 1 iron_ingot
- [ ] Furnace：fuel 烧完时 progress 暂停（不倒退）
- [ ] HungerSystem：每 30s Hunger--，Saturation=0 加速
- [ ] 挖方块掉 ItemDropEntity，0.5s 后可拾取
- [ ] 拾取时 inventory 满 → entity 保留
- [ ] 现有 337 测试不退化

### Phase C 验收

- [ ] 4 种 biome 在 seed 42 下分布合理（plains 大片 + forest 斑块 + desert 沙带 + snow 边角）
- [ ] Biome.GetBiome 确定性（同 seed + 同坐标 → 同 biome）
- [ ] CaveCarver 不删 bedrock（y < 5 完整）
- [ ] 各 biome 地表方块正确（plains=grass / desert=sand / snow=snow / forest=grass+树多）
- [ ] TreeFeature 按 biome 调密度（forest 多、desert/snow 无）
- [ ] 截图 4 张（per biome）作为视觉基线
- [ ] E1 草色区间视情况放宽（如 biome 后草像素变少）

### Phase D 验收

- [ ] 白天 plains 自动生成 pig/cow/chicken（每 chunk 0-2 只）
- [ ] 夜晚 plains 自动生成 zombie（替代被动生物）
- [ ] Desert 不生成 zombie
- [ ] pig 死亡掉 raw_porkchop（1-3）
- [ ] zombie 死亡掉 rotten_flesh（0-2）+ 5% ingot
- [ ] zombie 在 8 格内追玩家，超出后回 wander
- [ ] villager 生成在 plains，TradeUi 显示 3-5 个 trade option
- [ ] 截图至少 1 只 pig + 1 只 zombie 可见
- [ ] 现有 356 测试不退化

### Milestone-3 整体验收

- [ ] **可玩循环 end-to-end**: punch tree → craft planks → crafting_table → tools → dig stone → iron → furnace → iron tools → 吃东西 → 死亡重生
- [ ] **世界多样性**: 4 种 biome + 洞穴 + 树 + 水
- [ ] **生物存在**: 白天动物在走、夜间 zombie 追玩家
- [ ] **测试基线**: 381/381 EditMode + 315/315 dotnet + Build OK
- [ ] **Visual regression**: 4 biome 截图 + 玩家视角截图 + 视觉冒烟脚本通过
- [ ] **No regression**: 上一 milestone 所有功能继续工作

### 视觉回归扩展点

- E1 `VisualRegressionTests` 已覆盖：不全黑 / 不全 magenta / 草色 > 4% / 方块材质无 magenta
- Phase C 后扩展：4 biome 截图 + 草地/沙漠/雪地像素比
- Phase D 后扩展：mob 截图 + magenta 仍为 0%
- 每 phase 完成时用 visual-smoke 跑一次

---

## Critical Files to Reuse（避免重复造轮子）

| 来源 | 函数/类 | 用途 |
|---|---|---|
| `Assets/Scripts/Core/Items/CraftingMatrix.cs` | `Match(recipe, inputs)` | Phase B 直接调用，wire 到 CraftingInventoryUi |
| `Assets/Scripts/Core/Items/RecipeDatabase.cs` | `GetByOutput(itemId)` | Phase B 反查 recipe |
| `Assets/Scripts/Core/Player/PlayerState.cs` | `Health` / `Position` | Phase B 加 `Hunger` + `Saturation` 字段 |
| `Assets/Scripts/Core/Entities/Mob.cs` / `MobAI.cs` / `MobKind.cs` | state machine + enum | Phase D 扩展 |
| `Assets/Scripts/Core/WorldGen/TreeFeature.cs` | `ShouldPlace` / `Place` | Phase C 调密度 |
| `Assets/Scripts/Core/ValueNoise2D.cs` | `Sample2D` | Phase C 加 `Sample3D` |
| `Assets/Scripts/Core/TimeOfDay.cs` | `IsNight()` | Phase D 用 |
| `Assets/Scripts/Unity/Player/BlockInteraction.cs` | `Place` / `Break` 钩子 | Phase A 接线音效 / Phase B 接线 drop / Phase C 改 hardness |
| `Assets/Scripts/Unity/UI/HotbarUI.cs` | 刷新接口 | Phase B 加 wire |
| `Assets/Scripts/Unity/WorldBootstrap.cs` | 启动初始化 | Phase B 注册新 system |
| `Assets/Scripts/Unity/Editor/ScreenshotCapture.cs` | `CaptureAllDefault` | Phase C/D 拍 biome/mob 截图 |

---

## Out of Scope（明确不做，避免范围蔓延）

- **完整 save/load**（玩家状态）：MVP 玩家状态随 session
- **Redstone wiring**：有 RedstoneNode + RedstoneSystem 骨架，独立子系统，建议延后
- **Enchanting UI wire**：高级系统
- **结构生成**（村庄/神殿/废墟）：Phase E+
- **多种敌对生物**（skeleton/creeper/spider）：Phase D+ 扩展
- **mob 路径寻找 / 团队协作 / 村民职业 / 繁殖 / 村庄入侵**
- **触摸控制 / 移动端**
- **网络多人**
- **性能基准测试**（非强制，留给未来 milestone）
- **AABB 物理优化**（沿用现有 PlayerMotor）
- **Light propagation 优化**（沿用现有 LightPropagator）
- **玩家第三人称相机优化**（B3 已实现，沿用）

---

## Open Questions（不需要阻塞 spec 的开放问题）

1. **音效源**：是否使用 procedural 生成（pyo / ffmpeg tone）vs free 库（Minecraft Java 默认 .ogg）？—— spec 接受任一，brief 内实现者定
2. **Mob AI 视野**：zombie 32 格追 vs 16 格 vs 64 格？—— spec 默认 32 格，可调整
3. **Biome 边界过渡**：硬边界 vs 平滑过渡（biome blend）？—— spec 默认硬边界 + 大区域，简化生成
4. **Persistence**：玩家死亡后 inventory 掉落 vs 保留？—— spec 默认保留（重生后满饥饿 + Health 但 inventory 完整，避免简单错误）
5. **Push 待补**：86 commits ahead of origin/main，等网络恢复后 push
