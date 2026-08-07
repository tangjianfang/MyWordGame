# 玩家层设计（里程碑 2）

## 背景与目标

里程碑 1 结束时，世界能渲染了：按下 Play 能用自由飞行相机漫游 7×7 区块的地形，
贴图正确、区块拼接无缝、URP 已接入，187 个测试全过。

但那还不是"游戏"——相机穿墙飞行，方块不能挖也不能放，走出预生成的 7×7 范围就是虚空。
本里程碑把"能看"变成"能玩"。

**验收标准**：按下 Play 后，玩家站在地表上，能走、能跳、会被墙挡住、会掉进坑里；
准星指着的方块有高亮框，左键挖掉、右键放上；往任意方向一直走，地形持续生成不会走到边界。

## 范围（本里程碑只做这些）

- **Core：`PlayerMotor`** —— 重力、跳跃、地面/空中移动，走已有的 `VoxelCollision.Move`。
  纯函数式：`(状态, 输入, 参数, dt) → 新状态`，不持有 Unity 对象，可完整单测
- **Core：`BlockPlacement`** —— 由射线命中结果推出放置位置，并拒绝会把玩家卡进方块的放置
- **Core：`DirtySections`** —— 改一个方块之后，哪些区块段需要重建网格（含跨区块边界的邻居）
- **Unity：`PlayerController`** —— 输入 → `PlayerMotor` → transform；鼠标视角、眼高、指针锁定
- **Unity：`BlockInteraction`** —— 相机射线选中方块、线框高亮、左键挖、右键放
- **Unity：`ChunkViewRegistry`** —— (区块, 段) → `ChunkSectionView` 的索引，支持按脏标记重建
- **Unity：`ChunkStreamer`** —— 跟随玩家位置生成/卸载区块列
- 预览场景换成玩家出生点，`FreeFlyCamera` 退役

## 范围外（明确不做，留给后续里程碑）

- **存档**（里程碑 3）：本里程碑挖掉的方块退出即丢，`RegionFile` 已就绪但不接
- **物品栏、准星以外的 UI、手持物品**（里程碑 4）：本里程碑放置的方块类型写死成一个
  可在 Inspector 里改的字段
- **多线程网格生成**（里程碑 5）：流式加载先在主线程做，用"每帧最多处理 N 个"限流
- **游泳、爬梯、潜行、疾跑视野变化**：先做最基础的走跳
- **方块破坏进度（长按几秒才碎）**：先做单击即碎
- **实体、怪物、昼夜**：不在路线图的这一段

## 为什么玩家运动放进 Core

直觉上"玩家控制"是 Unity 侧的事——它读输入、动 transform。但**运动解算本身**
（重力累积、跳跃初速、撞墙后速度归零、是否落地）是纯数值逻辑，放 Core 有三个好处：

1. **可测**。`dotnet test` 里能断言"从 y=100 自由落体 1 秒后速度是 −9.8"、
   "跳跃后第一帧离地"、"贴着墙推 100 帧位置不变"。放 MonoBehaviour 里就只能靠手玩
2. **确定性**。同样的输入序列必然得到同样的轨迹，将来做回放、联机预测都依赖这一点
3. **`VoxelCollision` 已经在 Core 了**。让调用方跨层反而更绕

Unity 侧只留三件事：读输入、把输入旋转到世界空间、把结果写回 transform。

分界线很清楚：**Core 不知道"相机朝哪"**。`PlayerInput` 里的 `MoveX`/`MoveZ` 是
**已经旋转好的世界空间水平方向**，由 Unity 层根据相机 yaw 算出来。Core 只管把它当成
一个方向意图。

## 玩家运动模型

### 状态与输入

```
PlayerState  = { Position: Float3（脚底中心）, Velocity: Float3, IsGrounded: bool }
PlayerInput  = { MoveX: float, MoveZ: float（世界空间，已归一化到 ≤1）, Jump: bool, Sprint: bool }
```

`Position` 取**脚底中心**而不是几何中心：放置方块、落地判定、出生点摆放都是按脚下算的，
取脚底中心能省掉一堆 `± height/2`。`Aabb.FromBottomCenter` 已经是这个约定。

### 参数表

