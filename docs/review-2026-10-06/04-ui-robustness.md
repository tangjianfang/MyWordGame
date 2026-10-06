# 04 · Unity 层崩溃风险与运行稳定性审查（UI 稳定性角色）

- 审查对象：MyWordGame.exe（m12 收口包，MyWordGame_Data 文件 2026-08-21 02:03），commit `f0eb3ea`（main，工作树 clean）
- 审查日期：2026-10-06；方法：全量代码审计（Unity 层 60+ 文件逐个读 + Core 持久化层关键路径 + 近 8 提交 git show 复核 + Player.log 只读取证 + dotnet 测试复跑）
- 结论基线：**未发现可直接导致进程崩溃（native crash / 未捕获 NullReferenceException）的缺陷**；发现 **2 个 P1 级存档数据丢失竞态**、1 个 P2 级物品蒸发路径，以及若干 P3 级状态泄漏 / UX / 防御缺口。目标"0 崩溃"在崩溃维度当前成立，但"存档不丢、状态不串"维度存在实机可触发的缺陷。
- 复跑验证：`dotnet test tools/dotnet/MyWorld.Tools.sln` → **1039/1039 通过**（2026-10-06 本机实测，与 CLAUDE.md 基线一致）。

---

## 一、结论：线索假设的证实 / 证伪

| # | 线索（任务给定） | 裁决 | 证据 |
| --- | --- | --- | --- |
| H1 | "历史 65 会话零异常" | **不可证** | Unity 只保留两份日志。`C:\Users\tjf\AppData\LocalLow\DefaultCompany\MyWordGame\Player.log` / `Player-prev.log`（2026-08-21 03:06 / 03:09 两会话）grep "Exception" 计数 0，尾部均有完整 Memory Statistics（干净退出）。更早会话日志已被覆盖，无崩溃转储机制，故历史零异常**既不能证实也不能证伪** |
| H2 | （自查假设）`PlayerContext.Awake` 的 `Health.Current` 若 Health 为 class 且未初始化会 NRE | **证伪** | `Core/Entities/Health.cs` —— `public struct Health`；`Experience` 同为 struct。Awake 中 `Health.Current <= 0` 读默认值 0，无 NRE |
| H3 | 静态事件订阅（QuestEventBus 等）场景重载后悬垂 | **证伪** | `QuestEventBus.OnDisable/OnDestroy` 均摘 `CombatEvents.OnEntityDied`（QuestEventBus.cs:240-259）；`MobManager.OnDisable`（MobManager.cs:113-116）、`CodexHost.OnDisable`、`ProjectileManager.OnDisable` 同款。`AchievementSystem.Unlocked` 是实例事件且 `AchievementUi.Bind` 先解旧再订（AchievementUi.cs:21-33） |
| H4 | Esc 退出保存与 Alt+F4 / 30s 自动保存并发写盘 | **证伪** | `SaveLoadService.SaveNow(async:false)` 先 `while (_writeInProgress) Sleep(1)` 等在途后台写（SaveLoadService.cs:102-107），同步路径内联执行——单写者串行成立（**但见 R-1：ChunkStreamer 旁路破坏了这一假设**） |
| H5 | 暂停（timeScale=0）期间 30s 自动保存与"保存并退出"并发 | **证伪** | `SaveLoadService.Update` 用 `Time.deltaTime` 累计计时（SaveLoadService.cs:73），timeScale=0 时 deltaTime=0，计时冻结——暂停期间不触发自动保存 |
| H6 | 换世界（场景重载）时静态状态清理 | **部分证实** | 清理了：`PlayerContext.Instance`/`QuestEventBus.Instance`（OnDestroy 清）、`UiCursorGate.Reset()`（TitleScreenUi.Awake SkipMenuNextLoad 分支，TitleScreenUi.cs:100）、`PendingSeed`（消费即清）。**残留**：`Explosion.LastResult`（→R-4）、`BlockInteraction.InputLocked`（被 TitleScreenUi.Awake 两条分支覆盖，实际无窗口）、`EnchantStore.Default`/`BossSummonState.Default`（按设计跨世界共享？——注意：**这两个是全局单例，不按 seed 隔离**，换世界后附魔/Boss 图腾状态跟人走，属于设计取舍非崩溃） |

---

## 二、摘要：按风险排序的 Top 发现

