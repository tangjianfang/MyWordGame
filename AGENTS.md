# AGENTS.md

本文件供 ZCode / 子代理快速接入 MyWordGame（自研体素沙盒，Unity 2022.3.62f3c1 + 纯 C# Core 层）。完整的约定、命令、里程碑沉淀见同目录 `CLAUDE.md`——**修改前先通读 CLAUDE.md**，本文件只列易踩坑的硬约束与速查。

## 当前基线

- m13 W5 收口（2026-08-20），双链全绿——dotnet **974/974**、EditMode **1732/1732**（2026-08-20 实测）
- **m12 计划已定稿但未实施**（DigProgress/WorldCatalog/成就/图鉴/药水乐器均未创建，m13 插队先行）——勿把 m12 设计文档当现状
- 完整约束见 `CLAUDE.md`，本文档只是速查；三份文档（README/CLAUDE/AGENTS）在里程碑收口时同步更新

## 仓库速查

- 解决方案：`tools/dotnet/MyWorld.Tools.sln`（**不要走根目录的 `.sln`/`.csproj`，Unity 会覆盖**）
- 源码物理位置：`Assets/Scripts/Core/`、`Assets/Scripts/Unity/`、`Assets/Tests/EditMode/`、`Assets/StreamingAssets/`；`tools/dotnet/*.csproj` 用 `<Compile Include>` 链接同一份文件，不复制
- 测试必须放 `Assets/Tests/EditMode/**`，**不放 `Assets/Tests/Core/`**（m11 实证：后者不在编译链）
- SDK：`global.json` 钉死 `9.0.x` (`rollForward: latestFeature`)，**别改**
- 文档：`docs/specs/`（画面/玩法）、`docs/superpowers/specs/`（设计）、`docs/superpowers/plans/`（实施）、`docs/小孩子玩后需求/`
- 美术需求：`art/README.md` + `art/requests/`；程序占位快路径见 `art/scripts/`
- 语言：**所有文档、注释、断言消息一律中文**

## 常用命令（仓库根目录执行，无需 Unity）

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln                                    # 全部测试
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~X"    # 单类
dotnet build tools/dotnet/MyWorld.Tools.sln
dotnet run --project tools/dotnet/MyWorld.Preview                            # ASCII 世界预览
./tools/scripts/build-and-run.sh                                             # 测试+Build+启动一键
```

Unity 侧 EditMode（Windows）：

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . -runTests -testPlatform EditMode `
  -testResults unity-test-results.xml -logFile unity-tests.log
