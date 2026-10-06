# 08 · 功能扩展机会评审（MyWordGame.exe）

- 评审日期：2026-10-06；基线：HEAD `f0eb3ea`（m12 全波次收口 + m13 全 8 commit，仓库 clean）
- 方法：一手精读（Core 全目录树、`BlockInteraction`/`PlayerController`/`VillageFeature`/`RedstoneSystem`/`FurnaceSystem`/`BreedingSystem`/`VillagerOffers`/`EnchantSystem`/`QuestEventBus` 等关键源码逐段读）+ 4 个只读探查代理交叉盘点（StreamingAssets 全部 JSON、Unity 侧全部 UI/接线、美术/孤儿资产/用户存档、里程碑设计文档全量）+ `blocks/items/recipes` 全量文件名与内容 grep。**未启动游戏、未跑任何配额脚本**。
- 证据标记：[V] = 亲自读到代码/JSON；[A] = 探查代理报告且经我抽查认可；[I] = 推断，注明依据。
- 交叉引用：07-ux（交互问题）、06-architecture（复杂度热点，`WorldBootstrap.Awake` 382 行 / `MobAI.Tick` CC≈41——本报告的接线类提案都要求"新宿主组件、不动 WorldBootstrap 热点"）。

## 摘要（一段话）

孩子的两批书面/口头需求（m12 三根因 + m13 五条）**已全部落地且双链绿**——当前是该项目第一次进入"主动挖掘需求"阶段。但**用户存档画像改变了排序**（见 §2.7）：主力世界（seed 42）成就 2/16、图鉴 0/26、任务停在第一章第 2 步"合成木板"，药水/乐器/附魔/穿戴/农业/Boss 在真实存档里零触碰——**孩子的瓶颈是早期循环的可达性与可发现性，不是内容总量**。因此第一优先是 F0 内容断链修复批（十处"看得见够不着"的死胡同，大半纯 JSON）与 F1 帮助菜单 m13 补课；其次把"已入库未接线的零件"接通：钓鱼（成就/附魔占位/药水/水生生物四件套已为它预留）、剪羊毛（任务文案已承诺）、纯 JSON 第三章任务链。另有两项盘点修正值得注意：P2 美术批 71 项**不含任何 mob/物品/方块贴图**（全是氛围/主菜单/季节/营销），而孤儿贴图堆里有 diamond-ore/stairs-*/bread 等**未来功能的美术已经躺在仓库里**。楼梯/台阶按 2026-08-17 评估文档走"先台阶后楼梯"的既定路线放 Later。

---

## 1. 现有功能清单（功能 | 入口 | 成熟度）

成熟度定义：**成熟**=有 UI/接线/测试三件套；**可用**=能用但明显简化或有欠账；**骨架**=核心逻辑在、体验缺胳膊少腿；**孤儿**=代码/资产在但没接线。