| 编号 | 级别 | 一句话 | 崩溃? |
| --- | --- | --- | --- |
| R-1 | **P1** | 后台 30s 保存与 ChunkStreamer 卸载落盘**并发写同一 region 文件**，read-modify-write 竞态可静默丢失单个区块的玩家改动 | 否（异常被吞） |
| R-2 | **P1** | 换世界（`RequestWorldSwitch`）**不保存直接 LoadScene**——丢最近 ≤30s 的全部改动；极端时序下旧后台写盘可污染新会话的 level.dat 读取 | 否 |
| R-3 | P2 | 合成网格材料 / 箱子手持栈**不进存档**——退出时（含帮助菜单明示的 Alt+F4）物品蒸发 | 否 |
| R-4 | P3 | `Explosion.LastResult` 静态跨场景残留——换世界后新世界凭空重放旧爆炸掉落 | 否 |
| R-5 | P3 | Esc 同帧双响：帮助菜单开着按 Esc → 帮助关 + 暂停开（UX） | 否 |
| R-6 | P3 | 暂停菜单互斥只覆盖 HelpMenuUi，其它模态 UI 开着时 Esc 叠开暂停（重叠绘制） | 否 |
| R-7 | P3 | `MobManager.Update` 读 `PlayerContext.Instance.Death` 未判 null（防御缺口） | 潜在 NRE，运行时不可达 |
| R-8 | P3 | `ItemDatabaseLoader` 无 try-catch，坏 items JSON 会中断 `WorldBootstrap.Awake`（启动黑屏） | 启动失败面 |
| R-9 | P3 | Achievement/Codex 页 OnGUI 每帧 new GUIStyle（GC 纪律漂移） | 否 |
| R-10 | P3 | WeaponPanelUi 可把 SelectedHotbarIndex 设为 9..35，hotbar 无高亮（视觉怪，无越界） | 否 |
| R-11 | P3 | TitleScreenUi 的 1920×1080 RenderTexture 在换世界路径不 Destroy（延迟 GC 回收） | 否 |

---

## 三、发现清单（含 file:line / 触发 / 后果 / 修复草案 / 置信度）

### R-1 【P1·数据丢失】后台保存与区块卸载落盘并发写 region 文件（lost update）

- **位置**：
  - `Assets/Scripts/Unity/Persistence/SaveLoadService.cs:97-180`（SaveNow：async 路径把 `RegionSaveCoordinator.SaveDirty(chunkSnapshot, ...)` 放进 `Task.Run` 后台执行）
  - `Assets/Scripts/Unity/World/ChunkStreamer.cs:319-332`（`UnloadDistant`：卸载脏区块前**主线程同步**调 `RegionSaveCoordinator.SaveDirty(_world, ...)`）
  - `Assets/Scripts/Core/Persistence/RegionSaveCoordinator.cs:50-84, 133-151`（`SaveDirtyCore` = LoadOrCreate 读旧 region → StoreChunk → `AtomicWrite`（`path + ".tmp"` → File.Replace/Move），`catch (IOException || InvalidDataException)` 吞掉并保持 dirty）
- **触发条件**：30s 自动保存的后台写盘进行中（窗口通常 <1s），玩家移动使某脏区块越出 UnloadRadius=8——`UnloadDistant` 只要有任一待卸载列是脏的就**整批**落盘全部脏区块（ChunkStreamer.cs:324-331），移动玩家高频触发。
- **后果**：两写者的 load→merge→write 不是原子的。时序：后台线程 LoadOrCreate 读旧 region（不含区块 X）→ 主线程同步版写入 X 并回调 `world.ClearDirty(X)`（SaveDirtyCore 的同步回调清整组脏，RegionSaveCoordinator.cs:25-29）→ 后台线程用它手里的旧 region 覆盖落盘 → **X 的改动被覆盖且已清脏，永久丢失**。另一形态：两线程同时 `File.Create(path + ".tmp")` 抢同一 tmp 文件 → IOException/FileNotFoundException（均继承 IOException，被吞）→ 该批保持 dirty 下轮重试，不崩但打错误日志。SaveLoadService 注释自述"原子写的前提是单写者"（SaveLoadService.cs:104），但 `_writeInProgress` 只约束自己，**不知道 ChunkStreamer 这条旁路**。
- **修复草案**：`RegionSaveCoordinator` 加 `private static readonly object WriteLock = new object();`，`SaveDirtyCore` 整体进 `lock`（后台持锁时主线程短暂阻塞，写盘 <100ms 可接受）；这保住 read-modify-write 原子性，也天然消除 tmp 抢占。备选：把 ChunkStreamer 的卸载落盘改为向 SaveLoadService 申请（暴露 `SaveNow(async:false)` 供 UnloadDistant 调用——同步路径已会等待在途后台写）。
- **置信度**：代码路径 **Confirmed**（三个文件均已通读）；实机触发频率 **Likely**（估算：连续跑图 5 分钟若发生 10+ 次卸载落盘，与 30s 后台写 1s 窗口碰撞的概率可达数十个百分点；单次后果是个别区块改动回退，孩子表现为"我挖的方块又回来了/放的没了"）。

### R-2 【P1·数据丢失】换世界不保存直接场景重载

