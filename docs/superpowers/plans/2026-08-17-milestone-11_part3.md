# milestone-11 实施计划 · 第 2 波（集成点②后展开）

> REQUIRED SUB-SKILL: superpowers:subagent-driven-development。前置阅读主计划 Global Constraints（继续有效：不 commit / 中文 / 双链只增不减 / 遇文件锁重试）。
> 基线（2026-08-18 集成点②实测）：dotnet **748** / EditMode **1315** 双链全绿。
> 第 1 波已交付：17 生物（3 敌对+9 被动+旧 5）、60+ 方块（8 树种/12 花草/家具/功能/农业）、弓盾爆、农牧繁殖、~200 张 AI 贴图。

## 第 2 波总则

- 热点归属：`PlayerContext.cs` 归 W2-1（盔甲）；`BlockInteraction.cs`/`WorldBootstrap.cs` 本波禁改（如需接线写进汇报，收口批处理）；UI 文件按各卡清单互斥
- 所有新 UI 物品格走 `ItemSlotDrawer`、贴图走 `Assets/StreamingAssets/ui/` 或已入库 `Assets/Art/UI/`

## Task W2-1：盔甲穿戴栏

**Files:**
- Modify: `Assets/Scripts/Unity/Gameplay/PlayerContext.cs`（ArmorSlots[4] + Defense 汇总从手持改为穿戴；`PlayerSnapshot` 扩 4 槽位进存档——`LevelDataCodec` 往返）
- Modify: `Assets/Scripts/Core/Player/PlayerInventory.cs`（或独立 ArmorInventory：4 槽读写、only-armor 可入）
- Create: `Assets/Scripts/Unity/UI/ArmorSlotsUi.cs`（背包界面右侧 4 格，穿戴生效提示；ItemSlotDrawer 复用）
- Test: 穿戴防御叠加/手持旧模型迁移（金系攻击仍走 attackDamage 手持）/存档 roundtrip/UI 槽位交互

## Task W2-2：附魔系统

**Files:**
- Create: `Assets/Scripts/Core/Enchanting/EnchantSystem.cs`（经验消耗 5/级；锋利+1攻/级、效率挖速×1.2/级、耐久+20%/级，封顶 3 级；写入 ItemStack.Metadata 高位或 Enchantments 字典——与 LevelData.PlayerEnchantments 对齐）
- Modify: `Assets/Scripts/Unity/Player/BlockInteraction.cs` **本卡独占**：手持 enchanted_book 右键装备→融合（书消失、装备带魔）
- items/recipes：附魔台方块（enchanting-table 贴图已入库 `Assets/Art/UI/`? 检查——没有就用 codex 风格程序占位）；`enchanted_book` 获取：书+青金石（lapis 已注册）合成
- Test: 经验扣减/等级封顶/效果三件套数值/Metadata 编码不撞耐久位/融合消耗

## Task W2-3：UI 三件（血心/飘字/箱子）

**Files:**
- Modify: 血 UI 随上限画心（现固定 10 心——读 `PlayerContext.Health` 有效上限，>10 心按半心排两行）
- Create: `Assets/Scripts/Unity/UI/FloatTextUi.cs`（经验 +N 飘字、伤害数字；1s 上浮淡出）
- Create: `Assets/Scripts/Unity/UI/ChestUi.cs`（27 格 ItemSlotDrawer + 取放/Shift 整组；打开走 BlockInteraction 箱子分支已有 no-op 占位——**本卡独占 BlockInteraction 该分支**替换为开 UI；ChestSystem Core 已备）
- Test: 心数随上限/半心/飘字计时与文本/箱子取放与存档往返（EditMode IMGUI 测试模式照 TradeUi 测试）

## Task W2-4：任务第二章

**Files:**
- Create: `Assets/StreamingAssets/quests/chapter2.json`（8 步：做床过夜→种小麦→收 first harvest→驯羊剪毛→铁盔甲→附魔→弓杀骷髅→击败苦力怕，链式解锁照 chapter1 模式）
- Modify: `QuestChainLoader`/`QuestSystem`（多章节加载与切换；事件缺口：SleepInBed/HarvestCrop/EnchantItem/KillKind——`QuestEventBus` 补转发，游戏逻辑不感知任务系统铁律保持）
- Test: chapter2 全链可解锁/旧档无 chapter2 字段全新开始/事件转发不侵入玩法代码

## 集成点 ③（第 2 波收口）

评审提交 → 双链全绿 → `--ui-shot` 截图管线过新 UI（血心/盔甲栏/箱子/飘字）→ 展开第 3 波（`_part4.md`：村庄/Boss/下界合金/BGM/天气/云/粒子/和平模式/楼梯评估）→ 终审 + CLAUDE.md 更新 + 给孩子的验收剧本。
