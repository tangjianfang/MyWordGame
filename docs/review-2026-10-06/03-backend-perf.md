# 分报告 3 · 后台线程 / 存档与区块 IO / 流式加载 / 缓存

- 审查对象：MyWordGame @ main `f0eb3ea`（2026-08-21 基线，m12 全波次）
- 审查人角色：并发与后端性能
- 日期：2026-10-06
- 方法：全量亲读关键源码（见各条 file:line）+ 合成数据基准实测（`docs/review-2026-10-06/exp/Bench/`，两轮复跑，数据落在 %TEMP% 已清理，未触碰真实存档；真实 `worlds/42/level.dat` 只做只读解析）
- 机器语境：RTX 2070 Super Max-Q / 16 逻辑核；存档盘按 NVMe/SSD 假设（未验证型号）——所有 IO 数字含此假设
- **重要校准**：基准跑在 .NET 9 JIT 上；游戏实机是 **Mono** 后端（Player.log `Mono path[0]` 实证），Mono 通常比 .NET 9 慢 1.5-2×。**下文所有毫秒数对实机应视为乐观下界**。

---

## 摘要 · Top 风险与后端归因

**线程清单盘点结论（先说清楚架构现状）**：整个游戏只有**一个**自建后台任务——`SaveLoadService.WriteExecutor`（`Task.Run`，Assets/Scripts/Unity/Persistence/SaveLoadService.cs:51）。世界生成、网格构建、光照、全部实体 tick、区块保存（卸载路径）**全在主线程串行**。PhysX 16 workers 是引擎内部，与玩法无关。这个"单后台写盘线程 + 全主线程"的结构本身把后端风险集中在两处：① 主线程上的周期性/突发大活（卡顿），② 唯一的后台写盘线程与主线程卸载保存之间的双写者竞态（丢档窗口）。

**对整体卡顿的后端归因（按证据强度排序）**：

1. **ChunkLightSystem 每 2 秒主线程全量重建光照体积，实测 170-270ms/轮 + 每轮 1.3MB 垃圾**（B-1，Confirmed，实测两轮稳定）。这是唯一一个**恒定周期、无条件触发**的大活：站着不动也在烧。代码注释自评"单次毫秒级"（ChunkLightSystem.cs:84），实测差 2 个数量级。0.5Hz 规律性卡顿的第一嫌疑。
2. **区块流式的"单件超预算"**（B-2，Confirmed，实测）：一件工作 = 生成一列（实测 12-56ms）+ 存档 overlay 整文件读（最高 ~19ms），而预算只有 8ms 且**在件前检查**——跨区块移动时每帧稳定超载 20-40ms，持续数秒到数十秒（初始 13×13 列 × ~190ms/列的总量）。
3. **编辑同步重建网格**（B-3，Confirmed，实测）：挖/放一格触发 1-8 段重建，每段 4-10ms；贴段边界编辑单帧 30-80ms，完全不在预算体系内。
4. **卸载时主线程同步全量保存**（B-4，Confirmed）：走远甩开脏区块那一刻，把**全世界所有脏块**同步序列化+写盘（今日规模 ~50-600ms 单帧冻结；随规模线性涨），并且与后台自动保存构成同文件双写者。
5. 保存体系其余部分（level.dat 全量 JSON、region 读-改-写放大、退出同步等待）**今日规模下不是痛点**（重档 level.dat 写 ~30ms 后台完成），但都是随世界增长的线性负债，且各有一个明确的坏交织点（见 B-4/B-5/B-6/B-7）。

---

## 发现清单

置信度标记：Confirmed = 亲读代码+实测复现；Likely = 代码路径确认、后果推断未实测触发；Speculative = 推断。

### B-1 光照体积每 2s 主线程全量重建：~170-270ms/轮 + 1.3MB/轮垃圾 【Confirmed，今日卡顿第一嫌疑】