```

**坑**：跑前确认无其他 Unity 实例占用本项目；**Unity 批处理退出码不可信**（崩溃仍返回 0），看日志和结果 XML。

## 硬约束（违反就编译不过 / 静默出错）

| 约束 | 后果 |
| --- | --- |
| `MyWorld.Core.asmdef` 设了 `"noEngineReferences": true` | Core 出现任何 `UnityEngine.*` 直接编译失败。要向量用 `Core/Math/Float3` |
| `MyWorld.Core.csproj`：`netstandard2.1` + `LangVersion 9.0` + `TreatWarningsAsErrors=true` | Core 一条警告都不能留 |
| `EnableDefaultCompileItems=false` | 新增 `.cs` 只要落在被链接目录下就自动纳入，不要改 csproj |
| `Packages/manifest.json` | **保持 URP 14.0.11** 等 2022.3 兼容版本；改成 Unity 6 包号会卡死解析 |
| `ProjectSettings/` 与 `.meta` | 已入版本管理，**别删**（断 GUID = 场景引用全断） |
| Newtonsoft Json 包 | 缺了 Core 编译失败 |

## 玩家与战斗的"唯一入口"

不要绕开这些入口直写底层字段，否则无敌帧/伤害序列/契约会破（详见 CLAUDE.md m7/m9 节）：

- **玩家受伤**唯一入口：`PlayerController.TakeDamage`（摔落/饥饿/怪物近战全走它）
- **玩家血量**唯一真源：`Gameplay/PlayerContext.Health`（UI 与 Combat 都读写它）
- **mob 受伤**唯一入口：`MobAI.TakeHit`（致死一击走 `TransitionToDying`，绕过会丢掉落/经验序列）
- **昼夜判定**：只走 `Combat/MobManager.IsNightPhase`，别处手写时间比较
- **飞行态**（m13 W1）：Core 真源 `Core/Player/FlightState.cs`，存档不记飞行态（重进世界默认步行）；飞行中受伤 `PlayerController.TakeDamage` 守卫短路；HUD IMGUI 一行；双击空格 <0.3s 窗口 + F 等效切换；6 向 8m/s；触地/再切换退出
- **宝宝难度**（m13 W2）：Core 静态开关 `DifficultyMode`（**不放 PlayerPrefs，Core 禁 UnityEngine**），Unity 镜像 `DifficultyModeBridge` 键名 `BabyMode`。宝宝模式 = `MobAI.TakeHit` 入口处 `damage = mob.Health.Max`。**Core 静态 bool 跨测试夹具污染**——调用 `MobAI.TakeHit` 的测试 TearDown 加 `DifficultyMode.ResetCache()`（`PlayerAttackTests` / `ProjectileManagerTests` / `HostileAiTests` / `MachineGuardianTests` / `MobDeathSequenceTests` / `SettingsPanelUiTests` 共 6 个）

## 数据驱动注册表（改 JSON 通常不改 C#）

`Assets/StreamingAssets/` 下所有 JSON，每个目录有 `_format.md` 说明 schema（详见 CLAUDE.md）：

| 目录 | 内容 | 关键约束 |
| --- | --- | --- |
| `blocks/*.json` | 方块 | 内置 7 个 `numericId` 写死 0–6 必须对得上 `Core/Voxel/BlockIds.cs`；1000-1063 矿石段已固化，**1064+** 后续新方块；贴图必须在 `art/requests/blocks/` 提过需求 |
| `items/*.json` | 物品 | numericId 1000 起；1607/1608 已被 musket/bullet 占用、1700/1701 为早期物品（crafting_table/redstone_dust），新增建议 1609-1699 顺延；m13 W3 新增 `range` 字段（米，缺失=近战，负数抛异常）+ `IsStraightLine` 标记（直射无重力） |
| `recipes/*.json` | 合成/熔炉 | 按 `tier` 区分 2x2 / 3x3 / 熔炉 |
| `biomes.json` | 生物群系 | 群系名与 `spawn_rules.json` 引用一致 |
| `mobs/spawn_rules.json`、`mobs/drop_tables.json` | 生物生成/掉落 | 生成一律走 `MobSpawnRules.PickKind`，**不要写 `UnityEngine.Random`**；掉落走 `MobDropTable.RollAll` |
| `mobs/models/*.json` | 生物造型（m11 I1 外置） | `MobKind` 1-27 已占（5 旧 + 9 被动 + 3 敌对 + 村民 + Boss=27），新 kind 从 **28** 起；坐标约定「脚底原点、面朝 +Z」；**加生物 = 1 份 JSON + spawn_rules 一行，不动 C#** |
| `vegetation/trees.json`、`vegetation/flowers.json` | 植被（m11 I2 外置） | 树种哈希通道互相独立；oak 与旧常量逐格一致有守卫 |
| `quests/chapter1.json`、`quests/chapter2.json` | 引导任务双章 | 12 类事件词汇含 `ObtainItem/CraftItem/SmeltItem/SurviveNight/SleepInBed/HarvestCrop/EnchantItem/KillKind` 等；事件由 `QuestEventBus` 转发；进度进 `level.dat`，旧档无字段=全新开始 |

**铁律**：掉落/生成数量一律整数哈希掷骰（参考 `BlockDrops.RollCount`），不持有随机数对象；跨表引用改完跑 `dotnet test`，集成测试会抓悬空引用。

## 坐标与存档的关键陷阱（详见 CLAUDE.md "Core 层设计要点"）

- 坐标换算只走 `VoxelCoords`：世界高度 `[-64, 320)`，区块 16×16，纵向 24 section；换算一律用 `>>4` 和 `&15`（`/`/`%` 在负坐标上向零取整会出错）
- 读容忍、写严格：`GetBlock` 越界或未加载**返回空气而非抛异常**（网格生成需要采样邻居）；`SetBlock` 越界**一律抛异常**
- "实心"两套互不相同：`ChunkMeshSource.IsSolid` 判 `Opaque`（挡视线，水不挡所以水下地形要渲染）；`WorldSolidSource.IsSolidAt` 判 `Solid`（挡移动，水不挡能游进去）
- 热路径用泛型约束而非接口引用：`GreedyMesher.Build<TSource>` / `VoxelRaycaster.Cast<TSource>` 用 `where TSource : I...` 让 JIT 特化掉虚调用——新数据源请沿用 `readonly struct` + 泛型
- 存档格式：`MWRG` 小端序，32×32 区块聚一文件；改二进制布局必须 bump `FormatVersion` 并处理旧版本；写盘先落 `.tmp` 再原子改名；坏 `level.dat` 重命名 `.corrupt` 后全新开始
- 存档双层：每世界一目录 = `level.dat`（JSON: seed/时间/玩家/熔炉/掉落物 + m11 起扩展 ChestContents/BedSpawnPoints/PlayerEnchantments/Stats/FarmStates，旧档缺字段=null 自然兼容）+ `regions/*.mwr`（脏区块 overlay，Deflate 压缩）
- 自动保存（m5+）：主线程只收集快照，JSON+压缩+写盘在 `Task.Run` 后台（防重叠）；`OnApplicationQuit` 同步落盘
- 附魔存 `EnchantStore`（独立于 `ItemStack.Metadata`——16 位全归耐久，a1fb425 教训）；附魔书编码占自身 Metadata 低 8 位
- 死亡天然不掉落（m7 设计）；和平模式 `PeaceMode` 只是钉死该契约
- Boss（机元守卫 MobKind=27）只召唤不自然刷：2×2 机元矿石图腾右键触发；已用图腾记 `LevelData.UsedBossTotems`

## Unity 侧运行时约定（m5+ 起的硬要求）

- **色彩空间 Linear**（m5 切换，根治 Gamma+URP 偏暗）——别改回 Gamma
- 材质一律经 `Rendering/UrpMaterialFactory` 取——裸 `CreatePrimitive` 的 Default-Material 在 URP 下渲染**粉红**
- UI 贴图走 `streamingAssetsPath` 前缀——**不要用 `dataPath/../Assets` 编辑器专用路径**（standalone 必 fallback）
- 物品格一律走 `Unity/UI/ItemSlotDrawer`（图标+数量角标+选中高亮），不要另写黑字 Label
- UI 视觉验证必须走 `--ui-shot`（`visual-smoke.sh` 1c 步骤）——`Camera.Render` 拍不到 IMGUI，只有 `ScreenCapture.CaptureScreenshot` 能抓含 IMGUI 的完整 backbuffer
- 流式/UI 热路径**不许每帧 new 容器或闭包**（GC 移动顿挫）；`ChunkStreamer` 按毫秒预算分帧（默认 8ms）
- `PlayerAudioSystem` 必须挂在 `WorldBootstrap` **步骤 7.5**——先于 `PlayerController.Awake`（步骤 8）与 `BlockInteraction.Bind`（步骤 9）的一次性 `_audio` 缓存点，挪后 footstep/place/break 三音全哑
- Esc 暂停（`UI/PauseMenuUi`）开即 `Time.timeScale = 0`、关恢复 1；退出保存全程保持 0，**依赖 timeScale 的系统别用 `unscaledDeltaTime`**（退出停留窗用 `unscaledTime` 是例外）
- 物品图标 16×16 → 48×48 整数 3 倍 + `FilterMode.Point`

## m12/m13 关键陷阱（新增）

### m12 · 手感与世界管理（计划定稿、**未实施**）

- **挖掘计时**（`Core/Player/DigProgress.cs`，**未创建**）：设计为按住左键累计 `dt × (1 / BreakTime(...))`，松开/移开/换目标重置；进度 ≥1 才 `BreakAt`。现状：`BreakTime` 门槛矩阵（徒手挖石 4s 不掉落/铁镐挖金 3s 等）**至今仍只被测试断言、从未被游戏循环消费**——一次点击仍直接 `BreakAt` 瞬挖
- **落点预览 + 跳跃垫脚**（未实施）：设计为准星命中时在目标格（`hit + Normal`）画半透明幽灵框（`PlacementGhostUi` 或扩 `SelectionBox`）；玩家**腾空**（AABB 底高于目标格顶 - 0.5 容差）且目标格在脚下一格时放宽 `IntersectsPlayer` 允许放置；放置成功把玩家 snap 到新块顶；站立时仍全禁（防自封）
- **世界管理**（`Assets/Scripts/Unity/Persistence/WorldCatalog.cs`，**未创建**）：设计为扫 `worlds/` 目录列表；主菜单三按钮「继续上次/新世界（种子输入框+随机按钮）/世界列表（选中/删除二次确认）」；删除走回收站式改名 `.deleted` 防误删

### m13 · 飞行/血条/难度/远程/武器面板

- **飞行存档不记**（m13 W1）：重进世界默认步行
- **怪物血条**（m13 W2）：`MobView` 头顶 IMGUI 2D 条，红底绿前景，受击 **3s 淡出**；Boss（机元守卫）常显大号；与 `MobHitFeedback` 闪红不重复（两条渲染通道）
- **远程武器参数化**（m13 W3）：items JSON `range` 字段（米）；弓 60（保留重力抛物线）/ 火枪 25（**直射无重力**）；`ProjectileEntity` 飞行 ≥ Range 强制 `Dead`；`BlockInteraction.UseAt` 弓分支后插火枪分支（直射无蓄力 + 装填 1.5s + 弹药扣减 + `PlayerAudioSystem.PlayFire/PlayClick` 程序生成兜底）
- **模态 UI 5 处点外关闭**（m13 W4）：`CraftingInventoryUi` / `CraftingWorkbenchUi` / `ChestUi` / `ArmorSlotsUi` / `WeaponPanelUi`——方案 A（每 UI 自管 `mouseDown` + `!IsInsideRect` → 关闭），不引 `ModalUiManager`；5 个 UI 都暴露 `BackgroundBounds` 属性供 EditMode 断言
- **SHIFT+click 必须先于点外关闭判断**——保留 m13 P0 commit `70f35da` 的合成网格 SHIFT+click 主背包入料路径
- **TitleScreenUi 多视频源**（m13 W5）：4 选项视频源（Hailuo-2.3 1080P 极光/破晓等），通过 `TitleScreenUi.SelectVideo`
- **postprocess_art.py** 支持 `moon-phases`（月相）+ numpy 2.0 兼容

## 编辑器菜单（`MyWorld/` 下五个批处理入口）

均可用 `-executeMethod` 无头跑：

| 菜单项 | 方法 |
| --- | --- |
| 重建预览场景 | `PreviewSceneBuilder.Build` |
| 无头验证玩家层 | `PlayHarness.Run`（含 6 个 NUnit 断言） |
| 接入 URP 管线 | `UrpSetup.Apply` |
| 把 Preview.unity 加入 Scenes In Build | `BuildSetup.Apply` |
| Build Windows | `BuildPlayer.Build` → `Builds/Windows/MyWordGame.exe` |

**不要手改 ProjectSettings 的 Graphics/Quality YAML**——由 `UrpSetup.Apply` 生成并挂到 Graphics 与全部质量档位。

## 收口铁律（m11/m13 教训沉淀，详见 CLAUDE.md）

- **每个里程碑收口与每次内容合入后必须 `./tools/scripts/build-and-run.sh` 重新出包 `Builds/Windows/MyWordGame.exe` 验证**——**双链绿 ≠ 实机可见**
- 验收剧本头部标注包构建时间（`Builds/Windows/MyWordGame.exe` mtime）
- 教训实证：av 赛道 08-18 凌晨提交的 BGM/环境音/标题画面视频在 08-14 旧 exe 里全部"不存在"——孩子在旧包上验收，以为没集成
- **代理不 commit**，由主会话评审后按域顺序提交（避免 index.lock 竞争）
- commit 整理子代理要**明文指定文件归属每个 commit**，避免"按主域"自由发挥带来歧义
- 共享 working tree 并发写**有文件瞬断风险**（m13 W2/W3 并行实证），完成后必须复核文件存在性
- `#if UNITY_EDITOR` 盲区：dotnet 绿 ≠ Unity 编译过，波次收口必跑 EditMode 批处理

## 修改前必读

| 触动区域 | 先读 |
| --- | --- |
| 方块/网格/坐标/存档 | `CLAUDE.md` 的"Core 层设计要点"整节、`docs/specs/visual-text-conventions.md` |
| 美术流程 | `art/README.md`、`art/requests/` 调色板 |
| 新里程碑 | 先写 `docs/superpowers/specs/` 设计，再写 `docs/superpowers/plans/<日期>-<主题>.md` 计划 |
| 玩家/战斗/UI 接入 | `CLAUDE.md` 对应里程碑小节（m5/m6/m7/m8/m9/m10/m11/m12/m13 都有专门约定） |
| 孩子提的需求 | 先落 `docs/小孩子玩后需求/` 原文，对照真实 Minecraft 参数评估后修 spec |
| 音频/视频 | `docs/superpowers/specs/2026-08-18-audio-video-design.md` + 每日 3 次视频配额队列 `art/incoming/video/quota.json` |
| 飞行/血条/难度/火枪/武器面板 | `docs/superpowers/specs/2026-08-19-milestone-13-flight-combat-design.md` + `docs/superpowers/specs/2026-08-19-milestone-13-验收剧本.md` |
| 挖掘/放置/世界管理 | `docs/superpowers/plans/2026-08-18-milestone-12.md`（三大根因 + P0/P1 任务卡；**未实施**，落地前先读） |
