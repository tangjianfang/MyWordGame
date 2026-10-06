# 06 · 软件工程与高可扩展性评审（MyWordGame）

- 评审日期：2026-10-06 · 评审人：架构评审子代理 · 基线 commit `f0eb3ea`（2026-08-21，共 346 commits，`git rev-list --count` 核实）
- 方法：**全静态**——只读代码 / git / grep / 自写启发式脚本（`docs/review-2026-10-06/exp/metrics.py`），未构建、未跑测试、未启动 exe、未动用户数据。所有"测试数/构建产物"结论均为静态推断。
- 复核纪律：每条结论标注 `file:line` + 证据 + 置信度（高=亲自读码读 diff；中=grep 统计；低=推断）。被推翻的假设单列 §9。

---

## 1. 摘要

**架构健康度总分：6.5 / 10**（语境：父子单机项目、无团队协作压力、主玩家是小学生——该语境下纪律水准显著高于同类业余项目；按"可长期扩展的软件工程"标尺扣分）。

| 维度 | 评分 | 一句话 |
| --- | --- | --- |
| 分层纯度 | 7/10 | Core 零引擎依赖由编译器强制、真实成立；但"三层"声称不实（Gameplay 无程序集） |
| 数据驱动扩展性 | 6/10 | 方块/物品/配方 🟢 真 JSON 驱动；生物 🔴 假驱动；UI 🟡 样板手抄 |
| 可测试性 | 7/10 | Core 可测性优秀、守卫测试文化罕见地好；静态全局状态是系统性隐患 |
| 横切一致性 | 6.5/10 | "唯一入口/读容忍写严格"大体贯彻；C#/JSON 双真源漂移面在扩大 |
| 构建与质量基建 | 4/10 | 无 CI、双链只覆盖 Core 侧、符号化/崩溃报告缺失 |
| 测试体系 | 7/10 | EditMode 1813 静态精确核实；并发/性能/实机链缺层 |
| 文档 | 6/10 | 结构与密度业界少有；但已证 6 处硬漂移（含一处文档内部自相矛盾） |
| 仓库卫生 | 8/10 | gitignore 齐、体积合理、根目录干净、行尾纪律有 .editorconfig 背书 |

### Top 10 结构性问题（按影响排序）

