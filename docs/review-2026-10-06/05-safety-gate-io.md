# 05 · 危险操作门禁（B）与配置/外部 IO（C）安全审查

- 审查对象：`Builds/Windows/MyWordGame.exe` 对应源码（m12 全波次，commit `f0eb3ea`，工作树 clean）
- 审查人：系统安全与稳健性工程师（子代理），2026-10-06
- 方法：源码通读 + 仓库只读约束下的合成样本实验（`docs/review-2026-10-06/exp/05-safety-io/`，dotnet 宿主直链 Core 源码工程）+ 用户真实数据只读实测（注册表 / Player.log / worlds 目录列表）
- 实验代码：`docs/review-2026-10-06/exp/05-safety-io/Program.cs`（A–K 共 11 节，全部跑通，输出见本文各表）
- 用户真实存档**未被改动**：所有坏样本实验均在 exp 目录合成副本上进行

---

## 摘要（Top 风险，按处置优先级）

| # | 风险 | 级别 | 一句话 |
| --- | --- | --- | --- |
| 1 | T-B1 | **高** | region 文件损坏后所有保存**静默失败**：退出菜单显示"已保存"照常退出，玩家新改动全部丢失，无任何提示 |
| 2 | T-C1 | 中高 | `RegionFile.Load` 对区块 length 无上界校验，坏档可触发 64MB~2GB 内存分配（实测 64ms/64MB），主线程卡死或低配机 OOM |
| 3 | T-C2 | 中 | region 文件头部坐标与文件名不符时抛 `ArgumentOutOfRangeException`，冒过 `SaveDirtyCore` 的类型过滤 catch，在 `WorldBootstrap.Update` 每帧冒泡（实测复现） |
| 4 | T-B2 | 中 | 主菜单删除世界的 `Directory.Move` 无兜底：文件被杀毒/备份工具占用时 OnGUI 异常循环，删除按钮永久失效且无提示（实测复现 IOException） |
| 5 | T-C3 | 中 | PlayerPrefs 读取侧无钳制；用户注册表实测 `m6.fov`=-2.0、`m6.sensitivity`=-3.69e19（均越量程，来源无法由历代代码解释）——读入即坏值生效 |

其余为低危/信息级。**无网络调用、无进程启动、无注册表越界写**——单机离线儿童安全边界完整成立（全仓 grep 唯一命中 `UnityWebRequest` 是一句注释）。

---

## B 段发现清单

### T-B1（Confirmed，高）坏 region 文件 → 保存静默失败 → "保存并退出"丢档

- **位置**：`Assets/Scripts/Core/Persistence/RegionSaveCoordinator.cs:78-81`
  ```csharp
  catch (Exception e) when (e is IOException || e is InvalidDataException)
  {
      // 写失败（含旧 region 文件损坏读不回来）：这批 chunk 保持 dirty，下轮保存重试
  }
  ```
  空吞、无日志、无返回错误；`Assets/Scripts/Unity/Persistence/SaveLoadService.cs:135-143` 的 region 层 catch 只有 `SaveDirty` **抛出**才会 `failed=true`——而 `SaveDirty` 内部把 IO/坏档异常全部吞掉，永远不抛。
- **复现**（实验 D）：regions 目录预置魔数损坏的 `r.0.0.mwr`，制造脏区块后调 `RegionSaveCoordinator.SaveDirty(world, dir)` → 返回 `saved=0`、脏区块保留、region 文件未被改写、**无任何异常**。
- **后果链**：region 文件一旦损坏（磁盘坏区 / 半截写 / 外部工具）——
  1. `TryLoadChunk` 返回 false，该 region 旧改动读不回（回退 seed 生成）；
  2. 之后每轮 30s 自动保存、区块卸载保存、退出保存对这批区块**全部静默失败**（每轮都撞同一个坏文件）；
  3. `SaveNow(async:false)` 返回 true → `HelpMenuUi.RequestSaveAndQuit` 判"存成" → 退出 → **该 region 内全部新改动丢失**；
  4. 玩家全程无感知（catch 空块在 Core 层，连日志都没有）。
  违反 CLAUDE.md 承诺「保存失败不退出、可重试」的精神面——该承诺只覆盖 level.dat 层（level.dat 失败确实会 `failed=true` 拦截退出）。
