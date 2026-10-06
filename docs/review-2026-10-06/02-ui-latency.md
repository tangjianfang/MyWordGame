# UI 线程（Unity 主线程）卡顿/延迟审查报告

- 审查对象：`MyWordGame.exe`（仓库 `C:\tjf\github\MyWordGame`，commit `f0eb3ea`，Unity 2022.3.62f3c1 + URP 14.0.11）
- 审查日期：2026-10-06；方法：静态代码审读（全部关键路径亲自读到行级）+ Core 层微基准实测（本机 = 用户实机同款 CPU 环境）+ 只读读取 Player.log / 注册表 / 存档目录
- 用户目标：**操作丝滑 0 延迟**。实测机器：RTX 2070 Super Max-Q / DX11 / 16 逻辑核
- 基准工程与输出留档可重跑：`docs/review-2026-10-06/scratch/bench/`（`dotnet run -c Release`，输出在 `scratch/bench-output.txt`）

---

## 一、摘要：Top 卡顿根因排名

| # | 根因 | 判定 | 量级（实测/推算） |
| --- | --- | --- | --- |
| 1 | **光照体积每 2 秒同步全量重建**（`ChunkLightSystem.Update` → `RebuildNow`，48×96×48 × 2 份，同步在主线程、无预算、主菜单也跑） | **Confirmed（代码+基准）** | **单次 83-89ms（.NET 9 实测；Unity Mono 通常更慢，1.5-3×）+ 每次新增 5.4MB 托管垃圾（≈2.7MB/s 持续喂 GC）**。唯一消费方是刷怪光照采样（约 0.4 次/秒）——成本/收益严重倒挂 |
| 2 | **走远卸载脏区块时的同步落盘**（`ChunkStreamer.UnloadDistant` → `RegionSaveCoordinator.SaveDirty`，读旧 region→合并→Deflate→整文件重写，全在主线程且在 8ms 预算之外） | **Confirmed（代码+基准）** | 2 脏列 41-46ms；**32 脏列（离家 8+ 区块的真实基地规模）679ms 单帧冻结** |
| 3 | **每挖/放一格当帧同步重建受牵连段网格**（`ChunkViewRegistry.MarkBlockChanged`，即时反馈的设计取舍，但无预算） | **Confirmed（代码+基准）** | 每 1-4 段 × 5.5ms/段（.NET 9 实测，Mono 更慢）→ **挖一格的当帧成本 5.5-22ms，常超 16.6ms 帧预算**——连续挖掘时规律性微顿 |
| 4 | **成就页/图鉴页每帧 new GUIStyle**（m12 新 UI，违反 m5「热路径不分配」纪律） | **Confirmed（代码）** | 成就页开着时 ~34 个 GUIStyle/帧 ≈ 1-2MB/s 垃圾（仅开页期间）；图鉴页 2 个/帧 + 插值串 |
| 5 | **常驻 IMGUI 无 Repaint 事件门 + 每帧字符串分配**（HotbarUI/HealthBarUI/FoodBarUI/ExperienceBarUi/QuestHudUi/HandController 的 OnGUI 在每个 IMGUI 事件轮全量重画并分配小字符串） | **Confirmed（代码）** | 每帧 ≥2 事件轮全量重画；小字符串 ~0.5-2KB/帧 常驻喂 GC |
| 6 | **MobView 每帧对每部位做 GetPropertyBlock/SetPropertyBlock**（24 mob × 平均 8 部位 ≈ 384 次/帧 native MPB 调用，且逐渲染器 MPB 破坏 SRP 合批） | Confirmed（代码）/ Likely（量级） | 估计 0.3-1ms CPU/帧 + 渲染侧合批损失 |
| 7 | 主菜单常驻成本：1080P 视频解码 + 世界流式生成照跑 + 根因 1 的 86ms/2s | Confirmed | 主菜单也每 2s 卡一帧 |
| 8 | 挖掘期间每帧 3 条射线（交互 DDA + Physics.Raycast 4m + 遮挡复核 DDA） | Confirmed（代码）/ 影响小 | ~0.1ms 级，非问题 |

**一句话结论**：本项目的 GC 纪律在 m5-m11 的老代码里守得很好（ChunkStreamer scratch 容器、ItemSlotDrawer 共享缓存、PlayerAudioSystem 全缓存均可验证），**手感卡顿的主犯不是 GC 而是三个无预算的主线程同步重活（根因 1/2/3）**；m12 新 UI（成就/图鉴两页 + 主菜单世界管理）是纪律回潮的局部破口（根因 4/5 的一部分）。

---

## 二、方法与证据来源