- 位置：`Assets/Scripts/Unity/World/ChunkLightSystem.cs:69-79`（Update 定时触发）、`ChunkLightSystem.cs:86-107`（RebuildNow 两份体积）、`Assets/Scripts/Core/Lighting/WorldLightVolumeBuilder.cs:75-117, 134-141`（三趟全扫 + BFS）、`Assets/Scripts/Core/Lighting/ArrayLightVolume.cs:14-22`（每次 new 三个 221,184 元素数组）。
- 触发：无条件每 2s（`RebuildIntervalSeconds=2`）；`MarkDirty()` 全仓库**零调用方**（grep 实证）——定时器是唯一模式，方块改动不会提前也不会跳过重建。
- 实测（exp G7，48×96×48 两份体积）：`Build` 119-191ms + `BuildBlockLight` 48-66ms ≈ **170-270ms/轮**；每轮新建 byte[221k]+bool[221k]+byte[221k] × 2 份 ≈ **1.3MB 托管垃圾 → 38MB/分钟**。Mono 实机预计更差。
- 后果：主线程每 2s 一个超长帧（掉到 <7fps 的单帧）+ 持续 GC 压力（与 B-9 的掉落物垃圾叠加）。玩家语汇里就是"每隔两秒顿一下"。
- 修复草案（可分四步各自合入）：① 去掉无条件定时，改为「跨区块或 MarkDirty（由 BlockInteraction/爆炸调用）才重建」；② 体积数组双缓冲复用，消掉每轮 1.3MB 分配；③ 合并扫趟（`FillOpaqueAndEmission` 单趟版已存在，Build 的不透明+发射两趟可并一趟）；④ 把构建移 `Task.Run`/Job（纯 Core 代码零 UnityEngine，天然可后台），主线程只原子换引用。
- 版本归属：m11 W1-4 引入（代码注释自述）。

### B-2 流式"一件工作"远超 8ms 预算：生成 12-56ms + overlay 读最高 ~19ms 【Confirmed】

- 位置：`Assets/Scripts/Unity/World/ChunkStreamer.cs:126-138`（预算在**件前**检查，单件可任意超时）、`:156-169`（GenerateOne = 生成 + `TryLoadChunk` overlay 同一件）、`:177-233`（MeshOneUnit 每件 = 一个 section）。
- 实测：`WorldGenerator.Generate` 单列 12-56ms（两轮 avg 20/35ms，正式链路 4 参构造）；overlay 命中时每根再加 1.5-18.5ms（随 region 大小，见 B-8）；单 section 网格 4-10ms。
- 后果：预算制名存实亡——移动跨区块时每帧至少一件 ≈ 20-40ms，帧预算 8ms 被 2.5-5 倍击穿，持续整段加载期；初始进场 169 列 × (生成+24 段网格) ≈ 30s+ 的持续半负载。
- 修复草案：① 生成移后台（见"调度重构方案"步骤 2，G8 实测并行安全且近线性）；② `TryLoadChunk` 改用常驻 region 缓存（消掉重复整文件解析）；③ 预算检查改"件后+件前双查"并在超时件后提前收工（当前已等价，真正要解决的是单件本身太大）。

### B-3 挖/放方块同步重建 1-8 段网格，不在任何预算体系内 【Confirmed】

- 位置：`Assets/Scripts/Unity/Player/BlockInteraction.cs:1191-1231`（BreakAt → `MarkBlockChanged` 同步调用）、`Assets/Scripts/Unity/Rendering/ChunkViewRegistry.cs:70-77`（立即逐段 Rebuild）、`Assets/Scripts/Core/Voxel/DirtySections.cs:16-55`（边界方块牵连最多 8 段：水平 3 根 × 竖直 2 段）。
- 实测：单段地表网格 4-10ms（exp G2，68 quads 参考值）。贴边编辑 8 段 = 单帧 30-80ms（Mono 下更高）。
- 后果：连续挖掘时的不规律尖峰，与挖掘节奏同相；床/门双格放置、爆炸（多块连改）一次更多。
- 修复草案：把 `MarkBlockChanged` 改为登记脏 section 进 `ChunkStreamer` 的 mesh 队列（复用既有预算分帧），玩家眼前 1 段立即建、邻接段延后 1-2 帧（贪心网格接缝剔除依赖邻居数据而非网格时序，延后重建不产生错误面，只晚一帧可见）。

