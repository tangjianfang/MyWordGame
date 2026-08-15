# 任务链定义文件格式

这个目录里每个 `.json` 文件定义一章引导任务链（当前只有 `chapter1.json`）。
文件内容是一个**任务数组**，数组顺序就是任务解锁顺序——前一个完成才解锁下一个。
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
    "id": "ch1_08_survive_night",
    "name": "活过一夜",
    "desc": "天黑别慌，撑到日出就算赢下第一夜",
    "condition": { "type": "SurviveNight" },
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
| `type` | 字符串 | **是** | — | 四选一：`ObtainItem` / `CraftItem` / `SmeltItem` / `SurviveNight` |
| `itemId` | 整数 | 物品类条件必填 | — | 必须是 `items/*.json` 里注册过的 numericId（log=1000、plank=1001、cobblestone=1003、iron_ingot=1004、wooden_pickaxe=1400、stone_pickaxe=1401、crafting_table=1700）。`SurviveNight` 时忽略 |
| `count` | 整数 | 物品类条件必填 | — | 要求的数量，≥ 1。`SurviveNight` 时忽略 |

## 四类条件语义

- **ObtainItem**：事件到达时「背包现存量 ≥ count」即完成。事件由拾取/挖方块接线发出，
  事件里带的是当前库存量，不是本次增量
- **CraftItem**：合成产出该物品，本次产出数量 ≥ count 即完成
- **SmeltItem**：从熔炉取出该物品，本次取出数量 ≥ count 即完成
- **SurviveNight**：昼夜循环跨过一次日出即完成，无参数

## 写错会怎样（写严格）

加载失败立刻抛异常，游戏拒绝带病启动任务链：

- 文件不存在 → `FileNotFoundException`
- JSON 语法错误 / 解析为空 / 数组为空 / 缺 `id`、`name`、`condition` / `id` 重复 /
  物品类条件缺 `itemId`（≤0）或 `count`（<1）/ `rewardExp` 为负 → `InvalidDataException`
- `condition.type` 不在四选一里 → `ArgumentException`

注意：`itemId` 是否真的在 `items/*.json` 注册过，**这个加载器不查**——悬空引用由
EditMode 侧的集成守卫测试（C2）负责，报错时会指出具体的任务 id。