1. **代码审读**：下列发现的每一条都亲自读到对应行（file:line 见各条）；版本归属用 `git log -S` 核对引入提交。
2. **Core 微基准**（`scratch/bench/Program.cs`，.NET 9 Release，本机）：真实 `BlockRegistry`（79 贴图槽，从 StreamingAssets 加载）+ `WorldGenerator(seed=42)` 生成 7×7 区块后测：
   - 光照体积 `WorldLightVolumeBuilder.Build + BuildBlockLight`（48×96×48，与 `ChunkLightSystem.RebuildNow` 逐参一致）：**83-89ms/次，5393KB 托管分配/次**（3 次运行稳定）
   - 区块列生成：6.2-6.7ms/列，53KB/列
   - 单段贪心网格（`GreedyMesher.Build + SplitByTexture`，y=64..80 石层段，16016 顶点）：5.3-5.5ms/段，1.3KB/段
   - 脏区块同步落盘：2 脏列 → 41-46ms/次；32 脏列（8×4 列，每列 5 处改动）→ **656-679ms 单次**
   - ⚠️ 注意：.NET 9 RyuJIT 数字；游戏是 Unity Mono JIT，纯数值/字典代码通常慢 1.5-3×，**表中数字应视为主线程成本下界**
3. **只读用户数据**：`Player.log`（328 行，1894 帧会话、干净退出）、`Player-prev.log`、注册表 `HKCU\Software\DefaultCompany\MyWordGame`（reg query）、`worlds\42`（level.dat 36KB + regions 4 文件共 528KB，最大 r.-1.-1.mwr 190KB）、`worlds\92726749`（新档）。
4. **未运行**任何黑名单脚本；未启动游戏；未写用户数据/注册表。

---

## 三、发现清单（U-1…）

### U-1【Confirmed｜最高优先级】光照体积 2 秒同步重建：每 2s 一帧 83ms+，主菜单也在跑

- **位置**：`Assets/Scripts/Unity/World/ChunkLightSystem.cs:69-107`（`Update` 定时 2s → `RebuildNow` 同步调 `WorldLightVolumeBuilder.Build` + `BuildBlockLight`）；`Assets/Scripts/Core/Lighting/WorldLightVolumeBuilder.cs:75-141`（两份 48×96×48=221,184 格体积，扫 3 趟世界 + 3 次 BFS 洪泛）；`Assets/Scripts/Core/Lighting/ArrayLightVolume.cs:14-22`（每次 new 三个 221,184 长度数组 ≈ 660KB/份）。
- **触发条件**：组件挂上后无条件每 2s 一次（`RebuildIntervalSeconds=2`）；`timeScale=0` 真暂停时 `Time.deltaTime=0` 计时停走（暂停不受影响）；**主菜单遮罩期间照跑**（TitleScreenUi 不动 timeScale，世界已装配完毕）。
- **后果**：每 2 秒一帧被独占 ~83ms+（Mono 下可能 130-250ms）→ 60fps 下连续丢 5-15 帧，表现为"每隔两秒顿一下"，鼠标转动/挖掘蓄力进度都在这一帧里冻结；同时 5.4MB/次的分配以 2.7MB/s 持续喂 GC（`gcIncremental: 1` 已开增量 GC，尖峰被摊平但 CPU 持续消耗）。Player.log 的 1894 帧短会话无法体现逐帧尖峰——这正是"业务无日志"的可观测性缺口（见第七节）。
- **成本/收益倒挂**：全项目唯一消费方是 `MobManager.TickSpawn` 的刷怪光照采样（`MobManager.cs:454-462`，刷怪尝试 ~0.4 次/s）。为每秒 0.4 次的单点采样每 2 秒重建 44 万格。
- **修复草案**（工作量 M）：
  1. 改"按需 + 局部"：刷怪点确定后只对该点做单列小体积洪泛（16×h×16，成本 <1ms），或缓存体积 + 仅当采样点落在体积外/`MarkDirty` 时重建；
  2. 若保留全量重建：把体积构建拆片进 `ChunkStreamer` 同款毫秒预算循环（或 Task.Run 后台构建 + 双缓冲切换），并复用 `ArrayLightVolume` 数组（成员级 `Clear()`）消掉 5.4MB/次分配；
  3. 至少把间隔从 2s 放宽到 10-30s + `MarkDirty` 触发（现有 API 已有）。
- **版本归属**：`df7ecce`（m11 W1-4 集成点②A 引入）。注释自称"单次毫秒级"——实测差一个数量级，属当时估算未实测。

### U-2【Confirmed】走远卸载脏区块：主线程同步落盘，大基地场景 0.68 秒冻结