- **位置**：
  - `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs:54-60`（`RequestWorldSwitch`：写 PendingSeed → LoadScene，**无任何保存调用**）
  - `Assets/Scripts/Unity/Persistence/SaveLoadService.cs`（无 `OnDestroy`——场景卸载不触发保存；`OnApplicationQuit` 只在退进程时触发）
  - `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs:477-482`（`OnDestroy` 只 `_materials?.Dispose()`；ChunkStreamer 是纯 C# 类，无 Dispose 调用点——其卸载落盘只在玩家走远时触发，场景卸载不冲刷）
- **触发条件**：主菜单世界列表/新世界里选中不同种子进入（m12 P1 路径）。注意主菜单是遮罩模式、世界照常跑，30s 自动保存照常，所以丢失窗口 ≤30s。
- **后果**：① 自上次自动保存以来的方块改动与 level.dat 状态（位置/背包/任务进度）全部丢弃；② 后台 Task.Run 写盘闭包在场景重载后仍持有旧 World 引用继续写旧目录——若玩家 30s 内切回同一 seed，新会话 `TryRestore` 读 level.dat 可能与旧后台写的 `File.Replace` 交错，读到坏档 → `TryRenameCorrupt` 把档改名 `.corrupt` → **进度回档**（region 方块仍在，但背包/位置/任务回到更早）。②为极端时序，概率很低。
- **修复草案**：`RequestWorldSwitch` 在 LoadScene 前调用 `FindObjectOfType<SaveLoadService>()?.SaveNow(async:false)`（同步落盘、失败也只是丢 30s 窗口，不阻塞换世界）；同时给 `SaveLoadService` 补 `OnDestroy` 场景卸载保存（或至少在 WorldBootstrap.OnDestroy 里冲刷）。
- **置信度**：① **Confirmed**（代码路径明确，无任何保存钩子）；② **Speculative**（需后台写盘与读档精确同窗）。

### R-3 【P2·物品蒸发】合成网格材料与箱子手持栈不进存档

- **位置**：
  - `Assets/Scripts/Unity/UI/CraftingInventoryUi.cs:17, 76-81`（`_craft` 数组存网格物品；`OnDisable` 只 `SetOpen(false)` **不归还网格物品**）
  - `Assets/Scripts/Unity/UI/CraftingWorkbenchUi.cs` / `CraftingPocketUi.cs`（同款网格语义）
  - `Assets/Scripts/Unity/UI/ChestUi.cs:104-130`（`_held` 手持栈只在 `Close()` 归还；退出路径保存时 UI 未关）
  - `Assets/Scripts/Unity/Persistence/SaveLoadService.cs:292-311`（`CollectPlayer` 只快照 Inventory 36 格 + ArmorSlots 4 槽——网格与手持栈不在任何存档字段里）
- **触发条件**：合成网格放着材料（m13 P0 后 SHIFT+click 主背包入料让这更容易发生）或箱子界面拿着物品时 Alt+F4 / 帮助菜单"保存并退出"。帮助菜单按键表明示"Alt+F4 直接退出（自动存档）"（HelpMenuUi.cs:283），孩子被教导这是安全操作。注意 `OnApplicationQuit`（保存）先于 `OnDisable`（归还）执行，所以退出保存快照时物品确实不在背包。
- **后果**：网格/手持的物品在下次进世界时消失。
- **修复草案**：`SaveLoadService.CollectLevelData` 前调一个静态归位入口（把三个合成 UI 的网格物品与 ChestUi 手持栈 TryAdd 回背包，塞不下 spawn 掉落物——照 ChestUi.CloseHeldOnly 的兜底写法）；或 `OnApplicationQuit` 保存前先触发各 UI 的 Close。
- **置信度**：**Confirmed**（保存链字段与 UI 生命周期均已通读比对）。

### R-4 【P3·状态泄漏】Explosion.LastResult 静态跨场景残留

- **位置**：`Assets/Scripts/Core/Combat/Explosion.cs:56`（`public static ExplosionResult LastResult`）+ `Assets/Scripts/Unity/Combat/MobManager.cs:313-332`（`TickExplosionDrops` 按**实例**字段 `_lastHandledExplosion` 引用比对去重）
- **触发**：苦力怕爆炸产生掉落 → 从主菜单换世界 → 新 MobManager 的 `_lastHandledExplosion` 为 null ≠ 旧静态 LastResult → Update 首轮把旧爆炸的 drops spawn 进**新世界**的 `PlayerContext.ItemDrops`（新 PlayerContext，物品凭空出现，坐标是旧世界的爆炸点）。
- **后果**：新世界凭空多出一堆掉落物（也会反向：若旧爆炸无掉落则 no-op）。非崩溃、低危害，但属于"静态状态场景重载清理契约"缺口。
- **修复草案**：`WorldBootstrap.OnDestroy` 里 `Explosion.LastResult = null`（需加 internal setter），或改为按 seed+单调 id 比对。
- **置信度**：代码路径 **Confirmed**；实机触发需"爆炸后未消费完就换世界"（掉落在爆炸当帧即被消费——`TickExplosionDrops` 在爆炸后下一帧 Update 就 spawn 并记引用，所以只要爆炸后跑过一帧就消费了；换世界发生在爆炸同帧的概率极小）→ 实际风险 **Low**。

