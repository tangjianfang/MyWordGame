# CLAUDE.md

本文件供 Claude Code / 接手开发者阅读。**完整的项目约束、命令、陷阱、里程碑约定都集中在这里**——修改前请通读相关章节。

子代理速查表见 `AGENTS.md`，本文是其详细版。

## 项目速览

- **MyWordGame**：自研体素沙盒游戏，父子共同开发
- **当前基线**：m13 W5 收口（2026-08-20），双链全绿——dotnet **974/974**、EditMode **1732/1732**（2026-08-20 实测）
- **注意：m12 计划已定稿但未实施**（DigProgress / WorldCatalog / 成就 / 图鉴 / 药水乐器均未创建，m13 插队先行）——别把 m12 设计文档当现状
- **三层架构**：`MyWorld.Core`（`Assets/Scripts/Core`，纯 C# 零 UnityEngine） / `MyWorld.Unity`（`Assets/Scripts/Unity`，URP 2022.3 适配层） / `MyWorld.Gameplay`（`Assets/Scripts/Gameplay`，玩法接线）
- **引擎**：Unity 2022.3.62f3c1（中国版）+ URP 14.0.11
- **仓库内所有文档、注释、断言消息一律中文**，新增内容请保持一致

## 常用命令

全部在**仓库根目录**执行，**不需要安装 Unity**：

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln                # 全部测试
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~GreedyMesherTests"   # 单类
dotnet build tools/dotnet/MyWorld.Tools.sln              # 编译三个工程

dotnet run --project tools/dotnet/MyWorld.Preview        # ASCII 世界预览（默认种子）
dotnet run --project tools/dotnet/MyWorld.Preview 12345  # 指定种子
```

`global.json` 把 SDK 固定在 9.0.x（`rollForward: latestFeature`），保证构建可复现，**不要改它**。`.editorconfig` 强制 LF 行尾、C# 4 空格缩进、JSON/csproj 2 空格，新增文件请遵守，不要单独配本地格式覆盖。

**解决方案必须显式指定路径**：Unity 每次导入都会在仓库根目录重新生成自己的 `.sln`/`.csproj`，因此我们的解决方案放在 `tools/dotnet/MyWorld.Tools.sln`，根目录的工程文件已入 `.gitignore`。

Unity 侧跑同一批测试（EditMode）：

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . -runTests -testPlatform EditMode `
  -testResults unity-test-results.xml -logFile unity-tests.log