- **位置**：`Assets/Scripts/Unity/World/ChunkStreamer.cs:319-332`（`UnloadDistant` 里只要本批要卸载的列中有脏区块就调 `RegionSaveCoordinator.SaveDirty(_world, ...)` —— **同步版**重载，在 8ms 预算循环之外、每帧 Tick 的开头）；`Assets/Scripts/Core/Persistence/RegionSaveCoordinator.cs:18-30, 50-84, 128-166`（LoadOrCreate 读整个旧 region → 逐区块 StoreChunk（Deflate 压缩）→ 整文件写 .tmp → File.Replace 原子顶替）。
- **触发条件**：玩家距改过的区块 > `UnloadRadius`(8) 格即触发（走路离开基地/探索）。**注意与 30s 自动保存无关**——自动保存走 `SaveLoadService` 的快照版（后台 Task.Run，设计良好）；这条是卸载路径专属的主线程同步版。
- **后果**：实测 2 脏列 41-46ms；32 脏列 679ms。用户重档 `worlds/42` regions 共 528KB（最大单文件 190KB）——离开基地一趟的单帧冻结估计 100-600ms。探索新区域时周期性"卡死半秒"的直接嫌疑。
- **修复草案**（M）：
  1. 卸载路径复用 `SaveLoadService` 的后台写盘机制：主线程只 `FreezeDirtyChunks` 快照（微秒级），Task.Run 落盘，完成后再经 `ApplyPendingClears` 清脏——**代码库里已经有全套现成模式，搬过来即可**；
  2. 或者：卸载时不落盘，只把脏列移进"待保存"表，交给 30s 周期后台保存统一处理（代价：退出前必须确保冲刷——`OnApplicationQuit` 已是同步保存，天然覆盖）。
- **版本归属**：`e958716`（2026-08-15，m4 B1"卸载前保存"引入；m5 C3 只把 30s 自动保存移到后台，卸载路径漏改）。

### U-3【Confirmed】挖/放一格的当帧同步网格重建：5.5-22ms 无预算

- **位置**：`Assets/Scripts/Unity/Rendering/ChunkViewRegistry.cs:70-77`（`MarkBlockChanged` → 同步 `Rebuild`）；`Assets/Scripts/Core/Voxel/DirtySections.cs:20-56`（牵连 1-8 段）；调用方 `BlockInteraction.BreakAt/PlaceHeldItemOrPlaceholder`（`BlockInteraction.cs:1224-1225, 586-587` 等）。
- **后果**：实测单段重建 5.3-5.5ms（.NET 9；Mono 更慢）。地表/石层段顶点上万，挖一格当帧 5.5-22ms（贴段边界时更多）+ 当帧其余开销（流式预算 8ms + mob + IMGUI）——**挖掘破坏瞬间常超 16.6ms**。这是"即时反馈"的有意取舍（挖掉立刻可见），方向正确，缺的是量控。
- **修复草案**（M）：把编辑触发的重建也纳入毫秒预算（同帧最多重建 1 段，其余段下一帧补）；或先重建被挖段（当帧反馈）、邻居段延后 1-2 帧（视觉差异不可感知——邻居面只在接缝剔除上变化）。挖空气回填场景收益立竿见影。
- **备注**：`ChunkMeshBuilder`（`Assets/Scripts/Unity/Rendering/ChunkMeshBuilder.cs`）scratch 列表复用规范，仅 `ChunkSectionView.Rebuild`（`ChunkSectionView.cs:57`）每次 new 一个 `Material[]`（几十字节，可忽略）。

### U-4【Confirmed】成就页/图鉴页每帧 new GUIStyle（m12 纪律回潮）

- **位置**：
  - `Assets/Scripts/Unity/UI/AchievementUi.cs:101-107`——**循环体内** `new GUIStyle(white)`，16 个成就格 × 每轮 OnGUI = 16 个/轮，Layout+Repaint ≥2 轮 → **~34 个 GUIStyle/帧**；`:115-117` 底部再 new 1 个；`:72` 标题行插值串每帧 2 次。
  - `Assets/Scripts/Unity/UI/CodexUi.cs:52-53`——`title`/`nameStyle` 每次 `DrawTab` 各 new 一个（4/帧）；`:48` 插值串。
- **后果**：GUIStyle 是重对象（克隆样式 + 内部数组），开成就页 ≈ 1-2MB/s 垃圾（增量 GC 持续工作，偶发尖峰）；仅开页期间，关闭即停。违反 CLAUDE.md m5「流式/UI 热路径不许每帧 new」——同文件里 `ItemSlotDrawer.WhiteStyle()` 的缓存模式就是现成范本。
- **修复草案**（S）：两页样式提为 `static GUIStyle` 懒建缓存（照 `ItemSlotDrawer.CountStyle()` 模式）；成就格样式其实只有"解锁/未解锁"两态，建 2 个缓存实例切换即可。
- **版本归属**：`739bbbc`（m12 W1/W2，2026-08-21）。

### U-5【Confirmed】主菜单每帧做文件系统查询 + 全目录扫描

- **位置**：`Assets/Scripts/Unity/UI/TitleScreenUi.cs:304-313`（`DrawMainPanel` **每次 OnGUI** 调 `ResolveContinueSeed`）；`ResolveContinueSeed`（`:212-224`）→ `PlayerPrefs.GetString` + `Directory.Exists`，pref 失效时还走 `WorldCatalog.Latest` → `WorldCatalog.List`（`Assets/Scripts/Core/Persistence/WorldCatalog.cs:48-99`：`Directory.GetDirectories` + 逐目录 `File.Exists`/`GetLastWriteTimeUtc` + `new List<Entry>` + **闭包排序**）；`:309` 按钮文案 `$"继续上次（种子 {seed}）"` 每帧 2 次插值分配。
- **后果**：主菜单每帧 ≥2 次磁盘 stat（SSD 上 ~0.05-0.5ms/次 + 分配）；`WorldCatalog.LastSeed` 有效时只 stat 一次目录，失效时全扫。量级中等但纯属白做——数据只在进菜单/删世界时变。
- **修复草案**（S）：`Awake`/开菜单时解析一次缓存进字段；按钮文案缓存字符串（种子变了才重建）。世界列表页同理：`DrawWorldListPanel`（`:382-433`）每行每帧 `ToLocalTime().ToString("yyyy-MM-dd HH:mm")` + 插值（≤8 行 × 2 轮 = 32 次 DateTime 格式化/帧）——`WorldCatalog.List` 结果缓存 + 每行 label 缓存。
- **版本归属**：`c2a0261`（m12 P1，2026-08-21）。