### R-5 【P3·UX】Esc 同帧双响：帮助菜单关 + 暂停菜单开

- **位置**：`Assets/Scripts/Unity/UI/HelpMenuUi.cs:101-105`（Update 轮询 `Input.GetKeyDown(Escape)`）与 `Assets/Scripts/Unity/UI/PauseMenuUi.cs:180-194`（OnGUI 事件层拦 Esc + `evt.Use()`）
- **机理**：Update 先于 OnGUI；帮助菜单开着按 Esc → HelpMenuUi.Update 关闭帮助（IsOpen 变 false）→ 同帧 PauseMenuUi.OnGUI 的 KeyDown 事件仍到达（IMGUI 事件与 `Input.GetKeyDown` 是两条独立通道，`evt.Use()` 不抑制 Input 轮询）→ `HandleKey` → `SetOpen(true)` 弹出暂停菜单。净效果：想关帮助菜单的孩子看到暂停菜单弹出，需要再按一次 Esc。
- **修复草案**：PauseMenuUi 的 Esc 开门分支加"本帧有关闭其它模态则让位"检查（如 `UiCursorGate.OpenCount` 本帧发生变化则跳过），或统一把 Esc 路由收敛到单一入口。
- **置信度**：代码路径 **Confirmed**；实机表现 **Likely**（未实机复现，仅代码推演——两通道时序是 Unity 既有行为）。

### R-6 【P3·UX】暂停菜单互斥不覆盖全部模态 UI

- **位置**：`Assets/Scripts/Unity/UI/PauseMenuUi.cs:88-106`（`SetOpen(true)` 只级联关 `_help`）
- **机理**：背包/工作台/口袋/熔炉/箱子/武器面板/穿戴栏开着时按 Esc → 暂停菜单叠开（timeScale=0，但 IMGUI 不受 timeScale 影响，两 UI 都画、都收事件）。点击不会双触发（第一个处理者 `Event.current.Use()` 后 type 变 Used，后续 `IsLeftClickIn` 均失败），同一 GameObject 上 OnGUI 按挂载序：背包（步骤 23）先于暂停（步骤 30）→ 背包格子先吃点击。不崩，但视觉重叠、恢复暂停后背包还原样开着，行为不整洁。
- **修复草案**：PauseMenuUi.SetOpen(true) 时遍历关闭所有已开模态（给各 UI 提供静态注册表，或让 UiCursorGate 记录登记方）。
- **置信度**：**Confirmed**（挂载序与 OnGUI 事件语义为 Unity 既有行为）。

### R-7 【P3·防御缺口】MobManager.Update 未判 Death null

- **位置**：`Assets/Scripts/Unity/Combat/MobManager.cs:258-261` —— `if (PlayerContext.Instance != null) { PlayerContext.Instance.Death.Tick(dt); }`，`Death` 字段若为 null 则 NRE。对照 `DeathScreenUi.Update`（DeathScreenUi.cs:91-92）有 `ctx.Death == null` 判断。
- **运行时可达性**：WorldBootstrap.Awake 步骤 7 恒设 `Death = new DeathSystem()`，Instance 存在则 Death 必非 null——**运行时不可达**，仅 EditMode/异常装配面。同类问题在本仓库的惯例是判空（如 BlockInteraction 全链判空），此处属漂移。
- **修复草案**：`var death = PlayerContext.Instance?.Death; death?.Tick(dt);`
- **置信度**：Confirmed（缺口存在）；崩溃风险 Low（不可达路径）。

### R-8 【P3·启动失败面】ItemDatabaseLoader 无降级

- **位置**：`Assets/Scripts/Unity/Bootstrap/ItemDatabaseLoader.cs:11-43` —— `Load()`/`LoadRecipes()` 直接 `ItemDatabase.FromJson(documents)`，无 try-catch；调用点 `WorldBootstrap.Awake` 步骤 2（WorldBootstrap.cs:103-104）同样无包裹。
- **后果**：任一 items JSON 坏（例如 m13 W3 新增的 `range` 负数抛 `InvalidDataException`、悬空 itemId 引用抛异常）→ Awake 中断 → **游戏起不来**（黑屏/卡启动）。对照：block_drops / achievements / quests / vegetation / FarmSystem / ChestSystem 全都有 try-catch warn 降级——items/recipes 是唯一裸奔的主数据源。守卫测试（dotnet 链）通常会在合入前抓住坏 JSON，故现实风险=打包流程漏检。
- **修复草案**：与 BlockDropsLoader 同款 try-catch：失败 warn + 空表（游戏能起，缺物品）；或至少在 WorldBootstrap 外层 try-catch 弹错误提示。
- **置信度**：Confirmed（缺口）；现实触发 Low（数据受控 + 守卫测试）。