1. **"三层架构"实为两层**：`MyWorld.Gameplay` 程序集不存在。`Assets/Scripts/` 下只有 `Core` 与 `Unity` 两目录（find 核实），所谓 Gameplay 层只是 `Assets/Scripts/Unity/Gameplay/` 子目录（7 个文件），编译上属于 MyWorld.Unity；asmdef 只有 `MyWorld.Core.asmdef`（`noEngineReferences:true`）与 `MyWorld.Unity.asmdef`（references:[MyWorld.Core]）。README.md:26-29 / CLAUDE.md / AGENTS.md 均声称三层。置信度高。
2. **BlockInteraction.cs 1603 行 God object**：一个类承载 15+ 条右键路由（食物/药水/乐器/附魔书/弓/火枪/Boss 图腾/锄地/播种/骨粉/床/箱子/音乐盒/门/放置）+ 挖掘计时 + 裂纹 + 幽灵框 + 弓蓄力状态机 + 火枪装填 + 耐久磨损 + 碎裂 + 掉落生成 + 3 行 OnGUI 提示 + 蓄力条（`BlockInteraction.cs:291-389` UseAt 路由链亲读）。新交互 = 继续往里塞。置信度高。
3. **WorldBootstrap.Awake 382 行 33 个编号步骤**（0→33，含 7.4/7.5/7.6/7.7/7.8/9.5/22.5/23.5），顺序约束只活在注释里：7.5 音频必须先于 8/9（已实证踩坑）、22.5/24 必须先于 25 TryRestore、5 材质先于 7.8 VideoScreenSystem、12 先于 31 BindLightSampler（`WorldBootstrap.cs:85-467` 全读）。无任何编译期/测试期保护防止步骤被挪动——除了 PlayerAudioSystemTests.MountOrder 一个点。置信度高。
4. **"加生物 = 1 份 JSON + spawn_rules 一行，不动 C#"是假承诺**：m12 W4 提交 `cc19117` 实际改 8 个 C# 文件（Mob.cs +13 / MobAI.cs +193 / MobKind.cs +15 / MobSpawnRules.cs +13 / MobManager.cs +55 / MobView.cs +10 / MobModelLibrary.cs +10 / HelpMenuUi.cs +9），合计 +356 行 C#；全仓 `case MobKind.` 标签 246 个、分布在 8 个源文件（grep 统计）。置信度高（读了 diff）。
5. **静态可变全局状态网**：4 个单例（PlayerContext.Instance / QuestEventBus.Instance / PlayerAudioSystem.Instance / WorldBootstrap.CurrentWorld）+ 3 组静态注入（Explosion.BoundRegistry/BoundDrops `Explosion.cs:50-53`、MobAI.DropTable、EnchantStore.Default / BossSummonState.Default）+ 6 个静态事件（CombatEvents×3、BlockBroken、AfterDetonate、Enchanted）+ 2 个静态开关（PeaceMode / DifficultyMode）。跨测试夹具污染已被 CLAUDE.md 记为通病（6 个夹具要手工 `DifficultyMode.ResetCache()`）。置信度高。
6. **模态 UI 样板 5 处手抄**：`ShouldCloseOnMouseDown` + `BackgroundBounds` + OnGUI 前置判罚在 ArmorSlotsUi/ChestUi/CraftingInventoryUi/CraftingWorkbenchUi/WeaponPanelUi 各抄一份（每处含注释约 35-45 行，`WeaponPanelUi.cs:176-215`、`ChestUi.cs:186-200` 亲读）；CLAUDE.md 称"图鉴 CodexUi / 成就 AchievementUi 同样适用"，**两类均无该代码**（grep BackgroundBounds 无命中）。置信度高。
7. **C# 与 JSON 双真源漂移面**：`BlockInteraction.BreakTime` switch 硬编码方块秒数/门槛（`BlockInteraction.cs:1317-1361`，注释自认"静态函数拿不到注册表"，靠守卫测试对表）；music_box numericId=1064 手抄 const（`BlockInteraction.cs:395`）、iron_door=1005（`:680-681` 与 RedstoneSystem.IronDoorId 手动一致）、bullet=1608、planks=1000 等 magic id 散落。置信度高。
8. **dotnet 链只编译 Core + 测试**：`MyWorld.Tools.sln` 仅 3 工程（Core / Core.Tests / Preview），Unity 层约 2 万行源码无 dotnet 编译护栏；"双链同批"实为**同一批文件的两个不同子集**——121/202 个测试文件含 `#if UNITY_EDITOR`，117/202 引 UnityEngine（纯逻辑仅 85 个文件）。CLAUDE.md 承诺的 1039 vs 1813 差值（57%）即来源于此。置信度高。
9. **无 CI、无符号化策略**：无 `.github/`、无任何 CI 配置（核实）；质量门禁完全依赖开发者本机跑 `build-and-run.sh`（187 行，含逐步 grep 验证，中规中矩）；崩溃后无符号化栈可查，业务运行时日志近乎为零（源码 Debug.Log* 约 90 处，其中 Editor 工具占 52）。置信度高。
10. **词汇表/教学文案分散**：新增一个 QuestEventBus 事件词汇触 Core 枚举 + QuestCondition 解析语义 + AchievementSystem 判定 + CodexSystem 判定 + 1-2 个 Raise 点 + 2 份 JSON（quest/achievements）≈ 6 处；帮助菜单教学行是硬编码常量 + 固定 y 偏移 GUI.Label（`HelpMenuUi.cs:390-398`），加一行要手调面板高度。置信度高。

---

## 2. 度量数据表

### 2.1 行数（git ls-files + wc，核实）

- 源码 `Assets/Scripts/**.cs`：**32,634 行**（Core 13,862 / Unity 18,772，占比 42.5% / 57.5%，wc 核实）
- 测试 `Assets/Tests/EditMode/**.cs`：**37,382 行 / 202 文件**（与主审给定一致，核实）
- `.meta` 文件 1,215 个入库（Unity 资源 GUID 纪律，符合约定）

### 2.2 函数长度 / 圈复杂度 Top（启发式脚本 `exp/metrics.py`；局限：正则识别签名，漏表达式体成员、字符串内花括号未完全剥离、switch 的 case 计入 CC——结果为**下界估计**，仅用于排序）

| 函数 | 行数 | CC≈ | file:line |
| --- | --- | --- | --- |
| WorldBootstrap.Awake | 382 | 19 | Unity/Bootstrap/WorldBootstrap.cs:84 |
| MobAI.Tick | 141 | **41** | Core/Entities/MobAI.cs:143 |
| CraftingWorkbenchUi.OnGUI | 118 | 28 | Unity/UI/CraftingWorkbenchUi.cs:170 |
| CraftingInventoryUi.OnGUI | 99 | 22 | Unity/UI/CraftingInventoryUi.cs:187 |
| MobAI.TickBoss | 98 | 21 | Core/Entities/MobAI.cs:843 |
| BlockInteraction.UseAt | 98 | 23 | Unity/Player/BlockInteraction.cs:291 |
| WorldGenerator.Generate | 96 | 21 | Core/WorldGen/WorldGenerator.cs:94 |
| ChestUi.OnGUI | 96 | 18 | Unity/UI/ChestUi.cs:173 |
| MobManager.TickSpawn | 85 | 17 | Unity/Combat/MobManager.cs:436 |
| SaveLoadService.SaveNow | 83 | 12 | Unity/Persistence/SaveLoadService.cs:97 |