- **修复草案**：
  1. `SaveDirtyCore` 的 catch 记录错误并经 out 参数/返回值上抛给调用方（Core 不能用 UnityEngine，返回 `List<string>` 即可）；
  2. `SaveLoadService.Write` 比较**应存数**（`chunkSnapshot.Count`）与**实存数**（`written.Count`），不等即 `failed=true`；
  3. 坏 region 读不回时的修复路径：`LoadOrCreate` 抛 `InvalidDataException` 的场合，可以**改名坏文件 `.corrupt` 后重建**（与 level.dat 同策略），比永远失败重试对孩子更友好。

### T-B2（Confirmed，中）删除确认流：`TryDelete` 失败无兜底 → OnGUI 异常循环

- **位置**：`Assets/Scripts/Unity/UI/TitleScreenUi.cs:416-423`（两段确认后直接调 `WorldCatalog.TryDelete`，返回值丢弃、无 try-catch）+ `Assets/Scripts/Core/Persistence/WorldCatalog.cs:123`（`Directory.Move` 可抛）。
- **复现**（实验 E）：
  - 目录内 `level.dat` 被独占句柄打开（模拟杀毒扫描/备份工具）→ `Directory.Move` 抛 `IOException: Access to the path ... is denied`；
  - 改名目标撞名（GUID 4 hex 同秒相撞，概率 1/65536/同秒对）→ `IOException: Cannot create ... because a file or directory with the same name already exists`。
- **后果**：Unity 会接住 OnGUI 异常不闪退，但当帧控件绘制中断、`_worlds` 不刷新、`_deleteArmed` 停在 armed 态；玩家每点一次"确认删除?"刷一条 error log，看不到任何失败原因，删除功能失效。
- **修复草案**：`TitleScreenUi` 调用处包 try-catch，失败时把异常消息写进 `_seedError` 风格的红字提示（"删除失败：文件被其他程序占用，请稍后再试"）；`WorldCatalog.TryDelete` 本身改为内部 catch 返回 false。
- **附带**：两段确认的 armed 态**无超时**（`_deleteArmed` 永不过期）。对儿童场景建议 5~8 秒未确认自动解除武装。世界列表固定最多 8 行且无滚动，第 9 个世界无法进入/删除（轻微 UX）。

### T-B3（代码 Confirmed / 后果 Likely，中低）死亡瞬间"保存并退出"→ 重进 0 血活尸

- **位置**：`Assets/Scripts/Unity/Persistence/SaveLoadService.cs:422-424`（`Mathf.Clamp(p.HealthCurrent, 0f, health.Max)`——0 是合法恢复值）+ `Assets/Scripts/Unity/Player/PlayerController.cs:210-220`（死亡画面只在 `TakeDamage` 内触发 `Show()`）。
- **场景**：死亡画面上点"保存并退出"（死亡画面右键=复活，但也可能经 Esc 暂停菜单退出）→ 存档 `HealthCurrent=0` → 重进世界 → 0 血"存活"、无死亡画面、无复活按钮；任何 0.5 点伤害（碎镐扎脚）即死。
- **修复草案**：`ApplyPlayer` 里 `HealthCurrent <= 0` 视为死亡档——直接回满血（对儿童宽容）或触发死亡画面。

### T-B4（Confirmed，低/设计评估）`RequestWorldSwitch` 不保存当前世界

