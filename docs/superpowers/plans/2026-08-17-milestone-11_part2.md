# milestone-11 实施计划 · 第 1 波（集成点①后展开）

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development。前置阅读主计划 `2026-08-17-milestone-11.md` 的 Global Constraints（全部继续有效：不 commit / 中文 / 双链只增不减 / 遇文件锁重试）。
> 基线（2026-08-17 集成点①实测）：dotnet **585** / EditMode **1064** 双链全绿。

## 第 1 波总则

- **并行纪律新增**：`WorldBootstrap.cs`、`BlockInteraction.cs`、`PlayerContext.cs` 全波禁改（Unity 胶水统一在集成点②接线）；`MobAI.cs`、`MobManager.cs`、`MobSpawnRules.cs`、`spawn_rules.json`、`drop_tables.json` 由 P0 前置任务与 W1-1 独占，其余代理禁改
- 新方块/物品一律 JSON 注册 + 守卫测试（`BlockDefinitionFilesTests` 模式）；贴图引用写 A1-A5 已立的需求名（运行时无 png 走品红占位，不阻塞）
- 每代理完成前自跑 dotnet 全绿（≥585+自己的新增）

---

## Task P0（前置·串行独占）：生物枚举与分派预接线

**Files:**
- Modify: `Assets/Scripts/Core/Entities/MobKind.cs`（+12 成员）
- Modify: `Assets/Scripts/Core/Entities/MobAI.cs`（被动 case 组 += 9 被动 kind，两处 switch）
- Modify: `Assets/Scripts/Core/Entities/MobSpawnRules.cs`（PickKind 名字表 += 12）
- Modify: `Assets/Scripts/Unity/Combat/MobManager.cs`（UsesPartTable/MobKindToTypeId += 12；DayCandidates += 9 被动、NightCandidates += 骷髅/蜘蛛/苦力怕）
- Test: 扩展既有映射守卫测试 + 新增「新 kind 未注册模型时 spawn 不崩」守护

**Interfaces:**
```csharp
// MobKind 新成员（值 15-26，顺序固定——存档 spawn 数据按值序列化）：
Sheep=15, Rabbit=16, Fox=17, Deer=18, Panda=19, Penguin=20, Goat=21, Raccoon=22, Hamster=23,
Skeleton=24, Spider=25, Creeper=26
// PickKind 接受的 spawn_rules 名字："Sheep"/"Rabbit"/"Fox"/"Deer"/"Panda"/"Penguin"/"Goat"/"Raccoon"/"Hamster"/"Skeleton"/"Spider"/"Creeper"
// 9 被动 kind 的 MobAI 行为 = 猪组（受击逃 3s）；骷髅/蜘蛛/苦力怕暂走 Zombie 组（P0 只接线不写专属 AI——W1-1 替换）
```

- [ ] Step 1: 失败测试——映射守卫：12 新名 PickKind 可解析、MobKindToTypeId 双向、UsesPartTable(新 kind)=true
- [ ] Step 2: 实现四处接线（数值/名字照上面 Interfaces；spawn_rules 未加条目前不会真的刷出，安全）
- [ ] Step 3: dotnet 全绿 ≥585+新增；汇报（不 commit）

## Task W1-1（战斗）：敌对生物 AI + 弓

**Files:**
- Modify: `Assets/Scripts/Core/Entities/MobAI.cs`（Skeleton 远程：距玩家 8-12m 保持距离每 2s 射一箭；Spider：夜间追白天不追、速度 4.5；Creeper：距玩家 <3m 进入 1.5s 引信膨胀 → 爆炸——半径 3 格方块破坏（木头硬度以下）+ 距离衰减伤害最高 6）
- Create: `Assets/Scripts/Core/Entities/ProjectileEntity.cs`（箭实体：抛物线重力 12、命中玩家伤害 2、命中方块停止并成为可拾取掉落、归属 MobManager tick 列表）
- Create: `Assets/Scripts/Core/Combat/Explosion.cs`（中心/半径 → 破坏方块列表 + 实体伤害衰减；确定性：不破坏 y<0 与 bedrock；复用 BlockDrops 掉落）
- Create: `Assets/StreamingAssets/mobs/models/skeleton.json` `spider.json` `creeper.json`（八脚蜘蛛横体、苦力怕四短腿立柱、骷髅细长灰白——数值自定但遵守脚底原点/面朝+Z/AABB 重叠≤半）
- Modify: `Assets/StreamingAssets/mobs/spawn_rules.json`（Skeleton/Spider 夜间 Plains/Forest/Mountains，Creeper 夜间 Plains/Forest；weight 3-5）+ `drop_tables.json`（skeleton→bone 0-2+arrow 0-2；spider→string_ 0-2；creeper→gunpowder 0-1）
- Create: `Assets/StreamingAssets/items/bow.json`（attackDamage 1 蓄力另计）`shield.json`；recipes：`bow_recipe.json`（木棍×3+string×3）、`arrow_recipe.json`（木棍+圆石→箭×4）、`shield_recipe.json`（铁锭+木板×6）
- Modify: `Assets/Scripts/Unity/Player/PlayerController.cs`（TakeDamage 内：手持盾时伤害 ×0.5，耐久 -1）——唯一允许动 PlayerController 的代理
- Test: `Assets/Tests/EditMode/Combat/`（爆炸衰减/引信时序/箭抛物线离散步进断言）+ dotnet