高复杂度（CC≥15）方法 30 个；≥60 行方法 34 个。CC 榜另有典型的 **kind 换算表**：MobManager.UsesPartTable(28)/MobKindToTypeId(28)/KillExperience(27)、MobModelLibrary.FileNameOf(28)、MobSpawnRules.ParseKind(27)、MobDropTable.ParseKind(18)——它们是"加生物要改 C#"的直接证据（见 §4b）。

### 2.3 依赖方向（asmdef 亲读核实）

```
MyWorld.Core.asmdef      references: []                 noEngineReferences: true   ← 编译器强制零 UnityEngine ✓
MyWorld.Unity.asmdef     references: [MyWorld.Core]                                 ← 单向向下 ✓
MyWorld.Core.Tests.asmdef references: [MyWorld.Core, MyWorld.Unity, TestRunner]     ← 测试引用两层 ✓
（不存在 MyWorld.Gameplay.asmdef —— "第三层"只是 Unity 程序集内的命名空间/目录）
```

Core 侧 `<Compile Include="Assets\Scripts\Core\**\*.cs">` + `EnableDefaultCompileItems=false`（csproj 亲读），Unity 侧 asmdef 目录自动纳入——**两份清单都是目录通配，不会各自漂移**（这是该机制最聪明的一点）。代价：dotnet 链不编译 Unity 层（见 Top10-8）。

### 2.4 God object 职责簇

- **PlayerContext**（Unity/Gameplay/PlayerContext.cs，201 行）：公开可变字段 17+（Inventory/Health/Time/Items/Recipes/Experience/Death/HungerSystem/FurnaceSystem/FarmSystem/BreedingSystem/ChestSystem/BedSystem/ItemDrops/DeathScreen/Achievements/Codex/Potions/ArmorSlots）+ 单例 + 装备三属性计算（RefreshGearBonuses 内嵌铁套判定与任务事件触发 `PlayerContext.cs:105-159`）。它是"万物汇聚点"——每个新系统的第一件事就是往这里挂字段（注释自述"照 FurnaceSystem 挂法"）。
- **BlockInteraction**：见 Top10-2，职责簇 = 选择高亮 / 挖掘计时 / 放置路由 / 15+ 右键交互 / 弓 / 枪 / 耐久 / 掉落 / 提示 UI。
- **WorldBootstrap**：组合根 + 33 步装配 + 换世界握手 + 自适应分辨率。

### 2.5 重复代码

- 点外关闭样板 ×5（§Top10-6，量化见 §4c）。
- `case MobKind` 换算表 ×8 文件（§4b）。
- UI 面板 OnGUI 的"居中矩形 + GUI.Box + 标题 + 关闭按钮"前奏在各面板手写（CraftingInventoryUi/ChestUi/WeaponPanelUi 等对比亲读，几何参数各自微调，刻意不同宽——属可容忍的浅重复）。
- ItemSlotDrawer 纪律**良好**：15 个 UI 文件全部引用公共绘制器，未发现绕行画黑字数量的物品格（grep 核实）。

---

## 3. 分层与可测试性

**Core 无接口的具体类如何 mock？** 答案是**不 mock，直接构造真对象**。Core 仅 3 个 interface（IBlockSource/ISolidBlockSource/ILightVolume，全在热路径泛型约束上）。这符合体素引擎的数据局部性诉求，且 Core 全确定性（整数哈希、无随机对象、无时钟注入——时间由调用方喂 `Time.time`），因此"真对象测试"成本低、稳定。**真正缺的接缝**是：静态注入点（Explosion.BoundRegistry 这类 `{ get; set; }` 全局）与全局单例——它们不是接缝而是**焊死的缝**，测试只能靠 ResetCache/重绑来擦除状态。

**Unity 层测试构成**：202 文件中 117 个引 UnityEngine（接线/结构断言），85 个纯逻辑。EditMode 对 MonoBehaviour 生命周期的覆盖是**点状深、面上浅**：Awake/OnEnable/OnGUI 都靠"直调 public 方法绕过 Input"模式（BlockInteraction.UseAt/TryTickDig/BreakAt 全为此开 public，注释自述）；挂载顺序仅 PlayerAudioSystem 一处有 MountOrder 结构断言。没有 PlayMode 测试，实机链依赖 `--ui-shot` 截图管线（本次禁跑，未验证其现状）。