### B-4 卸载路径主线程同步全量保存 + 与后台自动保存的双写者竞态 【Confirmed(路径) / Likely(丢档窗口)】

- 位置：`Assets/Scripts/Unity/World/ChunkStreamer.cs:319-332`（任一卸载块是脏的 → `RegionSaveCoordinator.SaveDirty(_world, …)` **主线程同步、且保存全世界全部脏块**，不只卸载的那些）；`Assets/Scripts/Core/Persistence/RegionSaveCoordinator.cs:133-151`（AtomicWrite 用 `path + ".tmp"`）；`Assets/Scripts/Unity/Persistence/SaveLoadService.cs:51`（后台 Task.Run 同样走 AtomicWrite 同一 `.tmp` 名）。
- 后果一（卡顿，Confirmed by 实测量级）：今日重档 ~25-50 脏块 → 单帧 0.5-1s 冻结（每块序列化 18-28ms + IO）；脏块数随活动量线性涨。
- 后果二（丢档窗口，Likely——交错窗口窄但真实）：后台自动保存与主线程卸载保存是**两个独立写者**，无互斥。坏交错：后台在 T0 冻结快照并 Load 旧文件 → 玩家在 (T0, T0+t) 又改了块 X 并走远触发卸载保存 → 卸载保存把**当前全部脏块**（含 X）落盘并**立即清脏** → 后台慢一轮（大 region 序列化秒级）最后 File.Replace 用「不含 X 的旧合并视图」覆盖 → **X 的改动从磁盘消失且脏标记已清**，静默丢档。另有良性交错：两写者同时开同一 `.tmp` → 后到者 IOException → 被 `catch (e is IOException …)` 静默吞掉、该批保留下轮重试（RegionSaveCoordinator.cs:78-81）。
- 修复草案：**单写者规约**——`ChunkStreamer.UnloadDistant` 不再直接 SaveDirty，改为向 SaveLoadService 请求「本轮立即同步保存」（等在途后台写完成后内联执行，与退出路径同语义）；或最简：卸载保存前 `while (_writeInProgress) Sleep(1)` 复用同一门槛（仍留主线程成本，见 B-6/B-8 缓解）。

### B-5 后台序列化的 torn read：区块列本体无锁并发读写 【Confirmed(race) / Likely(后果)】

- 位置：`Assets/Scripts/Unity/Persistence/SaveLoadService.cs:212-235`（注释明言「区块列本体允许后台写盘期间读到主线程的新改动（torn read）」）；`Assets/Scripts/Core/Persistence/ChunkSerializer.cs:46-55`（逐 voxel `section.Get`）；`Assets/Scripts/Core/Voxel/ChunkSection.cs:115-154`（`GetOrAddPaletteIndex`/`Grow`：`Array.Resize(_palette)` + 换 `_data` 数组 + 改 `_bitsPerEntry`，全无同步）。
- 触发：后台写盘窗口内（大时秒级）主线程 `SetBlock` 恰好引起该 section 调色板升位（新方块种类首次出现）。
- 后果两型：① 读到新 `_bitsPerEntry` 配旧（更短）`_data` → `IndexOutOfRangeException`——它**不是** IOException/InvalidDataException，逃出 `SaveDirtyCore` 的 per-region 过滤器（RegionSaveCoordinator.cs:78），中止**整轮**剩余 region 的保存（Write 的 catch-all 兜底、脏块保留下轮，自愈但有日志噪音）；② 读到旧 bits 配新数组/旧调色板 → 写出**结构合法但方块 ID 映射错**的区块（如石头变矿石），若此后到下轮成功保存之间进程终止，错误数据固化进档。
- 既有缓解：版本守卫（`ClearDirtyIfUnchanged`，World.cs:56-59）让保存窗口内被改的块保持脏、下轮重写——**正常流程下自愈**；风险只在「坏数据写入后、下轮覆盖前」退出/崩溃。
- 修复草案（最小改动、顺带消后台 CPU）：`FreezeDirtyChunks` 在主线程直接把每个脏列 `ChunkSerializer.Serialize()` 成 byte[]（序列化全进快照阶段），后台线程只做 region 合并 + 写盘（纯 IO）。序列化成本回到主线程但每 30s 一次、可分帧摊（今日 ~几十 ms）。