### R-9 【P3·GC】成就/图鉴页 OnGUI 每帧 new GUIStyle

- **位置**：`Assets/Scripts/Unity/UI/AchievementUi.cs:101-107, 115-117`（每格 `new GUIStyle(white)` ×2 + 底部 1 个，16 成就 ≈ 33 个/帧）；`Assets/Scripts/Unity/UI/CodexUi.cs:44-45`（每帧 2 个 + 循环内未看完全部，同款模式）
- **后果**：打开帮助菜单成就/图鉴页时每帧数 KB IMGUI 分配，违反 m5"热路径不 new 容器或闭包"纪律。不崩，仅 GC 压力（量级 ~100KB/s，远小于卡顿阈值）。
- **修复草案**：照 HelpMenuUi/_countStyle 模式缓存为 static 字段。
- **置信度**：Confirmed。

### R-10 【P3·行为怪】SelectedHotbarIndex 可为 9..35

- **位置**：`Assets/Scripts/Unity/UI/WeaponPanelUi.cs:172`（`ctx.Inventory.SelectedHotbarIndex = target`，target ∈ 0..35）+ `Assets/Scripts/Core/Player/PlayerInventory.cs:20`（自动属性，无钳制）
- **后果**：选中主背包武器时 HotbarUI 无高亮（HotbarUI.cs:139/186 按 `i == SelectedHotbarIndex` 只画 0..8）；`GetSelected()` 经 `GetSlot` 的 InRange 检查返回正确物品，功能正常。滚轮 `(x+8)%9`/`(x+1)%9`（HotbarUI.cs:281-282）从 35 出发回到 7/0——**无越界无崩溃**，注释也自知此取舍（WeaponPanelUi.cs:166-171）。SaveLoadService 存 `SelectedHotbarIndex` 原值往返，读档后同样安全。
- **修复草案**（可选）：HotbarUI 画选中框时按 `(SelectedHotbarIndex % 9)` 或由 WeaponPanelUi 选中时把武器挪进 hotbar 空槽（CombatController 既有行为）。
- **置信度**：Confirmed（边界行为已验算）。

### R-11 【P3·资源延迟回收】TitleScreenUi 的 RenderTexture 换世界不 Destroy

- **位置**：`Assets/Scripts/Unity/UI/TitleScreenUi.cs:182-193`（`_rt = new RenderTexture(1920,1080,0)`）；`StartGame()` 只 `Release()+null`（TitleScreenUi.cs:203），无 OnDestroy——换世界路径（LoadScene）时菜单还开着视频，RT 失去引用后等 GC finalizer 回收 native 显存。`VideoScreenSystem` 的 640×360 RT 同理（材质 Dispose 时断引用）。频繁换世界时短暂累积，进程内上限为个位数 RT，无崩溃。
- **修复草案**：TitleScreenUi/VideoScreenSystem 补 OnDestroy Destroy RT。
- **置信度**：Confirmed。

---

## 四、近期 8 个提交逐条复核

| 提交 | 内容 | 复核结论 |
| --- | --- | --- |
| `4ba7b84` P0 挖掘计时+放置手感 | DigProgress/TryTickDig/裂纹/幽灵框/tower-up/LiftTo | **未发现崩溃级缺陷**。`BlockInteraction.Update` 的门开早退正确作废蓄力/幽灵框/裂纹/弓蓄力（BlockInteraction.cs:186-206）；`DigCrackOverlay` 缺图整体降级不炸（DigCrackOverlay.cs:96-100）；`LiftTo` 只向上、速度清零（PlayerController.cs:78-90）。`TryTickDig` 的 `hardness` NaN 分支与 `digTimeMultiplier<=0` 回退 1 均有防御（BlockInteraction.cs:1317-1361） |
| `c2a0261` P1 世界管理 | WorldCatalog/TitleScreenUi 三态/RequestWorldSwitch | **发现 R-2（换世界不保存）与 R-11（RT 不销毁）**，均非崩溃。`WorldCatalog.TryParseSeed` 拒空/非数字/溢出；`TryDelete` 改名带 GUID 防同秒撞名；两段确认删除状态机安全（连点第三次即重新武装）。种子输入非法走 `_seedError` 提示不炸。`SkipMenuNextLoad` 握手在 TitleScreenUi.Awake 消费即清，且该分支调 `UiCursorGate.Reset()` 兜住旧场景残留门位——设计到位 |
| `739bbbc` wave1 W1-W3 成就/图鉴/药水乐器 | AchievementSystem/CodexSystem/PotionSystem/PlayTone/music_box | **未发现崩溃级缺陷**。`AchievementUi.Bind` 先解旧订阅；成就/图鉴缺 JSON 时 WorldBootstrap try-catch 降级（WorldBootstrap.cs:149-160）；药水分支 `TryDrinkSelectedPotion` 对未知 potion id 显式 return false（BlockInteraction.cs:431-432）；`PlayTone` 静态 ToneCache 跨场景复用（少量 native clip 进程级常驻，可接受）。发现 R-9（GUIStyle GC） |
| `cc19117` wave2 W4+W6+W7 水生飞行生物 | MobKind 28-36/TickAquatic/TickFlyer/水柱分流/白名单 | **未发现崩溃级缺陷**。`TickAquatic` 纯 Core：world null 跳过水判定、`by` 越界检查、GetBlock 读容忍（MobAI diff 通读）；水柱分流在 `TickSpawn` 里 `_world != null` 先决（MobManager.cs:468-469）；MobView/MobManager/MobModels 白名单五处已逐个对齐（MobManager.UsesPartTable/MobKindToTypeId、MobView.Setup、MobModelLibrary）。`MobManager.Update` 的 `Death.Tick` 缺判空记为 R-7 |
| `42a6282` / `5b1283d` / `9f528b4` / `f0eb3ea`（4 个 docs 提交） | 文档收口 | 与代码现状抽检一致（基线数字 1039/1813 与本次复跑 dotnet 侧吻合；"新 kind 从 37 起"与 MobKind.cs 一致）。无代码 |
| 结论 | | **8 个提交均未引入崩溃级缺陷**；引入的问题集中在**存档时序**（R-2 随 c2a0261、R-3 部分随 m13 P0 强化）与状态清理（R-4） |

