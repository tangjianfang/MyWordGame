# 方块掉落表格式（block_drops.json）

挖掉一个方块时掉什么。一个 JSON 数组，每个元素描述一种方块的掉落，由
`BlockDropsLoader.Load` 解析成 `BlockDrops` 注入 `BlockInteraction`（挖矿掉落）。

## 一个完整的例子

```json
{
  "blockId": "stone",
  "blockNumericId": 1,
  "drops": [
    { "itemId": "cobblestone", "countMin": 1, "countMax": 1 }
  ]
}
```

## 字段说明

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `blockId` | 字符串 | **是** | 方块的字符串 id（可读性 + 掉落数量掷骰的确定性种子来源） |
| `blockNumericId` | 整数 | **是** | 方块的 numericId，**必须与 `blocks/*.json` 里的同 id 方块一致**（FunctionalBlockFilesTests 逐条对表） |
| `drops` | 数组 | **是** | 掉落条目列表；空数组 = 明确声明「挖了就没」（如 bedrock） |
| `drops[].itemId` | 字符串 | **是** | 掉落的物品 id，**必须是 items/*.json 里已注册的 id**——悬空引用加载即抛 `InvalidDataException`（`BlockDropsTests` 真实加载器集成测试守着） |
| `drops[].countMin` / `countMax` | 整数 | **是** | 掉落数量区间。数量用 `BlockDrops.RollCount` 的整数哈希掷骰（blockId+itemId 派生种子），**确定性**：同一份表每次解析结果一致，不持有随机数对象 |
| `drops[].chance` | 小数 | 否 | 触发概率 [0,1]，缺省 1 = 恒掉（m11 W2-4 E1 起）。带 chance 的条目在**每次查询时**掷骰（整数哈希；挖掘路径把方块坐标揉进 salt，同一方块两次挖独立掷）——如 `tall_grass` 0.3 掉 `seeds_wheat`（农业首发种子来源）。越界值加载即抛 `InvalidDataException` |

## v1 暂缺的掉落条目（m11 ②，别当漏配）

- **七新树种原木/树叶**（birch/pine/cedar/jungle/bush/sequoia/cherry）：原木掉对应原木物品 ×1、
  树叶 10% 掉对应树苗——**这批物品（`birch_log`、各树苗等）尚未在 items/*.json 注册**，
  悬空 itemId 会让加载器直接抛异常，先整段不写。等物品注册后补条目。
- **12 花草**（flower_* / tall_grass / fern / mushroom_*）：除 tall_grass 外**刻意不掉**——
  装饰方块挖了就没，防止随手清场刷出一地花。tall_grass 是唯一例外（m11 W2-4 E1）：
  30% 掉 `seeds_wheat`，是农业的首发种子来源（见上表 chance 字段）。
- **作物方块**（wheat/beet/mung_stage0-2）：不走这张表——成熟收获走 `FarmSystem.Harvest`
  的确定性掉落（产物 1-2 + 种子），挖未成熟作物无掉落（无条目即无掉落）。

## 改完怎么验证

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln
```

`BlockDropsTests`（真实加载器）抓悬空 itemId 与非法区间；`FunctionalBlockFilesTests`
对表 `blockNumericId` 与方块 id 的一致性。