### U-6【Confirmed 代码 / Likely 量级】MobView 每帧逐部位 MPB 写入

- **位置**：`Assets/Scripts/Unity/Combat/MobView.cs:220-233`（`LateUpdate` 对 `_assembled.PartRenderers` **无条件**逐部位 `SetInstanceColor` → `GetPropertyBlock + SetPropertyBlock`，`MobView.cs:168-176`）。模型平均 ~8 部位（mobs/models JSON 实测均值 7.8，最多 14），`MaxMobs=24` → ~190 渲染器 × 每帧 2 次 native 调用。
- **后果**：非分配（MPB 复用），但 (a) 每帧 ~380 次 native 调用的纯 CPU 浪费——99% 的帧颜色根本没变；(b) 逐渲染器 MPB 会让这些部位脱离 SRP Batcher 合批，渲染线程每帧多做 ~190 个 material setup。
- **修复草案**（S）：只在状态变化时写——受击闪红/苦力怕引信的边沿（进入/退出）各写一次；基色在拼装时写一次后不再动。`MobHitFeedback.ApplyTint/ClearTint` 已经有同款"边沿写 MPB"的正确范本。

### U-7【Confirmed】常驻 HUD OnGUI 无 Repaint 门 + 每帧小字符串

- **位置与明细**（全部为每 IMGUI 事件轮执行，Layout+Repaint ≥2 轮/帧）：
  - `HotbarUI.cs:123-198`——无 `Event.current.type` 门（对照 `ItemSlotDrawer.Draw:76-78`、`MobView.OnGUI:284`、`ChestUi` 都有）；`:181` `stack.Count.ToString()` 每格每轮（≤9×2=18 串/帧）
  - `HealthBarUI.cs:106-122`——`Refresh()` 每次 OnGUI `new HeartMath.Fill[totalHearts]`（2 次/帧）
  - `QuestHudUi.cs:122,135`——任务卡两行插值串每轮（常驻显示）
  - `ExperienceBarUi.cs:51`——`$"Lv {xp.Level}"` 每轮
  - `FoodBarUI.cs` / `HandController.cs:100-120`——无门重画（无分配）
  - `WeatherSystem.cs:193-224`——雨天 60 次 `GUI.DrawTexture`/轮（可接受，仅雨天）
- **后果**：单帧 0.5-2KB 垃圾 + 全量重画 2-6 轮（鼠标/键盘事件也会触发 OnGUI 轮）。单项都小，合计是常驻 GC 底噪（增量 GC 下不至可见尖峰，但持续吃 CPU）。
- **修复草案**（S，可一次清完）：常驻 HUD 统一加 `if (Event.current.type != EventType.Repaint) return;`（交互类控件所在的模态 UI 不能一刀切，只动纯展示 HUD）；`ToString`/插值结果按"值变化才重建"缓存（hotbar 角标数字、Lv、任务卡文本都是慢变数据）。
- **备注**：`BlockInteraction.OnGUI:1532-1539`、`PlayerController.OnGUI:631-633`、`FloatTextUi.OnGUI:175-187` 均有早退门且样式缓存——合规。

### U-8【Confirmed，minor】MobManager 每帧对每 mob `GetComponent<MobAudioSystem>()`

- **位置**：`Assets/Scripts/Unity/Combat/MobManager.cs:244-254`——tick 循环里每 mob 每帧 `go.GetComponent<MobAudioSystem>()`（24 次/帧 native 查询）。同文件 `_viewComponents` 字典（`:75`）就是正确范本。
- **修复草案**（S）：`SpawnMob` 时把 `MobAudioSystem` 一并缓存进字典（或塞进 `WalkDrive`）。

### U-9【Confirmed 代码 / Likely 量级】笔记本视频全程解码