- **位置**：`Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs:54-60`——直接 `LoadScene`，旧世界 dirty 区块不落盘（`ChunkStreamer` 是纯 C# 类无卸载回调，`SaveLoadService` 无 `OnDestroy`）。
- **现实可达性**：**当前不可达**——主菜单只在启动时出现（`TitleScreenUi.SkipMenuNextLoad` 换世界后直接进游戏），游戏内没有"回主菜单"入口；启动菜单期无人操作，无脏数据。丢失上限=30s 自动保存窗口。
- **建议**：`RequestWorldSwitch` 里加一次 `SaveNow(async:false)`（一行保险），防未来加"游戏中回主菜单"时变成真实丢档通道。

### T-B5（Confirmed，信息）门禁正面验证（承诺逐条核实，均兑现）

| CLAUDE.md 承诺 | 核实结果 |
| --- | --- |
| 删除走回收站式改名 `.deleted` 防误删 | ✓ `WorldCatalog.TryDelete` 改名不真删；实验 I：改名后目录仍在（可手动找回）；列表扫描跳过含 `.deleted` 的目录 |
| 写盘先落 `.tmp` 再原子改名 | ✓ `LevelDataCodec.Save` / `RegionSaveCoordinator.AtomicWrite`（`File.Replace`/`File.Move`）；实验 F：4 线程 ×50 轮并发写**终文件始终可读**（180 次 IOException 全被原子性吸收，无半截档） |
| 坏 level.dat 重命名 `.corrupt` 后全新开始 | ✓ 实验 A 全部 19 个坏样本都走 `InvalidDataException`/null → `TryRenameCorrupt` → 全新开始 |
| TryRestore 按序恢复、逐层失败跳过 | ✓ `SaveLoadService.TryRestore` 顺序与代码一致；各 Apply* 自带 null/坏行容忍 |
| 退出保存失败不退出可重试 | ✓ level.dat 层（`HelpMenuUi.RequestSaveAndQuit` 状态机：`_quitPending` 挡双击、`_quitError` 带原因可重试、`QuitClock` 半秒窗只退一次）；**region 层例外见 T-B1** |
| Alt+F4 走 OnApplicationQuit 同步保存 | ✓ `SaveLoadService.cs:81`；与在途后台写互斥（`async:false` 自旋等 `_writeInProgress`，单写者保证） |
| 死亡天然不掉落（m7 契约） | ✓ `PlayerController.cs:210-220` 注释明示 grep 全仓无清空路径，`PeaceModeTests.死亡_*` 守卫 |
| 悬空引用不抛（放置路由） | ✓（A 组同僚范围，本次未深查，标记未验证细节） |

---

## C 段发现清单

### T-C1（Confirmed，中高）`RegionFile.Load` length 无上界 → 内存炸弹

- **位置**：`Assets/Scripts/Core/Persistence/RegionFile.cs:101-108`——`length` 只查 `< 0`，随后 `new byte[length]` **先分配再读**。
- **实测**（实验 C）：声明 `length=64MB` + 实际 8 字节 → 分配 **65542KB**（64MB 全额发生）后抛"区域文件被截断"。声明 `int.MaxValue` 时将尝试分配 2GB（64 位进程虚拟内存可满足，低配机/多坏条目叠加可 OOM）。
- **触发面**：`TryLoadChunk`（区块流式加载，**主线程**）与 `LoadOrCreate`（保存合并路径）。坏档/截断文件每次命中区块加载都重试分配。
- **后果**：主线程 27ms+ 卡顿（64MB 实测）；2GB 声明可致内存膨胀/崩溃；叠加"该 region 永远读不出"（静默回退 seed 生成，旧改动丢失，无提示）。
- **修复草案**：`length` 上界校验——`(uint)length > 剩余流长度` 直接抛 `InvalidDataException`（区块序列化上限约 24×8192+5 ≈ 197KB，取 1MB 上界都极宽松）。

### T-C2（Confirmed，中）region 头部坐标不符 → 漏网异常类型冒进帧循环