### B-6 region 文件读-改-写放大：每轮保存重读重写整个文件 【Confirmed，前向规模风险】

- 位置：`Assets/Scripts/Core/Persistence/RegionSaveCoordinator.cs:153-164`（`LoadOrCreate` 每轮全文件读入）+ `:50-84`（每轮重写全部 payload）。
- 实测（exp G4）：
  | region 内已存块数 | 增量保存 1 块 | 首轮全量写 |
  |---|---|---|
  | 8（63KB 文件） | 45-71ms | 144ms |
  | 64（504KB） | 44-64ms | 1.31s |
  | 256（1.99MB） | 60-78ms | 5.32s |
  | 1024（7.77MB） | **118-138ms** | **22.2s** |
- 后果：后台自动保存线程被单轮耗时拖长 → 重叠保护持续跳过新轮（SaveLoadService.cs:101）；大 region 首轮 22s 期间玩家点「保存并退出」→ 主线程 `Thread.Sleep(1)` 自旋等它（见 B-7）。**今日实档最大 region 190KB ≈ 25 块，增量 ~45ms，无感**；随探索范围增长线性恶化。
- 修复草案：进程内 region 句柄缓存（LRU，见"调度重构方案"步骤 3）+ payload 按版本复用（`RegionFile._payloads` 已存压缩字节，未再改的块直接复用上轮 payload，省掉重复 Deflate——这是 G3 里 18-44ms/列的大头）。

### B-7 退出路径主线程自旋等待 + 同步全量序列化 【Confirmed】

- 位置：`Assets/Scripts/Unity/Persistence/SaveLoadService.cs:102-107`（`while (_writeInProgress) Thread.Sleep(1)` 主线程忙等）、`:177`（同步内联写全部脏块）；`Assets/Scripts/Unity/UI/HelpMenuUi.cs:154-186`（「保存并退出」按钮 → `SaveNow(async:false)`，期间 UI 只有静态文本）。
- 后果：退出时主线程冻结 = 在途后台写剩余时间（大 region 时秒级-数十秒）+ 全部脏块同步序列化（今日 ~0.5s 内；50 脏块 ≈ 1-1.5s，Mono 下×1.5-2）。
- 修复草案：等待加上限（如 3s，超时提示「后台保存中，请稍候再退」重试）；配合 B-6 的 payload 复用后同步路径只剩 IO。

### B-8 TryLoadChunk 每根区块整文件解析（无 region 缓存） 【Confirmed】

- 位置：`Assets/Scripts/Core/Persistence/RegionSaveCoordinator.cs:90-110`（每次调用 File.OpenRead + `RegionFile.Load` 解析**整个** region 的全部 payload）；调用点 `Assets/Scripts/Unity/World/ChunkStreamer.cs:164-167`（每根生成列一次）。
- 实测（exp G5，连读 8 根）：region 16 块 12-27ms（~1.5ms/根）；256 块 76-102ms（~9.5-13ms/根）；1024 块 **148-179ms（~18.5-22ms/根）**。
- 后果：走进重度改造区时，每根列的加载成本翻倍（生成 ~20ms + overlay 最高 ~20ms，全在一"件"里，见 B-2）；payload 字节数组反复分配产生 GC 压力。
- 修复草案：与 B-6 同一个 region LRU 缓存即可同时解决（命中时只剩 `ChunkSerializer.Deserialize` 单块 8-13ms）。

### B-9 ItemDrops 无上限、无过期：真实重档 76% 是掉落物 【Confirmed】