版本归属核对：R-1 的双写者面自 m5 C3（后台写盘引入）+ m4 B1（卸载落盘引入）即存在，非近期提交引入；R-2/R-11 由 `c2a0261`（2026-08-21 00:56:02 +0800）引入；R-9 由 `739bbbc`（01:33:42）引入；R-10 由 m13 W4 `acea016` 引入（本次包不含，但已合入 main——注意：**当前 main HEAD=f0eb3ea，m13 的提交在更早历史里，包内已含**）。

---

## 五、系统性加固方案

### 5.1 模态 UI：保留"方案 A 每 UI 自管" + 三条守卫清单

现有方案 A 运行良好（计数门 + OnDisable 复位 + SHIFT 优先 + 点外关闭在画格子前判断），不必推倒重做 ModalUiManager。守卫清单（新模态 UI 合入前过一遍）：

1. 开关一律走 `SetOpen(bool)` 单入口，内部维护 `UiCursorGate.Open()/Close()` 与（必要时）`BlockInteraction.InputLocked`；禁止直改 `_open` 字段。
2. 必须实现 `OnDisable` 复位（门位 + 输入锁 + 手持/网格物品归位）——现有 8 个模态全有，但归位只有 ChestUi 做了（见 R-3）。
3. 按键开关走 Update 时必须带"其它模态开着不叠开"检查（`!UiCursorGate.IsOpen`）；点外关闭必须 SHIFT 优先且在 OnGUI 画格子之前判断（现有 5 处一致，保持）。
4. （补 R-5/R-6）Esc 的最终归宿建议收敛：PauseMenuUi 是 Esc 的最终消费者——`SetOpen(true)` 时级联关闭所有已开模态（可用反射注册表或让 UiCursorGate 记录登记方列表，`Close()` 遍历）。

### 5.2 静态状态的场景重载清理契约

在 `WorldBootstrap.OnDestroy` 增加一段集中清理（当前只有 `_materials?.Dispose()`）：

```csharp
private void OnDestroy()
{
    // 换世界/退场景前同步冲刷（修 R-2）
    GetComponent<SaveLoadService>()?.SaveNow(async: false);
    MyWorld.Core.Combat.Explosion.LastResult = null;   // 修 R-4（需加 internal setter）
    MyWorld.Core.Combat.Explosion.BoundRegistry = null;
    _materials?.Dispose(); _materials = null;
    if (CurrentWorld == _world) CurrentWorld = null;
}
```

静态状态清单（重载后行为）：需要清的——`Explosion.LastResult`；已被消费清理的——`PendingSeed`、`TitleScreenUi.SkipMenuNextLoad`、`PlayerContext.Instance`、`QuestEventBus.Instance`、`UiCursorGate`（TitleScreenUi 兜）；按设计保留的——`PeaceMode`/`DifficultyMode`（玩家偏好）、`MusicVolumeBus`、`PlayerPrefs` 各键、`ItemSlotDrawer/AchievementUi/CodexUi/PlayerAudioSystem.ToneCache` 的贴图与 clip 缓存（进程级常驻，量固定）；**设计上需注意的**——`EnchantStore.Default`/`BossSummonState.Default` 全局单例不按 seed 隔离（换世界附魔跟着走——若未来多档同 seed 分世界会串，当前单玩家单档语义下无害）。