- **位置**：`Assets/Scripts/Core/Persistence/RegionSaveCoordinator.cs:78`（catch 过滤不含 `ArgumentOutOfRangeException`）+ `RegionFile.cs:127-136`（`LocalIndex` 对不属于本 region 的区块抛该异常）。
- **实测**（实验 K）：构造 `r.0.0.mwr`（头部 regionX=5,Z=5，文件名 0.0）→ `SaveDirty` 抛 `ArgumentOutOfRangeException: 区块 (0, 0) 不属于区域 (5, 5)`。
- **冒泡路径**：
  1. `SaveLoadService.Write` 有 `catch(Exception)` → `failed=true` → 退出菜单能拦（✓）；
  2. **`ChunkStreamer.UnloadDistant`（`WorldBootstrap.Update` 每帧调用，WorldBootstrap.cs:473）无 catch** → Unity 主循环每帧异常，直到玩家走远/区块卸载，期间游戏刷 error log、卸载保存失效。
- **修复草案**：`RegionFile.Load` 读入时丢弃 `RegionFor` 不匹配的条目（或 `SaveDirtyCore` catch 放宽到 `Exception` 并记错误）。

### T-C3（Confirmed，中）PlayerPrefs 读取无钳制 + 注册表实存越界值

- **位置**：`Assets/Scripts/Unity/UI/SettingsPanelUi.cs:68-92`——`LoadSensitivity/LoadVolume/LoadFov/LoadMusicVolume` 直接 `GetFloat` **无 Clamp**（Save 侧全有）。
- **注册表实测**（`HKCU\Software\DefaultCompany\MyWordGame`，只读 reg query）：

| 键 | REG_DWORD | 按 float 位模式 | 量程 | 判定 |
| --- | --- | --- | --- | --- |
| m6.volume | 0x40000000 | +2.0 | [0,100] | 合法（音量 2/100） |
| m6.musicVolume | 0x40000000 | +2.0 | [0,100] | 合法 |
| **m6.fov** | **0xc0000000** | **-2.0** | [60,90] | **越界** |
| **m6.sensitivity** | **0xe0000000** | **-3.69e19** | [0.5,2.0] | **越界** |
| myword.fullscreen | 0x1 | int 1 | — | 正常 |
| TitleScreenUi.VideoIndex | 0x3 | int 3 | [0,3] | 正常（读侧有越界回落） |
| WorldCatalog.LastSeed | REG_BINARY "92726749" | string | — | 正常 |

- **编码结论**：Unity PlayerPrefs float 键 = IEEE754 位模式存 REG_DWORD，DWORD 即值本身（无双重编码）。**写入源无法定位**：m6 原版（commit `12056a9`，git 追溯）起 Save 就带 `Mathf.Clamp`，历代代码写不出 -2.0/-3.69e19；测试代码不写 PlayerPrefs（grep 证实）；推断为外部/历史环境修改（【假设，未能证实】）。**无论来源，读侧防御缺失是事实**。
- **后果**：`cam.fieldOfView=-2`（Unity 钳到 1，极度变焦）；`LookSensitivityMultiplier=-3.69e19`（鼠标一动视角飞转）。孩子拖一次滑条即自愈（`HorizontalSlider` 输入被钳）。注册表键被整删时 `GetFloat(key, default)` 回默认值 ✓。
- **修复草案**：四个 Load 都包 `Mathf.Clamp`（一行/个）。

### T-C4（Confirmed，低中）坏档处置的两个边角

- a) `TryRenameCorrupt`（SaveLoadService.cs:490-502）先 `File.Delete` 旧 `.corrupt` 再 Move——第二次坏档**覆盖**第一次案卷。建议带时间戳后缀。
- b) **seed 不符 → 整档忽略但不重命名**（SaveLoadService.cs:345-348）：目录名与内容 Seed 不符（手动改目录名等）时 level.dat 原样保留、永不加载，30s 后被新档**覆盖**——旧进度无声消失。建议改名 `.wrongseed` 留案。