- **位置**：`Assets/Scripts/Unity/Rendering/VideoScreenSystem.cs:63-72`——`Apply` 在启动时把 640×360 RT 接上材质后 `player.Play()` **永不停止**，与画面里有没有笔记本方块、玩家在不在旁边无关。Player.log 第 38-39 行证实整个会话 laptop-loop.mp4 在解码（unload 后又重新 prepare）。
- **后果**：常驻一路 H.264 解码 + 每帧 RT 上传（WMF 解码在 worker 线程，主线程成本小但非零）；5 个 mp4 的 "Unexpected timestamp / Color primaries" 告警也每次 prepare 打一遍（一次性，无害）。
- **修复草案**（S）：按需播放——材质所在方块进入视野/附近才 Play，远离即 Stop；或至少提供开关。收益中等（笔记本本机 ~几 % CPU）。
- **主菜单视频**（`TitleScreenUi.EnsurePlayer:175-193`）机制相同但生命周期正确（StartGame 即 Stop+Release，`:202-203`）；VideoPlayer 直渲染进 RT，**无代码侧每帧 Blit**——线索中"视频纹理每帧 Blit"不成立。

### U-10【Confirmed】流式加载本身：预算制在起作用，但初始/快速移动期帧预算被打满

- **位置**：`Assets/Scripts/Unity/World/ChunkStreamer.cs:107-139`（预算循环 8ms/帧、件数上限 16——纪律执行良好，scratch 容器齐备）。
- **实测**：列生成 6.2-6.7ms（预算内一帧一件）、段网格 5.3-5.5ms（一帧一件）。LoadRadius=6 → 13×13=169 列 × 每列若干段 → 初次进世界/快速跑图时**每帧流式工作满 8ms**，叠加正常帧成本后常在 12-16ms 徘徊（60fps 边缘），再撞上 U-1 的 2s 尖峰就是肉眼卡顿。
- **修复草案**（M）：生成/网格移 Job System 或后台线程（Core 已是纯 C#，`GreedyMesher.Build<TSource>` 泛型特化设计天然可迁移）；短期可把 `FrameBudgetMillis` 从 8 降到 4-5 换手感（代价是世界填得慢）——建议做成设置面板滑条。

### U-11【Confirmed】退出/换世界的同步长操作（可接受但需知情）

- 退出：`OnApplicationQuit → SaveNow(async:false)`（`SaveLoadService.cs:81, 97-180`）同步写 level.dat + 全部脏 region（基准 32 脏列 679ms + JSON）→ 点"保存并退出"后有 0.5s 停留窗掩盖（`HelpMenuUi.QuitDelaySeconds`），Alt+F4 则直接冻结约 0.7-1s。属设计取舍，量级应记入预算表。
- 换世界：`RequestWorldSwitch`（`WorldBootstrap.cs:54-60`）整场景重载 → 全套 `Awake` 链再来一遍（注册表 72 JSON + 物品 131 + 配方 75 + 模型 27 + 91 张方块贴图解码 + 5 视频探测/2 路播放 + `TryRestore`）——估 1-3s 黑屏。

### U-12【Confirmed，防御缺失】SettingsPanelUi 加载路径不钳制

- **位置**：`Assets/Scripts/Unity/UI/SettingsPanelUi.cs:68-92`（Save 侧有 Clamp）vs `:94-117`（`Awake → LoadXxx → ApplySettings` **直接应用**，无钳制）。`ApplySettings` 把 `CurrentSensitivity` 原样写进 `LookSensitivityMultiplier`、`CurrentFov` 写 `cam.fieldOfView`。
- **后果**：当前注册表值类型不匹配实际未生效（见第五节解读），但只要未来出现一个真被读回的越界 float（损坏档/手改注册表写成 REG_BINARY），灵敏度会变成任意大数——鼠标一帧转几万度。一行钳制的保险值得加。
- **修复草案**（S）：`LoadSensitivity/LoadFov/LoadVolume/LoadMusicVolume` 返回前各 `Mathf.Clamp` 到量程。

### U-13【Confirmed，minor】其它每帧/每事件轮小分配（清单）

- `HelpMenuUi.cs:347-348`：`GUI.Toolbar(..., new[]{...})` 每轮新数组（开菜单期间）
- `CraftingInventoryUi.cs:252` / `CraftingWorkbenchUi` 同款：输出格每轮重跑 `RecipeDatabase.FindMatch`（75 配方遍历，~0.05-0.1ms，且每轮都算两遍）
- `CraftingPocketUi.cs:77,125`：`new[]{_input}` 每轮
- `MobAudioSystem.cs:62,76,93-101`：idle/hurt 触发时 `ClipPath` 字符串插值 + `Resources.Load`（Unity 内部有缓存，非每帧，8-20s 一次——可忽略，列出备查）
- `EnchantingUi.cs:76,94`、`TradeUi.cs:86,108`、`ArmorSlotsUi.cs:160-162`、`CraftingFurnaceUi.cs:149`：开面板期间每轮插值串
- `PlayerController.Tick:515` 每帧 `GetComponent<PlayerContext>()` ×2（Tick + PickupNearbyDrops）——非分配，native 查询可缓存（minor）

---

## 四、被推翻的怀疑 / 已验证无问题