**永真断言金丝雀扫描**：`Assert.IsNotNull(单标识符)` 孤立断言 = **0 个**（grep，启发式；不能保证扫出所有弱断言，但至少没有最浅的那类）。守卫测试文化是本仓最亮的资产：数值对表（BreakTime vs JSON hardness 逐块一致）、文案锁 const、贴图需求差集恒为空——这些把"文档漂移"变成了"测试变红"。

---

## 4. 扩展性走查（a–h，逐项实证）

| # | 场景 | 评级 | 改动点 | 霰弹式？ | 编译期保护 |
| --- | --- | --- | --- | --- | --- |
| a | 新方块+物品+配方+掉落 | 🟢（普通方块）/ 🟡（可交互方块） | JSON×4 + art 需求；可交互则 +BlockInteraction 路由与 const | 否 / 是 | 部分（守卫测试） |
| b | 新生物 | 🔴 | JSON×2 + **8 个 C# 文件 ~356 行**（cc19117 实证） | **是**（case 标签 246 个/8 文件） | 无 |
| c | 新模态 UI 面板 | 🟡 | ~40 行样板×1 + 指针门 + Esc + 帮助行 + 测试 | 半（5 处已抄） | 无 |
| d | 新 QuestEvent 词汇 | 🟡 | Core 枚举+条件语义 + 成就/图鉴判定 + Raise 点 + JSON×2 | 是（≈6 处） | 部分（悬空引用有集成测试） |
| e | 新 Core 子系统接线 | 🔴 | WorldBootstrap 步骤 + PlayerContext 字段 + SaveLoadService 快照 + tick 宿主 | 是（≥4 文件） | **无**（顺序只活在注释） |
| f | 新存档字段 | 🟡 | LevelData + Codec + Collect/Apply + 系统 Export/Import + 测试 | 中（4-5 文件） | 好（缺字段=null 兼容） |
| g | 新音频/贴图 | 🟢 | 放 ogg/png 即生效；缺文件程序生成兜底 | 否 | 无需 |
| h | 新静态模式开关 | 🔴 | 新类 + 6+ 个测试夹具 TearDown 手工 Reset | 是 | 无（污染已实证） |

**(a) 方块+物品+配方+掉落**：`blocks/*.json`（numericId 缺省自动按字母序分配）→ `items/*.json`（带 blockId 可放置）→ `recipes/*.json` → `blocks/drops/block_drops.json`，普通方块确实零 C#——BreakTime 的 default 分支自 m11 起采信 JSON hardness（`BlockInteraction.cs:1351-1355`），即挖判定走 hardness<0.05。**但**：① art 流程耦合——贴图须在 `art/requests/blocks/` 立项并维护 `art/README.md` 索引，BlockDefinitionFilesTests 校验差集恒为空（好约束，代价是流程步数）；② numericId 段位是**文档口头约定**（"1064+ 预留"），music_box 已占 1064，下一个该用 1065——没有任何代码阻止你写 1062 撞车（有 ID 冲突测试兜底，属事后红）；③ 任何带交互的方块（音乐盒/箱子/门）都要在 BlockInteraction 加 const + 路由分支。综合 🟢→🟡。

**(b) 生物**：承诺与事实的直接矛盾。cc19117 diff：9 个新 kind 需要改 MobKind.cs（枚举+ParseKind 字符串表+报错文案）、Mob.cs（Create switch 硬编码 Health/MoveSpeed）、MobManager.cs（UsesPartTable 白名单 / MobKindToTypeId / IsHostileSpawn / KillExperience / Day/Night/Aquatic 候选数组）、MobAI.cs（行为分发 + 全新 TickAquatic/TickFlyer 193 行）、MobView.cs、MobModelLibrary.cs（FileNameOf）、HelpMenuUi.cs（教学行）。"白名单 5 处"即 MobManager 内的 kind 换算函数群。**根因**：生物属性（血量/速度/经验/敌对性/体型）从未进 JSON——模型造型外置了（I1），数值没有。评级 🔴。

**(c) 模态 UI**：新面板需要——① OnGUI 前奏 + 背景 Box（每面板手写，约 15-25 行）；② `BackgroundBounds` 属性 + `ShouldCloseOnMouseDown(EventType,int,bool,Vector2)` 谓词（5 处各约 35-45 行含文档注释，`WeaponPanelUi.cs:176-215` 亲读）；③ 开/关时 `UiCursorGate.Open()/Close()`（计数制指针门，设计好）；④ Esc/E 键处理；⑤ 帮助菜单教学行（可选）；⑥ EditMode 断言（ModalCloseOutsideTests 460 行已存在）。合计**新文件 ~250-350 行 + 手抄样板 ~40 行**。无基类/无组合组件，方案 A（每 UI 自管）是 m13 W4 的明示决策——5 处以内可忍，第 6 处起应抽 ModalPanelUi。评级 🟡。

