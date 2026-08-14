# milestone-4 设计：完整存档系统（玩家进度 + 方块世界）

> 日期：2026-08-14
> 状态：已通过设计评审（父子共同开发项目）
> 前置：milestone-3（玩法核心 + 生物 AI）已完成，dotnet 417 / EditMode 509 全绿

## 背景与问题

milestone-1 写了完整的方块存档层（`Core/Persistence/RegionFile.cs` 32×32 区块聚合 +
`ChunkSerializer.cs` 非空 section Deflate 压缩），Core 层测试全绿——**但从未接入 Unity 运行时**：
`World` 没有脏区块追踪，`WorldBootstrap` 没有任何 save/load 调用。玩家侧更是一片空白：
背包、位置、生命、饥饿、经验、时间、熔炉进度、地面掉落物，退出即全部丢失。

这是当前「玩下去」体验的最大缺口：挖 30 分钟铁锭，重开全没了。它也是后续所有玩法
（附魔 / 红石 / 箱子）的地基——那些系统的状态都需要保存。

## 目标

退出重进，游戏状态无损：方块改动在、背包在、熔炉进度在、掉落物在、时间连续。
自动保存，孩子不需要记任何按键。

## 非目标（YAGNI，明确不做）

- 多存档槽 / 世界选择界面（单世界 `worlds/<seed>/`）
- 生物序列化（重进重新生成，Minecraft 同款行为）
- 手动保存按键、保存提示 UI / 动画
- 红石 / 门 / 箱子状态（尚无可存的生产状态）

## 总体架构

双层存档，落在 `Application.persistentDataPath/worlds/<seed>/`（当前单世界 seed=42）：

```
worlds/42/
├── level.dat          ← JSON（Newtonsoft）：玩家全套 + 世界轻量状态
└── regions/
    ├── r.0.0.mwr      ← 二进制（RegionFile，魔数 MWRG 小端）：方块改动态
    └── r.-1.2.mwr
```

为什么双层：方块层复用已有且测试过的 RegionFile 二进制格式（**不 bump FormatVersion**），
改动多的世界不会让 JSON 膨胀；level.dat 人可读、好调试、可手改。两层靠 level.dat 里的
seed 字段校验一致性。

## Core 层设计（无 UnityEngine，netstandard2.1）

### LevelData（DTO）+ LevelDataCodec

`Core/Persistence/LevelData.cs`：

```csharp
public class LevelData
{
    public long Seed;                 // 与 bootstrap seed 校验一致才恢复
    public float TimeTick;            // TimeOfDay.CurrentTick（float，0..24000）
    public PlayerSnapshot Player;     // 见下
    public FurnaceSnapshot Furnace;   // 见下
    public List<DropSnapshot> Drops;  // 地面掉落物
}

public class PlayerSnapshot
{
    public float X, Y, Z;             // Position（脚底中心）
    public float VX, VY, VZ;          // Velocity
    public int Health, Hunger; public float Saturation;
    public int ExpCurrent, ExpLevel;
    public List<SlotSnapshot> Hotbar; // 9 格，空格 itemId=0
    public List<SlotSnapshot> Inventory; // 背包格
}

// Metadata 必须保存：工具耐久（MaxDurabilityMask/CurDurabilityMask）存在这里
public class SlotSnapshot { public int ItemId; public int Count; public ushort Metadata; }

public class FurnaceSnapshot
{
    public SlotSnapshot Input, Fuel, Output;
    public float SmeltProgress;       // 当前烧炼进度秒
}

public class DropSnapshot { public int ItemId; public int Count; public float X, Y, Z; }
// 掉落物不存 SpawnTime：恢复时统一赋当前 Time.time，0.5s 拾取宽限期重新计时
```

`Core/Persistence/LevelDataCodec.cs`：

- `Save(LevelData data, string path)`：先写 `path.tmp` 再 `File.Move` 原子替换
  （半写的文件永远不会覆盖上一次好档）
- `Load(string path)`：解析失败抛 `InvalidDataException`，由 Unity 侧决定降级策略

### World 脏区块追踪

`Core/Voxel/World.cs` 增强（读容忍、写严格不变）：

- `SetBlock` 成功后把所在 `ChunkPos` 加入内部 `HashSet<ChunkPos> _dirty`
- `IEnumerable<ChunkPos> DirtyChunks { get; }`：只读枚举
- `void ClearDirty(ChunkPos pos)`：单区块保存成功后调用
- 既有 API 与行为零变化，新增仅此三个