1. **"m12 挖掘裂纹换图每帧建纹理/GUIStyle"——不成立**。`DigCrackOverlay.Create` 一次性加载 5 张 PNG（`DigCrackOverlay.cs:49, 54-85`），`ShowAt` 只 set `mainTexture`/transform（无分配）；唯一可挑的是每帧重复 set 同值 `mainTexture`（可按档位变化才写，收益微小）。
2. **"放置幽灵框每帧建材质"——不成立**。`PlacementGhostUi.Create` 建两态材质各一份缓存（`PlacementGhostUi.cs:48-49`），`ShowAt` 只切换引用。
3. **"血条淡出/成就飘字违反 GC 纪律"——不成立**。`MobView.OnGUI` 有 Repaint 门 + alpha 早退（`MobView.cs:280-287`）；`FloatTextUi` 样式缓存 + 条目驱动（`FloatTextUi.cs:175-205`）。
4. **"30s 自动保存卡主线程"——不成立**。`SaveLoadService` 主线程只做快照收集（微秒-毫秒级小对象图），JSON+压缩+写盘全在 Task.Run（`SaveLoadService.cs:109-175`），重叠保护/版本守卫齐备。真正的主线程落盘在**卸载路径**（U-2）。
5. **"ChunkStreamer 没按毫秒预算分帧"——不成立**。Stopwatch 预算 8ms + 件数上限 + scratch 容器全在（`ChunkStreamer.cs:40-44, 126-139`）；例外是 U-2 的同步落盘发生在预算循环之外。
6. **"ItemSlotDrawer 纹理缓存没共享"——不成立**。静态字典跨五个 UI 共享（`ItemSlotDrawer.cs:25-51`），首次加载后零磁盘。
7. **"注册表 m6.* 是用户设了越界值（FOV -2 / 灵敏度 -3.7e19）"——不成立**。见第五节：类型不匹配，游戏实际用的是默认值。
8. **"视频纹理每帧 Blit"——不成立**（U-9 节备注）。
9. **"MobAI.Tick 36 种生物每帧遍历很贵"——基本不成立**。`MobAI.Tick` 纯结构数学（switch 分派 + 距离平方 + wander 哈希），`MaxMobs=24` 封顶；贵的是外围（U-6 的 MPB、U-8 的 GetComponent）。

---

## 五、对线索的逐条回应