**(d) QuestEvent 词汇**：`Core/Quests/Quest.cs:15-43` 枚举 12 值（亲读）；QuestCondition 按类型区分"累计 vs 背包现存量"语义（`QuestEventBus.cs:97-100` 注释）；消费侧 AchievementSystem.OnEvent / CodexSystem.OnQuestEvent 在 `QuestEventBus.Raise` 前置转发（`QuestEventBus.cs:118-127`）；Raise 点 15 处分布 7 文件（grep）。新增词汇 = 枚举 + 条件解析 + （可选）成就/图鉴判定 + 玩法侧 Raise 点 + quest/achievements JSON。分散度中等，但**词汇语义文档只活在枚举注释里**。评级 🟡。

**(e) Core 子系统接线**：完整清单（以 m11/m12 各系统为样板）：WorldBootstrap 建实例挂 PlayerContext（还要挑对步骤号——22.5 的注释明说"必须在步骤 25 TryRestore 之前"）+ PlayerContext 加公开字段 + SaveLoadService.CollectLevelData/Apply* 各一段 + tick 宿主（FarmingHost 模式）+ 帮助菜单教学行 + 测试。**顺序依赖无编译期保护**，步骤 7.5 教训是实证：挪后则三音全哑，且只有一条 MountOrder 测试守着那一个点。评级 🔴。

**(f) 存档字段**：LevelData.cs 字段 + SaveLoadService.CollectLevelData（`:239-289`）+ Apply*（`:323-401`）+ 系统自持 Export/Import + 往返测试。设计上乘：Newtonsoft 缺字段=null 自然兼容、坏层不拖垮好层、Codec 走 tmp+File.Replace 原子写（`LevelDataCodec.cs:15-31` 亲读）、坏档隔离 `.corrupt`。4-5 文件但每处都薄。评级 🟡（偏绿）。

**(g) 音频/贴图**：`MobAudioSystem.cs:59-76` 约定路径 `Audio/Mobs/<kind小写>-{idle,hurt}` 缺文件回退 generic——**放两个 ogg 确实零代码**（核实）；PlayerAudioSystem Resources 优先真资源、缺 .ogg 程序生成兜底（PlayFire/PlayClick 等亲读）。贴图走 art 程序占位快路径同理。评级 🟢（本仓数据驱动承诺兑现最好的一条）。

**(h) 静态开关**：PeaceMode（Unity 侧 PlayerPrefs+缓存）/ DifficultyMode（Core 纯 bool）双例已带来 6 个夹具 TearDown 手工 ResetCache 的维护税（CLAUDE.md 自认）。更好的模式：① 开关集中到一个 `GameOptions` POCO 经 PlayerContext 下发；② 或测试侧用 NUnit `[SetUpFixture]` 反射扫描所有 `ResetCache` 统一复位。现状每加一个开关就多一笔散落税。评级 🔴。

---

## 5. 横切一致性

**异常处理范式**：读容忍/写严格在 Core 贯彻（GetBlock 空气 vs SetBlock 抛）；Unity 层 57 处 catch、无空 catch（grep 核实）；WorldBootstrap 8 个 try 全部"warn + 降级继续"。**代价**：降级是单向静默的——achievements.json 坏了 = 整局无成就无提示重试路径，孩子视角是"功能消失"。可接受但应至少在帮助菜单亮降级横幅。

**唯一入口契约**（grep 全量核查）：
- 玩家受伤：`PlayerController.TakeDamage` 3 个玩法调用点（MobManager:135 / PickaxeShard:219 / 摔落饥饿内部）+ **1 个文档化兜底直写** `MobManager.cs:146 ctx.Health.Damage(ev.Amount)`（无 PlayerController 的纯逻辑场景，注释明示是旧路径保留）。契约基本成立，兜底是灰区。置信度高。
- mob 受伤：`MobAI.TakeHit` 唯一（`MobAI.cs:331` 是其内部实现）。✓
- 血量真源 PlayerContext.Health：UI/Combat 读写同源 ✓（ApplyPlayer 整体替换实例属恢复语义）。
- 昼夜判定：BgmAudioSystem/AmbientAudioSystem 均调 `MobManager.IsNightPhase(dayPhase)`（`BgmAudioSystem.cs:130`、`AmbientAudioSystem.cs:60` 亲读）✓；QuestEventBus 直接用 NightStartTick/NightEndTick 做跨夜/睡觉的**边沿检测**——语义不同（跨刻 vs 相位），属合理派生，非违约。

**日志**：业务运行时近零日志（Core 构架上不能引 UnityEngine，也无自有日志门面）。实机"音效全哑/功能消失"类问题只能靠复现。这是单机小游戏可接受的取舍，但与"孩子在旧包验收"的历史教训叠加时，排查成本高。