- 位置：`Assets/Scripts/Core/Items/ItemDropEntity.cs`（无寿命/过期字段，仅拾取状态机）；`Assets/Scripts/Unity/Items/ItemDropViewRegistry.cs:42-74`（每帧全量 diff + 每掉落物一个自转 GameObject）；`Assets/Scripts/Unity/Player/PlayerController.cs:353-397`（每帧全列表遍历）；存档侧 `SaveLoadService.CollectLevelData` → `SnapshotMappers.SnapshotDrops` 每 30s 全量进 level.dat。
- 实证（只读解析真实 `worlds/42/level.dat`）：总 34,176B 中 **Drops 26,099B（76%）= 237 个未拾取掉落物**；其余 Player 3.1KB / Stats 47B / 箱床田全空。
- 后果：孩子挖了不捡 → 掉落物永久累积：每帧 O(n) 拾取遍历 + n 个 GameObject（各有 LateUpdate 自转）+ level.dat 与 30s 快照成本线性涨。注释自评「量级 ≤60」（ItemDropViewRegistry.cs:13）是假设不是机制。
- 修复草案：① 5 分钟过期（对齐 MC）；② 上限 200，超限合并同 itemId 或最旧消失；③ 离玩家 >40 格的掉落物不入档（重进世界时本来也看不到它们在哪生成）。

### B-10 level.dat 全量 JSON（Indented）随世界增长 【Confirmed 量级，今日非痛点】

- 位置：`Assets/Scripts/Core/Persistence/LevelDataCodec.cs:20`（`Formatting.Indented`，体积 +30-50%、序列化更慢）；主线程启动读 `SaveLoadService.TryRestore`（Awake 内）。
- 实测（exp G6 合成档）：空档写 6-22ms / 读 0.6ms；≈重档现状（237 掉落）+50 箱 → 写 29-38ms / 读 21-34ms；放大 4×（200 箱/500 掉落/1000 统计/500 田）→ 写 34-87ms / 读 17-110ms、文件 886KB。
- 后果：写盘在后台线程（除退出路径），今日无感；启动读在主线程，放大档下逼近百 ms 级。B-9 修复后增长主源消失。
- 修复草案：去掉 Indented（或仅 debug 开关）；长期可把 Drops/Chests 移出 level.dat 分文件。

### B-11 换世界（场景重载）不触发保存；SaveLoadService 无 OnDestroy 【Likely，影响面今日很小】

- 位置：`Assets/Scripts/Unity/UI/TitleScreenUi.cs:228-243`（`EnterWorld` → `RequestWorldSwitch` 直接 `SceneManager.LoadScene`，无 SaveNow）；`Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs:54-60`；`SaveLoadService` 没有 OnDestroy/OnDisable 保存钩子。
- 缓解事实（亲读确认）：主菜单只在场景加载时可见（`IsVisible` 仅 Awake 置 true，grep 全仓库无运行中重开路径），菜单期玩家输入锁定（InputLocked）无直接编辑；在途后台写写的是旧世界目录、同 seed 不重载（EnterWorld 同种子走 StartGame）→ 常规流程丢档面 ≈ 菜单期间世界侧系统（怪物/爆炸/作物计时）产生的改动 ≤30s。
- 后果：一旦未来加"游戏内返回主菜单"（孩子迟早会要），这里立刻变成实打实的丢档口；后台写与新区块加载**不争抢同一文件**（不同 seed 目录），已知线索①的"并发争抢"部分不成立。
- 修复草案：`RequestWorldSwitch` 前先 `SaveNow(async:false)`；SaveLoadService 加 OnDestroy 兜底（区分 quitting 语义避免与 OnApplicationQuit 双存）。

### B-12 MobManager.Update 每 mob 每帧 GetComponent 【Confirmed，Minor】