- [ ] Step 1: 失败测试（爆炸球判定/箭步进/盾减伤）
- [ ] Step 2: Core 实现 + JSON 注册 + 守卫测试全过
- [ ] Step 3: dotnet 全绿 ≥基线+新增；汇报（不 commit）

## Task W1-2（动物）：羊 + 8 被动生物（纯数据）

**Files:**
- Create: `Assets/StreamingAssets/mobs/models/sheep.json` `rabbit.json` `fox.json` `deer.json` `panda.json` `penguin.json` `goat.json` `raccoon.json` `hamster.json`（配色与 A3 entities 图标需求同源：熊猫黑白灰/企鹅黑白领橙/狐狸橙白黑等）
- Test: `Assets/Tests/EditMode/Visual/MobModelLibraryTests.cs` 扩展（九模型可加载、AABB 守卫、腿相位守卫——照既有模式）
- **不改** spawn_rules/drop_tables（产出建议条目清单进汇报，集成点②由主会话合并，避免与 W1-1 冲突）

- [ ] Step 1: 写 9 份模型 JSON（尺寸参考：仓鼠体高 0.3 / 兔 0.5 / 羊 1.2 / 鹿 1.8）
- [ ] Step 2: 守卫测试过 + dotnet 全绿
- [ ] Step 3: 汇报含 spawn/drop 建议条目清单（不 commit）

## Task W1-3（植被）：8 树种 + 12 花草方块注册与投放

**Files:**
- Create: `Assets/StreamingAssets/blocks/`：`birch_log.json` `birch_leaves.json` `pine_log.json` `pine_leaves.json` `cedar_log.json` `cedar_leaves.json` `jungle_log.json` `jungle_leaves.json` `bush_leaves.json` `sequoia_log.json` `sequoia_leaves.json` `cherry_log.json` `cherry_leaves.json`（13）+ `flower_poppy.json` `flower_dandelion.json` `flower_orchid.json` `flower_cornflower.json` `flower_rose.json` `flower_sunflower.json` `flower_lilac.json` `flower_daisy.json` `tall_grass.json` `fern.json` `mushroom_red.json` `mushroom_brown.json`（12）
- Modify: `Assets/Scripts/Core/WorldGen/WorldGenerator.cs`（地表生成处：按 `VegetationTable` 遍历 8 树种调 `ShouldPlaceTree(species)/TryGenerate(species)`；花草按 DensityPerChunk 确定性散布——同树通道模式派生独立哈希通道）
- Create: `Assets/Scripts/Core/WorldGen/FlowerFeature.cs`（花草散布纯函数）
- Modify: `Assets/Scripts/Core/WorldGen/TreeFeature.cs`（BindBlockRegistry 已有——仅需 WorldGenerator 传入注册表；若 Bootstrap 绑定属集成点②则本任务在 Core 侧用默认注册表构造）
- Test: 守卫（13+12 方块 JSON 全通过 BlockDefinitionFilesTests；Preview 剖面新树种可见；同 seed 确定性）

- [ ] Step 1: 失败测试（真实 JSON + 花草散布确定性）
- [ ] Step 2: 注册方块 + 生成接线（leaves 类 `Opaque` 照 leaves.json；花草 `Solid=false`+`Opaque=false` 十字贴图路径照 sapling 先例）
- [ ] Step 3: `dotnet run --project tools/dotnet/MyWorld.Preview` 目检五群系树种差异；dotnet 全绿；汇报（不 commit）

## Task W1-4（方块）：火把/玻璃/箱子/木门/床