**命名与注释密度**：中文决策注释密度极高且**记录了为什么**（取舍/教训/反例），例如 `WorldBootstrap.cs:47-53` 解释为何场景重载而非就地重绑。这是本仓最值钱的知识资产。

**魔数分布**：numericId 段位（0-6 内置 / 1000-1063 矿石 / 1064 music_box / 1607-1619 物品 / 1700-1701 早期）、PlayerPrefs 键名（m6.* / BabyMode / PeacefulMode / WorldCatalog.LastSeed）、WorldBootstrap 步骤号——**真源是 CLAUDE.md 的散文**，代码里是散落 const。无集中 registry，已发生两处漂移（见 §7）。

---

## 6. 构建、质量基建与仓库卫生

- **双链机制**：同批文件 + 双侧目录通配，不会清单漂移（好）。但 dotnet 侧无 Unity 工程，Unity 层 ~2 万行只有 Unity 编辑器一条编译链；`TreatWarningsAsErrors` 仅 Core（Unity 侧 csproj 由引擎生成，加不了——但可以在 build-and-run.sh 里 grep Unity 编译日志的 warning 数做门禁，目前没有）。
- **CI**：不存在（无 .github / .gitlab-ci / azure-pipelines，核实）。dotnet test 无需 Unity 即可跑——**这是最便宜的未兑现基建**。
- **发布物**：build-and-run.sh 187 行逐步 grep 验证、`set -e` 风格纪律好；但无符号保留策略、无崩溃日志回收约定（Player.log 分析靠人）。静态推断，未实跑。
- **仓库卫生**：`.gitignore` 含 `[Bb]uilds/`、`art/incoming/*`（保留 README）、unity-*.log（核实）；入库总体积 44.5MB（ogg 36 条 6.7MB 最大单文件、mp4 5 条 4.5MB 最大、png 356 张）——体积健康；根目录仅 8 个配置/文档文件，干净；`.editorconfig` LF+缩进纪律明确；`.superpowers/sdd` 工作区已清（git ls-files 无痕）。
- **art/README.md**："程序占位" 24 处标注，索引纪律执行中。P2 美术批 71 项卡配额（status 2056）的说法无法离线验证，标为**未验证**。

---

## 7. 测试体系与文档漂移清单

### 7.1 测试计数（静态核实）

- EditMode：`[Test]` 1466 + `[TestCase]` 347 = **1813**，与 CLAUDE.md/AGENTS.md/README 声称**精确吻合**（grep 计数；TestCase 多参数合行时会少计，但恰好人头对齐，置信度高）。
- dotnet 1039/1039：**未验证**（禁跑）；静态推断可信——85 个纯逻辑文件构成其子集。
- 最大测试类：HelpMenuUiTests 673 / BlockInteractionUseRoutingTests 589 / MobModelLibraryTests 536 / QuestEventBusTests 536（前 12 名均 >400 行）。测试目录与源码目录 1:1 镜像（23 个子目录），可导航性好。
- 覆盖缺口：PotionHost **0** 个测试文件引用（grep）；SaveLoadService 8 个测试文件、WriteExecutor 可注入设计优秀，但真 Task.Run 竞态路径无专项并发测试（注入的都是同步/假执行器）；性能/压力/PlayMode 全缺；WorldCatalog 仅 1 文件（删除流有覆盖但薄）。
- 测试自身的债：以复制粘贴断言块为主的大类存在（HelpMenuUiTests 文案锁定类占多），可忍。

### 7.2 文档漂移清单（逐条对代码核对，置信度高除注明）