```

跑之前必须确保没有其它 Unity 实例占用项目（否则报 `another Unity instance is running`）。
**Unity 批处理的退出码不可信——崩溃时仍返回 0**，必须看日志和结果文件。

**一键流水线**（测试 + Build + 启动）：

```bash
./tools/scripts/build-and-run.sh                       # 完整流水线
./tools/scripts/build-and-run.sh --skip-tests         # 只 Build + 启动
./tools/scripts/build-and-run.sh --no-launch --clean  # Build 但不启动，先清旧产物
./tools/scripts/build-and-run.sh --skip-build         # 只跑测试
```

脚本自动 export Git Bash 下必需的 `ProgramFiles(x86)` / `APPDATA` / `LOCALAPPDATA` / `DOTNET_ROOT`，按顺序跑 dotnet test → Unity EditMode → URP 接入 → Scenes In Build 注册 → Windows Standalone Build → 后台启动 `Builds/Windows/MyWordGame.exe`。每步都 grep 结果文件/log 确认通过，失败立刻 `exit 1`。Build 产物与日志在 `.gitignore` 内（`[Bb]uilds/` + `/unity-*.log`）。

`MyWorld.Preview` 输出纵向剖面、俯视高度图、贪心网格压缩比，是本项目**验证世界生成效果的主要手段**——改了 `WorldGenerator` 参数后跑一次，对比前后两张图。字形约定见 `docs/specs/visual-text-conventions.md`。

## 源码位置与工程结构（关键陷阱）

源码的**物理位置在 `Assets/` 下**，`tools/dotnet/*.csproj` 只是用 `<Compile Include="..\..\..\Assets\...">` **链接**同一份文件，不复制。因此：

- **永远编辑 `Assets/Scripts/Core/**` 和 `Assets/Tests/EditMode/**`**，`tools/dotnet/` 下只有 csproj 和 Preview 的 Program.cs
- 两个工程都设了 `EnableDefaultCompileItems=false`，新增 `.cs` 文件只要落在被链接的目录下即自动纳入，无需改 csproj
- `Assets/Tests/EditMode` 用 NUnit，与 Unity Test Framework 一致，同一批测试将来可原样在 EditMode 下跑
- m11 实证：`Assets/Tests/Core/` **不在编译链**，测试必须放 `Assets/Tests/EditMode/**`

## 文档结构

| 目录 | 内容 | 何时新增 |
| --- | --- | --- |
| `docs/specs/` | 视觉/玩法规范文本（参考 `visual-text-conventions.md`） | 需要用文字定画面或玩法时 |
| `docs/superpowers/specs/` | 阶段性技术设计（如玩家层、渲染层的设计文档） | 开启一个新里程碑前 |
| `docs/superpowers/plans/` | 实施计划（`<日期>-<主题>.md`，每完成一项 commit 一次） | 设计文档通过后落地 |

设计（specs/）与实施计划（plans/）拆开存放：前者回答"做成什么样"，后者回答"按什么顺序改哪些文件"。两份都要能脱离上下文独立读懂。实施计划超过单文件长度时拆 `_part2.md` 等后缀继续追加。

**里程碑工作流**（m3 起固定）：spec → plan → 按 superpowers SDD 执行（每 task 派子代理实现→评审→必要时修复轮，最后全分支终审+一次修复波）。SDD 工作区在 `.superpowers/sdd/<plan 名>/`（gitignored，收尾即删——恢复进度靠 `git log`，不靠工作区）。**孩子提的需求**先落 `docs/小孩子玩后需求/` 原文，然后对照真实 Minecraft 设计参数逐条评估：合理项照用、不合理项修正并把勘误写回 spec（spec 与实现打架时以实现为准修文档，评审会抓这种漂移）。

## 视觉设计一律写成文本

本项目不产出视觉稿、不做浏览器原型。画面相关设计写进 `docs/specs/`，用三种形式：**ASCII 示意**（由 `MyWorld.Preview` 从真实生成数据渲染，禁止手绘）、**画面描述**（必须可落地为参数——FOV/相机高度/色号/区间等具体数值）、**参数表**（可直接复制进代码或 JSON 的数字）。避免"更好看""更有氛围"这类无法验证的描述。规范与字形约定见 `docs/specs/visual-text-conventions.md`。

## 分层与硬约束

| 层 | 位置 | 状态 |
| --- | --- | --- |
| `MyWorld.Core` | `Assets/Scripts/Core` | 已实现 |
| `MyWorld.Unity` | `Assets/Scripts/Unity` | 已实现渲染层 + 玩家层 + 玩法接线（Bootstrap 启动器、Player 控制、Combat 生物、UI 物品栏/合成/熔炉/交易，均通过 `Gameplay/PlayerContext` 单例汇聚） |
| `MyWorld.Gameplay` | `Assets/Scripts/Gameplay` | 接线为主（玩法逻辑目前分居 Core 各子系统 + Unity 侧接线） |

Core 层的约束由编译器强制，不是约定：

- `MyWorld.Core.asmdef` 设了 `"noEngineReferences": true` —— **Core 里出现任何 `UnityEngine.*` 都编译不过**。需要向量就用 `Core/Math/Float3`、`Float2`，不要用 `Vector3`
- `MyWorld.Core.csproj`：`netstandard2.1` + `LangVersion 9.0`（对齐 Unity 的 API 兼容级别）+ `TreatWarningsAsErrors=true`。**Core 层一个警告都不能留**
- Core 唯一的外部依赖是 Newtonsoft.Json（方块注册表解析 JSON）

**Unity 侧引擎约束**（与 Core 约束同级重要）：

- `Packages/manifest.json` 的版本已实机解析验证，**不要改成 Unity 6 的版本号**（URP 17.x、Burst 1.8.30 在 2022.3 上不存在，会卡死包解析）
- **URP 用 14.0.11**（编辑器内置，解析不走网络）；管线资产在 `Assets/Settings/`，由 `UrpSetup.Apply` 生成并挂到 Graphics 与全部质量档位——**不要手改那两个 ProjectSettings 的 YAML**
- `ProjectSettings/` 与 `.meta` 文件**已纳入版本管理**，不要删——`.meta` 决定资源 GUID，丢失会导致场景引用全部断链

## Core 层设计要点

**坐标换算只走 `VoxelCoords`。** 世界高度 `[-64, 320)`，区块 16×16、纵向切成 24 个 section。换算一律用算术右移 `>>4` 和位掩码 `&15`，因为 `/` 和 `%` 在负坐标上会向零取整而出错。不要在别处手写坐标换算。

**读容忍、写严格。** `ChunkColumn.GetBlock` / `World.GetBlock` 越界或未加载时**返回空气而非抛异常**——网格生成需要采样区块外的"邻居"来剔除接缝面，宽容读取免掉大量边界判断；且读取不会创建区块，避免采样时撑大内存。而 `SetBlock` 越界一律抛异常。

**"实心"有两套互不相同的含义，别混用：**
- `ChunkMeshSource.IsSolid` → 判 `Opaque`（是否挡视线）。水不挡视线，所以水下地形要渲染
- `WorldSolidSource.IsSolidAt` → 判 `Solid`（是否挡移动）。水不挡移动，玩家可以游进去

**热路径用泛型约束而非接口引用。** `IBlockSource` / `ISolidBlockSource` 的实现都是 `readonly struct`，`GreedyMesher.Build<TSource>` 和 `VoxelRaycaster.Cast<TSource>` 用 `where TSource : I...` 约束，让 JIT 特化掉虚调用。新增数据源请沿用 struct + 泛型的写法。

**`ChunkSection` 是局部调色板 + 位打包。** 位宽只取 0/1/2/4/8/16（能整除 64，条目不跨 `ulong` 边界），随调色板大小自动升位。位宽 0 表示整段同一种方块，此时**不分配数组**。

**贪心网格的 UV 按格数铺开**（`(0,0)..(width,height)`），所以**方块贴图必须四边无缝平铺**，否则大平面会出现规则网格状接缝。这条约束反向决定了整个美术流程，见 `art/README.md`。遍历从 `axis = -1` 开始，使最外层能与邻区块比对；区域外的面留给邻区块生成，避免双方重复出面。

**确定性生成。** `ValueNoise2D` 用整数哈希，不持有随机数对象；`WorldGenerator` 只依赖 seed 与**世界坐标**（不是区块内坐标），因此与生成顺序无关、跨区块接缝自然对齐、可并行调用。改动这里务必保持这个性质。

**存档只存改动过的区块。** `RegionFile` 把 32×32 区块聚成一个文件（魔数 `MWRG`，小端序），未改动的区块加载时由 seed 重新生成。`ChunkSerializer` 只写非空 section（用 32 位掩码标记），数据段 Deflate 压缩。改二进制布局必须同步 bump `FormatVersion` 并处理旧版本。

**存档是双层结构（m4 起接入实机）。** 每个世界一个目录：`level.dat`（JSON，seed/时间/玩家/熔炉/掉落物 + m11 起扩展 ChestContents/BedSpawnPoints/PlayerEnchantments/Stats/FarmStates 字段，旧档缺字段=null 自然兼容）+ `regions/*.mwr`（脏区块 overlay）。30s 自动保存 + 退出保存，均走 `SaveLoadService`；读取用 `TryRestore`，按时间→玩家→熔炉→掉落物的顺序恢复，任何一层失败跳过该层继续。坏 `level.dat` 重命名 `.corrupt` 后全新开始；写盘先落 `.tmp` 再原子改名，杀进程不会留下半截档。自动保存拆两段（m5 起）：主线程只收集纯数据快照，JSON 序列化 + 压缩 + 写盘在 `Task.Run` 后台做（后台写盘期间跳过新一轮，防重叠），`OnApplicationQuit` 的退出保存保持同步落盘。

## 数据驱动注册表

`Assets/StreamingAssets/` 下所有 JSON，每个目录有 `_format.md` 说明 schema：

| 目录/文件 | 内容 | 加载者 | 要点 |
| --- | --- | --- | --- |
| `blocks/*.json` | 方块定义 | `BlockDefinitionFiles` | 内置 7 个方块的 `numericId` **写死为 0–6**，必须与 `Core/Voxel/BlockIds.cs` 的常量一一对应；新方块不写 `numericId` 时，系统按 `id` 字母序从 1000 起自动分配（排序保证跨机器结果一致）；m10/m11 加的矿石类已固化到 1000-1063 段，m12+ 后续新方块从 1064 起。贴图必须在 `art/requests/blocks/` 提过需求。`BlockDefinitionFilesTests` 校验 JSON 解析、ID 冲突、numericId 与 `BlockIds` 一致、贴图需求全部已立项 |
| `items/*.json` | 物品定义（numericId 1000 起） | `ItemDatabase.FromJson` | 跨表引用的 itemId 必须在此注册，否则加载抛 `InvalidDataException`；m13 W3 起新增 `range` 字段（米，`ItemDefinition.Range` int；缺失默认 0=近战；负数抛异常）+ `IsStraightLine` 标记（直射无重力） |
| `recipes/*.json` | 合成/熔炉配方 | `RecipeDatabase` | 2x2 口袋 / 3x3 工作台 / 熔炉按 `tier` 区分；`FindMatch` 排序稳定（shaped→materialCount→id） |
| `biomes.json` | 生物群系（温度/湿度/地表/树密度） | `BiomeConfig` | 群系名与 `spawn_rules.json` 引用需一致 |
| `mobs/spawn_rules.json` | 各生物在哪些 biome/光照生成 | `MobSpawnRulesLoader` → `MobManager` / `VillagerManager` | 生成一律走 `MobSpawnRules.PickKind`，**不要在 Unity 侧写 `UnityEngine.Random`** |
| `mobs/drop_tables.json` | 生物死亡掉落（`countMin`/`countMax` 区间 + `chance` 概率） | `MobDropTable.Load` → `MobAI.DropTable` | 掉落走 `MobDropTable.RollAll`（每条 entry 独立掷骰，整数哈希，确定性） |
| `mobs/models/*.json` | 生物部位造型表（m11 I1 外置；脚底原点/面朝 +Z 约定不变） | `MobModelLibrary` → `MobModels` 门面 | **加生物 = 1 份 JSON + spawn_rules 一行，不动 C#**；schema `{ kind, parts: [{ name, size, position, color, isLeg, legPhase }] }`；`MobKind` 数值 1-27 已固定（5 旧 + 9 被动 + 3 敌对 + 村民 + Boss=27），新 kind 从 28 起 |
| `vegetation/trees.json`、`vegetation/flowers.json` | 植被特征表（m11 I2 外置：8 树种/12 花草） | `VegetationTable` → `TreeFeature`/`FlowerFeature` | 树种哈希通道互相独立（世界坐标 + species 序号派生）；oak 与旧常量逐格一致有守卫 |
| `blocks/drops/block_drops.json` | 挖方块掉落 | `BlockDropsLoader` → `BlockInteraction` | 整数哈希掷 count，确定性 |
| `quests/chapter1.json`、`quests/chapter2.json` | 引导任务链双章 | `QuestChainLoader` → Core `QuestCampaign`/`QuestSystem` | 首章 8 步（挖→合→烧→活过夜）；二章 8 步（床→农→收麦→驯羊剪毛→铁甲→附魔→弓杀骷髅→退苦力怕，链式解锁）；事件由 Unity 侧 `QuestEventBus` 转发（游戏逻辑不感知任务系统）；12 类事件词汇含 `ObtainItem/CraftItem/SmeltItem/SurviveNight/SleepInBed/HarvestCrop/EnchantItem/KillKind` 等；CraftItem/SmeltItem 按**任务激活以来累计**、ObtainItem 看**背包现存量**；进度进 `level.dat`，旧档无字段 = 全新开始 |

**改这些 JSON 时两条铁律**：掉落/生成数量一律用整数哈希掷骰（参考 `BlockDrops.RollCount`），不持有随机数对象；itemId/biome 名等跨表引用改完必须跑 `dotnet test`，集成测试会抓住悬空引用。

**美术硬约束**：`art/README.md` 完整流程；要点——
- 需求写在 `art/requests/`，产物先落 `art/incoming/`（不入版本管理），验收后才移进 `Assets/`
- **程序占位快路径**（m6 起常用）：`art/scripts/*.py` 确定性哈希生成像素占位（贴图/图标）直接入 `Assets/`，`art/requests/` 同步立需求并在 `art/README.md` 索引标注「程序占位，待正式美术替换」——保证贴图引用差集恒为空
- 方块贴图统一 **32×32**、无抗锯齿、**四边无缝**；生成时要 512/1024，再用**最近邻**降采样
- 物品图标 16×16 → 48×48 整数 3 倍 + `FilterMode.Point`
- 透明走洋红键控（`#FF00FF` 后处理键控）；Alpha 只有 0/255 两态
- 入库后要把 `art/README.md` 需求索引表的状态改成「已入库」

## 里程碑约定（m5-m13 按节列"非做不可"的硬约束）

### m5 · 视觉与体验打磨

- **色彩空间是 Linear**——m5 切换，根治 Gamma+URP 系统性偏暗；**别改回 Gamma**，会全局变闷
- 人物/生物/UI 叠加层材质一律经 `Rendering/UrpMaterialFactory` 取——裸 `CreatePrimitive` 的 Default-Material 是 Standard shader，URP 下渲染**粉红**
- 玩家血量唯一真源是 `Gameplay/PlayerContext.Health`，UI 与 Combat 都读写它，**不要另建血条**
- 昼夜判定只走 `Combat/MobManager.IsNightPhase`，别在别处手写时间比较
- 启动时 `WorldBootstrap` 按物理屏系统分辨率自适应无边框全屏（修非 16:9 屏两侧黑边）
- `ChunkStreamer` 每帧区块工作按**毫秒预算**分帧（Stopwatch 计时，默认 8ms，耗尽等下一帧），不是固定根数
- 流式/UI 热路径**不许每帧 new 容器或闭包**（GC 会造成移动顿挫）——用成员级复用容器 + `Clear()`

### m6 · UI 修复 + 帮助菜单 + 引导任务链

- UI 贴图（hotbar 槽底/选中框等）放 `Assets/StreamingAssets/ui/`，加载走 `streamingAssetsPath` 前缀——**不要用 `dataPath/../Assets` 这类编辑器专用路径**（standalone build 必 fallback，曾导致选中框整块纯白盖住图标）
- 物品 UI（背包/工作台/口袋/熔炉/交易）的物品格一律走 `Unity/UI/ItemSlotDrawer`（图标 + 数量角标 + 选中高亮，纹理缓存共享），不要另写黑字 Label
- IMGUI/OnGUI 视觉验证必须走 `--ui-shot` 截图管线（`visual-smoke.sh` 1c 步骤）——`Camera.Render` 拍不到 IMGUI，只有 standalone 的 `ScreenCapture.CaptureScreenshot` 能抓到含 IMGUI 的完整 backbuffer
- `HelpMenuUi`（H 键）：两页 Tab「怎么玩」+「设置」；`SettingsPanelUi` 三滑条（灵敏度/音量/FOV），PlayerPrefs 键名 `m6.sensitivity` / `m6.volume` / `m6.fov`
- `QuestEventBus` 单入口转发事件，游戏逻辑不感知任务系统

### m7 · 生存可用性三修

- 玩家受伤**唯一入口**是 `PlayerController.TakeDamage`（摔落/饥饿/怪物近战全走它）——直写 `PlayerContext.Health.Damage` 会绕过复活后的 3 秒无敌帧（`RespawnAtSpawn` 设 `InvincibleUntil`，重生固定回世界出生点，不消费 `LastDeathPosition`）
- 僵尸平衡参数是 C# 常量（不进 JSON）：**白天不追**（`MobAI.Tick` 的 `isNight` 参数，真源 `MobManager.IsNightPhase`）、AttackRange **4** / ChaseRadius **20**、距玩家 >40 格 despawn（`MobManager.DespawnDistance`）
- 右键路由**食物优先**：选中槽 `ItemDefinition.IsEdible`（= `HealAmount > 0`）时右键=吃（`HungerSystem.Eat`，饥饿+饱和同回），不放方块；`CombatController` 的旧右键吃分支已删，别加回来——双路并存会一次右键双扣物品
- 帮助菜单（H）的「保存并退出」走 `SaveLoadService.SaveNow(async:false)` 同步落盘后 0.5s 退出（保存失败不退出、可重试）；Alt+F4 走既有 `OnApplicationQuit` 同步保存
- 掉落物视觉是 `ItemDropView`（0.25 格悬浮自转小方块，颜色取物品贴图均值色）；拾取吸附状态机在 Core `ItemDropEntity.TickPickup`——半径 **2.5m** 内飞向玩家，贴脸 0.3m 才入包，`TryPickupBy` 只做完成判定

### m8 · 生物拼装造型 + Esc 暂停菜单

- 五生物（猪牛鸡僵尸村民）造型是 `Rendering/MobModels` **纯静态部位表**（部位尺寸/位置/颜色/腿相位），`MobView` 按表拼装（模式抄 `PlayerVisual`），部位表坐标约定「脚底中心为原点、面朝 +Z」——改造型改表不改代码，EditMode 直接断言表
- `MobManager` host cube 禁用 Renderer（与 Body 重合会渲染冲突）
- Esc 暂停（`UI/PauseMenuUi`）打开即 `Time.timeScale = 0` 真暂停、关闭恢复 1；退出保存全程保持 0，**依赖 timeScale 的系统别用 `unscaledDeltaTime`**（退出停留窗用 `unscaledTime` 是例外）
- 三滑条设置面板是公共组件 `UI/SettingsPanelUi`，帮助菜单（H）与暂停菜单共用同一实例——调灵敏度/音量/FOV 别再建第二套

### m9 · 战斗手感与打猎闭环

- 左键攻击与挖掘**同键分流**：准星 4m 内命中 mob 优先攻击（空手基础伤 1，武器取 items 表 `attackDamage`），否则挖掘；mob 命中后再经 `VoxelRaycaster` 体素视线复核（墙后不命中、也不抑制挖掘），冷却 0.5s，模态 UI 开着不打（与挖矿同款指针门）
- 玩家伤害**唯一入口** `MobAI.TakeHit`——别直扣 `Health` / 直置 `Dying`：致死一击同步走 `TransitionToDying`（LastDrops + 击杀经验与 Core-only 死亡共用一条序列），尸体再击 no-op
- 被动动物**受击才逃**（替换旧的「靠近惊跑」）：`TakeHit` 开 3s 逃跑窗、`FleeSpeed`=4.0（< 玩家走速 4.3，追得上但不轻松），可再打再逃；血量 猪 10 / 牛 15 / 鸡 4
- 击杀经验常量表 `MobManager.KillExperience`：猪 3 / 牛 5 / 鸡 2 / 僵尸 10，与掉肉同处观察死亡、独立结算（经验显示走底部经验条，spec 的「+N 飘字」未做，归 m11 W2-3 落地）
- 战斗手感宿主 `Unity/Combat/MobHitFeedback`（`SpawnMob` 自动挂）：闪红 0.15s（Core `MobAI.HitFlashDuration` 同值、两条渲染通道不漂移）、击退 1.5m 冲量**写 Core `Mob.Position`**（`MobView.LateUpdate` 每帧覆写 transform，写 transform 无效）、死亡 0.3s 缩小（< Dying 0.5s 移除，不腰斩）、命中音 `PlayHit`（无 `hit.ogg` 时程序生成）
- `PlayerAudioSystem` 必须挂在 `WorldBootstrap` **步骤 7.5**——先于步骤 8 `PlayerController.Awake` / 步骤 9 `BlockInteraction.Bind` 的一次性 `_audio` 缓存点，挪后则 footstep/place/break 三音全哑

### m10 · 矿物与装备进阶

- 挖矿门槛是数据不是代码：`blocks/*.json` 的 `minToolTier`（0 手/1 木/2 石/3 铁/4 钻），镐自身档位在 items 表 `toolTier`——**低于门槛挖得掉方块但无掉落、时间 ×4**（Core `Blocks/BlockGating`），守卫测试强制每把镐显式声明 `toolTier`/`maxDurability`（写漏=测试红）
- 4 新矿石（金 y<32 / 粗铁 y<48 / 合金 y<24 / 机元 y<16，字形 `$ % & @`）——**机元矿石方块 15% 掉落（图腾可达）**（commit `3aed5be`），世界生成在石层按确定性哈希嵌矿（`OreFeature.OreAt` 用世界坐标 FNV-1a 哈希，按稀有度+地层判矿石），改嵌矿参数跑 `MyWorld.Preview` 目检矿层剖面
- 镐耐久走 `ItemStack.Metadata`（木 59 / 石 131 / 铁 250 / 钻封顶 255——8 位编码上限），hotbar 图标叠耐久条；耐久尽镐**碎裂**成碎块散落（0.5 伤、同一次碎裂共享一次伤害、2s 消失）
- 装备属性是 items 表 `gearBonus`（defense/moveSpeed/maxHealth；金系攻击直接叠在 `attackDamage` 上不走 gearBonus）；**m11 W2-1 起** `Defense` 改为 4 槽穿戴叠加，金系攻击仍走 attackDamage 手持
- 升级 = 同材料 2 件**满耐久**合 1 件 `*_plus`（加成翻倍，残次品拒合——防洗耐久）
- 死亡画面**右键=点复活按钮**（`BlockInteraction.UseAt` 见死亡画面即让位——右键复活不会误吃食物/误放方块）

### m11 · 完整游戏产品化

**八梯队 33 项 + 12 大类美术树 + 五波并行**，已完成；落地沉淀：

- **三注册表外置**（I1/I2/I3）：
  - **I1** `MobModelLibrary` + `mobs/models/*.json`（schema `{ kind, parts: [...] }`）
  - **I2** `VegetationTable` + `vegetation/{trees,flowers}.json`（树种 8 / 花草 12，哈希通道互相独立）
  - **I3** `LevelData` 新字段：`ChestContents` / `BedSpawnPoints` / `PlayerEnchantments` / `Stats` / `FarmStates`（Newtonsoft 缺字段=null 自然兼容）
- **附魔系统**：`EnchantStore` 独立于 `ItemStack.Metadata`——16 位全归耐久（m3 预留 `MaxDurabilityMask 0xFF00 / CurDurabilityMask 0x00FF` + `HasDurability`），附魔书编码占自身 Metadata 低 8 位
- **Boss（机元守卫 MobKind=27）**：只召唤不自然刷，2×2 机元矿石图腾右键触发；三招状态机（冲撞/范围震荡波/召唤 2 骷髅，半血召），掉下界合金锭 + 附魔书；已用图腾坐标记 `LevelData.UsedBossTotems`
- **村庄**：400 格村格哈希 + 干地坡度守卫 + 蓝图盖章跨区块 + 村民偏向 API
- **和平模式**（`PeaceMode` 静态开关）：敌对不刷 + 死亡天然不掉落（m7 设计钉契约）
- **天气雨雪 / 云层漂移 / 粒子池**（atmos 端到端）：纯视觉不碰逻辑
- **通用放置路由**：物品 JSON 带 `blockId` 字段即可放置；床/门自动双格原子放置防半放；悬空引用不抛
- **盔甲穿戴栏** 4 槽：`ArmorInventory` 部位校验、旧甲换回、双源属性汇总、存档往返 + 16 件盔甲注册 armorPart 词表
- **任务双章 16 步**：见上「数据驱动注册表」表
- **成就 + 图鉴**（m12 W1/W2，**未实施**）：`LevelData.Stats` 字段已预留；徽章/卡牌美术**已入库**（`Assets/Art/Codex/` 27 张：16 徽章 + 8 内容卡 AI 正式美术 + 3 卡框程序生成——2026-08-20 补丁批，注意目录大写 C）

**音频/视频四系统**（m11 av 落地）：
- **BGM**：双 AudioSource 交叉淡入淡出 3s 线性渐变；状态机 `menu → day ↔ night`（昼夜判定走 `MobManager.IsNightPhase`）
- **环境循环**：单 AudioSource；选择规则（每 2s 判定一次）——玩家 y<40 → `amb-cave`；否则 `IsNightPhase` → `amb-crickets` / 白天 → `amb-birds`
- **玩家事件音**：`PlayerAudioSystem` 现共 17 个 `Play*` 方法（m7 三音 footstep/place/break + av 11 事件 eat/hurt/die/pickup/craft/door-open/door-close/hoe-till/plant/harvest/tool-break + m13 W3 PlayFire/PlayClick），挂在既有钩子上；Resources 优先真资源，无 .ogg 时程序生成兜底
- **生物叫声**：idle 整数哈希掷骰间隔 8-20s（**距玩家 >16 格不播**省性能）；查表 `Resources.Load("Audio/Mobs/<kind小写>-idle")` 缺文件回退 `generic-small/large`——**新生物加专属叫声 = 放两个 ogg，零代码**
- **`SettingsPanelUi` 第四滑条**「音乐音量」（0–100，PlayerPrefs key `m6.musicVolume`），经静态 `MusicVolumeBus.Volume`（0–1）只管 BGM+环境两类 AudioSource；音效仍走原音量滑条；`AudioListener.volume` 保持全局总闸；面板高度常量 220→300
- **`TitleScreenUi` 极简主菜单**：遮罩模式（`WorldBootstrap` 步骤序不动，菜单以最上层遮罩呈现，底下世界照常生成）；**m13 W5 起支持 4 选项视频源**（`TitleScreenUi.SelectVideo`，Hailuo-2.3 1080P 极光/破晓等）
- **`VideoScreenSystem`**：laptop_block / hacker_pc_block 屏幕面走材质 `mainTexture` 替换，**贪心网格零改动**；两方块共用 `laptop-screen` 贴图名与同一段视频；**已知副作用**：hotbar 图标里屏幕面会拍到视频某一帧，视觉上等同"屏幕亮着"，不处理
- **视频每日配额队列**（**核心机制**：Token Plan 视频每天只能 3 次）：状态文件 `art/incoming/video/quota.json`（gitignored），`{date, usedToday, maxPerDay: 3, tasks: [{name, status, attempts}]}`；状态机——读 quota.json；date 非今天重置；提交前检查 `usedToday < maxPerDay`；失败 attempts+1 次日重试（attempts≥3 不再自动重试）；成功 status=done、usedToday+1；每次状态变更即写回（半截中断不丢进度）；音频不受配额
- 视频生成：MiniMax API 768P → 1080P + ffmpeg crf 18 + 1920×1080 上采样兜底 + 0.6s alpha overlay 无缝循环 fade（RMSE 0.20→0.097）
- 视频 RT 同步升档：主菜单 1920×1080 / 笔记本屏 640×360（commit `9a811c3`）
- 音频 36 条 .ogg 实际入库（BGM 3 + 环境 3 + 玩家事件 14 + 生物叫 idle/hurt 16）；视频现共 5 个 mp4（m11 时 menu-bg + laptop-loop；m13 W5 加 aurora-burst / aurora-storm / minecraft-dawn，启动菜单 4 选项）

### m12 · 游戏手感与完成度（计划已定稿、**实施待执行**）

> **状态警示**：m12 只有 docs 立项（plan `2026-08-18-milestone-12.md`），实现文件（`DigProgress` / `WorldCatalog` / `PlacementGhostUi` / `AchievementSystem` / `CodexUi` / `PotionSystem`）**均未创建**——m13 插队先行落地。下述是定稿设计，实施时照此执行。

**三大根因**（孩子实机反馈并入，均已核实）：
1. **挖掘无计时**：`BlockInteraction.Update` 里 `Input.GetMouseButtonDown(0)` 一次点击直接 `BreakAt`，`BreakTime(blockId, biome, toolTier, …)` 门槛矩阵**只被测试断言、从未被游戏循环消费**——玩家实际体验是全瞬挖
2. **放置六向体感缺失 + 无法向上搭**：`BlockPlacement.TryResolve` 用 `hit + Normal` 机制上六向可选，但 `IntersectsPlayer` 防卡身锁死跳跃垫脚、面选择不可见无落点预览
3. **主菜单无世界管理**：`TitleScreenUi` 旧版只有「开始游戏/退出」

**落地方案（定稿，未实施）**：
- **`DigProgress` 挖掘计时**（Core `Assets/Scripts/Core/Player/DigProgress.cs`）：按住左键累计 `dt × (1 / BreakTime(...))`，松开/移开/换目标重置；进度 ≥1 才 `BreakAt`；准星方块叠破坏裂纹（`Assets/Art/UI/break-0..4.png` 五档已有，按进度分档换图）；模态 UI 开着暂停蓄力（共用既有指针门）；作物/花草秒挖分支（hardness<0.05 → 0.15s）自然兼容；效率附魔乘数走 `SelectedDigTimeMultiplier()`；同一方块内小幅晃动不清进度（容差 0.1 格吸附）
- **六向放置 + 跳跃垫脚**：
  - **落点预览**（`PlacementGhostUi` 或扩 `SelectionBox`）：准星命中时在目标格（`hit + Normal`）画半透明幽灵框，材质两态照水款半透，放哪一格**看得见**
  - **跳跃垫脚（tower-up）**：玩家**腾空**（AABB 底高于目标格顶 - 0.5 容差）且目标格在脚下一格时，放宽 `IntersectsPlayer` 允许放置；放置成功把玩家 snap 到新块顶；站立时仍全禁（防自封）
  - **五向命中测试钉死**（参数化 EditMode）：正上方俯视打顶面、正下方仰视打底面、东西南北四侧平视打侧面，各自 `Normal` 与落格正确
  - **俯仰角核查**：确认相机 pitch 无 ±80° 钳制（若有放开到 ±89°）
- **`WorldCatalog` 世界管理**（`Assets/Scripts/Unity/Persistence/WorldCatalog.cs`）：扫 `worlds/` 目录列表（存档结构 `<seed>/level.dat` 已有）；主菜单三按钮「继续上次（无则灰）/新世界（种子输入框+随机按钮，非法输入提示）/世界列表（名字=seed+日期，选中进入/删除二次确认）」；最近玩的世界记 `PlayerPrefs`；删除走回收站式改名 `.deleted` 防误删；世界名沿用 seed 作目录名（不改存档结构）

**内容与收集**（第 1 波规划，未实施）：
- **W1 成就**：16 成就定义表 JSON `achievements.json` + `LevelData.Stats` 字段复用 + `QuestEventBus` 事件词汇已通
- **W2 图鉴**：`codex/card-*.png` 已入库，首次遭遇解锁（mob TakeHit 见过/方块首次挖到/花草首次采）
- **W3 药水与乐器**：7 瓶药水 + 4 件乐器贴图已有，v1 手持右键发声 + 音盒放置后右键播放 8 音符小星星
- **W4 P2 美术 + 水生飞行生物**：P2 美术批 m13 W5 已部分入仓；水生飞行生物未做

**numericId 现状**：方块最大 1062（1064+ 为 m12+ 预留段）；物品 1607/1608 已被 musket/bullet 占用，1700/1701 是早期物品（crafting_table/redstone_dust），新增物品建议 1609-1699 段顺延。

### m13 · 飞行与远程战斗（孩子第二批需求）

m13 设计文档 `docs/superpowers/specs/2026-08-19-milestone-13-flight-combat-design.md` 含 11 条需求逐条评估（照用 / 采纳 / 修正 / 验证即可）。落地沉淀：

- **P0 合成修复**（commit `70f35da`，插队诊断）：孩子原话「M1和MP背包和工作台里面的物品都没办法合成」。根因 = UI 把 hotbar 选中格当作合成网格的**唯一**入料通道，主背包 9..35 共 27 格完全没有 SHIFT+click / 拖拽 / 右键快捷送入任何路径入网。`RecipeDatabase.FindMatch` 排序全绿（21 个 Recipe dotnet 测试通过），**UX 缺口非 bug**。修复：`CraftingGridInteraction.cs` 加 `IsShiftLeftClickIn` / `PutMainSlotOne` / `TakeBackToMainSlotOne`；`CraftingInventoryUi` / `CraftingWorkbenchUi` 主背包 27 格加 SHIFT+click；`HelpMenuUi` 加 `CraftingShiftHint` 教学文案；`Assets/Tests/EditMode/UI/CraftingGridInteractionTests.cs` 6 个 EditMode 测试钉死契约；`HelpMenuUiTests` 加 1 个钉死文案
- **W1 飞行系统**（commit `7b1f1f6`）：Core 真源 `Core/Player/FlightState.cs` 纯数学状态机（双链可测），Unity 侧 `PlayerController` 注入飞行分支。**双击空格（<0.3s 窗口）+ F 键**等效切换；飞行中**空格升 / Shift 降 / WASD 平移**（8m/s ≈ 走速 1.9 倍），无重力、掉血豁免；**触地/再切换**退出；HUD IMGUI 一行；**存档不记飞行态**（重进世界默认步行）。飞行态下 `PlayerController.TakeDamage` 守卫短路——CLAUDE.md "玩家受伤唯一入口" 契约，不直改 `Health.Damage`
- **W2 怪物血条 + 难度系统**（commit `e46e14b`）：
  - **血条**：`MobView` 头顶 2D 条 IMGUI 绘制，红底绿前景（宽随体型 0.6-1.2m），受击显示 **3s 淡出**；**Boss（机元守卫 MobKind=27）常显大号**。普通 mob `_healthBar` 是构造期快照 `MobHealthBarTimer`，与 `MobHitFeedback` 闪红不重复（两条渲染通道）
  - **难度**：仿 `PeaceMode` 静态开关模式新增 `Core/Entities/DifficultyMode.cs`（Core 真源，**不放 PlayerPrefs**——Core 禁引 UnityEngine）+ `Unity/Gameplay/DifficultyModeBridge.cs`（Unity 侧 PlayerPrefs 镜像，键名 `BabyMode`）；设置面板「宝宝（怪 1 血）/ 普通（现值）」toggle 行紧贴和平模式 toggle 之后（`SettingsPanelUi.PanelHeight` 370→440）；**宝宝模式 = `MobAI.TakeHit` 入口处 `damage = mob.Health.Max`**（不改 Boss 单独规则；切难度即时生效；不改存档）
  - **Core 静态 bool 跨测试夹具污染警告**：所有调用 `MobAI.TakeHit` 的测试 TearDown 加 `DifficultyMode.ResetCache()`（`PlayerAttackTests` / `ProjectileManagerTests` / `HostileAiTests` / `MachineGuardianTests` / `MobDeathSequenceTests` / `SettingsPanelUiTests` 共 6 个）
- **W3 远程武器参数化 + 火枪**（commit `f2b0ede`）：items JSON 新增 `range` 字段（米，`ItemDefinition.Range` int；缺失默认 0=近战；负数抛 `InvalidDataException`）。弓 60（保留重力抛物线）/ 火枪 25（**直射无重力**，新增 `IsStraightLine` 标记）；`ProjectileEntity` 飞行 ≥ Range 强制 `Dead`。**musket 火枪**（numericId=1607，attackDamage=6，range=25）+ **bullet 子弹**（numericId=1608，maxStack=64）；配方 musket=铁锭2+木板2+火药1 / bullet=铁锭1→4（3×3 workbench）。`BlockInteraction.UseAt` 弓分支后插火枪分支：**直射无蓄力** + **装填 1.5s**（`MusketReloadUntil=Time.time+1.5`）+ 弹药扣减（无弹不开火+播咔哒）。音效走 `PlayerAudioSystem.PlayFire()` / `PlayClick()`：Resources 优先真资源，无 .ogg 时**程序生成**120ms 低通（火枪）/ 30ms 高通（咔哒）兜底。弓 JSON 已补 `range: 60`。**镐 tier 链**：木1/石2/铁3/金4/合金4/机元5——火枪 tier=2 需石镐以上
- **W4 武器面板 + 模态 UI 点外关闭**（commit `acea016`，EditMode 1732/1732 全绿 + CLAUDE.md 收口）：
  - **武器面板** R 键 `Unity/UI/WeaponPanelUi.cs` 列表背包全部武器（近战/弓/枪+各自弹药数）+ 点击换到 hotbar 选中槽
  - **模态 UI 5 处点外关闭**：`CraftingInventoryUi` / `CraftingWorkbenchUi` / `ChestUi` / `ArmorSlotsUi` / `WeaponPanelUi`（图鉴 `CodexUi` / 成就 `AchievementUi` 同样适用）——**点击 UI 外 = 关闭**，与 Esc/E/关闭按钮并存；方案 A（每 UI 自管 `mouseDown` 位置 + `!IsInsideRect(mousePos, uiRect)` → 关闭），不引 `ModalUiManager`；5 个 UI 都暴露 `BackgroundBounds` 属性供 EditMode 断言
  - **SHIFT+click 必须先于点外关闭判断**——保留 m13 P0 commit `70f35da` 的合成网格 SHIFT+click 主背包入料路径
- **W5 美术入仓 + 多视频源**（commit `5e4c357` + `76f8cec`）：
  - 27 张缺省贴图入库 + 6 张实体美化
  - `postprocess_art.py` 加 `moon-phases`（月相）支持，**numpy 2.0 兼容**
  - `TitleScreenUi` 支持 **4 选项视频源**（Hailuo-2.3 1080P 极光 / 破晓等）通过 `TitleScreenUi.SelectVideo`

### m13 收口铁律

- m13 全部 8 个 commit（`70f35da` P0 / `743601e` + `9a811c3` video / `7b1f1f6` W1 / `e46e14b` W2 / `f2b0ede` W3 / `acea016` W4 / `76f8cec` + `5e4c357` W5）落地后双链全绿——dotnet 974/974、EditMode 1732/1732
- **双链绿 ≠ 实机可见**——必跑 `./tools/scripts/build-and-run.sh` 重新出包 `Builds/Windows/MyWordGame.exe` 验证，交付时在验收剧本头部标注包构建时间（`Builds/Windows/MyWordGame.exe` mtime）
- **教训实证**：av 赛道 08-18 凌晨提交的 BGM/环境音/标题画面视频在 08-14 旧 exe 里全部"不存在"——孩子在旧包上验收，以为没集成

### m13 并行子代理纪律（实战沉淀）

- 多子代理**共享 working tree 并发**有文件瞬断风险（m13 W2/W3 并行实证——W2 改了 W3 范围测试的 TearDown，W3 改了 W2 范围测试的逻辑）
- commit 整理子代理要**明文指定文件归属每个 commit**，避免"按主域"自由发挥带来歧义
- `#if UNITY_EDITOR` 测试盲区：dotnet 绿 ≠ Unity 编译过，波次收口必跑 EditMode 批处理

## 测试纪律

- 测试必须放 `Assets/Tests/EditMode/**`，**不放 `Assets/Tests/Core/`**（m11 实证：后者不在编译链）
- **dotnet 链与 EditMode 链只增不减**——同一批测试文件，双链都能跑
- EditMode 与 dotnet 是同一批测试文件，EditMode 侧还多 Unity 专属的 MonoBehaviour 接线测试
- m13 W5 收口基线（2026-08-20 实测）：dotnet **974/974** + EditMode **1732/1732** 双链全绿
- **`MobAI.TakeHit` 静态 bool 跨测试夹具污染**：所有调用 `MobAI.TakeHit` 的测试 TearDown 加 `DifficultyMode.ResetCache()`；同理 `PeaceMode.IsPeaceful` 等其他 Core 静态开关
- **`#if UNITY_EDITOR` 盲区**：dotnet 绿 ≠ Unity 编译过，波次收口必跑 EditMode 批处理

## 收口铁律

- **每个里程碑收口与每次内容合入后必须 `./tools/scripts/build-and-run.sh` 重新出包 `Builds/Windows/MyWordGame.exe` 验证**——双链绿 ≠ 实机可见
- 验收剧本头部必须标注 `Builds/Windows/MyWordGame.exe` mtime（包构建时间）
- 教训实证：av 赛道 08-18 凌晨提交的 BGM/环境音/标题画面视频在 08-14 旧 exe 里全部"不存在"——孩子在旧包上验收，以为没集成
- **代理不 commit**——主会话评审后按域顺序提交（避免 index.lock 竞争）
- **commit 整理子代理要明文指定文件归属每个 commit**，避免"按主域"自由发挥带来歧义
- **热点文件按任务卡独占**：`WorldBootstrap.cs`、`BlockInteraction.cs`、`PlayerContext.cs` 等集成点统一接线，并行期禁改
- **里程碑收口时三份文档同步更新**：`README.md`（进度）/ `CLAUDE.md`（约束）/ `AGENTS.md`（速查）共享同一事实层，改一处必查另两处——本次 review 实证：三份曾同时把未实施的 m12 写成"已完成"

## 编辑器菜单

`MyWorld/` 下有五个批处理入口，都可用 `-executeMethod` 无头跑：

| 菜单项 | 方法 | 作用 |
| --- | --- | --- |
| 重建预览场景 | `PreviewSceneBuilder.Build` | 重新生成 `Assets/Scenes/Preview.unity` |
| 无头验证玩家层 | `PlayHarness.Run` | 无头跑通「玩家运动 + 挖/放 + 流式加载」整条链路（含 6 个 NUnit 断言） |
| 接入 URP 管线 | `UrpSetup.Apply` | 建管线资产并挂到 Graphics 与质量档位 |
| Build 准备: 把 Preview.unity 加入 Scenes In Build | `BuildSetup.Apply` | 把场景注册到 `EditorBuildSettings.scenes`，build 前置 |
| Build Windows | `BuildPlayer.Build` | 出 `Builds/Windows/MyWordGame.exe`（StandaloneWindows64） |

**不要手改 ProjectSettings 的 Graphics/Quality YAML**——由 `UrpSetup.Apply` 生成并挂到 Graphics 与全部质量档位。