- 位置：`Assets/Scripts/Unity/Combat/MobManager.cs:245-254`（`go.GetComponent<MobAudioSystem>()` 在 24 只 mob 的循环内每帧调用）。
- 后果：Component 查找是引擎侧 native 调用，24 次/帧 ≈ 小但纯浪费；与 `_viewComponents` 字典已缓存的模式不一致。
- 修复草案：spawn 时缓存进字典（照 `_walkDrives` 挂法）。

### B-13 主菜单 OnGUI 每帧目录扫描 【Confirmed，Minor】

- 位置：`Assets/Scripts/Unity/UI/TitleScreenUi.cs:304-306`（DrawMainPanel 每次调用 `ResolveContinueSeed`）→ `:212-224`（PlayerPrefs 读 + `WorldCatalog.Latest` → `Directory.GetDirectories` + 每目录 mtime 系统调用）。OnGUI 每帧至少 Layout+Repaint 两次。
- 后果：菜单期每帧 ≥2 次目录 IO；2 个世界时 <1ms，纪律问题而非卡顿。
- 修复草案：进 DrawMainPanel 时缓存一次（世界列表模式本来就在打开时才 `WorldCatalog.List`，:326-328 是对的）。

### B-14 RedstoneSystem 无条件每 0.1s 扫 17³ 区域重建电路 【Confirmed 存在，量级 Minor（估算）】

- 位置：`Assets/Scripts/Unity/World/RedstoneSystem.cs:73-84`（每 TickInterval=0.1s）→ `:138-165`（`SyncSourcesAndWiresFromWorld` 扫玩家周围 17×17×17=4,913 格，无论场景里有没有红石元件）。
- 后果：~5k 次 `World.GetBlock`（按 G7 折算 ≈ 0.5ms）/0.1s —— 恒定底噪，今日可忽略；本条未单独 bench，量级为推算（如实标注）。
- 修复草案：场景无 lever/redstone_dust 时降频（比如 1s 一次）或由放置/破坏事件驱动登记，不做轮询扫描。

### 正面确认（已验证无问题 / 设计得当）

- **线程纪律**：`Write()` 后台闭包内零 UnityEngine API（亲读全文）；错误日志由主线程 `ApplyPendingClears` 补发；结果发布用 volatile 写后清标志（release/acquire），`_pendingClear/_pendingErrors` 的跨线程交接正确（SaveLoadService.cs:145-155, 197-210）。
- **任务异常不吞**：Write 内部 catch-all 收集；执行器本身抛也有复位分支（:159-175），不会出现"标志卡 true 永久跳过自动保存"。
- **原子写**：`.tmp` + `File.Replace`（Win32 ReplaceFile）不破坏旧档；崩溃残留 `.tmp` 会被下次 `File.Create` 覆盖（RegionSaveCoordinator.cs:133-151、LevelDataCodec.cs:16-33）。
- **退出不静默丢档**：帮助菜单保存失败不退出、可重试（HelpMenuUi.cs:154-186）。
- **生成确定性 + 并行安全**：同一 `WorldGenerator` 实例 8 线程并发 256 列，产物与单线程**逐字节一致**（exp G8 两轮复现），加速 4.3-8.8×——Job 化的潜力是实证的，不是纸面的。
- **卸载不丢进度**：脏块卸载前必落盘（ChunkStreamer.cs:314-332）——代价是 B-4 的主线程冻结，方向与已知线索③的猜测相反。
- **ItemSlotDrawer 纹理缓存**：static Dictionary 按贴图名，上限 = 物品贴图数，无泄漏路径（UI/ItemSlotDrawer.cs:25-49）。
- **ParticlePool**：64 槽预分配 + ring 复用声明与结构一致（未逐行读完 602 行，如实标注）；静态贴图缓存有界。
- **WorldCatalog**：仅打开世界列表/进入时扫目录（除 B-13 的每帧 ResolveContinueSeed 外），2 世界规模无成本问题。

---

## 量化实验结果

实验代码：`docs/review-2026-10-06/exp/Bench/`（Program.cs + MyWorld.Bench.csproj，ProjectReference 只读链接 Core 源码编译）；原始输出两轮：`exp/bench-run1.txt`、`exp/bench-run2.txt`。合成数据在 %TEMP%（已清理）。正式链路同款装配（4 参生成器 + 真实 blocks/vegetation/biomes JSON，72 个方块定义）。