### T-C5（Confirmed 解析层 / Likely 游戏后果，低中）坏档位置字段无校验

- 实验 A：`"Player":{"Y":1e40}` 解析 **OK 得 ∞**（Newtonsoft 宽松上溢不抛）。`ApplyPlayer` 直接 `RestoreCoreState(new Float3(p.X,p.Y,p.Z))`，无 NaN/∞/范围校验 → 玩家在无穷高，永远落不了地（`HealthCurrent=-5` 有 Clamp 兜住、`HealthMax=0` 有 `>0?:20` 守卫 ✓，唯独位置裸奔）。
- 修复草案：`float.IsNaN/IsInfinity` 或范围（y∈[-64,320]、|x|,|z|<3e7）外 → 丢弃 Player 段用出生点。

### T-C6（Confirmed 代码路径，低中）blocks/items/recipes JSON 坏 → 启动硬崩无降级

- `WorldBootstrap.Awake` 步骤 1/2（WorldBootstrap.cs:100-104）直调 `BlockRegistryLoader.Load()` / `ItemDatabaseLoader.Load()` / `LoadRecipes`，**无 try-catch**；`BlockRegistryLoader` 目录缺失/空/坏 JSON 全抛。
- standalone 包内只读（风险=包损坏，低）；编辑器下改 JSON 是日常（中）。用户所见：黑屏起不来，只有 Player.log 里的异常。
- 对照：vegetation/biomes（降级 oak-only ✓）、spawn_rules/drop_tables（降级旧路径 ✓）、quests（降级 no-op ✓）、achievements（降级 ✓）、block_drops（降级"挖方块不掉落" ✓）——**失败面不一致**，建议统一为"弹错误对话框 + 继续/退出"。

### T-C7（实测通过，信息）解析健壮性正面结论

- 深嵌套炸弹（200~200000 层）：全部被 Newtonsoft **默认 MaxDepth=64** 拦截（实验 J 内层 `JsonReaderException`）→ `InvalidDataException` → 坏档降级。**栈溢出炸弹不可行** ✓。
- Deflate 解压炸弹：输出上限被 section mask（≤24×8192B=196,608B）封死（实验 G：196KB 原始数据压到 70B，解出恒 ≤192KB）✓。
- fuzz：3000 轮（随机字节+头部变异+区间覆写+截断）无崩溃/无挂起/无 OOM；异常分布 `InvalidDataException×2402`、`ArgumentOutOfRangeException×407`（后者为 fuzz 宿主直查不匹配 region 所致，游戏路径不触发，但见 T-C2 的真实触发面）。
- `ChunkSerializer`：版本错/截断/mask 越位全抛 `InvalidDataException` ✓；注意 Deflate 末尾字节位翻转**可能静默通过**（实验 G 位翻转样本解出了原数据——流末冗余区无独立校验），属数据完整性弱化点，非崩溃面。
- BOM 头正常解析 ✓；重复键取末值（无害）；字符串数字 `"Seed":"42"` 被宽松接受为 42（无危害）。

### T-C8（Confirmed 代码路径，信息）models/*.json 缺失 → 刷怪路径异常

`MobModelLibrary.Load` 文件缺失/坏 JSON 抛（MobModelLibrary.cs:84-117），调用链 `MobManager.SpawnMob`（无 catch）→ 首次刷怪起每帧冒泡。standalone 包内只读，风险=打包遗漏（低）。颜色值非法有"品红哨兵"兜底 ✓。

### T-C9（实测，信息）日志与隐私卫生