| 参数 | 值 | 说明 |
| --- | --- | --- |
| `Width` | 0.6 | 包围盒宽度（比一格窄，才能贴着墙走过 1 格宽的缝） |
| `Height` | 1.8 | 包围盒高度（站直挡住 2 格，跳起来能上 1 格台阶） |
| `EyeHeight` | 1.62 | 相机相对脚底的高度 |
| `WalkSpeed` | 4.3 | 格/秒 |
| `SprintMultiplier` | 1.3 | 按住 Shift 的倍率 |
| `JumpSpeed` | 8.4 | 起跳瞬间的 Y 速度，配合下面的重力约合 1.25 格跳跃高度 |
| `Gravity` | −28.0 | 格/秒²。比现实的 −9.8 大得多——体素游戏里现实重力显得"飘" |
| `MaxFallSpeed` | −60.0 | 终端速度，防止长距离下落穿透薄地板 |
| `AirControl` | 0.35 | 空中水平加速相对地面的比例 |
| `GroundFriction` | 12.0 | 松开方向键后水平速度的衰减率（每秒） |
| `AirFriction` | 1.0 | 空中几乎不衰减 |

这些数值是**起点不是终点**，Inspector 里可调。写进设计文档是为了让"手感不对"这类
反馈能落到具体数字上，而不是停留在"感觉太飘"。

### 单步解算顺序

```
1. 水平：目标速度 = 方向 × 速度上限
   地面用全额加速，空中乘 AirControl；没有输入时按摩擦系数向 0 衰减
2. 竖直：若 IsGrounded 且按下跳跃 → Velocity.Y = JumpSpeed，IsGrounded = false
        否则 Velocity.Y += Gravity × dt，并夹到 MaxFallSpeed
3. 用 VoxelCollision.Move 推进 Velocity × dt
4. 被挡的轴速度归零（撞墙不该继续攒速度，否则松开方向键会突然弹出去）
5. IsGrounded 取解算结果
```

**为什么先算速度再解算碰撞、而不是反过来**：`VoxelCollision.Move` 已经把"被挡住多少"
算进返回值了，速度归零必须发生在它之后。反过来会让贴地站立时 Y 速度每帧都在
"−28×dt → 0" 之间跳，落地判定不稳。

**落地判定的一个陷阱**：`MoveResult.IsGrounded` 的定义是 `blocked[1] && delta.Y < 0`，
也就是"这一帧向下移动被挡住了"。站着不动时 Y 速度是 `Gravity × dt`（负值），
所以每帧都会重新确认落地——这是对的，不需要额外的"贴地射线"。

## 方块交互

### 选中

从相机位置沿视线方向 `VoxelRaycaster.Cast`，最大距离 `ReachDistance = 5.0`。
数据源用 `WorldSolidSource`（判 `Solid`，挡移动的才能选中——水选不中，符合直觉）。

命中后画一个线框立方体高亮。线框用 `GL` 立即模式还是一个稍微放大的反面 Mesh？
**用后者**：`GL` 要挂 `OnRenderObject` 且在 URP 下行为不一致；一个 1.002 倍大小的
`Mesh`（12 条边做成细长方体）配无光照材质更省事，也能被 URP 正常渲染。

### 挖掘

左键 → 命中格设为空气 → 标脏 → 重建网格。

### 放置

右键 → 目标格 = 命中格 + 法线。三个拒绝条件：

1. 目标格不是空气（射线打到的是**背面**时会出现，比如站在方块里往外看）
2. 目标格的方块 AABB 与玩家 AABB 相交——**否则玩家会把自己封进方块里**
3. 目标格超出世界高度 `[-64, 320)`

第 2 条是必须的，且**放在 Core 里测**：它是纯几何判断，`Aabb` 已经有了。

## 网格重建：脏标记的范围

改一个方块，要重建的不只是它所在的那个区块段。因为贪心网格生成时会采样**邻居**来剔除
接缝面，所以：

- 方块所在的段——必须重建
- 若方块在段的**上下边界**（`localY == 0` 或 `15`）——上/下相邻段也要重建
- 若方块在区块的**水平边界**（`localX/localZ == 0` 或 `15`）——对应的邻区块同一段也要重建
- 角上的方块最多牵连 **1 + 3 = 4 个水平方向 × 2 个竖直 = 8 个段**（实际上是
  1 + 3 + 1 + 3 = 8，含自身）