### 5.3 Unity 假 null 防御规范

- 现状良好的模式：`ChunkViewRegistry.Destroy`、`ItemDropViewRegistry.DestroyView`、`ProjectileManager.SyncView/RemoveAt`、`HelpMenuUi.GetProgressSummary` 都用 UnityEngine 重载 `!= null` 判 fake-null，保持。
- 需要成文的规则：**`?.` 与 `??` 不防 Unity fake-null**（C# null 传播只查托管引用）。现仓库有两处靠"侥幸"安全：`PlayerAudioSystem.Instance?.PlayTone(...)`、`RedstoneSystem` 的 `PlayerAudioSystem.Instance?.PlayDoor...`——场景重载窗口内 Instance 为 fake-null 时会进方法体，靠 `_source == null`（Unity 重载）或 `isActiveAndEnabled` 守卫兜住。建议：静态 Instance 模式统一加 `OnDestroy { Instance = null; }`（PlayerAudioSystem 目前缺，TitleScreenUi 的 `_player.Stop()` 置 null 但组件未 Destroy 也属同类小尾巴）。
- 可加一个扩展 `public static bool IsAlive(this UnityEngine.Object o) => o != null;` 并在 review 清单里强制。

### 5.4 后台任务互斥（修 R-1 的最小改法）

`RegionSaveCoordinator` 加静态写锁（见 R-1 修复草案）。更完整的模式是 CancellationToken：`SaveLoadService` 持有 `CancellationTokenSource`，场景卸载时 Cancel——但写盘本身不可中断，锁方案已足够且改动最小。**不建议**把锁放 SaveLoadService（ChunkStreamer 不认识它）；锁必须放在两条路径的共同下游 RegionSaveCoordinator。

### 5.5 Player.log 业务打点

当前除 SaveLoadService 错误 LogError 外，关键生命周期零日志——出问题时无证据。建议最小打点集（每次一行，不进热路径）：
- `WorldBootstrap.Awake`：`[WorldBootstrap] 启动 seed={seed} PendingSeed={...}`
- `WorldBootstrap.OnDestroy`：`[WorldBootstrap] 场景卸载 seed={seed}`
- `RequestWorldSwitch`：`[WorldSwitch] {oldSeed} -> {newSeed}`
- `SaveLoadService.SaveNow` 完成时（后台回调主线程消化处）：`[Save] seed chunks=N ok/fail`
- `TitleScreenUi.StartGame` / `PauseMenuUi.SetOpen` 不打（高频/低值）。

### 5.6 回归测试思路（EditMode 可落地）

1. **场景重载模拟**：EditMode 下 `DestroyImmediate(playerHost)` 后断言静态残留——`Explosion.LastResult == null`、`UiCursorGate.OpenCount == 0`、`PlayerContext.Instance == null`、`BlockInteraction.InputLocked == false`（R-4/门位泄漏的直接回归）。
2. **并发保存交错**：给 `SaveLoadService.WriteExecutor` 注入"先执行一半再回调主线程 UnloadDistant 的 SaveDirty"的受控执行器，断言区块 X 落盘后不被旧快照覆盖（R-1 回归；锁合入后该测试转绿）。
3. **换世界保存**：注入临时目录，改 `RequestWorldSwitch` 走可注入的"保存钩子"，断言 LoadScene 前 level.dat 已含最后状态（R-2 回归）。
4. **连续开关 UI**：循环 `SetOpen(true/false)` ×100 交替混开（背包+工作台+武器面板+暂停）后断言 `UiCursorGate.OpenCount == 0` 且指针态复位；连点删除确认（`HandleGuiEvent` 模拟）断言两段确认不跳步。
5. **退出路径物品归位**：合成网格放物品 + ChestUi 手持 → 调 `OnApplicationQuit` 等价路径 → 断言背包/掉落物含网格物品（R-3 回归）。

---

## 六、建议修复顺序

- **P0**（下个提交，皆为小改动）：R-1（RegionSaveCoordinator 静态写锁，~5 行）、R-2（RequestWorldSwitch/OnDestroy 同步保存，~10 行）
- **P1**：R-3（退出/保存前网格与手持归位，~40 行 + 测试）
- **P2**：R-4（LastResult 清理）、R-7（Death 判空）、R-8（loader try-catch 降级）
- **P3**：R-5/R-6（Esc 路由统一与暂停级联关闭）、R-9（GUIStyle 缓存）、R-10（选中高亮取舍）、R-11（RT Destroy）

---

## 七、已检查且安全清单（复核过代码、未见缺陷）