- `Player.log` / `Player-prev.log` 干净退出（Input System Shutdown + 内存统计 = 正常退出路径），无刷屏、无敏感路径泄漏 ✓。
- 5 个 mp4 全部报 `Unexpected timestamp values...not encoded with the baseline profile` + `Color primaries 0 is unknown`（WindowsMediaFoundation 解码）——已知线索复证，纯视觉瑕疵（时间轴校正/轻微色偏），无安全影响。修复在视频产出侧（ffmpeg 编码参数 `-profile:v baseline -color_primaries bt709`）。
- 崩溃诊断：依赖 Unity 默认 Crashes 目录，游戏侧无自定义转储/无"最近一次保存时间"自助信息。建议：崩溃恢复提示（"上次未正常退出，存档时间 HH:mm"）。

### T-C10（实测，信息）文件系统边界

- worlds 目录不存在：`SaveLoadService.Write` / `SaveDirtyCore` 均 `Directory.CreateDirectory` 自建 ✓；`WorldCatalog.List` 返空 ✓。
- 只读属性：不影响目录改名（实验 E）✓。
- 路径长度：最长形态 ≈115 字符（中文用户名 + 极值 seed/region 名），不触 260 限制 ✓。
- 非 ASCII 用户名：`persistentDataPath` 由 Unity 处理，代码无手拼用户目录 ✓。
- 文件占用（OneDrive/杀毒）：LocalLow 默认不同步；独占锁影响删除（T-B2）与保存（level.dat 层有拦截 ✓，region 层静默 T-B1）。
- **双开进程**：游戏内单写者互斥完整（async 重叠跳过 + sync 等待）；双开 exe 无 Mutex 单实例锁——实验 F 表明原子性保住数据不坏，但 level.dat 最后写者赢、region 读改写丢更新。单机儿童场景低概率，建议 `Mutex` 单实例。

### （A 段复核）网络与进程启动：不存在 ✓

全仓 grep `Process.Start|ProcessStartInfo|HttpClient|UnityWebRequest|TcpClient|UdpClient|System.Net.Sockets|OpenURL`：唯一命中为 `BlockRegistryLoader.cs:10` 的注释（"将来要出 Android 版再换成 UnityWebRequest"）。注册表写入全部经 PlayerPrefs（键清单见 T-C3），无 `Microsoft.Win32.Registry` 直接操作。**儿童安全边界（离线/无 UGC/无付费）成立。**

---

## 门禁绕过统计（对抗样本分类表）

| # | 输入 | 当前行为 | 应然 | 判定 |
| --- | --- | --- | --- | --- |
| G1 | 两段确认连点（正常流） | 第一次 armed 变红"确认删除?"，第二次真删 | 同 | ✓ 设计内 |
| G2 | 同一秒删两个不同世界 | 都成功（时间戳+GUID4 后缀防撞） | 同 | ✓（假设①证伪） |
| G3 | 对同一世界连点两次"确认删除" | 第二次 `Directory.Exists` false 静默返回 | 同 | ✓ |
| G4 | armed 后等很久再点 | 仍可确认（无超时） | 建议 5-8s 解除武装 | △ T-B2 附带 |
| G5 | 删除时 level.dat 被杀毒独占 | OnGUI 抛 IOException，UI 中断、无提示、可反复触发 | 捕获+红字提示+可重试 | ✗ T-B2 |
| G6 | 删除"继续上次"指向的世界 | `ResolveContinueSeed` 的 Directory.Exists 兜住，回落 mtime 最新 | 同 | ✓ |
| G7 | "新世界"输入已有种子 | 直接续玩该世界（注释明示设计），无目录合并污染 | 同 | ✓（实验 I 证伪"静默合并旧 regions"假设） |
| G8 | 种子输入全角数字/小数/科学计数/溢出/0x | 全拒，红字"种子只能是整数" | 同 | ✓（实验 E 矩阵 17 例） |
| G9 | 双击"保存并退出" | `_quitPending` 挡住，只退一次 | 同 | ✓ |
| G10 | 保存失败后重试 | `_quitError` 清空重走一轮，真可重试 | 同 | ✓ |
| G11 | Esc 菜单开时按 H / 反之 | 互斥先关对方，同帧不双开 | 同 | ✓ |
| G12 | 退出挂起期间按 Esc/点继续 | 全被挡（timeScale 保持 0） | 同 | ✓ |
| G13 | Alt+F4（退出保存进行中） | OnApplicationQuit 同步保存，与后台写单写者互斥 | 同 | ✓ |
| G14 | 30s 自动保存与退出保存并发 | async 重叠跳过 / sync 自旋等待 | 同 | ✓（假设②证实但已被兜住） |
| G15 | 挖掉方块后 10s 强杀进程 | 丢最多 30s（level.dat+region 同窗口）；远离玩家的区块卸载时已同步落盘 | 可接受；建议关键事件即时保存 | △ |
| G16 | 死亡画面上保存退出 | 重进 0 血活尸（无死亡画面、一击再死） | 回满血或触发死亡画面 | ✗ T-B3 |
| G17 | 换世界握手期操作 | 场景重载中无输入对象，不可操作 | — | ✓（且 RequestWorldSwitch 不保存，当前不可达，见 T-B4） |
| G18 | 坏 region 下保存退出 | 显示"已保存"照常退出，改动全丢 | 拦截+提示+坏文件改名重建 | ✗ T-B1 |