这段"哪些段变脏"是纯坐标运算，放 Core 的 `DirtySections`，用测试钉死。
Unity 侧的 `ChunkViewRegistry` 只负责按这个列表去重建。

**为什么不无脑重建 3×3×3 = 27 个段**：改一个方块要重建 27 个段，每段几毫秒，
连续挖掘时会明显掉帧。精确到 8 个（且绝大多数情况只有 1 个）差别很大。

## 区块流式加载

```
每帧：
  1. 算出玩家所在区块 (cx, cz)
  2. 需要加载的集合 = 以 (cx,cz) 为中心、半径 LoadRadius 的方形
  3. 卸载：已加载但不在集合里的（超出 LoadRadius + 1，留一格迟滞避免边界抖动）
  4. 加载：在集合里但未加载的，按到玩家的距离排序，每帧最多处理 ChunksPerFrame 根
```

**必须先把区块列灌进 `World`，再建网格**——这是里程碑 1 已经踩过的坑
（`WorldBootstrap` 的注释里写着）：建网格要采样邻区块才能剔除接缝面，边生成边建网格
会让先建的那几根在接缝处凭空多出一整面。流式加载里这个问题更隐蔽，因为"邻居还没加载"
是常态。

处理方式：**生成与建网格分成两个队列**。一根区块列生成完只进 `World`；
只有当它的 4 个水平邻居都已在 `World` 里时，才允许进建网格队列。这样接缝永远是对的，
代价是最外圈一直不建网格（玩家看不到，因为它在视距之外）。

| 参数 | 值 | 说明 |
| --- | --- | --- |
| `LoadRadius` | 6 | 13×13 = 169 根区块列 |
| `UnloadRadius` | 8 | 迟滞：走回头路时不会立刻重新生成 |
| `ChunksPerFrame` | 2 | 生成 + 建网格的限流。169 根约 85 帧铺满 |

## 数据流

```
Input（Unity）
  → 相机 yaw 旋转 → PlayerInput（世界空间）
  → PlayerMotor.Step（Core，内部调 VoxelCollision.Move → WorldSolidSource）
  → PlayerState
  → transform.position（Unity）

鼠标点击（Unity）
  → 相机位置/朝向 → VoxelRaycaster.Cast（Core）→ VoxelRayHit
  → BlockPlacement.Resolve（Core）→ 目标格 or 拒绝
  → World.SetBlock（Core）
  → DirtySections.Collect（Core）→ (区块, 段) 列表
  → ChunkViewRegistry.Rebuild（Unity）

玩家位置（Unity）
  → ChunkStreamer：生成/卸载队列 → WorldGenerator.Generate（Core）→ World.AddChunk
  → ChunkViewRegistry.Ensure（Unity）
```

Core 依旧零 Unity 依赖。

## 输入方案

项目里 `com.unity.inputsystem` 已装，但里程碑 1 的 `FreeFlyCamera` 用的是**旧版
`Input.GetKey`**，且 `ProjectSettings` 里 `activeInputHandler: 0`（只启用旧版）。

**本里程碑继续用旧版 Input**。理由：切到新输入系统要建 `.inputactions` 资产、改
`activeInputHandler`（会触发一次编辑器重启）、所有代码改成回调式，是一整块独立的工作，
和"玩家能不能走能不能挖"没有关系。等真的需要手柄或按键重绑定（里程碑 4 的 UI 阶段）
再一次性切换，那时改动集中、好验证。

## 无头验证怎么做

玩家层的大部分逻辑在 Core，`dotnet test` 覆盖得到。Unity 侧真正需要眼睛的只有手感。
里程碑 1 建立的 `RenderSmokeCheck` 模式继续用，扩成 `PlayHarness`：
在编辑器非播放态下手动驱动若干帧，断言

- 玩家从出生高度下落后会停在地表且 `IsGrounded`
- 向前推 60 帧，位置确实前进了、没有陷进地里（`Position.Y` 不低于地表）
- 对着地面挖一格，该格变空气，且对应区块段的网格顶点数变了
- 放回去，顶点数复原

这挡不住"手感飘"，但能挡住"跳跃穿地板""挖了不刷新""流式加载漏区块"这类硬错误。
手感需要人来玩，这一点不假装能自动化。