- **指针门体系**：`UiCursorGate` 计数制、`Close` 防负（Mathf.Max）、`Reset` 有运行时调用点（TitleScreenUi.cs:100）；全部 8 个模态 UI 的 `OnDisable` 复位逐一核对（CraftingInventoryUi/CraftingWorkbenchUi/CraftingPocketUi/CraftingFurnaceUi/ChestUi/ArmorSlotsUi/WeaponPanelUi/HelpMenuUi/PauseMenuUi/TradeUi/EnchantingUi）
- **OnGUI 重入**：所有事件处理限定 `Event.current.type == EventType.MouseDown`（一次性事件），Layout/Repaint 轮无集合修改；`BackgroundBounds` 在点外判断前赋值；点外关闭 `Use()` 后 return
- **SHIFT 优先于点外关闭**：5 处 ShouldCloseOnMouseDown 全部先判 shift（m13 P0 契约保持）
- **静态事件退订**：QuestEventBus（OnEnable/OnDisable/OnDestroy 三保险）、MobManager、CodexHost、ProjectileManager
- **SaveLoadService 后台写**：快照冻结纯数据、volatile 标志的 release/acquire 发布、执行器本身抛异常时复位 `_writeInProgress`（防永久卡死）、同步路径等待在途写；`.tmp`+File.Replace 原子写；坏 level.dat 改名 `.corrupt` 后全新开始
- **退出状态机**：`_quitPending/_quitFired` 防重入、保存失败绝不退出可重试、停留窗用 unscaledTime（timeScale=0 可走完）、`OnApplicationQuit` 再存一次幂等
- **音频四系统**：全部 Resources.Load null 容忍（warn 一次）+ hit/fire/click/tone 程序生成兜底；BGM 双源交叉淡化的半途切曲处理正确；`ToneCache` 进程级复用
- **Unity 假 null**：ChunkViewRegistry/ItemDropViewRegistry/ProjectileManager/MobManager 视图销毁全部判 fake-null；`Camera.main` 判空（MobView.OnGUI、SettingsPanelUi）
- **挖掘蓄力状态机**：门开/换目标/松键/死亡画面四路作废；裂纹缺图降级；幽灵框两态与真实放置同一条判定
- **右键路由优先级链**：死亡让位 → 食物 → 药水 → 乐器 → 附魔书 → 弓 → 火枪 → 图腾 → 农业链 → 床 → 箱 → 音乐盒 → 门 → 放置，逐条判空防御（PlayerContext.Instance/_player.Eye/ResolveFarm/ResolveBeds）
- **玩家伤害唯一入口**：TakeDamage 的无敌帧/飞行豁免/盾减伤/死亡画面链完整；HandleDamageTaken 兜底路径判 null 齐全
- **MobAI 水生/飞行**（cc19117）：TickAquatic 的 world null 与 by 越界防御、GetBlock 读容忍；五处 kind 白名单对齐
- **WorldCatalog**：TryParseSeed 拒非法输入、删除两段确认 + GUID 防撞名、列表扫描跳过 .deleted
- **PlayerInventory 边界**：GetSlot InRange 防御、SetSlot 写严格抛、TryAdd leftover 语义；ChestTransfer（经 UI 层调用）双向判 null
- **PlayerContext.Awake**：Health/Experience 均 struct，无 NRE（假设 H2 证伪）
- **日志取证**：Player.log / Player-prev.log 0 异常、均完整 Memory Statistics 干净退出；仅 4 个 mp4 的 H.264 timestamp 编码警告（无害）与 777 unused assets 卸载（正常）
- **dotnet 链复跑**：1039/1039 通过（2026-10-06 实测）

---

## 八、说明与局限

1. **日志只覆盖最后两个会话**（2026-08-21 03:06 / 03:09，Unity 只留两份）——"历史 65 会话零异常"不可证；本报告的"无崩溃"结论主要建立在代码审计上，而非运行证据。
2. **未做实机动态验证**：按约束未启动 MyWordGame.exe、未跑 Unity 批处理（黑名单）。R-1/R-2/R-5 的实机触发概率是基于代码路径与 Unity 既有行为（OnGUI 事件与 Input 轮询双通道、LoadScene 帧末生效、Destroy 延迟）的推演，标了对应置信度。
3. **EditMode 链未复跑**（禁 Unity.exe 调用）；CLAUDE.md 记载 `#if UNITY_EDITOR` 盲区存在，本报告的 Unity 侧结论若有 `#if UNITY_EDITOR` 包裹的差异路径，以实机为准。审计中未见可疑的编辑器条件编译分支（grep 范围内）。
4. **Core 位打包/网格/光照**（ChunkSection/GreedyMesher/LightPropagator）属崩溃取证角色的纵深，本次只抽查了持久化链上的读写边界（GetBlock 读容忍/SetBlock 写严格均符合契约），未逐行审计——由既有 1039 项 dotnet 测试与双链纪律兜底。
5. R-1 的碰撞概率估算（"数十个百分点"）基于"每 5 分钟 10+ 次卸载落盘 × 1s/30s 窗口"的粗算，未经实测校准，量级参考而非精确值。
