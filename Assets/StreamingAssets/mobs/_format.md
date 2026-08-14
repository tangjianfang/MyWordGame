# Mob 数据文件格式

这个目录里的 JSON 文件定义 Mob 的行为数据，由 Core 层的
`MyWorld.Core.Entities.ItemDropTable` 和 `MyWorld.Core.Entities.MobSpawnRules`
在启动时加载。当前两个文件：
- `drop_tables.json`：死亡掉落
- `spawn_rules.json`：自然刷新规则

## drop_tables.json

键为 Mob 名称（`Pig` / `Cow` / `Chicken` / `Zombie`），值为该 mob 的
**掉落条目列表**。每条条目独立掷骰，按 `chance` 累计概率命中。

```json
{
  "Pig":     [{ "itemId": 1008, "countMin": 1, "countMax": 3, "chance": 1.0 }],
  "Zombie":  [
    { "itemId": 1010, "countMin": 0, "countMax": 2, "chance": 0.5 },
    { "itemId": 1004, "countMin": 1, "countMax": 1, "chance": 0.05 }
  ]
}
```

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `itemId` | 整数 | 是 | 物品 numericId，必须与 `Assets/StreamingAssets/items/*.json` 一致 |
| `countMin` | 整数 | 否（默认 1） | 单次掉落堆叠数下界（含） |
| `countMax` | 整数 | 否（默认 = countMin） | 单次掉落堆叠数上界（含） |
| `count` | 整数 | 否（旧 schema） | 若 `countMin`/`countMax` 都缺省则用此值兜底 |
| `chance` | 小数 | 否（默认 1.0） | 单条命中概率 0..1；多条时按列表顺序累计，余量未命中返回 null |

## spawn_rules.json

键为 Mob 名称，值为单条刷新规则对象。

```json
{
  "Pig":    { "biomes": ["Plains", "Forest"], "minLight": 9, "weight": 10 },
  "Zombie": { "biomes": ["Plains", "Forest", "Mountains"], "minLight": 0, "weight": 5 }
}
```

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `biomes` | 字符串数组 | 是 | 允许刷新的 biome 列表（值是 `MyWorld.Core.WorldGen.Biome` 枚举的 `ToString()`） |
| `minLight` | 整数 | 否（默认 0） | **最低光照阈值**：`lightLevel < minLight` 一律拒绝 |
| `weight` | 整数 | 否（默认 0） | 相对权重；实际命中率 = `weight × 5%`（weight=10 → 50%） |

## 添加新 Mob

1. 在 `Assets/Scripts/Core/Entities/MobKind.cs` 加 enum 值
2. 在 `Assets/Scripts/Core/Entities/Mob.cs` 加 `Mob.Create(mobTypeId, ...)` 分支
3. 在本目录的 `drop_tables.json` / `spawn_rules.json` 加键
4. 跑 `dotnet test` 确认 `ItemDropTableTests` + `MobSpawnRulesTests` 通过

## 改完怎么验证

```bash
dotnet test --filter "FullyQualifiedName~ItemDropTableTests|FullyQualifiedName~MobSpawnRulesTests"
```

错误会立刻报：找不到 JSON 路径、JSON 非法、MobKind 名打错、biome 字符串拼错。