1. **PlayerPrefs 编码疑点（重点解读）**：`reg query` 实测四键均为 **REG_DWORD**（m6.volume=0x40000000、m6.musicVolume=0x40000000、m6.fov=0xc0000000、m6.sensitivity=0xe0000000）。而 Unity Windows PlayerPrefs 的存储格式是：**int → REG_DWORD；float → REG_BINARY（8 字节 IEEE754 double）**（[Unity 官方文档](https://docs.unity3d.com/2018.4/Documentation/ScriptReference/PlayerPrefs.html) + [论坛实例：SetFloat(10.0) 存为 00 00 00 00 00 00 24 40](https://discussions.unity.com/t/how-does-playerprefs-setfloat-work/288625)）。代码侧 `SettingsPanelUi` 全部走 `SetFloat/GetFloat`（`SettingsPanelUi.cs:68-92`）——它写入的应是 REG_BINARY。因此这四个 REG_DWORD **不是当前游戏 SetFloat 路径写出的**（写入来源无法在只读条件下考证：手改注册表/外部工具/异常写入均可能）；GetFloat 对类型不符的值按"缺失"处理回落默认。**结论：游戏实际生效值 = 默认（灵敏度 1.0 / 音量 80 / FOV 70 / 音乐 80），"按 IEEE754 位解读出 2.0/-2.0/-3.7e19 并生效"的假设被 Gameplay 一致性反证推翻**（灵敏度若真是 -3.7e19，`PlayerController.UpdateLook:782-784` 一帧转天文数字度数，65 个会话不可能玩下来）。附带实证：同库 `WorldCatalog.LastSeed` 是 REG_BINARY ASCII "92726749"（字符串路径，能被 GetString 正常读回并匹配 worlds 目录）、session count "65"——与"~65 会话"吻合。**残余风险与动作**：U-12 的加载钳制补上后，任何来源的坏值都无法再毒化手感；用户拖一次滑条即会把键改写成正确的 REG_BINARY 类型（自愈）。
2. **Player.log 解读**：1894 帧样本 ≈ 31s@60fps 的短会话；干净退出无异常。`ALLOC_GFX_MAIN` 峰值 431.9MB 且帧分布 128-512MB 为主——这是**常驻显存水位**（169 列 × 每列若干段 × 每段上万顶点的顶点缓冲 + 1920×1080 视频 RT + URP 原生分辨率 RT），随流式加载增长，**不是每帧分配抖动**；`ALLOC_DEFAULT_MAIN` 135MB 同理（网格/纹理 native 分配）。真正的主线程托管分配抖动在日志里不可见（Unity 不打 GC 事件日志）——见第七节可观测性。5 个 mp4 的 timestamp/primaries 告警为一次性 prepare 告警，无逐帧成本。
3. **配置事实**：全屏原生分辨率（Screenmanager 1920×1080 + Use Native=1 + myword.fullscreen=1，`WorldBootstrap.ApplyAdaptiveFullscreen` 行为一致）；VideoIndex=3（minecraft-dawn）→ 主菜单播 1080P 视频 + U-1 光照重建 + 世界生成同时在跑（菜单帧成本高，见 U-1/U-9/U-10）；无 BabyMode 键（孩子档默认普通难度）；窗口记忆 1280×720 为窗口模式残留，与全屏运行无关。
4. **GC 纪律审查结论**：老代码（m5-m11）执行优秀（多处 scratch 容器/缓存可作范本）；**破口集中在 m12 新增**：U-4（成就/图鉴 GUIStyle）、U-5（主菜单每帧 IO+扫描）、U-7 的一部分（常驻 HUD 无门 + ToString 本可追溯更早但量小）。m12 P0 的挖掘/放置新 UI（裂纹/幽灵框）**未**违规。
5. **"主菜单挂着时底下世界照常生成的实测成本"**：证实在跑——`WorldBootstrap.Update → _streamer.Tick` 无菜单门（`WorldBootstrap.cs:469-475`），菜单期间每帧最多 8ms 流式 + 每 2s 一次 83ms 光照 + 1080P 解码。菜单不是"免费"的。

---

## 六、开背包时间线（模态 UI 打开成本样板）

| 阶段 | 工作 | 估计耗时 | 位置 |
| --- | --- | --- | --- |
| 按 E（Update 轮询） | `GetKeyDown` → `SetOpen(true)` + `UiCursorGate.Open()` | <0.05ms | `CraftingInventoryUi.cs:51-74` |
| 首次打开（冷） | 未命中图标缓存的物品逐个 `File.ReadAllBytes + LoadImage + Apply` | 每项 ~0.3-1ms；36 格全新最坏 **15-40ms 一次**（此后 0） | `ItemSlotDrawer.cs:35-51` |
| 打开期间每帧 | OnGUI ×事件轮：`FindMatch`（75 配方）×2 轮 + 41 格 `GUI.Box`+`Draw`（Draw 有 Repaint 门）+ tooltip + 角标 `ToString` ≤45 串/帧 | ~0.3-0.8ms/帧 + ~1KB/帧垃圾 | `CraftingInventoryUi.cs:188-305` |
| 关闭 | `SetOpen(false)` + 门关闭 | ~0 | 无资源释放（缓存保留——正确） |

对照：**开图鉴**首帧额外解码 27 张卡牌 PNG（93KB，~15-40ms 一次）+ 之后每帧 2 个 GUIStyle（U-4）；**开成就**同上 + 每帧 ~34 GUIStyle（U-4，最重）；**开工作台/熔炉/箱子**与背包同量级（箱子内容在内存，无 IO）。

---

## 七、延迟预算表

目标：稳态帧 <16.6ms（60fps），交互响应 <100ms。

| 操作 | 现状估计（Mono 下取上限） | 目标 | 差距 | 责任代码 |
| --- | --- | --- | --- | --- |
| 双击 exe → 主菜单可点 | ~3-5s（程序集 0.24s + Awake 全链 1-3s + 首帧） | <3s | 基本达标 | `WorldBootstrap.Awake`（同步 JSON/贴图/视频探测 ~400+ 文件） |
| 主菜单 → 可玩（StartGame） | <0.5s（世界已在跑，销毁遮罩即玩） | <1s | 达标 | `TitleScreenUi.StartGame` |
| 进新世界首屏（换世界重载） | 1-3s 重载 + 中心列就位后落地 | <2s | 基本达标 | U-11 |
| 稳态漫游帧 | 流式 8ms 预算 + 渲染 + 逻辑 ≈ 10-15ms | <16.6ms | 边缘 | U-10 |
| **每 2s 一次的光照帧** | **+83-250ms** | <16.6ms | **严重超标** | **U-1** |
| **走远卸载脏区块** | **+41-680ms（按基地规模）** | 0（应不可感知） | **严重超标** | **U-2** |
| 挖一格（破坏当帧） | +5.5-22ms 网格重建 | <16.6ms | 常超标 | U-3 |
| 放一格 | 同上（含幽灵框判定，无额外 IO） | 同上 | 同上 | U-3 |
| 挖掘蓄力期间每帧 | 射线×2-3 + 蓄力数学 + ghost/crack ≈ 0.1-0.3ms | <1ms | 达标 | `BlockInteraction.Update` |
| 开背包（热） | <1ms + 1 帧图标 | <16.6ms | 达标 | 第六节 |
| 开成就/图鉴（冷/热） | 冷 +15-40ms 一次；热每帧 +1-2ms（GUIStyle 垃圾） | 热 <1ms | 热>目标 | U-4 |
| 切 hotbar（数字键/滚轮） | ~0（索引写 + 下帧重画） | <50ms | 达标 | `HotbarUI.Update:268-283` |
| 30s 自动保存瞬间 | 主线程快照 <1ms（写盘后台） | <16.6ms | 达标 | `SaveLoadService` |
| 受击闪红 | MobAI.TakeHit 同步 + MPB 写 1 次 ≈ <0.1ms | <100ms | 达标 | m9 四件套 |
| 死亡→复活 | 按钮即 `RespawnAtSpawn`（传送+回满）<1ms；复活帧区块若已卸载需重生成（中心列预算内数帧） | <100ms | 达标 | `DeathScreenUi` |
| 退出保存 | 同步 0.3-1s（有 0.5s 停留窗掩盖） | <1s | 达标（知情） | U-11 |

---

## 八、卡顿可观测性方案（建议落地项）

Player.log 目前几乎无业务日志，逐帧尖峰完全不可见。建议（全部 S 工作量，均为"加了几行也零稳态成本"的埋点）：

1. **帧时间 HUD（调试开关）**：IMGUI 一行显示 `Time.unscaledDeltaTime` 的 1s 均值/最大值 + `Time.frameCount`；>33ms 的帧打一行 `Debug.Log`（含当帧标记位，见 3）。
2. **尖峰自动归因标记**：`ChunkLightSystem.RebuildNow`、`RegionSaveCoordinator.SaveDirty`（两处调用方）、`ChunkViewRegistry.MarkBlockChanged`、`SaveLoadService.CollectLevelData` 各包一个 `Stopwatch`，超阈值（如 8ms）`Debug.Log($"[perf] {name} {ms}ms")`——一局 Player.log 即可把"卡一下"对号入座。
3. **Profiler Marker**：上述四处 + `ChunkStreamer.DoOneWorkUnit` 加 `ProfilerMarker`（Unity 2022 自带，standalone 可用 Profiler.ecs 或命令行 `-profiler-enable` 采样），供日后深挖。
4. **GC 观测**：`GC.GetAllocatedBytesForCurrentThread` 每秒采样差值打进 HUD（增量 GC 开启下看分配速率比看停顿更有用；本项目目标可定 <100KB/s 稳态）。
5. **版本化**：埋点日志带 commit 短哈希（`Application.version` 或 build 时间戳），验收剧本对照包时间——呼应 CLAUDE.md"包构建时间标注"铁律。

---

## 九、修复顺序建议（按 收益/成本 排序）

| 序 | 修复 | 工作量 | 预期收益 |
| --- | --- | --- | --- |
| 1 | U-1 光照改按需/局部采样或预算化+复用数组 | M | 消灭最大周期性尖峰（每 2s 83ms+）与 2.7MB/s 垃圾——**单项解决"每隔两秒顿一下"** |
| 2 | U-2 卸载落盘改后台快照版（搬 SaveLoadService 现成模式） | S-M | 消灭探索离家时的 0.1-0.7s 冻结 |
| 3 | U-4 成就/图鉴 GUIStyle 缓存 | S | 开页期间 1-2MB/s 垃圾归零（半小时改完） |
| 4 | U-7 常驻 HUD Repaint 门 + 慢变字符串缓存 | S | 常驻 GC 底噪下降 ~一个量级 |
| 5 | U-3 编辑重建入预算（邻居段延帧） | M | 连续挖掘不再周期超帧 |
| 6 | U-5 主菜单解析缓存 | S | 菜单帧成本下降（顺带修世界列表页每帧 DateTime 格式化） |
| 7 | U-6 MobView MPB 边沿化 + U-8 GetComponent 缓存 | S | 每帧 ~400 次 native 调用清零 + SRP 合批恢复 |
| 8 | U-12 设置加载钳制 | S | 防御坏值毒化灵敏度/FOV |
| 9 | 第八节可观测性 | S | 让下一轮"卡顿报告"有数据可看 |
| 10 | U-10 网格/生成后台化（Job/Task） | L | 初载与跑图帧预算解放（最后做，先靠 1-7 腾出的余量） |

---

## 十、未验证项 / 局限

1. **Unity Mono 实机耗时未实测**（禁止启动游戏）：所有基准为 .NET 9 Release 数字，Mono 换算系数（文中按 1.5-3× 提示）未经实证；IMGUI 每事件轮的确切次数（Layout/Repaint/输入事件）依输入活动而变，未在实机采样。
2. **PlayerPrefs 判断的残余不确定性**：REG_DWORD 四键的"写入来源"无法在只读条件下考证（游戏自身 SetFloat 不会写 DWORD——由官方存储格式推断 + Gameplay 一致性反证支撑"未生效"结论，置信度 Likely-Confirmed；如需铁证可在下次构建加一行启动日志打印四值）。
3. 基准未覆盖：`RecipeDatabase.FindMatch` 绝对耗时（估计 <0.1ms）、`Newtonsoft` level.dat 解析耗时（36KB，估 <5ms）、URP 渲染线程成本（静态不可测）、`ChunkLightSystem` 在主菜单期的确切行为差异（代码推断必跑）。
4. `Player.log` 仅 1894 帧短会话，无长会话样本；未做 GPU view（无 RenderDoc/Profiler 附件）。
5. scratch/bench 的 region 写盘落在本仓库 scratch 目录（未触碰用户 `worlds/`）；基准用 `WorldGenerator(seed=42)` 默认 1 参构造（未注入植被表），列生成耗时与正式链路（4 参 DI）可能有 ±20% 差异，不影响数量级结论。