| 域 | 功能 | 入口 | 成熟度 | 证据 |
| --- | --- | --- | --- | --- |
| 世界生成 | 地形+海平面+洞穴（5 群系：Plains/Desert/Forest/Mountains/Snow） | seed 确定 | 成熟 | [V] biomes.json 5 群系；WorldGenerator.cs |
| 世界生成 | 矿脉 4 种（金/粗铁/合金/机元，FNV-1a 世界坐标哈希嵌矿） | 自动 | 成熟 | [V] OreFeature；CLAUDE.md m10 |
| 世界生成 | 树 8 种 + 花草 12 种（vegetation JSON 外置） | 自动 | 成熟 | [V] vegetation/*.json |
| 世界生成 | 村庄（400 格村格哈希 + 水井 + 3-5 栋 5×5 楼，蓝图盖章跨区块） | 自动，约每 400×400 一村 | **可用（空壳）** | [V] VillageFeature.cs + WorldGenerator.cs:134,186 亲核接线；楼内仅原木/木板/玻璃/门洞，**无家具无箱子** |
| 世界生成 | **没有**的结构：沉船/矿井/要塞/神殿/埋藏宝藏 | — | 缺失 | [A] Loot/Treasure 全库零匹配 |
| 挖掘放置 | 挖掘计时（蓄力+裂纹五档+门槛矩阵+效率附魔） | 按住左键 | 成熟 | [V] DigProgress + BlockInteraction.cs:482 |
| 挖掘放置 | 六向放置 + 幽灵框 + 跳跃垫脚 tower-up | 右键 | 成熟 | [V] PlacementGhostUi/BlockPlacement（m12 P0） |
| 挖掘放置 | 床/门双格原子放置、通用 blockId 放置路由 | 右键 | 成熟 | [V] UseAt 路由表 BlockInteraction.cs:29 |
| 生存 | 血/饥/摔/夜/重生无敌帧 | 自动 | 成熟 | [V] PlayerController.TakeDamage 唯一入口 |
| 生存 | 床睡觉+重生点（BedSpawnPoints 进存档） | 右键床 | 成熟 | [V] BedSystem.RespawnPoint |
| 生存 | **无氧气/溺水/游泳差异**（水不挡移动也不闷人） | — | 缺失 | [V] 全库 oxygen/drown 零匹配；水肺药水=+4 心上限占位 |
| 战斗 | 近战（同键分流 4m mob 优先）+ 击退/闪红/命中音 | 左键 | 成熟 | [V] CombatController/MobHitFeedback |
| 战斗 | 弓（抛物线 60m 蓄力）+ 火枪（直射 25m 装填 1.5s） | 右键 | 成熟 | [V] items range 字段 + ProjectileEntity |
| 战斗 | 36 种生物（12 被动可繁殖 + 3 敌对 + 村民 + Boss + 5 水生 + 4 飞行），spawn_rules 26 条自然刷新 | 自动 | 成熟 | [V] MobKind.cs 1-36 + spawn_rules.json |
| 战斗 | Boss 机元守卫（2×2 图腾召唤、三招状态机、半血召骷髅） | 右键图腾 | 成熟 | [V] MachineGuardianSummon + MobAI.TickBoss |
| 战斗 | 怪物血条 3s 淡出 + Boss 常显；宝宝/普通难度 | 自动 | 成熟 | [V] MobView/MobHealthBarTimer/DifficultyMode |
| 战斗 | 盾牌（手持减伤 ×0.5 + 耐久） | 手持 | 成熟 | [V] PlayerController.cs:224 ApplyShieldMitigation |
| 物品 | 合成（1×1 口袋 B / 2×2 背包 E / 3×3 工作台 P）+ SHIFT+click 入料 | B/E/P | 成熟 | [V] 75 个配方 JSON |
| 物品 | 熔炉（4 对硬编码：粗金/粗铁→锭、沙→玻璃、1003→1004） | F 键 | **可用（不可扩展）** | [V] FurnaceSystem.TryGetSpecialSmelt 硬编码；**recipes JSON 无熔炉 tier**（文档漂移，见 §2） |
| 物品 | 箱子（27 行式 + Cursor 手持 + Shift 转移） | 右键箱 | 成熟 | [V] ChestSystem/ChestTransfer/ChestUi |
| 物品 | 交易（3 职业 × 6 模板池，位置哈希挑 3-5 条） | V 键 | **可用（池硬编码 C#）** | [V] VillagerOffers.cs 非 JSON |
| 物品 | 附魔（锋利/效率/耐久三种生效，MaxLevel=3；真附魔走**附魔书右键融合**） | 书右键 | **可用（X 键附魔台是"扣真资源不产出"的占位）** | [V] 亲核 `EnchantingUi.DoEnchant`：真扣青金石+经验、只掷 Sharpness 出 `Debug.Log` 提示、**不写 EnchantStore**（m10 终审刻意不改写工具栈）；RollBookKind 三分支可出全部 3 种；等级口径 1-5 vs MaxLevel=3 不一致 |
| 物品 | 附魔书（书+青金石→未鉴定书→融合三选一） | 右键 | 成熟 | [V] RollBookKind |
| 物品 | **LuckOfTheSea 附魔占位不生效** | — | 孤儿（待钓鱼） | [V] Enchantment.cs:12 注释"占位" |
| 物品 | 盔甲 4 槽穿戴 + gearBonus 汇总 + 铁甲套彩蛋 | 背包侧栏 | 成熟 | [V] ArmorInventory/PlayerContext.RefreshGearBonuses |
| 物品 | 药水 6 瓶（5 buff 30s + 即时治疗） | 右键喝 | 成熟 | [V] PotionSystem + items/potion_*.json |
| 物品 | 乐器 4 件（鼓/笛/铃各一个固定音）+ 音乐盒（小星星 8 音） | 右键 | **骨架（不能自由演奏）** | [V] PlayerAudioSystem.PlayTone 已能合成任意频率，但每种乐器只绑一个音 |
| 物品 | 工具耐久（8 位编码）+ 碎裂伤害 + 热键栏耐久条 | 自动 | 成熟 | [V] ApplyDigDurability |
| 物品 | 铲/斧 6 件物品可合成可附魔，**但挖掘速度无工具类别加成**（只有镐 tier 门槛矩阵） | — | 骨架（功能近闲置） | [V] BreakTime 仅 pickaxe 分支；Unity 侧 shovel 零匹配 |
| 农牧 | 农耕 3 作物 3 阶段 + 湿地 ×2 + 骨粉催熟 + 树苗生长 | 锄/右键 | 成熟 | [V] FarmSystem/SaplingGrowth |
| 农牧 | 繁殖 12 种被动生物（发情 30s/孕期 30s/幼崽 0.5 缩放 600s 长大） | 手持饲料右键 | 成熟 | [V] BreedingSystem.cs 全读 |
| 农牧 | **无蛋、无奶、无驯服、无剪毛**（羊毛只能杀羊/交易） | — | 缺失 | [A] Egg/Tame/Shear 零匹配；**任务 ch2_01 文案却写"或剪羊"** |
| 进度 | 任务双章 16 步（链式解锁、多章节 Campaign、进度进 level.dat） | 右上 HUD | 成熟 | [V] QuestCampaign/QuestEventBus（12+ 事件词汇） |
| 进度 | 成就 16 枚（含 allOthers 终极成就；徽章美术已入库） | H→成就页 | 成熟 | [V] achievements.json 亲数 16 |
| 进度 | 图鉴 26 条目（18 生物+5 矿石+3 植物；首次遭遇解锁） | H→图鉴页 | **可用（18 生物无专属卡面）** | [A] CodexSystem 硬编码条目；内容卡仅矿石 5+植物 3（**不在 71 项待生成内**——codex 27 资源已全部入库，生物卡面属新立项） |
| 进度 | **fisherman 成就文案自认"钓鱼系统实装后改条件"**（现为收集 5 骨头） | — | 孤儿钩子 | [V] achievements.json:17 |
| 世界管理 | 多世界（继续上次/新世界种子/列表删除回收站式） | 主菜单 | 成熟 | [V] WorldCatalog（m12 P1） |
| 世界管理 | **无备份/导出/克隆** | — | 缺失 | [A] |
| 音频视频 | BGM 三态交叉淡化 + 环境循环 + 17 事件音 + 生物叫 + 5 视频 | 自动 | 成熟 | [V] CLAUDE.md av 节 + Audio 目录 |
| 设置 | 4 滑条（灵敏/音量/FOV/音乐）+ 和平 + 宝宝 | H/Esc 设置页 | 成熟 | [V] SettingsPanelUi |
| 设置 | **无视距/画质档**（LoadRadius=6 写死；FOV 60-90 与 F11 是仅有的图形项） | — | 缺失 | [V] ChunkStreamer.cs:48 可写属性但无人改 |
| 视角 | 第三人称 F5（身后 3 格简化版，无缩放/肩视） | F5 | **可用（帮助菜单没写）** | [V] CameraThirdPerson.cs:6-11 |
| 飞行 | 双击空格/F 切换、6 向 8m/s、受伤豁免、存档不记 | F/双击空格 | 成熟 | [V] FlightState |
| 天气 | 雨/雪确定性窗口 + 云层 + 粒子池——**纯视觉零玩法影响、无季节** | 自动 | 可用（按设计如此） | [V] WeatherSystem.cs:9 注释明示；**sun/moon-phases/clouds 三张贴图已入库但未接线**（DayNightCycle 只调 Light，[A]） |
| 红石 | 拉杆+红石粉信号传播+木/铁门联动（0.1s Tick） | 右键 | **骨架（唯一消费者是门）** | [V] RedstoneSystem/RedstoneCircuit 全读 |
| 教学 | 帮助菜单双页 + m10 挖矿门槛行 + m12 新系统教学行 + m13 SHIFT 行 | H | **可用（m13 三键缺行）** | [V] HelpMenuUi KeyTable 无 F5/R/F 飞行 |
| HUD | 血/食/经验/hotbar/任务卡/飘字/红闪/死亡屏——**无坐标、无 FPS、无朝向** | 自动 | 可用 | [A] DebugHud/F3 零匹配 |

## 2. 文档 vs 代码差异（会误导提议的，全部亲核）

1. **熔炉配方不在 JSON**：CLAUDE.md 数据表称 `recipes/*.json` 含"熔炉按 tier 区分"——实测 75 个配方文件 tier 只有 pocket/inventory/workbench（[V] RecipeDatabase.cs:68-74 的 switch 无 furnace 分支），熔炼对硬编码在 `FurnaceSystem`（4 对）。**推论：新熔炼内容要改 C#，不是加 JSON**——做"熟肉/烤鱼"提案时要按 C# 改动估成本。
2. **`_format.md` 覆盖不全**：CLAUDE.md 称"每个目录有 _format.md"——实测仅 blocks/blocks.drops/items/mobs/quests/ui 六份，recipes/vegetation/ 无、biomes.json/achievements.json 无（[V] find 亲核）。
3. **CLAUDE.md m12 节内部自相矛盾**：前文"全波次已落地"，同节残留旧快照"内容与收集（第 1 波规划，**未实施**）""成就 + 图鉴（m12 W1/W2，**未实施**）"（[A] 文档代理定位到行号 235 vs 253-257/220）。AGENTS.md:158 也残留"第 1 波任务卡，未实施"。**读文档提议会误判成就/图鉴/药水未做而重提**。
4. **任务文案承诺了不存在的机制**：ch2_01 "羊毛找村民换**或剪羊**"——无剪刀物品、无剪毛代码（[V] Shear 零匹配）。孩子照任务文案找不到剪羊玩法。
5. **帮助菜单按键表缺 m13 全部新键**：F（飞行，且 07-ux 发现 F 与熔炉 UI 双义）、双击空格、R（武器面板）、F5（第三人称）都不在表里（[V] HelpMenuUi.cs KeyTableLeft/Right 全读）。m12 W6 教学行也没盖 m13。
6. **图鉴 26 条目中 18 生物无卡面美术**（深灰块 "??？" 顶格）——注意这**不在** P2 待生成 71 项内（codex 27 资源已全部入库）；生物卡面属新立项（[A] 美术代理核实）。
7. **药水"7 瓶"口径**：计划写 7 瓶，落地 6 瓶效果 + 1 张 potion-base 母版（[A] 文档代理对照）。
8. **村庄楼是空壳**：CLAUDE.md m11 写"村庄 + 村民偏向 API"给人以内容感；实测蓝图仅 L/P/G/D 四字符（墙+玻璃+门洞），**无家具、无箱子、无内部装饰**（[V] LocalBlock 全读）。
9. **成就 fisherman 自带改造钩子**："收集 5 根骨头（**钓鱼系统实装后改条件**）"——设计者已预留钓鱼位（[V] achievements.json:17）。

### 2.5 内容断链清单（会直接骗到玩家的"沉默死胡同"）[A] 探查代理全量扫描 + 我抽查认可

这是本次盘点最有行动价值的发现——**一批"物品定义在、获得途径断"的死胡同**，孩子碰到时没有任何提示：

| 断链 | 后果 | 修法（多数纯 JSON） |
| --- | --- | --- |
| **diamond 物品无任何来源**（无钻石矿石方块、无掉落、无交易卖钻石；Blacksmith 只卖 diamond_sword） | 成就 `diamond-age`（获得钻石）**不可达**；图鉴"钻石物品"条目不可解锁；钻镐/钻锄配方不可达 | 加 diamond_ore 方块 + OreFeature 嵌矿——**贴图 `blocks/textures/diamond-ore.png` 已在仓库躺着**（[A] 孤儿贴图），零配额；或 Farmer 池加"绿宝石→钻石"交易（一行 C#） |
| **金/夏季合金/机元盔甲 12 件（1454-1465）无配方无掉落无交易** | 三整套盔甲外观永远见不到 | recipes 加 12 个 JSON（照铁甲四件配方模板）——**纯 JSON** |
| **seeds_beet / seeds_mung 循环断链**：种子"仅收获自然掉"，但世界不刷野生作物、交易只卖成品 | 甜菜/绿豆**永远种不下去**；绿豆喂企鹅、甜菜喂猪的繁殖链名存实亡 | block_drops.json 给 tall_grass 加掉种子（MC 同款：打草掉麦种）——**纯 JSON**；甜菜/绿豆种子各加一条 |
| **book 无来源**（Librarian 只收购不出售） | enchanted_book 配方断链（Boss 掉落兜底存在） | 交易池加"绿宝石→book"或配方"3 纸?"无纸——建议交易（改 VillagerOffers 一行） |
| **redstone_dust 物品双断链**（无获得途径；拆红石粉线还不返还物品） | 拉杆电路的红石粉只能靠创造式调试获得 | 反向：`redstone_dust_to_redstone` 已有，加"redstone→redstone_dust"口袋配方——**纯 JSON**；RedstoneSystem 拆除返还一行 |
| **crafting_table 物品没写 blockId** | 工作台方块在正常玩法中**无法出现**（P 键 UI 兜底存在，但"造个工作台摆地上"的心智模型断了） | items/crafting_table.json 加 `blockId`——**一行 JSON** |
| **VillagerOffers.cs:26 引用不存在的 bread 物品**（"绿宝石 4→面包 1"） | 该条交易命中时按注册表查不到物品 | 加 items/bread.json（heal 5）+ 配方 3 麦——**纯 JSON**（交易本身不用改；`items/textures/bread.png` 已入库，[A]） |
| bowl_of_water / skull 两件物品无来源无用途 | 无声死物品 | bowl_of_water 接药水配方（对水右键装水？砍掉——直接并入药水配方成本）；skull 留给 F16 沉船战利品 |
| machine_essence_ore.json 写了 `name` 字段而非 `displayName` | 中文名"机元矿石"丢失（显示为 id） | 字段改名——**一行 JSON** |
| **6 个物品图标引用了不存在的 png**（bed-side/chest-front/glass/sand/torch/wooden-door-lower——同名图只在 blocks/textures，物品加载器不查那里） | 实机**品红占位块**：床/箱子/玻璃/沙/火把/木门六件热键栏图标（[V] 亲核 `ItemSlotDrawer.GetTextureOrPlaceholder` 候选路径 + 两目录实存对照） | 加载器加 blocks/textures 兜底或复制 6 张图到 items/textures——**一行代码或零代码** |
| biomes.json `temperature`/`humidity` 反序列化后全工程零消费 | 孤儿字段（群系实际由气候噪声决定） | 文档标注或接进 BiomeSelector——暂留，写进 _format.md 说明 |

### 2.6 孤儿资产清单（已入库未接线，[A] 全量扫描 + 抽查认可）

**"用现有零件拼新功能"的弹药库**——约 49 张 Art 图 + 30 张 StreamingAssets 贴图零引用：

| 孤儿堆 | 数量 | 对应的未来功能（本报告提案） |
| --- | --- | --- |
| `blocks/textures/`：diamond-ore、coal-ore、bricks、cobblestone、gravel、lava、stairs-bricks/planks/stone、chest-side、bed-foot-top、missing | 12 | **F0**（钻石/煤/圆石矿与砖块）、F18（**楼梯三材质贴图已备**——评估文档说的"仅石/木/砖三材质"美术零成本） |
| `items/textures/`：bread、treasure-map、kite、balloon、crown、coin-pile、gem-bag、leather、puzzle-cube、robot-toy、spinning-top、enchant-scroll、enchanting-rod、hacker-pc、office-desk、enchanted-book 三张分色图等 | 18 | **F0**（bread）、F3（treasure-map 可做钓鱼 treasure 掉落彩蛋）、玩具类=礼物/节日玩法素材 |
| `Assets/Art/Sky/`：sun、moon-phases、clouds | 3 | F26 天空贴图接线（DayNightCycle 挂日月面片、CloudLayer 用云贴图——m13 W5 给 postprocess 加了 moon-phases 支持**却没消费**） |
| `Assets/Art/Entities/` 全部 + `Art/Player/skin.png` | 21 | 生物走 models JSON 纯色拼装、玩家走程序绘制——这批贴图是"第二代表现层"素材（低优先） |
| `Assets/Art/Effects/` 15 张（torch-flame/water-glint/firefly/hit-spark/slash-arc/arrow-trail 等） | 15 | 粒子池扩容素材（火把火焰/命中火花——手感增强） |
| `Assets/Art/UI/` 7 张（button 三态/crosshair/panel/hotbar-slot-selected/title-decor）+ `Art/Codex/` 3 张卡框 | 10 | IMGUI 视觉升级包（稀有度卡框可给图鉴用） |
| Core 侧：`WorldGen/WaterFeatures.cs` 仅测试消费 | 1 | 水体玩法（瀑布/涌泉）备用 |

### 2.7 用户数据画像（孩子实际玩了什么，[A] 只读 `LocalLow\DefaultCompany\MyWordGame\worlds\`）

| 世界 | 存档快照 | 解读 |
| --- | --- | --- |
| **42（主力）** | 08-21 03:04 存档：hp 0/20（**死亡瞬间存档**）、饥饿 18、经验 36/Lv32；背包近乎全满（木板 64、泥土 64×4、圆石 38、箭 8）；穿戴 4 槽全空；Stats 仅 `ach:first-night` + `ach:explorer` 两条；任务停在 `ch1_02_craft_planks`；床 0、箱子 0、农田 0、附魔 0；**地面掉落物 237 个** | 成就 **2/16**、图鉴 **0/26**、第一章 **8 步走了 1 步**；挖了大量方块但没捡/死亡散落 237 件；m11/m12 的药水/乐器/附魔/穿戴/农业/Boss **全部零触碰** |
| 92726749 | 08-21 03:09 新建（比上一次存档晚 5 分钟）：黄昏、hp 20、背包仅开局木板 | 最后一天在**试用 m12 新出的世界管理**开了个新世界 |

**三条产品结论**（直接支撑本报告排序）：
1. **新系统触及率极低**——不是做更多系统，而是让已有系统被**看见、够得着**（F0 断链 + F1 教学 + F6 引导任务）。
2. **死亡→挫败循环**：hp 0 存档 + 237 个掉落物 + 穿戴全空——防挫败设计（宝宝难度/和平模式已有但默认关）值得在教学行里主动教。
3. **实机是 RTX 2070 Super**（Player.log，[A]）——性能档位（F8）紧迫性降为"未量化"而非"已知卡顿"；但 m12 W5 的 FPS 验证仍从未执行。

## 3. 孩子需求逐条状态表

`docs/小孩子玩后需求/` 仅一份原文（20260818_飞行与远程战斗.txt，5 条）+ m12 三大根因（口头反馈，CLAUDE.md 记录）。逐条：

| 需求原文（摘） | 状态 | 落地证据 |
| --- | --- | --- |
| 1. 按键起飞，空中六向移动 | **已满足**（m13 W1 commit 7b1f1f6） | [V] FlightState + 双击空格/F |
| 2. 射箭或枪远程杀僵尸猪牛 | **已满足**（m13 W3 f2b0ede） | [V] 弓 60m/火枪 25m + range 字段 |
| 3. 僵尸要有血条；一级血一打就嗝 | **已满足**（血条 m13 W2 + 宝宝难度一击必杀） | [V] MobView 血条 + DifficultyMode `damage = Health.Max` |
| 4. 僵尸掉腐肉和经验；经验用于附魔 | **已满足**（m9 经验 + m10 附魔） | [V] drop_tables Zombie→1010 腐肉 + Experience/EnchantSystem |
| 5. M1和MP背包和工作台没法合成 | **已满足**（m13 P0 commit 70f35da SHIFT+click） | [V] CraftGridInteraction.PutMainSlotOne |
| 6. 点背包/工作台外自动关闭 | **已满足**（m13 W4 点外关闭 5 处） | [V] 5 个 UI 的 BackgroundBounds + mouseDown 判定 |
| 7. 专门切换武器的面板 | **已满足**（m13 W4 R 键 WeaponPanelUi） | [V] |
| 8. 武器按不同参数定射程伤害 | **已满足**（items `range`/`attackDamage`） | [V] |
| 9.（口头）挖矿要有手感不是瞬挖 | **已满足**（m12 P0 DigProgress） | [V] |
| 10.（口头）放置六向体感 + 向上搭 | **已满足**（m12 P0 幽灵框+tower-up） | [V] |
| 11.（口头）主菜单世界管理 | **已满足**（m12 P1 WorldCatalog） | [V] |

**结论：书面与口头需求零欠账。** 这份报告的全部提案都是主动挖掘——按"孩子 30-60 分钟一局回路 + 父亲教学意图"倒推，且沿用项目流程（先评估真 MC 参数，不合理修正）。

## 4. 候选功能卡片总表

价值=孩子视角 1-5；频次=每局出现次数估计；成本 S<1天 / M=1-3天 / L>3天（含双链测试与出包验收）。

| # | 功能 | 价值 | 频次 | 成本 | 风险 | 依赖 | UI 增量 | 优先级 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| F0 | 内容断链修复批（盔甲 12 配方/种子来源/bread/红石粉/工作台 blockId/钻石来源，见 §2.5） | 5 | 每局 | S（大半纯 JSON） | 零 | 无 | 无 | **P0（第一优先）** |
| F1 | 帮助菜单 m13 补课（飞行/R/F5 按键行+教学行；顺带修 F 双义提示） | 4 | 每局 | S | 零 | 无 | 文案 | **P0** |
| F2 | 剪羊毛（剪刀+羊毛周期再生） | 4 | 每牧场局 | S | 零 | items/recipes JSON | 无新面板 | **P0** |
| F3 | 钓鱼系统（竿+浮标+鱼获+烤鱼） | 5 | 每局可 | M | 中（等待耐性设计） | 4 件套已预留 | 无新面板 | **P1** |
| F4 | 回家罗盘（HUD 箭头指床） | 4 | 迷路时 | S | 零 | BedSystem.RespawnPoint | HUD 一行 | **P1** |
| F5 | 小地图（M 键开关，色块缓存） | 4 | 每局 | M | 性能（见护栏） | EnumerateKnownChunks | 一块 128² 面板 | **P1** |
| F6 | 第三章任务链 + 新成就批（纯 JSON） | 4 | 每局 | S | 零 | QuestChainLoader 多章节 | 零 | **P1** |
| F7 | 乐器自由演奏（数字键音阶） | 3 | 玩耍时 | S | 零 | PlayTone 已合成任意频率 | 无 | **P1** |
| F8 | 视距/性能档位 + F3 FPS/坐标 overlay | 4（父）/3（子） | 常驻 | S | 低 | LoadRadius 可写属性 | 设置 1 行 + overlay | **P1** |
| F25 | 附魔台真化（X 键 UI 从"扣真经验+青金石只掷 Debug.Log"改为写真 EnchantStore） | 4 | 中期 | S-M | 低（口径 1-5 vs MaxLevel=3 一并钉死） | EnchantSystem.Fuse 已有真路径 | 无 | **P0**（扣资源不产出=陷阱） |
| F26 | 天空贴图接线（sun/moon-phases/clouds 三张已入库孤儿图挂进 DayNightCycle/CloudLayer） | 3 | 常驻 | S | 零 | §2.6 孤儿贴图 | 无 | P1 |
| F9 | 工具特化挖掘速度（铲挖土/斧砍木 ×2） | 3 | 每局 | S | 低 | BreakTime 加分支 | 无 | P1 |
| F10 | 食物烹饪（熟肉/烤鱼 heal 翻倍） | 3 | 每局 | S | 零 | FurnaceSystem 加 special 对 | 无 | P1 |
| F11 | 村庄内容化（家具+战利品箱） | 4 | 探索时 | M | 中（旧区块不追溯） | 蓝图扩字符 | 无 | P2 |
| F12 | 宠物驯服（狗 kind 37：喂骨→跟随/坐下） | 5 | 每局 | M-L | 中（AI/存档） | MobAI 新状态 | 无 | P2 |
| F13 | 鸡下蛋（蛋可扔出孵小鸡） | 3 | 农场局 | S-M | 低 | BreedingSystem | 无 | P2 |
| F14 | 红石灯 + 音符盒（电路第二个消费者） | 3 | 建造时 | M | 低 | RedstoneCircuit.IsPowered | 无 | P2 |
| F15 | 截图键 F2（存 Pictures，本地不联网） | 3 | 炫耀时 | S | 零 | ScreenCapture | 无 | P2 |
| F16 | 沉船/废弃矿井结构（盖章模板第 3/4 用户） | 4 | 探索时 | M-L | 中 | VillageFeature 模式 | 无 | P2 |
| F17 | 存档备份/克隆（世界列表加按钮） | 2（父）/3 | 偶尔 | S | 低 | WorldCatalog | 列表 1 按钮 | P2 |
| F18 | 台阶（slab 先行，楼梯评估方案 A） | 4 | 建造时 | L | 高（碰撞+性能） | 评估文档既定路线 | 无 | Later |
| F19 | 漏斗（箱子↔熔炉自动搬运） | 3 | 自动化 | M | 中 | ChestSystem 行式语义 | 无 | Later |
| F20 | 季节系统 | 2 | — | L | 高（推翻"纯视觉"决策） | — | — | Backlog |
| F21 | 第二 Boss（水系，联动沉船） | 3 | 末期 | L | 中 | — | — | Backlog |
| F22 | 第三人称增强（滚轮调距/肩视） | 2 | 偶尔 | S | 低 | CameraThirdPerson | 无 | Backlog |
| F23 | 迷你游戏（竞速/挑战） | 2 | — | L | 中 | — | 新面板 | Backlog |
| F24 | 互动教程升级（情境提示） | 2 | 首局 | M | 低 | 任务链已覆盖 | — | Backlog |

## 5. 重点提案深入（11 张卡片；F25 为后补的 P0，一并展开）

### F25 附魔台真化 —— P0，S-M

- **为什么**：[V] 亲核 `EnchantingUi.DoEnchant`——X 键附魔台**真扣青金石+经验**，然后只掷一个 Sharpness 等级写 `Debug.Log`，**不写 EnchantStore**。孩子 Lv32 的经验按 5 经验/级算会被静默白扣（附魔书融合路径才是真附魔）。m10 终审刻意"不改写工具栈"是为防耐久位写坏——现在 EnchantStore 已独立于 Metadata（m11 W2-2 教训沉淀），那个顾虑不再成立。
- **草案**：`DoEnchant` 扣费成功后改调 `EnchantSystem.Fuse` 的同款写入（选定工具 + RollBookKind 三选一 + 等级按 selectedLevel 上限 MaxLevel=3 钳制）；UI 文案从"掷提示"改为展示真实结果；`EnchantingUi` 注释里"等级 1-5"口径统一为 1-3。保守替代方案：若不想动写入路径，把 X 键面板改成"附魔书工作台"（引导合成书），两行文案改动——但失去直接附魔体验，不推荐。
- **测试**：EditMode——DoEnchant 后 `EnchantStore.TryGet` 命中新附魔；经验/青金石扣减不回归；MaxLevel 钳制断言。

### F0 内容断链修复批 —— P0，S，全项目性价比最高的一张牌

- **为什么第一**：§2.5 的十处断链里有六处是"孩子看得见目标却永远够不着"（金甲、绿豆田、钻石成就、面包……）。与新增功能不同，这些是**已承诺内容的兑现**——修复它们等于零新增学习成本地扩内容。其中约七成是纯 JSON（配方 12 个、drops 2 条、blockId 1 行、bread 物品 1 份）。
- **草案**（按依赖顺序一批做完）：
  1. 纯 JSON 批：金/合金/机元盔甲配方 ×12（照 `armor_iron_*_recipe.json` 模板，材料对应 ingot）；tall_grass 掉 seeds_wheat、再加 seeds_beet/seeds_mung 的获得（甜菜根掉自身种子、绿豆荚掉种子，或 Farmer 卖种子——后者改 C# 一行池）；`crafting_table.json` 加 blockId；新 `items/bread.json`（heal 5）+ 配方（3 wheat，贴图已备）；`redstone→redstone_dust` 口袋配方；`machine_essence_ore.json` 的 `name`→`displayName`。
  2. C# 微量批：`VillagerOffers` 不动（bread 物品补上后悬空自愈）；`RedstoneSystem` 拆粉返还物品一行；**6 个紫占位图标修复**（物品加载器加 blocks/textures 兜底）；钻石来源二选一——推荐加 diamond_ore 方块 + OreFeature 嵌矿（**贴图已在仓库**，嵌矿参数跑 `MyWorld.Preview` 目检），或 Farmer 池加"8 绿宝石→1 钻石"（一行，最稳）。
  3. 跑 `dotnet test`：集成守卫（`BlockDefinitionFilesTests` 悬空引用检查、配方可达性测试）自动抓错。
- **测试**：EditMode——盔甲 12 件配方 FindMatch 全绿（照铁甲先例）；tall_grass 掉落 `BlockDrops.RollCount` 确定性断言；`crafting_table` blockId 放置路由（照 m11 W3-1 测试）；bread 交易解析不再失败；**可达性专项**：以"开局送 64 木板"为起点对全部 16 成就做拓扑可达断言（新测试，防止未来再断链）。

### F1 帮助菜单 m13 补课 —— P0，S，当天可收

- **为什么第二**：m13 四个新入口（F 飞行/双击空格/R/F5）在按键表和教学行都不存在（[V]），功能存在但孩子大概率不知道——**已花的开发量在被浪费**。07-ux 另发现 F 键与熔炉 UI 同帧双义，教学行必须同时写清"F 开熔炉、双击空格飞行"的避让用法。
- **草案**：`HelpMenuUi.KeyTableRight` 加 3 行（F5 第三人称 / R 武器面板 / 双击空格 飞行）；"怎么玩"页加一段飞行教学（照 m12 W6 教学行格式）。**不动 WorldBootstrap**。
- **测试**：EditMode 断言按键表含新行（照 `HelpMenuUiTests.AltF4行_*` 先例）；文案测试钉死"双击空格"字样。

### F2 剪羊毛 —— P0，S

- **为什么**：任务 ch2_01 已经向孩子承诺"或剪羊"（[V] 文案），这是**内容契约欠账**；且牧场闭环（喂→繁→剪）比杀羊取毛友好得多，正合 65 会话里反复出现的动物向玩法。
- **草案**：`items/shears.json`（numericId 1609 段顺延，maxDurability 64）+ `recipes/shears_recipe.json`（铁锭 2，3×3）；`CombatController.TryFeedMobInCrosshair` 旁加 `TryShearSheepInCrosshair`（手持 shears + 准星 Sheep → 掉 1-2 wool + 羊进入"已剪"冷却 120s——计时语义照 BreedingSystem 的绝对时间阈值，零随机）。冷却状态存哪：羊是运行时实体不进存档，冷却丢失可接受（重进世界羊毛"长回来"，MC 同款语义）。
- **测试**：EditMode——剪后掉 wool 且不伤血、冷却内再剪 no-op 不扣耐久、非羊/空手回落原路由；`shears` 守卫测试（toolTier/maxDurability 显式声明，写漏=红）。

### F3 钓鱼系统 —— P1，M（本报告最高综合分）

- **为什么**：全项目**最完整的"半成品生态"**——fisherman 成就自带改造钩子（[V] achievements.json:17 "钓鱼系统实装后改条件"）、LuckOfTheSea 附魔占位待激活（[V] Enchantment.cs:12）、水肺药水现为 +4 心占位（[V] PotionSystem.cs:21 注释自认"水无氧气系统"）、水生 5 生物已刷在水里（[V] spawn_rules）。一个功能点亮四个沉睡零件 + 水体终于有玩法。MC 参数对照：竿=3 木棍 2 线、鱼咬钩 5-45s（儿童版收窄 3-10s）、LuckOfTheSea 每级 junk→good 概率提升。
- **草案**：`items/fishing_rod.json` + `raw_fish/cooked_fish.json` + 配方 + 熔炼（`FurnaceSystem.TryGetSpecialSmelt` 加 raw_fish→cooked_fish 对，heal 2→5）；`BlockInteraction.UseAt` 在弓分支后插竿分支（照 musket 模式）：右键抛竿→浮标实体（复用 `ProjectileEntity` 直线弹道 + 落水即停）→ 确定性哈希 3-10s 后"咬钩"（浮标下沉视觉 + `PlayTone` 提示音）→ 1.5s 窗口内再右键收杆→ RollAll 掷鱼获（鱼 80%/junk 15%/treasure 5%，LuckOfTheSea 每级 treasure+5%）。**不加氧气系统**（水肺药水保持现状，避免推翻 m12 取舍）。
- **测试**：Core 纯逻辑类 `FishingRoll`（哈希掷骰可无头断言）；EditMode 接线——抛竿/咬钩窗口/超时收空/弹药式耐久；`fisherman` 成就条件切到 ObtainItem raw_fish 5。

### F4 回家罗盘 —— P1，S（可单独先出）

- **为什么**：孩子 30-60 分钟一局，探索半径受 LoadRadius=6（208×208 格）限制，但村庄在 400 格一格、挖矿往下就是迷路三要素；HUD 无坐标无朝向（[A]）。迷路=挫败=提前退出。
- **草案**：v1 不做物品——HUD 角落一行"🏠 128m ←"（IMGUI，照 QuestHudUi 模式）：`BedSystem.RespawnPoint` 已有（[V]），玩家位置差向量 → 距离 + 箭头字符（↖↑↗…8 向，遵守 visual-text-conventions）。无床时显示"无家"。v2 可升格 compass 物品（合成：4 铁锭 1 红石——redstone 物品已有）。
- **测试**：EditMode——有床/无床两态文案、方向 8 向量化断言。

### F5 小地图 —— P1，M

- **为什么**：F4 的完整版，也是"探索激励"的载体（村庄/矿脉标记）。`ChunkViewRegistry.EnumerateKnownChunks()`（[V]）已给出现成枚举；MyWorld.Preview 的俯视采样逻辑可平移。
- **草案**：M 键开关 128×128 IMGUI 面板（居右上角，避开 QuestHudUi）。数据流：`EnumerateKnownChunks` → 每 chunk 顶面方块采样（16×16 次 GetBlock，**4Hz 节流**）→ 色块写入缓存 `Color32[208×208]`（成员级复用，不 new）→ `MarkBlockChanged` 时只失效对应 chunk 缓存 → IMGUI `Graphics.DrawTexture` 一张动态 Texture2D。玩家=中心白点+朝向三角；床=红点（F4 数据复用）。**不采样未加载区块**（避免触发加载）。
- **测试**：EditMode——缓存失效正确性（改方块后该 chunk 重采样）、节流（假时钟推进断言重绘次数）、面板开关不泄漏纹理。

### F6 第三章任务链 + 新成就批 —— P1，S，纯 JSON 零 C#

- **为什么**：`QuestChainLoader` 多章节顺序解锁是 m11 W2-4 现成能力（[V] QuestCampaign），成就系统事件词汇已含 FeedAnimal/EquipArmorFull 等 10+ 种。**用户画像给了它额外分量**（§2.7）：孩子实际停在 ch1_02——两章 16 步对他仍有 15 步未走，第三章不是"续命"而是**把引导链一直铺到新功能门口**；任务卡是本项目被验证过的最有效教学载体（m6 起）。
- **草案**：`quests/chapter3.json` 8 步：剪 3 次羊毛→做剪刀→钓第一条鱼→烤鱼吃→驯一只狗（若 F12 同期）→带宠物杀一只僵尸→乘船?（砍掉，未做）→改为"飞到 100 格高空俯瞰村庄"（复用 FlightState）→终步"图鉴点亮 20 条"。成就加 6 枚（钓鱼佬/牧场主/飞行员/探险家 1000 格外/铁人/全成就）；徽章美术走程序占位快路径（art/scripts 确定性生成，不占配额）。
- **测试**：JSON 集成测试自动覆盖（悬空引用/事件词汇非法即红，现有守卫）；EditMode 断言第三章加载后 ChapterCount=3。

### F7 乐器自由演奏 —— P1，S

- **为什么**：`PlayerAudioSystem.PlayTone` 已能程序合成任意频率正弦波（[V]），现在每种乐器只绑死一个音——引擎能力与玩法之间只差一张键位映射表。对小学生这是零门槛的"发现音乐"玩具。
- **草案**：手持鼓/笛/铃时数字键 1-7 = C4-B4 音阶（保持选中槽不变——用 `Input.GetKeyDown` 直读，照 WeaponPanelUi 模式），Shift+数字 = 高八度；笛 660Hz 基准其余按十二平均律换算。音乐盒右键从"固定小星星"扩为**三首轮换**（小星星/两只老虎/小蜜蜂，8-16 音符表写死 C# 常量，照 PlayTwinkleTune 模式）。
- **测试**：EditMode——键位→频率映射表断言、乐器音色参数（时长/波形）不回归。

### F8 视距档位 + F3 overlay —— P1，S

- **为什么**：LoadRadius=6/UnloadRadius=8 是可写属性但无人改（[V]）；m12 计划里 W5"性能实机验证（FPS overlay + ChunkStreamer 8ms 压测）"整波被裁（[A] 文档代理，计划原文"视第 1 波余量裁剪，可选"）——**性能从未被实机量化过**。实机是 RTX 2070 Super（[A] Player.log），暂无卡顿报告，所以本项价值偏工程侧（父亲后续每个波次都要出包验证，F3 overlay 是顺手工具）+ 向下兼容老设备的选择权。
- **草案**：`SettingsPanelUi` 加"视距"三档 toggle（近 4/中 6/远 8），写 `PlayerPrefs m13.viewDistance`，启动时注入 `ChunkStreamer.LoadRadius`；F3 键开关 overlay（FPS/帧 ms/已加载 chunk 数/坐标 xyz/朝向——数据源全部现成：Time.smoothDeltaTime、ViewCount、player transform）。**常驻关闭**默认。
- **测试**：EditMode——档位→LoadRadius 映射、PlayerPrefs 往返、overlay 开关；出包后跑一次 W5 原计划的 10 分钟实机记录（收口铁律本来就要出包）。

### F9 工具与厨房二件套（工具特化挖掘 + 食物烹饪）—— P1，S+S

- **为什么**：两件"闲置资产激活"打包一波做。铲 3 把 + 斧 3 把共 6 件物品可合成可附魔，但挖掘速度无差别（[V] BreakTime 只有镐 tier 矩阵）——功能近闲置；孩子原话"比如你可以拿铲子"说明他会做铲子，做了却没差异感。另一半：牛/猪/鸡三肉只能生吃（heal 3/3/2，[V]），熟肉翻倍给"回家做饭"一个理由，与 F3 烤鱼共用 `TryGetSpecialSmelt` 同一条代码路径。
- **草案**：`BreakTime` 加工具类别分支——手持 `*_shovel` 挖 dirt/sand/grass ×0.5、`*_axe` 挖 log/planks/木制品 ×0.5（`def.Id.StartsWith` 判定，照 hoe_ 前缀先例；MC 参数：石铲挖土 0.75s→0.15s）；`TryGetSpecialSmelt` 加 beef→cooked_beef(6)/raw_porkchop→cooked_porkchop(6)/chicken→cooked_chicken(4)，新物品 JSON ×3 + 图标程序占位。
- **测试**：EditMode 参数化——每把铲/斧 × 对应方块 × 期望秒数（与 BlockGatingTests 全量一致性同款）；熔炉进出对断言（照 RawIron→ingot 先例）+ healAmount 守卫。

## 6. 分阶段路线图

**Now（1 周内，全部 S 成本；目标是"让孩子把已有内容玩到"）**
1. **F0 内容断链修复批**——十处死胡同 + 6 个品红占位图标（§2.5，约七成纯 JSON）。
2. **F1 帮助菜单 m13 补课**（修 F 双义教学 + 防挫败教学：主动介绍宝宝/和平模式——用户画像死亡循环的证据）。
3. **F25 附魔台真化**（X 键现在扣真经验+青金石只掷 Debug.Log——孩子 Lv32 的经验会被白扣）。
4. **F2 剪羊毛**（还任务文案欠账）。
5. **F4 罗盘 HUD**（F5 的先行子集）+ **F26 天空贴图接线**（三张孤儿图零配额点亮）。
6. **F8 视距档位 + F3 overlay**（把被裁的 m12 W5 补上，工程侧抓手）。
7. P2 美术批 71 项消化（配额窗口见专项计划）。

**Next（1-2 个波次）**
8. **F3 钓鱼**（四件套联动：成就/附魔/药水语义/水生生物价值；treasure 掉落用孤儿贴图 treasure-map）。
9. **F6 第三章任务 + 新成就**（把引导链铺到新功能门口）。
10. **F9 工具特化 + 烹饪二件套**（让 6 件闲置工具和 3 种生肉有意义；烤鱼同路径）。
11. **F7 乐器自由演奏**；**F5 小地图完整版**（map-frame 贴图在 P2 批里有现成立项）。

**Later（独立里程碑）**
11. F12 宠物驯服（狗）；F13 鸡下蛋。
12. F11 村庄内容化 + F16 沉船/矿井（**美术树里 blueprint-mine/shipwreck/temple、village-blacksmith/farm/library/well、banner-village 已立项入库待用**——代码先行美术零等待）。
13. F18 台阶（严格按楼梯评估文档：先 slab 后楼梯、朝向拆 id、5-8 天预算、`VoxelCollision` 回归面全测；**stairs-stone/planks/bricks 贴图已入库**）。

**Backlog**：F14 红石灯/音符盒、F15 截图 F2、F17 存档备份、F19 漏斗、F21 第二 Boss（水系，配沉船）、F22/F23/F24、图鉴 18 生物专属卡面（新立项非 P2 遗留）。

### P2 美术批消化计划（Now 第 7 项）

- **真相修正**（[A] 只读复现 `--tree` 判定逻辑）：全量 343 资源 = 已入库 249 + 📥1 + 📄70 + ⬜23；**71 项 = 70 个 📄 + 1 个 📥（moon-full 已生成卡在后处理前）**，且"2056"不是文档状态而是 MiniMax API 配额拒绝码。**构成里 0 个 mob / 0 个物品图标 / 0 个方块贴图**（那些已被 2026-08-20 补丁 commit `5e4c357` 吃掉）——全部是氛围与外围资产：
  - UI 手感包 15（光标 attack/dig/grab/talk、飘字 damage-number/xp-float/combo/crit、armor-bar/boss-bar/chest-slot/enchant-panel/**map-frame**）
  - 天空/天气粒子 9+1（aurora/meteor/milky-way/rainbow/stars、hail/leaf-fall/rain/snow、moon-full）
  - 场景插画 11（panorama×5、menu×3、boss-intro、chapter1-complete、ending）
  - 建筑 11（banner/blueprint：mine/shipwreck/temple、sign-shop、village 五件套）
  - 季节 10、营销 8、玩家表情 6
- **消化顺序建议**（游戏内可见优先）：① UI 手感包（F5 小地图的 map-frame、boss-bar、飘字——直接喂给本报告 Next 波次）② 天空/粒子（配 F26）③ 建筑/场景（配 F11/F16 立项时再入）④ 季节/营销/表情最后（无代码消费者，纯入仓）。
- 动作：配额恢复日 `generate_art.py --all --n 4 --jobs 4` → `postprocess_art.py` → install → `art/README.md` 状态改"已入库"→ **当天出包**（收口铁律）。配额再被挡的 fallback：程序占位快路径——本报告所有新物品图标（剪刀/鱼竿/鱼/熟肉/bread）一律先走占位路径，**不新增配额依赖**。
- 注意：`art/incoming/video/quota.json` 显示视频队列已清空（5 任务全 done），配额文件只是历史记录——**别把它当待办**。

## 7. 不该做清单

| 项 | 理由 |
| --- | --- |
| 网络多人/在线联机 | m3 起设计非目标（[A] 文档代理两处 spec 佐证）+ 儿童安全硬边界 + Core 单世界单玩家假设（PlayerContext 单例） |
| 本地分屏 | 评估过：PlayerContext.Instance 单例、Input 全局轮询、单 Camera、IMGUI 全屏布局——四处同时改，成本 L+ 且砸手感基石；目标场景（父子）已被"共同开发"满足 |
| UGC 上传/在线分享/排行榜 | 儿童安全硬边界（单机离线无网络是产品前提） |
| 枪械树扩展（步枪/连发/狙击）、弹药 HUD 面板 | m13 设计"非目标（YAGNI）"节明确排除（[A]） |
| 无限射程武器 | 孩子提过"想打多远打多远"，m13 已修正为参数化射程——维持修正 |
| 红石完整逻辑集（中继器/比较器/活塞/漏斗车） | 复杂度与性能失控，且儿童无此需求；红石灯+音符盒（F14）是够用的终点 |
| 季节系统 | 天气"纯视觉不碰逻辑"是 m11 明确设计决策（[V] WeatherSystem.cs:9 注释）——玩法化要推翻确定性测试基线，性价比低 |
| 氧气/溺水系统 | m12 已取舍（水肺=+4 心占位）；引入溺水对小年龄玩家是挫败源 |
| 付费/广告/账号 | 硬边界 |
| 死亡掉落（普通模式） | m7 设计钉死"死亡天然不掉落"，PeaceMode 只是显式化——别翻案 |

## 8. 性能护栏（会加重卡顿的功能专项）

| 功能 | 护栏要求 |
| --- | --- |
| F5 小地图 | 采样 4Hz 节流；色块缓存 per-chunk、`MarkBlockChanged` 才失效；`Color32[]`/Texture2D 成员级复用（**禁止每帧 new**——CLAUDE.md m5 热路径铁律）；只画已加载区块，绝不为画图触发加载 |
| F12 宠物 | 宠物上限 4；不占 `MaxMobs=24` 自然刷新名额（照 Boss/幼崽的旁路先例 [V] MobManager.cs:336）；follow 向量每帧 O(1) |
| F11/F16 结构 | 盖章只在生成期（ChunkStreamer 8ms 预算内、MaxWorkUnitsPerFrame=16 分帧）；战利品箱**首次开启才 roll**（惰性），生成期零额外成本；蓝图查询保持 O(1) 反查（照 `BlockAtWorld`） |
| F3 钓鱼 | 浮标单实体；等待期纯时间阈值零轮询；鱼获 roll 仅收杆瞬间一次 |
| F8 overlay | 默认关；开时字符串 0.5s 才重建一次（IMGUI 每帧 ToString 是隐形 GC 源） |
| F6 任务第三章 | 零运行时代价（事件总线既有路径，游戏逻辑不感知任务系统——架构即护栏） |
| 通用 | 所有新右键分支进 `UseAt` 优先级表（首中即止，不新增每帧扫描）；新 UI 沿用 UiCursorGate 六模态门，不自开指针 |

## 9. 被提案功能中实际已存在的（自查纠正）

复核纪律要求的专项——**初稿曾考虑提案、核实后撤销**的：

1. **"新增村庄生成"**——探查代理 B 一度报告"没有村庄建筑（Village 仅命中村民代码）"。我亲核 `WorldGenerator.cs:134,186` 调用 `CollectVilles/StampIntoChunk` + `VillageFeature.cs` 全读：**村庄生成存在且接线**（水井+3-5 楼+跨区块盖章）。撤销"新增村庄"，改为 F11"村庄内容化"（楼内空壳是真缺口）。代理误报原因：只 grep 了 Unity 侧。
2. **"动物繁殖系统"**——初看像机会，核实 `Core/Farming/BreedingSystem.cs`（m11 W1-6，12 种生物全参数齐全）已完整落地。撤销，仅保留"蛋/剪毛"两个真缺口。
3. **"盾牌机制"**——物品表有 shield 一度疑为孤儿；核实 `PlayerController.ApplyShieldMitigation`（m11 W1-1，减伤×0.5+耐久+附魔掷骰）已功能化。
4. **"耐久/效率附魔生效"**——疑占位；核实 `ShouldWearDurability`（5/(5+L) 概率）与 `SelectedDigTimeMultiplier` 均已在游戏循环消费。真占位只有 LuckOfTheSea（并入 F3 钓鱼提案）。
5. **"第三人称相机"**——任务提示"已有截图"，核实 `CameraThirdPerson.cs` F5 已存在（简化版）。提案降级为"帮助菜单补行"（F1）+"滚轮调距"（Backlog F22）。
6. **"交易系统"**——核实 V 键 TradeUi + 3 职业池已存在；真缺口只是"池硬编码未外置 JSON"，价值低不单独立项。
7. **"挖掘计时/世界管理/成就/图鉴/药水/乐器"**——CLAUDE.md 旧快照写"未实施"，经 commit log（4ba7b84/c2a0261/739bbbc/cc19117）+ 源码亲核确认全部落地。**读者若只看 CLAUDE.md m12 节中段会被误导**（见 §2-3）。
8. **"P2 美术批 71 项里补图鉴生物卡面"**——我初稿把"18 生物无卡面"算作 P2 批待生成项；美术代理核实 codex 27 资源（16 徽章+8 卡+3 框）**已全部入库**，71 项里 0 mob/0 物品/0 方块——生物卡面属**新立项**，已从消化计划中移出。
9. **"附魔系统成熟"**——我初稿照 CLAUDE.md m10/m11 叙述把 X 键附魔台当成熟功能；亲核 `EnchantingUi.DoEnchant` 后确认它是**扣真资源不产出的占位**（真附魔只在附魔书融合路径），已立 F25 进 Now 波次。
10. **"天气需要新系统"**——天气本体已有（确定性雨窗+云+粒子）；缺口只是"纯视觉无玩法影响"与三张天空贴图未接线（F26）。

## 10. 未核实项（如实声明）

1. **测试基线数字**（dotnet 1039 / EditMode 1813）未重跑——仓库 clean + 三份根文档一致，采信文档；本评审未改任何源码，不影响该基线。EditMode 侧实际为 27 个子目录 202 个测试文件（[A] 结构盘点）。
2. **Builds 包时间口径**：`Builds/Windows/MyWordGame.exe` 本体 mtime 2026-08-14 06:08，但 `_Data/StreamingAssets` mtime 2026-08-21 01:36、Burst 调试信息 02:04——增量构建行为（patch-tasks 文档已注明），与 m12 收口记录"包构建时间 08-21 02:03"不矛盾，但**引用包时间时要说清是哪个文件**。
3. **实机帧率**——从未被量化（m12 W5 被裁），本报告所有性能判断是代码级推断 [I]：依据是 LoadRadius=6、8ms 预算、MaxWorkUnitsPerFrame=16、IMGUI 自绘粒子等既有约束的静态阅读；实机 GPU 为 RTX 2070 Super（Player.log）。
4. **孩子对新功能的偏好排序**——[I] 依据：65 会话、m12/m13 需求全落在"动物/武器/建造便利"三主题、§2.7 存档画像；钓鱼/宠物/剪毛的高价值判断由此推断，建议第一个验收剧本里 A/B 观察孩子先玩哪个。
5. **TradeUi "不要求 buy 物品在背包"的简化注释**与实际扣减行为是否一致——未逐行核实，属 07-ux 范畴。
6. **"开局送 64 木板"、任务停在 ch1_02 的成因**（是旧档兼容重置还是再度卡在合成）——存档快照无法区分，属 04/07 报告的 bug 侦察范畴；本报告只采用"进度停在 ch1_02"这一事实。
7. **代理报告中的个别引用行号**（如 VillagerOffers.cs:26 bread、RedstoneSystem.cs:126-132 拆粉不返还）未逐行亲核——方向已由我抽查（VillagerOffers 全文亲读确认 bread 在池中且 items 无 bread.json），行号可能有 ±2 偏差。