| # | 声称 | 事实 | 判定 |
| --- | --- | --- | --- |
| 1 | 三层架构 `Assets/Scripts/Gameplay`（README:8,26 / CLAUDE.md 速览 / AGENTS.md） | 无该目录、无该 asmdef；Gameplay 代码在 Unity 程序集内 | **漂移（架构级）** |
| 2 | "加生物 = 1 份 JSON + spawn_rules 一行，不动 C#"（CLAUDE.md/AGENTS.md 数据驱动表） | cc19117 改 8 个 C# 文件 +356 行 | **漂移（承诺失实）** |
| 3 | CLAUDE.md m12 节内自相矛盾：头部"全部波次已落地（含 W4）" vs 正文"内容与收集（第 1 波规划，**未实施**）…W4 水生飞行生物未做" | 代码已落地（TickAquatic/TickFlyer 在 MobAI.cs:481/396 实测存在） | **漂移（同文档内矛盾）** |
| 4 | "方块最大 1062（1064+ 为 m12+ 预留段）"（CLAUDE.md） | `music_box.json` numericId=**1064** 已占用（grep 排序核实），BlockInteraction.cs:395 同值 const | 漂移（数字过时） |
| 5 | "物品 1607/1608 已被 musket/bullet 占用…新增建议 1609-1699 顺延"（CLAUDE.md"现状"段） | 1609-1619 已被 m12 药水×6/乐器×3/glass_bottle/music_box 占满（逐文件核实） | 漂移（现状段停留在 m13 W3） |
| 6 | "图鉴 CodexUi / 成就 AchievementUi 同样适用（点外关闭）"（CLAUDE.md m13 W4） | 两类无 BackgroundBounds/ShouldCloseOnMouseDown（grep 零命中） | **漂移（能力虚报）** |
| 7 | PlayerAudioSystem"17 个 Play* 方法" | Play<Event> 恰 17 个（含 m9 PlayHit；文档分解漏列它）+PlayTone/PlayTwinkleTune 共 19 入口 | 数字对、分解微错 |
| 8 | 36 ogg / 5 mp4 / 16 成就 / 26 图鉴 / MobKind 上限 36、"从 37 起" | 全部逐项核实**吻合**（36/5/16/18+7+1=26/36/37） | ✓ 无漂移 |
| 9 | "12 类 JSON 注册表" | blocks/items/recipes/biomes/spawn_rules/drop_tables/models/trees/flowers/block_drops/quests/achievements = 12 ✓ | ✓ |
| 10 | "MobKind 数值 1-27 已固定（5 旧 + 9 被动 + 3 敌对 + 村民 + Boss=27）" | 枚举 30 成员、27 个显式赋值；算式 5+9+3+1+1=19≠27 | 算术不闭合（速记错误） |
| 11 | EditMode 1813 / dotnet 1039 | 1813 ✓（属性计数）；1039 未验证 | 半核实 |
| 12 | 图鉴"18 生物" | CodexSystem.MobEntries 确为 18，**不含 m12 W4 的 9 种水生/飞行生物**（MobKind 28-36） | 内容缺口（非文档错，但 README 宣传 26 条目会让人以为全覆盖） |

**知识管理可导航性**：三份根文档 + 19 specs + 26 plans + specs/visual-text-conventions 的结构对新人优秀；漂移集中在"现状数字"与"能力承诺"两类——恰是最容易被孩子验收戳穿的类别。CLAUDE.md 单文件已 500+ 行，接近"必读=没人读"阈值，建议拆分。

---

## 8. 值得保留的工程实践（重构时不要破坏）

1. **Core asmdef 编译器强制隔离**（noEngineReferences）——分层纯度的真源，勿以"就一个 Vector3"开口子。
2. **双链同批文件 + 双侧目录通配**——零清单维护的测试复用机制。
3. **守卫测试文化**：数值对表、文案锁 const、贴图差集恒空、五向命中的参数化测试——把文档漂移变红。
4. **读容忍写严格 + 确定性整数哈希**（无随机对象、无隐藏时钟）。
5. **SaveLoadService 快照-后台写-单写者-版本守卫清脏**（`SaveLoadService.cs:97-235`）与 LevelDataCodec 原子写——存档工程质量高于多数商业项目。
6. **UiCursorGate 计数制指针门**、ItemSlotDrawer 公共绘制器、UrpMaterialFactory 材质工厂——基础设施复用的三个正面样板。
7. **中文决策注释**（记录 why 与教训）+ 验收剧本标注包 mtime 的收口制度。
8. **art 程序占位快路径**——美术流水线不阻塞玩法迭代。

---

## 9. 被推翻的假设 / 已验证无问题

**被推翻**：
- "三层架构（Core/Unity/Gameplay 三个程序集）" → 实为两层（find + asmdef 亲读）。
- "加生物零 C# 改动" → cc19117 +356 行 C#（diff 亲读）。
- "点外关闭 7 处（含 Codex/Achievement）" → 5 处。
- "dotnet build 编译三个工程 = 三层各一" → 三工程是 Core/Tests/Preview，Unity 层不在内。

**已验证无问题（曾怀疑）**：唯一入口契约无恶意旁路（MobManager:146 为注释明示的兜底）；IsNightPhase 无违约（BGM/Ambient 都走它）；ogg/mp4/成就/图鉴/MobKind/JSON 类数全部对上；LevelDataCodec 原子写实现正确（netstandard2.1 无 overwrite 重载的 File.Replace 分支处理对）；WorldCatalog 回收站式删除实现正确；ChunkStreamer 毫秒预算 + StopwatchMillis 可注入；永真断言扫描 0 命中；ItemSlotDrawer 无绕行。