| # | 实验 | 结果（min~max，.NET 9） | 对实机（Mono）含义 |
|---|---|---|---|
| G1 | 单列 Generate | 12.2-56.2ms（两轮 avg 20/35） | 一"件"预算单位，8ms 预算被 1.5-7× 击穿 |
| G2 | 单 section 网格（地表段，68 quads） | 4.4-10.4ms | 编辑重建单元 ×8 段（B-3） |
| G3 | 列序列化（Deflate Optimal） | 地表列（~11 段）18.1-28.2ms → 8.9KB；满 24 段 40.8-49.7ms；反序列化 8.1-12.8ms/列 | 保存/读档的每列单价（B-5/B-6/B-8） |
| G4 | region 读-改-写 | 增量 1 块：45ms(8块)/44-64(64)/60-78(256)/**118-138ms(1024)**；全量首写 144ms→**22.2s** | 写放大曲线（B-6） |
| G5 | TryLoadChunk 连读 8 根 | 16 块 region 12-27ms；256 块 76-102ms；1024 块 **148-179ms** | 每根整文件解析（B-8） |
| G6 | level.dat 往返 | 空档写 6-22ms/读 0.6ms；重档现状级写 29-38ms/读 21-34ms；4× 放大写 34-87ms/读 17-110ms | 今日无感、线性负债（B-10） |
| G7 | 光照体积两份 | Build 119-202ms + BlockLight 48-66ms = **170-270ms/轮**；1.3MB/轮分配 | 每 2s 一次（B-1，头号嫌疑） |
| G8 | 生成并行化 | 1/2/4/8 线程 256 列：5012/2233/1169/572ms（run2：4339/2271/1790/999），8 线程产物与单线程**逐字节一致** | Job 化近线性收益，安全实证 |

真实档只读解析：`worlds/42/level.dat` 34,176B = Drops 26,099B（76%，237 个）+ Player 3,123B + 其它 <300B；regions 4 文件 190/104/187/47KB。

---

## 调度重构方案（每步可单独合入）

**步骤 1 · 救光照（消 0.5Hz 卡顿，收益最大、改动最小）**
`ChunkLightSystem`：删无条件 2s 定时 → 仅「跨区块 / MarkDirty（BlockInteraction.BreakAt、放置、Explosion.AfterDetonate 三处补调用）」触发；体积数组双缓冲复用；两趟不透明/发射扫并一趟；再进一步把 Build 移 `Task.Run`（纯 Core 零 UnityEngine，主线程只换引用）。预期：170-270ms/2s → 0（静止时），GC 38MB/min → ~0。

**步骤 2 · 生成后台化 + 加载 lane**
Generate（含 overlay 读）移后台线程池：G8 已证确定性 + 线程安全；主线程每帧从"生成完成队列"取 ≤预算根数做 `AddChunk` + 入 mesh 队列。mesh 侧维持每件一段的主线程节奏（短期），中期 GreedyMesher 上 Unity Job/Burst（Build<TSource> 是纯函数式泛型，天然 Job 友好）。编辑重建（B-3）改走同一 mesh 队列获得预算保护。

**步骤 3 · 保存分档（消写放大 + 单写者）**
① region LRU 缓存（容量 ~8 文件）：卸载/保存/加载共用，消 B-6 读-改-写与 B-8 重复解析；② payload 按编辑版本复用：`World._editVersions` 已有版本号，未变块直接复用上轮压缩字节，把"每轮全量 Deflate"变"只压新改块"；③ `ChunkStreamer.UnloadDistant` 的保存改经 SaveLoadService 单写者门槛（或最简：保存前等 `_writeInProgress=false`），消 B-4 双写者；④ `FreezeDirtyChunks` 顺带在主线程做序列化（B-5 torn read 根治 + 后台线程纯 IO 化）。

**步骤 4 · 观测补底**
业务零日志是观测缺口（Player.log 干净 ≠ 无问题——本次全部发现都无日志痕迹）。加一行轻量帧时长直方图（每 10s Debug.Log 汇总 p50/p95/max + 当帧大头系统标记），下次体检不用靠猜。

---

## 修复顺序建议

- **P0**：B-1（光照重建——恒定周期大卡顿）；B-4（双写者丢档窗口 + 卸载主线程全量保存）；B-3（编辑同步重建入预算）。
- **P1**：B-9（掉落上限+过期，顺带砍掉 level.dat 76%）；B-2（生成后台化）；B-6+B-8（region 缓存）；B-5（序列化进冻结阶段）；B-7（退出等待上限）。
- **P2**：B-10、B-11、B-12、B-13、B-14；GreedyMesher Job 化；步骤 4 观测。

---

## 跨报告线索（需主审 / 其它角色校对）

- B-1 的实机复现确认需要带 profiler 的实机跑（本报告只有 .NET 侧实测 + 代码路径）——建议主审安排一次 `--ui-shot` 之外的实际游玩采样，或先合步骤 1 再对比体感。
- B-3/B-2 的帧预算语义改动会碰 `ChunkStreamerTests`/`ChunkBudgetTests` 的守卫断言——需要测试角色一起改。
- B-9 掉落物上限涉及"孩子挖了不捡"的玩法预期（他可能故意铺一地方块欣赏）——产品/UX 角色定夺过期时长与上限值。
- ItemDropView 每掉落一个 GameObject 的渲染成本归渲染角色；本次只记 CPU/GC 侧。
- Player.log 无任何业务日志（观测缺口）建议主审统一立项。

---

## 被推翻的假设 / 已验证无问题 / 未验证项

**被推翻 / 修正的假设**：
1. 「区块卸载即丢进度」（已知线索③）——**错**：卸载前同步落盘（ChunkStreamer.cs:314-332）。真实问题反而是这次保存的主线程成本与双写者竞态（B-4）。
2. 「换世界时旧后台保存与新区块加载并发争抢文件」（已知线索①）——**基本不成立**：不同 seed 写不同目录、同 seed 不重载场景；争抢实际发生在**同一世界内**的"后台自动保存 vs 主线程卸载保存"（B-4）。真正的换世界缺口是"不保存就重载"（B-11）。
3. 「level.dat 36KB 全量序列化是保存变慢主因」（已知线索③后半）——**今日不成立**：写 ~30ms 且在后台；36KB 里 76% 是掉落物（B-9），增长主源是 Drops 而非 ChestContents/Stats。
4. 「spawn 白名单 36 kind 线性膨胀致密刷怪卡顿」（已知线索②）——**不成立**：MaxMobs=24 封顶、TickSpawn 每秒 ≤0.4 次尝试、PickKind 候选数组 ≤16 项（MobManager.cs:28-66, 268-275 亲读）；量级可忽略。
5. ChunkLightSystem 注释自评"单次毫秒级"——**实测 170-270ms**，差两个数量级（这正是"注释承诺 ≠ 实测"的样本）。

**未验证 / 如实声明**：
- 所有毫秒数来自 .NET 9 控制台基准，未在 Unity Mono 实机复测（方向与相对关系可靠，绝对值预计还要 ×1.5-2）。
- B-4 丢档交错、B-5 torn read 未实际触发复现（需要并发时序窗口，只做了代码路径证明）；B-5 的②型（静默错数据）尤其难无伤复现。
- ParticlePool 602 行未逐行读完（结构 + 声明一致）；VillagerManager / FarmingHost / WeatherSystem / ProjectileManager / BGM/环境音 tick 未逐行计时（低风险未测）；AudioSource/VideoPlayer 资源生命周期归音频/渲染角色。
- 磁盘为 NVMe/SSD 是环境假设（未查型号）；机械盘上 B-6/B-8 数字会显著恶化。
- 注册表侧未检查（后端域无关）。