统计：18 例中 13 通过、3 缺陷（T-B1/T-B2/T-B3）、2 可接受改进项。

## 坏样本实测结果表

### level.dat（JSON，19 样本，实验 A/B/J）

| 样本 | 结果 | 用户所见 |
| --- | --- | --- |
| 空文件 / 字面 null | 返回 null → 降级 | `.corrupt` 留案，全新开始 |
| 根为数组/数字/字符串、截断、二进制垃圾、字段类型错（TimeTick 字符串、Stats 值字符串、Seed null/溢出） | `InvalidDataException` → 降级 | 同上 ✓ |
| 深嵌套 200/2000/20000/200000 层 | 内层 `JsonReaderException: MaxDepth of 64 has been exceeded` → 降级 | 同上 ✓（无栈溢出） |
| `"Seed":"42"`（字符串数字） | 宽松接受为 42 | 正常恢复（无危害） |
| 重复键 | 取末值 | 无害 |
| BOM+正常 | 正常解析 | ✓ |
| `Player.Y=1e40` | 解析 OK 得 ∞ | **位置无校验直接生效**（T-C5） |
| `HealthCurrent=-5` | 解析 OK | ApplyPlayer 有 Clamp，兜住 ✓ |

### RegionFile / ChunkSerializer（二进制，实验 C/G/H/K）

| 样本 | 结果 |
| --- | --- |
| 坏魔数 / 版本 99 / count=-1 / count=1025 / 负 length / 截断 | 全抛 `InvalidDataException` ✓ |
| 声明 length=64MB 实际 8B | **先分配 64MB** 再抛截断（T-C1） |
| localIndex=-7 / 重复 localIndex | 接受入字典，无越界（查询不命中而已）✓ |
| 头部 regionX/Z 与文件名不符 | Load 通过；保存时 `ArgumentOutOfRangeException` 冒泡（T-C2） |
| Chunk 版本 2 / mask 全 1 截断 / 长度<5 | `InvalidDataException` ✓ |
| Deflate 位翻转（末尾） | 可能静默通过（无独立 CRC，弱完整性） |
| fuzz 3000 轮 | 无崩溃/挂起/OOM ✓ |
| 坏 region + SaveDirty | **saved=0、无异常、无日志**（T-B1） |

## 已验证无问题 / 被证伪的假设