### RegionSaveCoordinator

`Core/Persistence/RegionSaveCoordinator.cs`：

- `SaveDirty(World world, string regionsDir)`：把 dirty 区块按 32×32 分组，逐 region
  打开（存在则读旧记录合并，不存在则新建）→ `ChunkSerializer` 序列化 dirty 区块 → 写回。
  单个 region 写失败不影响其它 region（该批 chunk 保持 dirty，下轮重试）
- `TryLoadChunk(World world, ChunkPos pos, string regionsDir)`：查 region 文件 → 命中则
  反序列化 `ChunkColumn` 覆盖 world 内区块 → 返回 bool。未命中/文件损坏返回 false
  （调用方按 seed 生成结果保留）

## Unity 层接线

### SaveLoadService（新 MonoBehaviour，WorldBootstrap 挂载）

- **启动**（Bind 阶段，bootstrap 初始化顺序最后）：
  1. `LevelDataCodec.Load(worlds/42/level.dat)`；异常 → 重命名 `level.dat.corrupt` +
     `Debug.LogWarning`，全新开始（读容忍）
  2. `data.Seed != bootstrap seed` → 整档忽略 + 警告
  3. 恢复顺序：TimeTick → 玩家快照（位置/速度/生命/饥饿/经验/背包）→ 熔炉 → 掉落物
- **Update**：累计 30s 触发 `SaveNow()`（收集状态量小，同步写不卡帧）
- **OnApplicationQuit**：最后保存一次
- `SaveNow()`：从 PlayerContext / World 收集全部状态 → `LevelDataCodec.Save` +
  `RegionSaveCoordinator.SaveDirty`

### ChunkStreamer 恢复钩子

区块 mesh 生成前的数据阶段插入一步：生成器按 seed 生成 `ChunkColumn` 后，调
`RegionSaveCoordinator.TryLoadChunk` 覆盖——有存档的区块用存档，没有的照旧。
未改动区块零开销（一次文件存在性检查）。

### PlayerContext 侧

背包 / 熔炉 / 时间 / 生命状态都已在 `PlayerContext` 上，只需补「收集快照」和
「应用快照」两个方向的映射方法，不新增系统。

## 错误处理原则

读容忍、写严格（与区块读取同哲学）：

| 场景 | 行为 |
|---|---|
| `level.dat` 解析失败 | 重命名 `.corrupt`，全新开始 + LogWarning |
| level.dat seed ≠ bootstrap seed | 忽略整档 + 警告（防串档） |
| region 文件损坏 / 版本不符 | 该区域按 seed 重新生成，游戏照开 |
| SaveNow 期间 IO 失败 | log 错误，chunk 保持 dirty 下轮重试 |

## 测试策略

**dotnet（Core，net9 链）**：
- `LevelDataCodecTests`：round-trip 全字段、原子写（tmp 残留不影响旧档）、坏 JSON 抛
- `WorldDirtyTests`：SetBlock 标脏、ClearDirty 清除、只读区块不脏
- `RegionSaveCoordinatorTests`：跨 region 分组、合并旧记录、TryLoadChunk 命中/未命中/损坏

**EditMode（Unity 链）**：
- `SaveLoadServiceTests` 全链路：挖方块 + 塞背包 + 熔炉烧到一半 + 地面掉一个掉落物 →
  `SaveNow()` → 新 World + 新 PlayerContext → Load → 断言方块 / 背包 / 熔炉进度 /
  掉落物 / 时间全部一致
- seed 不匹配忽略档、坏 level.dat 降级全新开始

**验收清单**：
1. dotnet 417+ 全绿；EditMode 509+ 全绿；visual-smoke 4/4
2. 实机（build-and-run）：挖 → 捡 → 熔炉烧到一半 → 杀进程（模拟断电，不触发 Quit 保存）
   → 重进 → 30s 前的状态全部还在
3. 存档目录只含 `level.dat` + `regions/`，无 tmp 残留

## 与既有约束的关系

- Core 层零 `UnityEngine`（LevelData/Codec/Coordinator 全纯 C#，Newtonsoft 已是既有依赖）
- 确定性生成不变：恢复 = seed 生成 + 存档 overlay，与生成顺序无关
- RegionFile 二进制格式不动、`FormatVersion` 不 bump（Overlay 走读旧路径）
- 所有注释 / 断言中文；新增文件 LF + C# 4 空格缩进