**未验证（如实声明）**：dotnet 1039 通过数（禁跑）；P2 配额 status 2056；build-and-run.sh 实际执行质量；`--ui-shot` 截图管线现状；崩溃符号是否实际随包；EditMode 1813 是否全绿（只数了属性数）；HelpMenuUiTests 673 行内的断言质量抽查未逐条读。

---

## 10. 演进路线图（按 ROI 排序，全部可独立成 commit）

| ID | 项 | 动机 | 做法（增量步骤） | 风险 | 量 | 收益 |
| --- | --- | --- | --- | --- | --- | --- |
| R1 | **文档对齐现实** | 6 处硬漂移已在孩子验收面暴露风险 | 只改 md：删"三层"改"两层+Gameplay 命名空间"；加生物条目改"JSON+属性表+C# 行为分发"；1064/1609-1619 现状更新；点外关闭改"5 处（Codex/Achievement 待接入）"；CLAUDE.md m12 节删矛盾段 | 无 | **S** | 立即消除错误预期 |
| R2 | **CI 最小化** | 质量门禁目前=开发者记忆 | GitHub Actions：`dotnet test tools/dotnet/MyWorld.Tools.sln`（无需 Unity）+ `.editorconfig` 检查；后续加 Unity EditMode 自托管 runner | 无（只读仓库即跑） | **S** | 每次提交自动红/绿 |
| R3 | **MobKind 属性表外置** | 消除最大霰弹点（8 文件 246 case） | 新 `mobs/stats.json`（health/speed/exp/hostile/size/audioClass）；先让 MobManager.Create/MobKindToTypeId/KillExperience 读表（保留 switch 作默认值）；测试对表；ParseKind 用 Enum.TryParse 替换手写 switch | 中（改刷怪热路径，需守卫测试护航） | **M** | 加生物从 8 文件→2 处 |
| R4 | **右键路由策略链** | BlockInteraction 已 1603 行 | 定义 `IUseHandler { int Priority; bool TryUse(Context); }`；每条现有路由抽成小类（一次一个 commit，先抽 3 条最新的：药水/乐器/火枪）；BlockInteraction 只持有序列表 | 中（路由顺序=行为，需参数化测试钉死） | **M** | 新交互=1 新类，God object 停止生长 |
| R5 | **模态 UI 基类** | 样板第 6 份即将出现 | 抽 `ModalPanelUi`（含 BackgroundBounds/ShouldCloseOnMouseDown/指针门配对/Esc）；先做一个新面板试点，旧 5 个逐个迁移 | 低 | **S** | 删 ~200 行重复；Codex/Achievement 顺势接入点外关闭 |
| R6 | **装配顺序测试化** | 步骤序教训已两次实证 | WorldBootstrap 拆 `Phase_Early/Phase_Player/Phase_Systems/Phase_Restore` 私有方法 + 一条 EditMode 结构测试断言关键先后（音频先于 Player/BlockInteraction、TryRestore 最后）；不改行为 | 低 | **S** | 挪步骤立刻红 |
| R7 | **静态开关测试卫生** | 6 夹具手工 ResetCache 税 | `[SetUpFixture]` 里反射收集全部 `static void ResetCache()` 统一调用；新开关零成本接入 | 低 | **S** | 根除一类污染 |
| R8 | **运行时最小日志面** | 实机"功能消失"排查靠复现 | Unity 层加 `GameLog`（带 m12 降级 warn 的汇聚点 + 帮助菜单"系统状态"页显示降级项）；Core 不动 | 低 | **S/M** | 孩子报障可自查 |
| R9 | **BreakTime 表进注册表** | C#/JSON 双真源靠测试对表维持 | BlockRegistry 加载时缓存 (numericId→baseSeconds,minToolTier)；BreakTime 改读注入表；旧静态签名保留给测试作纯函数对照 | 中（守卫测试需重写口径） | **M** | 挖矿数值单一真源 |
| R10 | **Unity 层 dotnet 编译护栏** | 2 万行只有一条编译链 | tools/dotnet 加 MyWorld.Unity.Tests 工程，引用 Unity 安装目录的 UnityEngine.dll（仅编译不运行，Input/UI 代码用 `#if` 或排除） | 高（UnityEngine API 面大，维护成本持续） | **L** | PR 期即抓编译错（可暂缓，CI+EditMode 已覆盖大半） |

**先做什么**：R1 + R2 一个下午完成，立刻把"文档撒谎"和"门禁靠记忆"两个外部性最大的问题清零；随后 R5/R6/R7 三个 S 级清掉已实证的重复与隐患；R3/R4 是扩展性的主战场，各需一个独立里程碑，务必沿用本仓 spec→plan→SDD 流程并保持双链只增不减。

---

*报告完。静态评审的边界：所有行号为 commit `f0eb3ea` 时点；Unity 编辑器侧行为、实机表现、配额状态均未触碰。*