**Files:**
- Create: `Assets/StreamingAssets/blocks/torch.json` `glass.json` `chest.json` `wooden_door.json`（开关态走 DoorController 既有机制，`iron_door.json` 为样例）`bed.json`（头/脚两格简化为单方块双格放置——放置时自动占两格，Core 记脚朝向）
- Create: `Assets/Scripts/Core/Blocks/ChestSystem.cs`（坐标→内容字典；Put/Take/_contents 序列化进 `LevelData.ChestContents`——I3 字段已备）
- Create: `Assets/Scripts/Core/Blocks/BedSystem.cs`（放置登记 `LevelData.BedSpawnPoints`；`Sleep`：夜间右键 → 跳到早晨 0 tick + 设玩家重生点）
- Modify: 火把光照——Core 光照传播已有：torch.json 标 `lightEmission 14`（若 BlockDefinition 无此字段则加，ChunkLight 采样时计入——照水/天光既有通道）
- 玻璃：`glass.json` `Opaque=false`（ChunkMeshSource.IsSolid 判 Opaque 的路径已有，水先例）+ 熔炉配方 sand→glass（10s）
- recipes：`torch_recipe.json`（木棍+煤→4）、`chest_recipe.json`（木板×8）、`wooden_door_recipe.json`（木板×6）、`bed_recipe.json`（羊毛×3+木板×3）
- Test: 箱子 roundtrip 进 LevelData、床跳夜 tick 断言、火把光照值传播、玻璃不挡视线（IsSolid 两条路径断言）

- [ ] Step 1: 失败测试 ×4 组 → Step 2: 实现+JSON → Step 3: dotnet 全绿；汇报（不 commit）

## Task W1-5（家具）：九件趣味物品方块化

**Files:**
- Create: `Assets/StreamingAssets/blocks/`：`chair_block.json` `table_block.json` `office_desk_block.json` `laptop_block.json` `keyboard_block.json` `mouse_block.json` `notebook_block.json` `hacker_pc_block.json` `globe_block.json`（全部 `Solid=false` 装饰性、可徒手秒挖、贴图 32×32）
- Modify: 既有 9 个家具 items/*.json 补 `blockId` 关联（照 dirt.json↔dirt item 模式）；`blocks/drops/block_drops.json` 家具方块掉回物品
- Create: recipes 不需要（物品已有获取途径或创造给予；v1 不加合成）
- Test: 家具方块守卫（注册/掉落回物品/不挡移动 WorldSolidSource 断言）

- [ ] Step 1: 失败测试 → Step 2: JSON+关联 → Step 3: dotnet 全绿；汇报（不 commit）

## Task W1-6（农业）：耕地/作物/骨粉/繁殖

**Files:**
- Create: `Assets/StreamingAssets/blocks/farmland.json`（干/湿双态走两 id：`farmland.json`+`farmland_wet.json`）+ 作物 `wheat_crop.json` `beet_crop.json` `mung_crop.json`（`stage` 用 Metadata 或三 id——实现时对齐 ChunkSection 调色板能力，倾向 3 id：`wheat_stage0/1/2.json` 等 9 份，Solid=false）
- Create: `Assets/StreamingAssets/items/hoe_wooden.json` `hoe_stone.json` `hoe_iron.json` `hoe_diamond.json`（toolTier 1/2/3/4、maxDurability 59/131/250/255——守卫测试要求显式声明）+ `wheat.json` `seeds_wheat.json` `seeds_beet.json` `seeds_mung.json` `bone_meal.json` + recipes（锄×4、种子→? 收获循环）
- Create: `Assets/Scripts/Core/Farming/FarmSystem.cs`（锄右键草地/泥土→farmland；种子种上；Tick 生长：确定性哈希步进或累计 tick，成熟掉种子+产物；骨粉右键催熟一级；状态进 `LevelData.FarmStates`）
- Create: `Assets/Scripts/Core/Farming/BreedingSystem.cs`（喂成年同种两只食物 → 产一只幼崽（scale 0.5、跟随父母、600s 长大）——Core 数据模型 + tick，视觉接线②）
- Test: 生长步进/收获掉落/骨粉/繁殖计数（照 HungerSystem 测试模式）

- [ ] Step 1: 失败测试 → Step 2: Core+JSON → Step 3: dotnet 全绿；汇报（不 commit）

---

## 集成点 ②（第 1 波收口，主会话串行）

- 评审 6 代理产物 → 顺序 commit → 合并 W1-2 的 spawn/drop 条目 → Unity 胶水接线批（WorldBootstrap 步骤表 + BlockInteraction 右键路由：锄/种子/骨粉/床/箱子/门 + 弓蓄力左键）
- dotnet + EditMode 双链全绿 → `--ui-shot` 截图管线过一遍新 UI（若②含 UI 接线）
- 美术产物验收波（incoming→后处理→入库→`--tree` 状态刷新）
- 展开第 2 波任务卡（`_part3.md`）
