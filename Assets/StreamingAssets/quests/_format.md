# 任务链定义文件格式

这个目录里每个 `chapterN.json` 文件定义一章引导任务链（当前有 `chapter1.json` / `chapter2.json`）。
文件内容是一个**任务数组**，数组顺序就是任务解锁顺序——前一个完成才解锁下一个；
**章节文件也按 N 递增顺序解锁**——一章全链完成才开下一章（HUD 会弹「第 N 章已解锁」）。
改任务内容不需要改任何代码；加一章新 JSON 后由 Unity 侧 bootstrap 加载。

## 一个完整的例子

```json
[
  {
    "id": "ch1_01_punch_log",
    "name": "挖一根原木",
    "desc": "对着树干按住左键，挖下 1 根原木",
    "condition": { "type": "ObtainItem", "itemId": 1000, "count": 1 },
    "rewardExp": 5
  },
  {
    "id": "ch2_07_bow_kill_skeleton",
    "name": "用弓击败骷髅",
    "desc": "做一把弓（3 木棍 + 3 线）和箭，夜里拉满弓把一只骷髅射死",
    "condition": { "type": "KillKind", "kind": "Skeleton", "weapon": "bow", "count": 1 },
    "rewardExp": 30
  }
]
```

## 字段说明

| 字段 | 类型 | 必填 | 默认 | 说明 |
| --- | --- | --- | --- | --- |
| `id` | 字符串 | **是** | — | 任务唯一标识，同文件内不许重复。**存档里记的是它，定了就别改** |
| `name` | 字符串 | **是** | — | HUD 显示的任务名（中文） |
| `desc` | 字符串 | 否 | `""` | 给玩家的操作指引（中文） |
| `condition` | 对象 | **是** | — | 完成条件，见下表 |
| `rewardExp` | 整数 | 否 | `0` | 完成时入账的经验值，不能为负 |

## condition 字段说明

| 字段 | 类型 | 必填 | 默认 | 说明 |
| --- | --- | --- | --- | --- |
| `type` | 字符串 | **是** | — | 十二选一，见下节 |
| `itemId` | 整数 | 物品类条件必填 | — | 必须是 `items/*.json` 里注册过的 numericId（chapter2 里 `seeds_wheat`=1019）。无条件参数类与计数动作类**忽略**（写了也当 0） |
| `count` | 整数 | 计数类条件必填 | — | 要求的数量，≥ 1。无条件参数类**忽略**（固定按 1 显示 0/1） |
| `kind` | 字符串 | KillKind 必填；FeedAnimal 可选 | — | 生物名，必须是代码里 `MobKind` 的成员（如 `Sheep`/`Skeleton`/`Creeper`，大小写敏感）。FeedAnimal 不写 = 不限物种 |
| `weapon` | 字符串 | 否 | — | 仅 KillKind 使用：限定击杀武器，当前合法值 `bow`（箭击杀）。不写 = 任意武器都算 |

## 十二类条件语义

**无条件参数类**（事件到达即完成，itemId/count/kind/weapon 都忽略）：

- **SurviveNight**：昼夜循环自然跨过一次日出
- **SleepInBed**：夜里在床上睡觉（时间跳到早晨）
- **TillSoil**：锄头把草/泥翻成耕地一次
- **EquipArmorFull**：四件盔甲穿齐一次
- **EnchantItem**：完成一次附魔

**物品类**（类型 + itemId 匹配，count 有各自的比较口径）：

- **ObtainItem**：事件到达时「背包现存量 ≥ count」即完成。事件由拾取/挖方块接线发出，
  事件里带的是当前库存量，不是本次增量
- **CraftItem**：合成产出该物品，count 是**任务内累计**（本次产出数量逐笔加）
- **SmeltItem**：从熔炉取出该物品，count 同上累计
- **SowSeed**：播下该种子（itemId = 种子的 numericId），count 累计

**计数动作类**（itemId 恒忽略，count 累计）：

- **HarvestCrop**：收获 N 株成熟作物（不限种类）
- **FeedAnimal**：喂 N 只动物（kind 可选限定物种）
- **KillKind**：击杀 N 只指定生物（kind 必填；weapon 可选，`bow` = 必须箭击杀）

## 写错会怎样（写严格）

加载失败立刻抛异常，游戏拒绝带病启动任务链：

- 文件不存在 → `FileNotFoundException`
- JSON 语法错误 / 解析为空 / 数组为空 / 缺 `id`、`name`、`condition` / `id` 重复 /
  计数类条件缺 `count`（<1）/ 物品类条件缺 `itemId`（≤0）/ `rewardExp` 为负 → `InvalidDataException`
- `condition.type` 不在十二选一里 → `ArgumentException`
- `kind` 不是合法生物名，或 KillKind 不写 kind → 抛异常（前两者 `ArgumentException`、后者 `InvalidDataException`）

注意：`itemId` 是否真的在 `items/*.json` 注册过，**这个加载器不查**——悬空引用由
EditMode 侧的集成守卫测试负责，报错时会指出具体的任务 id。

## 存档与旧档兼容

- 每章进度各存一份（`level.dat` 的 `QuestChapters` 数组，顺序与章节文件一致）
- 旧档只有单章 `Quest` 字段：恢复进第一章，后续章节全新开始；
  若旧档第一章已全链完成，读档后直接解锁第二章