| 项 | 结论 |
| --- | --- |
| 假设① `.deleted` 同秒双删/名字超长失败 | **证伪**：GUID4 后缀防撞（实验 E 双删成功）；路径最长 ~115 字符不触限 |
| 假设② `.tmp` 原子改名并发竞态 | **证实但已兜住**：并发写抛 IOException 被逐层 catch；游戏内单写者互斥完整；唯双开进程无保护（T-C10） |
| 假设③ RegionFile 坏数据异常路径 | **三分**：读路径 catch(Exception) 全兜 ✓；写路径静默吞（T-B1）；坐标不符类型漏网（T-C2） |
| 假设④ JSON 坏 → 启动行为 | **分文件**：blocks/items/recipes 硬崩（T-C6），其余 7 类全部优雅降级 ✓ |
| "同名新建静默合并旧 regions（P0）" | **证伪**：删除即改名，新建走全新空目录（实验 I） |
| 深嵌套 JSON 栈溢出炸弹 | **证伪**：Newtonsoft MaxDepth=64 |
| Deflate zip 炸弹 | **证伪**：解压输出上限 192KB（mask 24 位封顶） |
| 游戏内存在 Process.Start / 网络调用 | **证伪（符合边界）**：全仓零命中 |
| PlayerPrefs float 编码疑点 | **已定性**：REG_DWORD=位模式；两个越界值来源不可考（非历代代码行为），读侧无钳制为真缺口（T-C3） |
| 未验证部分 | Unity 侧运行时行为（Camera.fieldOfView=-2 的实际钳制值、OnGUI 异常的逐帧表现）为代码+文档推断，未启动游戏进程实证；`MobModelLibrary` 刷怪冒泡未实机复现（代码链路 Confirmed） |

## 加固建议与测试草案（可追加到 Assets/Tests/EditMode）

1. `SaveDirtyCore` 错误上报 + `SaveLoadService` 应存/实存对比（修 T-B1）
2. `RegionFile.Load` length 上界（修 T-C1）；`LocalIndex` 归属校验移到 Load 侧（修 T-C2）
3. `TitleScreenUi` 删除 try-catch + 失败提示 + armed 超时（修 T-B2）
4. `SettingsPanelUi` 四个 Load 侧 Clamp（修 T-C3）
5. `ApplyPlayer` 位置有效性校验 + 0 血档处置（修 T-B5/T-B3）
6. `TryRenameCorrupt` 时间戳化；seed 不符改名 `.wrongseed`（修 T-C4）
7. `WorldBootstrap` 启动 JSON 失败统一降级+错误画面（修 T-C6）
8. `RequestWorldSwitch` 前置保存（修 T-B4）

测试草案（命名照项目惯例）：

- `RegionSaveCoordinatorTests.坏region文件_SaveDirty上抛错误_不再静默吞`
- `RegionSaveCoordinatorTests.声明length超剩余流长_直接判坏_不分配大数组`
- `RegionFileTests.头部坐标与文件名不符_Load侧丢弃条目或StoreChunk不抛`
- `WorldCatalogTests.TryDelete_目标被占用_返回false不抛`
- `TitleScreenUiWorldMenuTests.删除失败_显示错误提示_armed不卡死`（现文件 108 行无删除流测试，顺带补两段确认状态机用例）
- `TitleScreenUiWorldMenuTests.armed超时自动解除`
- `SettingsPanelUiTests.Load侧越界注册表值_被钳制到量程`
- `SaveLoadServiceRestoreTests.Player位置NaN或无穷_回退出生点`
- `SaveLoadServiceRestoreTests.HealthCurrent零_回满血或触发死亡画面`
- `LevelDataCodecTests.坏档二次发生_corrupt案卷不被覆盖`

## 修复顺序建议

1. **T-B1**（丢档主通道，Core 纯逻辑可测）
2. **T-C1 + T-C2**（同文件 `RegionFile.cs`，一并修）
3. **T-B2**（儿童高频操作面的失败循环）
4. **T-C3 / T-C5 / T-B3**（读侧防御三连，各一两行）
5. T-C4 / T-C6 / T-B4 / 其余信息级
