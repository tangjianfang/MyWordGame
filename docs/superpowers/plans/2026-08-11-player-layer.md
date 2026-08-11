# 玩家层实施计划（里程碑 2 · 完整版）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让世界从"能看"变成"能玩"——玩家站在地表上走跳、被墙挡、挖方块、放方块，往任意方向走地形持续生成。

**Architecture:** 运动解算、放置合法性、脏段收集三块纯数值逻辑放 Core（可 `dotnet test` 覆盖）；Unity 侧只做输入读取、坐标系转换、GameObject 生命周期、ChunkStreamer 流式加载、PlayHarness 无头验证。Core 依旧零 Unity 依赖。

**Tech Stack:** C# 9 / netstandard2.1（Core）、Unity 2022.3.62f3c1 + URP 14.0.11、NUnit、旧版 Input Manager。

---

## 设计依据

`docs/superpowers/specs/2026-08-07-player-layer-design.md`

## 与历史计划的关系

2026-08-07 写过两份计划：
- `docs/superpowers/plans/2026-08-07-player-layer.md`（已 commit，Task 1–3，Core 数据类型与运动）
- `docs/superpowers/plans/_part2.md`（未追踪，Task 4–7，BlockPlacement、DirtySections、ChunkViewRegistry、PlayerController）

两份文件仅覆盖到设计文档的一半（Task 1–7）。本文件（2026-08-11）将它们的内容**合并为单一文件**，并补上设计文档里 Task 7 之后的范围：

- Task 8 方块交互（射线选中 + 线框高亮 + 挖 + 放）
- Task 9 区块流式加载（ChunkStreamer，生成与建网格分两个队列）
- Task 10 Bootstrap 改造（WorldBootstrap 流式化、PreviewSceneBuilder 改成玩家出生点、删除 FreeFlyCamera）
- Task 11 无头验证 PlayHarness（编辑器非播放态手动驱动若干帧，断言下落停住、前进、挖了会刷新）

旧的两份文件**保留**为历史参考，不再更新。

---

## Global Constraints

来自 `CLAUDE.md`、`docs/specs/visual-text-conventions.md`、`art/README.md`，每个任务隐含遵守：

- **Core 零 Unity 依赖**：`MyWorld.Core.asmdef` 设了 `"noEngineReferences": true`，Core 出现任何 `UnityEngine.*` 编译不过
- **Core 零警告**：`MyWorld.Core.csproj` 设了 `TreatWarningsAsErrors=true`
- **Core 用 `Core/Math/Float3` / `Float2`**，不要 `Vector3`
- **C# 9 / netstandard2.1**
- **坐标换算只走 `VoxelCoords`**（`>>4` + `&15`），不要手写 `/`、`%`
- **读容忍写严格**：`World.GetBlock` 越界返回空气（不抛）；`SetBlock` 越界抛异常
- **`IsSolid`（`ChunkMeshSource`，判 `Opaque`）≠ `IsSolidAt`（`WorldSolidSource`，判 `Solid`）**
- **热路径用 `readonly struct` + 泛型约束**，避免虚调用
- **所有文档 / 注释 / 测试断言消息用中文**
- **.editorconfig**：LF 行尾、C# 4 空格、JSON/csproj 2 空格
- **global.json 固定 SDK 9.0.x**，不要改
- **`tools/dotnet/MyWorld.Tools.sln`** 是 .NET 端解决方案（根目录的 `.sln`/`.csproj` 是 Unity 生成的，已 gitignore）
- **dotnet 在 Git Bash 下需要补环境变量**（见 CLAUDE.md / `dotnet-needs-programfiles-x86-in-bash.md`）
- **Unity 批处理退出码不可信**，必须看日志和结果文件

---

## 文件结构

**Core 层新建**（`Assets/Scripts/Core/`）：

| 文件 | 职责 |
| --- | --- |
| `Player/PlayerState.cs` | 位置（脚底中心）、速度、是否着地 |
| `Player/PlayerInput.cs` | 世界空间的水平移动意图 + 跳跃 + 疾跑 |
| `Player/PlayerMotorSettings.cs` | 尺寸与运动参数，含 `Default` |
| `Player/PlayerMotor.cs` | `(状态, 输入, 参数, dt) → 新状态`，内部走 `VoxelCollision` |
| `Player/BlockPlacement.cs` | 由射线命中推出放置格，并判定是否合法 |
| `Voxel/SectionRef.cs` | `(区块, 段)` 不可变定位，含 `IEquatable<SectionRef>` |
| `Voxel/DirtySections.cs` | 改一个方块后哪些区块段需要重建网格（最多 8 个） |

**Tests 新建**（`Assets/Tests/EditMode/`）：

| 文件 | 测试数 | 覆盖 |
| --- | --- | --- |
| `Player/PlayerMotorGravityTests.cs` | 5 | 重力、终端速度、落地、撞天花板 |
| `Player/PlayerMotorMoveTests.cs` | 8 | 行走、疾跑、撞墙、沿墙滑、跳跃高度 |
| `Player/BlockPlacementTests.cs` | 5 | 法线放置、射线未命中、被玩家身体挡住、世界高度限制 |
| `Voxel/DirtySectionsTests.cs` | 7 | 段内 / 上下边界 / 水平边界 / 角上 / 去重 / 世界外 |

**Unity 层新建**（`Assets/Scripts/Unity/`）：

| 文件 | 职责 |
| --- | --- |
| `Rendering/ChunkViewRegistry.cs` | `(区块, 段) → ChunkSectionView`，按需建 / 重建 / 销毁 |
| `Player/PlayerController.cs` | 输入 → `PlayerMotor` → transform；鼠标视角与眼高 |
| `Player/BlockInteraction.cs` | 相机射线选中、线框高亮、左键挖、右键放 |
| `Player/SelectionBox.cs` | 线框立方体的 Mesh 与材质（12 边细长方体） |
| `World/ChunkStreamer.cs` | 跟随玩家生成 / 卸载区块列；生成与建网格分两队列 |
| `Editor/PlayHarness.cs` | 编辑器非播放态手动驱动若干帧，断言下落、前进、挖刷新 |

**修改**：

| 文件 | 改动 |
| --- | --- |
| `Bootstrap/WorldBootstrap.cs` | 从「一次性建 7×7」改成「建出生点周边 + 交给 ChunkStreamer」 |
| `Editor/PreviewSceneBuilder.cs` | 相机挂到玩家身上，不再生成 `FreeFlyCamera` |
| `Bootstrap/FreeFlyCamera.cs` | 删除 |

---

## 每个任务开始前

在**仓库根目录**执行，确认起点干净：

```bash
git status --short
```

Git Bash 下 `dotnet` 必须补环境变量（见 CLAUDE.md 与 `dotnet-needs-programfiles-x86-in-bash.md`），本计划里所有 `dotnet test` / `dotnet build` 都指这条完整命令：

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    dotnet test tools/dotnet/MyWorld.Tools.sln
```

短别名 `DOTNET`：

```bash
alias DOTNET='env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" "LOCALAPPDATA=C:\Users\tjf\AppData\Local" dotnet'
DOTNET test tools/dotnet/MyWorld.Tools.sln
```

Unity 编译（用于 Task 6/7/8/9/10/11）：

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
    -batchmode -nographics -projectPath "$(pwd)" -quit -logFile compile.log
grep -c "error CS" compile.log   # 预期：0
```

---

## Task 1: 玩家状态、输入与参数

三个纯数据类型，没有行为，所以不单独写测试——它们的正确性由 Task 2/3 的运动测试覆盖。先建出来是为了让后面的签名能一次写对。

**Files:**
- Create: `Assets/Scripts/Core/Player/PlayerState.cs`
- Create: `Assets/Scripts/Core/Player/PlayerInput.cs`
- Create: `Assets/Scripts/Core/Player/PlayerMotorSettings.cs`

- [ ] **Step 1: `PlayerState`**

```csharp
using MyWorld.Core.Math;

namespace MyWorld.Core.Player
{
    /// <summary>
    /// 玩家的运动状态。<see cref="Position"/> 是**脚底中心**而不是几何中心——
    /// 落地判定、出生点摆放、放置方块都是按脚下算的，取脚底中心能省掉一堆 ± 半高。
    /// </summary>
    public readonly struct PlayerState
    {
        public readonly Float3 Position;
        public readonly Float3 Velocity;
        public readonly bool IsGrounded;

        public PlayerState(Float3 position, Float3 velocity, bool isGrounded)
        {
            Position = position;
            Velocity = velocity;
            IsGrounded = isGrounded;
        }

        /// <summary>刚放到某个位置、还没有速度的初始状态。</summary>
        public static PlayerState AtRest(Float3 position) => new PlayerState(position, default, false);
    }
}
```

- [ ] **Step 2: `PlayerInput`**

```csharp
namespace MyWorld.Core.Player
{
    /// <summary>
    /// 一帧的移动意图。<see cref="MoveX"/>/<see cref="MoveZ"/> 是**已经旋转到世界空间**的
    /// 水平方向，长度不超过 1——Core 不知道相机朝哪，旋转由 Unity 层负责。
    /// </summary>
    public readonly struct PlayerInput
    {
        public readonly float MoveX;
        public readonly float MoveZ;
        public readonly bool Jump;
        public readonly bool Sprint;

        public PlayerInput(float moveX, float moveZ, bool jump, bool sprint)
        {
            MoveX = moveX;
            MoveZ = moveZ;
            Jump = jump;
            Sprint = sprint;
        }

        public static readonly PlayerInput None = new PlayerInput(0f, 0f, false, false);
    }
}
```

- [ ] **Step 3: `PlayerMotorSettings`**

字段用可读写属性（Unity 侧要从 Inspector 灌值），但保持 `sealed class` 不是 struct——参数集会被反复传递，struct 拷贝没意义。

```csharp
namespace MyWorld.Core.Player
{
    /// <summary>
    /// 运动参数。默认值见 `docs/superpowers/specs/2026-08-07-player-layer-design.md` 的参数表——
    /// 那些数是**起点不是终点**，"手感太飘"这类反馈应当落到这里的具体数字上。
    /// </summary>
    public sealed class PlayerMotorSettings
    {
        public float Width { get; set; } = 0.6f;
        public float Height { get; set; } = 1.8f;
        public float EyeHeight { get; set; } = 1.62f;

        public float WalkSpeed { get; set; } = 4.3f;
        public float SprintMultiplier { get; set; } = 1.3f;
        public float JumpSpeed { get; set; } = 8.4f;

        /// <summary>比现实的 −9.8 大得多——体素游戏里现实重力显得"飘"。</summary>
        public float Gravity { get; set; } = -28f;

        /// <summary>终端速度，防止长距离下落时单帧位移过大而穿透薄地板。</summary>
        public float MaxFallSpeed { get; set; } = -60f;

        public float AirControl { get; set; } = 0.35f;
        public float GroundFriction { get; set; } = 12f;
        public float AirFriction { get; set; } = 1f;

        /// <summary>能选中方块的最远距离。</summary>
        public float ReachDistance { get; set; } = 5f;

        public static PlayerMotorSettings Default => new PlayerMotorSettings();
    }
}
```

- [ ] **Step 4: 编译确认**

```bash
DOTNET build tools/dotnet/MyWorld.Tools.sln
```

预期：0 警告 0 错误（Core 开了 `TreatWarningsAsErrors`）。

- [ ] **Step 5: 提交**

```bash
git add Assets/Scripts/Core/Player
git commit -m "玩家层: 玩家状态、输入与运动参数"
```

---

## Task 2: 重力与落地

先只做竖直方向。水平移动放 Task 3，分开是为了让失败时能立刻定位到是哪一半。

**Files:**
- Create: `Assets/Scripts/Core/Player/PlayerMotor.cs`
- Create: `Assets/Tests/EditMode/Player/PlayerMotorGravityTests.cs`

- [ ] **Step 1: 先写失败的测试**

测试用的世界：`World` 里在 `y = 63` 铺一层石头（`SetBlock` 会自动建区块列），玩家从 `y = 70` 开始下落。所有测试共用一个 `WorldSolidSource`。

注册表按仓库里既有测试的写法**内联 JSON 构造**（见 `WorldSourceTests`），不去读 `StreamingAssets`——测试不该依赖磁盘上的数据文件。

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>竖直方向：重力累积、终端速度、落地、撞头。</summary>
    [TestFixture]
    public class PlayerMotorGravityTests
    {
        private const float Dt = 1f / 60f;
        private const float GroundTop = 64f;

        private World _world;
        private WorldSolidSource _source;
        private PlayerMotorSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _settings = PlayerMotorSettings.Default;

            // 一片 16×16 的石地板，顶面在 y = 64（方块占据 [63, 64)）
            for (var x = -8; x < 8; x++)
            for (var z = -8; z < 8; z++)
            {
                _world.SetBlock(x, 63, z, BlockIds.Stone);
            }

            _source = new WorldSolidSource(_world, BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" }, ""solid"": true, ""opaque"": true }"
            }));
        }

        [Test]
        public void Step_InAir_AppliesGravityForOneFrame()
        {
            var state = PlayerState.AtRest(new Float3(0f, 70f, 0f));

            PlayerState next = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);

            Assert.That(next.Velocity.Y, Is.EqualTo(_settings.Gravity * Dt).Within(1e-4f),
                "自由落体第一帧的竖直速度应当正好是 重力 × dt");
            Assert.That(next.IsGrounded, Is.False, "还在空中不应当判定为着地");
        }

        [Test]
        public void Step_FallingLong_ClampsToTerminalVelocity()
        {
            var state = PlayerState.AtRest(new Float3(0f, 300f, 0f));

            for (var i = 0; i < 600; i++)
            {
                state = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);
            }

            Assert.That(state.Velocity.Y, Is.GreaterThanOrEqualTo(_settings.MaxFallSpeed),
                "竖直速度不应当超过终端速度，否则单帧位移会大到穿透地板");
        }

        [Test]
        public void Step_Falling_LandsOnGroundSurface()
        {
            var state = PlayerState.AtRest(new Float3(0f, 70f, 0f));

            for (var i = 0; i < 120; i++)
            {
                state = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);
            }

            Assert.That(state.Position.Y, Is.EqualTo(GroundTop).Within(0.01f),
                "脚底应当停在地面顶面 y = 64 上");
            Assert.That(state.IsGrounded, Is.True, "停在地面上应当判定为着地");
        }

        [Test]
        public void Step_StandingOnGround_DoesNotAccumulateFallSpeed()
        {
            var state = PlayerState.AtRest(new Float3(0f, 64f, 0f));

            for (var i = 0; i < 60; i++)
            {
                state = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);
            }

            Assert.That(state.Velocity.Y, Is.EqualTo(0f).Within(1e-3f),
                "被地面挡住后竖直速度必须归零，否则站久了一离开地面就会瞬间高速下坠");
        }

        [Test]
        public void Step_HittingCeiling_ZeroesUpwardVelocity()
        {
            // 头顶加一层石头：玩家高 1.8，站在 y=64 时头在 65.8，把方块放到 y = 66
            for (var x = -8; x < 8; x++)
            for (var z = -8; z < 8; z++)
            {
                _world.SetBlock(x, 66, z, BlockIds.Stone);
            }

            var state = new PlayerState(new Float3(0f, 64f, 0f), new Float3(0f, 20f, 0f), true);

            state = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);

            Assert.That(state.Velocity.Y, Is.LessThanOrEqualTo(0f),
                "撞到天花板后不应当还保留向上的速度");
        }
    }
}
```

跑一次，确认是**编译失败**（`PlayerMotor` 还不存在）——这就是本步的"红"。

- [ ] **Step 2: 实现 `PlayerMotor`（本任务只填竖直部分）**

```csharp
using MyWorld.Core.Math;
using MyWorld.Core.Physics;

namespace MyWorld.Core.Player
{
    /// <summary>
    /// 玩家运动解算。纯函数：同样的输入必然得到同样的输出，不持有任何状态，
    /// 因此可完整单测，将来做回放或联机预测也不需要改。
    /// </summary>
    public static class PlayerMotor
    {
        public static PlayerState Step<TSource>(TSource source, PlayerState state, PlayerInput input,
            PlayerMotorSettings settings, float dt) where TSource : ISolidBlockSource
        {
            float velocityY = StepVertical(state, input, settings, dt);

            var velocity = new Float3(0f, velocityY, 0f);
            Aabb box = Aabb.FromBottomCenter(state.Position, settings.Width, settings.Height);
            MoveResult result = VoxelCollision.Move(source, box, new Float3(
                velocity.X * dt, velocity.Y * dt, velocity.Z * dt));

            var position = new Float3(
                state.Position.X + result.Delta.X,
                state.Position.Y + result.Delta.Y,
                state.Position.Z + result.Delta.Z);

            // 被挡的轴速度必须归零：继续攒速度的话，一旦障碍消失会瞬间弹出去
            if (result.HitY)
            {
                velocity = new Float3(velocity.X, 0f, velocity.Z);
            }

            return new PlayerState(position, velocity, result.IsGrounded);
        }

        private static float StepVertical(PlayerState state, PlayerInput input,
            PlayerMotorSettings settings, float dt)
        {
            if (state.IsGrounded && input.Jump)
            {
                return settings.JumpSpeed;
            }

            float velocityY = state.Velocity.Y + settings.Gravity * dt;
            return velocityY < settings.MaxFallSpeed ? settings.MaxFallSpeed : velocityY;
        }
    }
}
```

- [ ] **Step 3: 跑测试确认变绿**

```bash
DOTNET test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~PlayerMotorGravityTests"
```

预期：5 个全过。

**如果"下落最终停在地面顶上"差了 1e-4**：那是 `VoxelCollision` 的 `Skin` 间隙，断言容差 0.01 已经包住了；如果差得更多，说明速度没在落地时归零，回头看 Step 2 的 `HitY` 分支。

- [ ] **Step 4: 提交**

```bash
git add Assets/Scripts/Core/Player Assets/Tests/EditMode/Player
git commit -m "玩家层: 重力、终端速度与落地判定"
```

---

## Task 3: 水平移动与跳跃

**Files:**
- Modify: `Assets/Scripts/Core/Player/PlayerMotor.cs`
- Create: `Assets/Tests/EditMode/Player/PlayerMotorMoveTests.cs`

- [ ] **Step 1: 先写失败的测试**

沿用 Task 2 的地板世界，再加一堵墙。

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>水平移动、疾跑、撞墙与跳跃。</summary>
    [TestFixture]
    public class PlayerMotorMoveTests
    {
        private const float Dt = 1f / 60f;

        private World _world;
        private WorldSolidSource _source;
        private PlayerMotorSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _world = new World();
            _settings = PlayerMotorSettings.Default;

            for (var x = -16; x < 16; x++)
            for (var z = -16; z < 16; z++)
            {
                _world.SetBlock(x, 63, z, BlockIds.Stone);
            }

            _source = new WorldSolidSource(_world, BlockRegistry.FromJson(new[]
            {
                @"{ ""id"": ""air"", ""numericId"": 0, ""solid"": false, ""opaque"": false }",
                @"{ ""id"": ""stone"", ""numericId"": 1, ""textures"": { ""all"": ""stone"" }, ""solid"": true, ""opaque"": true }"
            }));
        }

        private PlayerState Run(PlayerState state, PlayerInput input, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                state = PlayerMotor.Step(_source, state, input, _settings, Dt);
            }

            return state;
        }

        private static PlayerState OnGround(float x = 0f, float z = 0f)
            => new PlayerState(new Float3(x, 64f, z), default, true);

        [Test]
        public void Step_HoldingForward_MovesAtWalkSpeed()
        {
            PlayerState state = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 60);

            // 有一段加速过程，所以断言"接近但不超过"一秒的行程
            Assert.That(state.Position.Z, Is.GreaterThan(_settings.WalkSpeed * 0.9f),
                "推满一秒应当走出接近 WalkSpeed 格");
            Assert.That(state.Position.Z, Is.LessThanOrEqualTo(_settings.WalkSpeed + 0.1f),
                "不应当超过行走速度上限");
        }

        [Test]
        public void Step_Sprinting_TravelsFartherThanWalking()
        {
            PlayerState walk = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 60);
            PlayerState sprint = Run(OnGround(), new PlayerInput(0f, 1f, false, true), 60);

            Assert.That(sprint.Position.Z, Is.GreaterThan(walk.Position.Z),
                "按住疾跑应当比普通行走走得更远");
        }

        [Test]
        public void Step_ReleasingInput_DecaysHorizontalVelocity()
        {
            PlayerState state = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 30);
            state = Run(state, PlayerInput.None, 60);

            Assert.That(System.Math.Abs(state.Velocity.Z), Is.LessThan(0.05f),
                "松开方向键一秒后水平速度应当已经衰减到接近零");
        }

        [Test]
        public void Step_IntoWall_StopsAndZeroesVelocity()
        {
            // z = 3 处砌一堵两格高的墙
            for (var x = -16; x < 16; x++)
            {
                _world.SetBlock(x, 64, 3, BlockIds.Stone);
                _world.SetBlock(x, 65, 3, BlockIds.Stone);
            }

            PlayerState state = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 120);

            Assert.That(state.Position.Z, Is.LessThan(3f),
                "玩家不应当穿过 z = 3 的墙");
            Assert.That(System.Math.Abs(state.Velocity.Z), Is.LessThan(0.01f),
                "撞墙后水平速度应当归零，否则障碍一消失会突然弹出去");
        }

        [Test]
        public void Step_DiagonalIntoWall_SlidesAlongIt()
        {
            for (var x = -16; x < 16; x++)
            {
                _world.SetBlock(x, 64, 3, BlockIds.Stone);
                _world.SetBlock(x, 65, 3, BlockIds.Stone);
            }

            // 同时向前和向右推：前进被墙挡住，向右的分量应当照常生效
            PlayerState state = Run(OnGround(), new PlayerInput(0.707f, 0.707f, false, false), 120);

            Assert.That(state.Position.X, Is.GreaterThan(1f),
                "被墙挡住的只应当是前进方向，侧向仍应能滑动");
        }

        [Test]
        public void Step_JumpOnGround_LeavesGround()
        {
            PlayerState state = PlayerMotor.Step(_source, OnGround(), new PlayerInput(0f, 0f, true, false),
                _settings, Dt);

            Assert.That(state.Velocity.Y, Is.GreaterThan(0f), "起跳后竖直速度应当向上");
            Assert.That(state.IsGrounded, Is.False, "起跳后应当离地");
        }

        [Test]
        public void Step_JumpInAir_Ignored()
        {
            var air = new PlayerState(new Float3(0f, 80f, 0f), default, false);

            PlayerState state = PlayerMotor.Step(_source, air, new PlayerInput(0f, 0f, true, false),
                _settings, Dt);

            Assert.That(state.Velocity.Y, Is.LessThan(0f),
                "空中按跳不应当生效，竖直速度仍应当在重力作用下向下");
        }

        [Test]
        public void Step_JumpArc_ClearsOneBlockButNotTwo()
        {
            PlayerState state = OnGround();
            var peak = 64f;

            for (var i = 0; i < 120; i++)
            {
                // 只有第一帧按跳，之后松开
                state = PlayerMotor.Step(_source, state, new PlayerInput(0f, 0f, i == 0, false), _settings, Dt);
                if (state.Position.Y > peak)
                {
                    peak = state.Position.Y;
                }
            }

            float height = peak - 64f;
            Assert.That(height, Is.GreaterThan(1f), $"跳跃高度 {height:F2} 应当能上 1 格台阶");
            Assert.That(height, Is.LessThan(2f), $"跳跃高度 {height:F2} 不应当能上 2 格");
        }
    }
}
```

- [ ] **Step 2: 把水平解算加进 `PlayerMotor`**

替换 Task 2 写的 `Step`，并新增 `StepHorizontal`：

```csharp
        public static PlayerState Step<TSource>(TSource source, PlayerState state, PlayerInput input,
            PlayerMotorSettings settings, float dt) where TSource : ISolidBlockSource
        {
            Float3 velocity = StepHorizontal(state, input, settings, dt);
            velocity = new Float3(velocity.X, StepVertical(state, input, settings, dt), velocity.Z);

            Aabb box = Aabb.FromBottomCenter(state.Position, settings.Width, settings.Height);
            MoveResult result = VoxelCollision.Move(source, box,
                new Float3(velocity.X * dt, velocity.Y * dt, velocity.Z * dt));

            var position = new Float3(
                state.Position.X + result.Delta.X,
                state.Position.Y + result.Delta.Y,
                state.Position.Z + result.Delta.Z);

            // 被挡的轴速度归零。逐轴判断而不是整体归零——沿墙滑动靠的就是"只死一个轴"
            velocity = new Float3(
                result.HitX ? 0f : velocity.X,
                result.HitY ? 0f : velocity.Y,
                result.HitZ ? 0f : velocity.Z);

            return new PlayerState(position, velocity, result.IsGrounded);
        }

        /// <summary>
        /// 水平速度朝目标速度收敛。地面上响应快、空中打折，没有输入时按摩擦衰减到零。
        /// 用"朝目标线性逼近"而不是直接赋值，起步和松手才不会是硬切换。
        /// </summary>
        private static Float3 StepHorizontal(PlayerState state, PlayerInput input,
            PlayerMotorSettings settings, float dt)
        {
            float speedLimit = settings.WalkSpeed * (input.Sprint ? settings.SprintMultiplier : 1f);
            float targetX = input.MoveX * speedLimit;
            float targetZ = input.MoveZ * speedLimit;

            bool hasInput = input.MoveX != 0f || input.MoveZ != 0f;
            float rate = hasInput
                ? (state.IsGrounded ? settings.GroundFriction : settings.GroundFriction * settings.AirControl)
                : (state.IsGrounded ? settings.GroundFriction : settings.AirFriction);

            float t = rate * dt;
            if (t > 1f)
            {
                t = 1f;
            }

            return new Float3(
                state.Velocity.X + (targetX - state.Velocity.X) * t,
                state.Velocity.Y,
                state.Velocity.Z + (targetZ - state.Velocity.Z) * t);
        }
```

- [ ] **Step 3: 跑测试确认变绿**

```bash
DOTNET test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~PlayerMotor"
```

预期：Task 2 的 5 个 + 本任务的 8 个，共 13 个全过。

**如果"跳跃高度约为一格多"落在区间外**：`JumpSpeed² / (2 × |Gravity|)` = 8.4² / 56 ≈ 1.26 格，理论值在区间内。真跑出界说明重力在起跳当帧就被扣了一次（顺序错了），检查 `StepVertical` 里跳跃分支是否**直接返回**而没有再减重力。

- [ ] **Step 4: 提交**

```bash
git add Assets/Scripts/Core/Player Assets/Tests/EditMode/Player
git commit -m "玩家层: 水平移动、疾跑、沿墙滑动与跳跃"
```

---

## Task 4: 放置位置与合法性

**Files:**
- Create: `Assets/Scripts/Core/Player/BlockPlacement.cs`
- Create: `Assets/Tests/EditMode/Player/BlockPlacementTests.cs`

- [ ] **Step 1: 先写失败的测试**

```csharp
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Player
{
    /// <summary>由射线命中推出放置格，并挡住三类非法放置。</summary>
    [TestFixture]
    public class BlockPlacementTests
    {
        private static VoxelRayHit HitAt(int x, int y, int z, int nx, int ny, int nz)
            => new VoxelRayHit { Hit = true, X = x, Y = y, Z = z, NormalX = nx, NormalY = ny, NormalZ = nz };

        /// <summary>玩家站在 (0.5, 64, 0.5)，包围盒占据 x/z 的 [0.2, 0.8]、y 的 [64, 65.8]。</summary>
        private static Aabb Player() => Aabb.FromBottomCenter(new Float3(0.5f, 64f, 0.5f), 0.6f, 1.8f);

        [Test]
        public void Resolve_PlacesAgainstHitFace()
        {
            bool ok = BlockPlacement.TryResolve(HitAt(5, 64, 5, 0, 1, 0), Player(), out int x, out int y, out int z);

            Assert.That(ok, Is.True);
            Assert.That(new[] { x, y, z }, Is.EqualTo(new[] { 5, 65, 5 }),
                "打在方块顶面上，新方块应当放在它正上方");
        }

        [Test]
        public void Resolve_WithoutHit_Fails()
        {
            Assert.That(BlockPlacement.TryResolve(default, Player(), out _, out _, out _), Is.False,
                "射线没打中任何方块时不应当放置");
        }

        [Test]
        public void Resolve_InsidePlayerBox_Fails()
        {
            // 打在玩家脚下那格的顶面 → 目标格正是玩家站的位置
            bool ok = BlockPlacement.TryResolve(HitAt(0, 63, 0, 0, 1, 0), Player(), out _, out _, out _);

            Assert.That(ok, Is.False, "不能把方块放到玩家身体里，否则会把自己封住");
        }

        [Test]
        public void Resolve_BesidePlayer_Succeeds()
        {
            // 玩家包围盒 x 只占 [0.2, 0.8]，x = 1 那一列是空的
            bool ok = BlockPlacement.TryResolve(HitAt(2, 64, 0, -1, 0, 0), Player(), out int x, out _, out _);

            Assert.That(ok, Is.True, "紧挨着玩家但不重叠的格子应当能放");
            Assert.That(x, Is.EqualTo(1));
        }

        [TestCase(319, 1, true)]
        [TestCase(-64, -1, false)]
        [TestCase(319, -1, true)]
        public void Resolve_ChecksWorldHeightLimits(int hitY, int normalY, int expected)
        {
            bool ok = BlockPlacement.TryResolve(HitAt(50, hitY, 50, 0, normalY, 0), Player(), out _, out _, out _);

            Assert.That(ok, Is.EqualTo(expected),
                "超出世界高度 [-64, 320) 的格子不应当能放");
        }
    }
}
```

- [ ] **Step 2: 实现**

```csharp
using MyWorld.Core.Physics;
using MyWorld.Core.Voxel;

namespace MyWorld.Core.Player
{
    /// <summary>由射线命中结果推出放置位置，并判定这次放置是否合法。</summary>
    public static class BlockPlacement
    {
        public static bool TryResolve(VoxelRayHit hit, Aabb playerBox, out int x, out int y, out int z)
        {
            x = 0;
            y = 0;
            z = 0;

            if (!hit.Hit)
            {
                return false;
            }

            // 沿命中面的法线往外挪一格，就是新方块该占的位置
            x = hit.X + hit.NormalX;
            y = hit.Y + hit.NormalY;
            z = hit.Z + hit.NormalZ;

            if (y < VoxelCoords.MinY || y >= VoxelCoords.MaxY)
            {
                return false;
            }

            // 这一条是必须的：不挡住的话玩家能把自己封进方块里出不来
            return !IntersectsPlayer(x, y, z, playerBox);
        }

        /// <summary>
        /// 格子与玩家包围盒是否重叠。用严格不等号——正好贴面（比如脚底那格的顶面 y = 64
        /// 对上包围盒底面 y = 64）不算重叠，否则站在地上时脚下一圈全都放不了。
        /// </summary>
        private static bool IntersectsPlayer(int x, int y, int z, Aabb box)
        {
            return box.Min.X < x + 1 && box.Max.X > x
                && box.Min.Y < y + 1 && box.Max.Y > y
                && box.Min.Z < z + 1 && box.Max.Z > z;
        }
    }
}
```

- [ ] **Step 3: 跑测试确认变绿，然后提交**

```bash
DOTNET test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~BlockPlacementTests"
git add Assets/Scripts/Core/Player Assets/Tests/EditMode/Player
git commit -m "玩家层: 放置位置解算与合法性判定"
```

---

## Task 5: 改一个方块之后哪些区块段变脏

贪心网格生成时会采样邻居来剔除接缝面，所以改一个方块牵连的不只是它所在的段。这段是纯坐标运算，放 Core 用测试钉死；Unity 侧只负责照单重建。

**Files:**
- Create: `Assets/Scripts/Core/Voxel/SectionRef.cs`
- Create: `Assets/Scripts/Core/Voxel/DirtySections.cs`
- Create: `Assets/Tests/EditMode/Voxel/DirtySectionsTests.cs`

- [ ] **Step 1: 先写失败的测试**

```csharp
using System.Collections.Generic;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Core.Tests.Voxel
{
    /// <summary>脏段收集：段内、上下边界、区块水平边界、以及角上的组合情况。</summary>
    [TestFixture]
    public class DirtySectionsTests
    {
        private readonly List<SectionRef> _result = new List<SectionRef>();

        private List<SectionRef> Collect(int x, int y, int z)
        {
            DirtySections.Collect(x, y, z, _result);
            return _result;
        }

        [Test]
        public void Collect_InsideSection_ReturnsOnlyItsOwnSection()
        {
            // (8, 72, 8) 在区块 (0,0)、段 8（y ∈ [64, 80)）的正中间
            Assert.That(Collect(8, 72, 8), Is.EqualTo(new[]
            {
                new SectionRef(new ChunkPos(0, 0), 8)
            }), "段内部的方块只牵连它自己所在的段");
        }

        [Test]
        public void Collect_AtSectionBottom_AlsoMarksSectionBelow()
        {
            List<SectionRef> dirty = Collect(8, 64, 8);

            Assert.That(dirty, Has.Count.EqualTo(2));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 7)),
                "贴着段底面的方块会改变下面那段顶面的可见性");
        }

        [Test]
        public void Collect_AtChunkEdge_AlsoMarksNeighbourChunk()
        {
            List<SectionRef> dirty = Collect(0, 72, 8);

            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(-1, 0), 8)),
                "贴着区块西边界的方块会改变西侧邻区块东面的可见性");
        }

        [Test]
        public void Collect_AtChunkCorner_MarksAllTouchedNeighbours()
        {
            // (0, 64, 0)：西、北、下三个方向同时贴边
            List<SectionRef> dirty = Collect(0, 64, 0);

            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(-1, 0), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, -1), 8)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, 0), 7)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(-1, 0), 7)));
            Assert.That(dirty, Contains.Item(new SectionRef(new ChunkPos(0, -1), 7)));
        }

        [Test]
        public void Collect_NeverReturnsDuplicates()
        {
            Assert.That(Collect(15, 79, 15), Is.Unique, "同一个段不应当出现两次，否则会被重建两遍");
        }

        [Test]
        public void Collect_NeverReturnsSectionOutsideWorld()
        {
            foreach (SectionRef section in Collect(0, VoxelCoords.MinY, 0))
            {
                Assert.That(section.SectionIndex, Is.InRange(0, VoxelCoords.SectionCount - 1),
                    "世界最底层的下方没有段，不应当被收进来");
            }
        }

        [Test]
        public void Collect_ClearsPreviousResult()
        {
            Collect(8, 72, 8);
            List<SectionRef> second = Collect(100, 200, 100);

            Assert.That(second, Has.Count.EqualTo(1),
                "Collect 应当先清空传入的列表，否则连续调用会越积越多");
        }
    }
}
```

- [ ] **Step 2: `SectionRef`**

```csharp
using System;

namespace MyWorld.Core.Voxel
{
    /// <summary>一个区块段的定位：哪根区块列的第几段。</summary>
    public readonly struct SectionRef : IEquatable<SectionRef>
    {
        public readonly ChunkPos Chunk;
        public readonly int SectionIndex;

        public SectionRef(ChunkPos chunk, int sectionIndex)
        {
            Chunk = chunk;
            SectionIndex = sectionIndex;
        }

        public bool Equals(SectionRef other) => Chunk.Equals(other.Chunk) && SectionIndex == other.SectionIndex;

        public override bool Equals(object obj) => obj is SectionRef other && Equals(other);

        public override int GetHashCode() => (Chunk.GetHashCode() * 397) ^ SectionIndex;

        public override string ToString() => $"{Chunk} 段 {SectionIndex}";
    }
}
```

- [ ] **Step 3: `DirtySections`**

```csharp
using System.Collections.Generic;

namespace MyWorld.Core.Voxel
{
    /// <summary>
    /// 改一个方块之后，哪些区块段需要重建网格。
    /// <para>
    /// 不是只有它自己那一段：贪心网格生成时会采样**邻居**来剔除接缝面，所以贴着段边界的
    /// 方块会改变相邻段的面可见性。最多牵连 8 个段（水平 4 个 × 竖直 2 个），
    /// 绝大多数情况只有 1 个——比无脑重建 3×3×3 = 27 个段省得多，连续挖掘时差别明显。
    /// </para>
    /// </summary>
    public static class DirtySections
    {
        /// <summary>结果写进 <paramref name="output"/>（会先清空），避免每次挖掘都分配一个新列表。</summary>
        public static void Collect(int worldX, int worldY, int worldZ, List<SectionRef> output)
        {
            output.Clear();

            if (worldY < VoxelCoords.MinY || worldY >= VoxelCoords.MaxY)
            {
                return;
            }

            int chunkX = VoxelCoords.WorldToChunk(worldX);
            int chunkZ = VoxelCoords.WorldToChunk(worldZ);
            int localX = VoxelCoords.WorldToLocal(worldX);
            int localZ = VoxelCoords.WorldToLocal(worldZ);
            int section = VoxelCoords.WorldToSection(worldY);
            int localY = VoxelCoords.WorldToLocal(worldY);

            // 水平：自己这根，加上贴边时的邻居（最多 3 根，角上是西/北两根 + 自己）
            AddColumn(output, chunkX, chunkZ, section, localY);

            if (localX == 0) AddColumn(output, chunkX - 1, chunkZ, section, localY);
            if (localX == VoxelCoords.ChunkSize - 1) AddColumn(output, chunkX + 1, chunkZ, section, localY);
            if (localZ == 0) AddColumn(output, chunkX, chunkZ - 1, section, localY);
            if (localZ == VoxelCoords.ChunkSize - 1) AddColumn(output, chunkX, chunkZ + 1, section, localY);
        }

        /// <summary>某根区块列上，本段以及（贴着段上下边界时）相邻的那一段。</summary>
        private static void AddColumn(List<SectionRef> output, int chunkX, int chunkZ, int section, int localY)
        {
            var chunk = new ChunkPos(chunkX, chunkZ);
            output.Add(new SectionRef(chunk, section));

            if (localY == 0 && section > 0)
            {
                output.Add(new SectionRef(chunk, section - 1));
            }
            else if (localY == VoxelCoords.ChunkSize - 1 && section < VoxelCoords.SectionCount - 1)
            {
                output.Add(new SectionRef(chunk, section + 1));
            }
        }
    }
}
```

**注意**：写实现前先确认 `VoxelCoords` 里**竖直方向**的换算方法名（`WorldToSection` / `WorldToLocal(int worldY)`）。如果命名不同，按 `VoxelCoords` 里实际的名字调用，**不要新写一套坐标换算**（CLAUDE.md：坐标换算只走 `VoxelCoords`）。

- [ ] **Step 4: 跑测试确认变绿，然后提交**

```bash
DOTNET test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~DirtySectionsTests"
git add Assets/Scripts/Core/Voxel Assets/Tests/EditMode/Voxel
git commit -m "玩家层: 方块改动后的脏区块段收集"
```

---

## Task 6: 区块段视图的索引与按需重建

里程碑 1 的 `WorldBootstrap` 是"建完就撒手"——`ChunkSectionView` 建出来之后没人再管它。挖方块要重建、走远了要销毁，都需要一张 `(区块, 段) → 视图` 的索引。

**Files:**
- Create: `Assets/Scripts/Unity/Rendering/ChunkViewRegistry.cs`

Unity 层没有 `dotnet test` 覆盖（`tools/dotnet` 只链接 `Core` 与 `Tests/EditMode`），所以这个类的验证靠 Task 11 的 `PlayHarness`。写的时候把逻辑压到最薄，复杂的判断都已经在 Core 里测过了。

- [ ] **Step 1: 实现**

```csharp
using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.Rendering
{
    /// <summary>
    /// (区块, 段) → <see cref="ChunkSectionView"/> 的索引。
    /// <para>
    /// 视图的生命周期只有三件事：需要时建出来、脏了重建、走远了销毁。
    /// 空段（一个面都没有）不保留 GameObject——13×13 区块 × 24 段有 4000 多个，
    /// 其中绝大多数是纯空气或纯石头，留着白占场景层级。
    /// </para>
    /// </summary>
    public sealed class ChunkViewRegistry
    {
        private readonly Dictionary<SectionRef, ChunkSectionView> _views =
            new Dictionary<SectionRef, ChunkSectionView>();

        private readonly Transform _parent;
        private readonly World _world;
        private readonly BlockRegistry _registry;
        private readonly BlockMaterialLibrary _materials;

        private readonly List<SectionRef> _dirtyScratch = new List<SectionRef>();

        public ChunkViewRegistry(Transform parent, World world, BlockRegistry registry,
            BlockMaterialLibrary materials)
        {
            _parent = parent;
            _world = world;
            _registry = registry;
            _materials = materials;
        }

        public int ViewCount => _views.Count;

        /// <summary>建好一根区块列上所有非空段的网格。返回真正出了面的段数。</summary>
        public int BuildColumn(ChunkPos chunk)
        {
            if (!_world.TryGetChunk(chunk, out ChunkColumn column))
            {
                return 0;
            }

            var built = 0;
            for (var section = 0; section < VoxelCoords.SectionCount; section++)
            {
                if (column.HasSection(section) && Rebuild(new SectionRef(chunk, section)))
                {
                    built++;
                }
            }

            return built;
        }

        /// <summary>改了一个方块之后调用，牵连到的段一并重建。</summary>
        public void MarkBlockChanged(int worldX, int worldY, int worldZ)
        {
            DirtySections.Collect(worldX, worldY, worldZ, _dirtyScratch);
            foreach (SectionRef section in _dirtyScratch)
            {
                Rebuild(section);
            }
        }

        /// <summary>重建一个段。返回它是否还有可见面——没有面的段会被销毁。</summary>
        public bool Rebuild(SectionRef section)
        {
            if (!_views.TryGetValue(section, out ChunkSectionView view))
            {
                view = ChunkSectionView.Create(_parent, section.Chunk, section.SectionIndex);
                _views[section] = view;
            }

            if (view.Rebuild(_world, _registry, _materials))
            {
                return true;
            }

            // 段被挖空（或本来就被完全包裹）：留着只是白占一个 GameObject
            Destroy(section);
            return false;
        }

        /// <summary>卸载整根区块列的视图。</summary>
        public void UnloadColumn(ChunkPos chunk)
        {
            for (var section = 0; section < VoxelCoords.SectionCount; section++)
            {
                Destroy(new SectionRef(chunk, section));
            }
        }

        private void Destroy(SectionRef section)
        {
            if (!_views.TryGetValue(section, out ChunkSectionView view))
            {
                return;
            }

            _views.Remove(section);
            if (view != null)
            {
                DestroyObject(view.gameObject);
            }
        }

        /// <summary>编辑器非播放态下 Destroy 不生效，必须走 DestroyImmediate。</summary>
        private static void DestroyObject(GameObject target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
```

**实现前先验证依赖**：`ChunkSectionView.Create`、`ChunkSectionView.Rebuild(World, BlockRegistry, BlockMaterialLibrary)`、`World.TryGetChunk` 这三个 API 必须已经存在且签名对得上。如果里程碑 1 的实现不一致，以 `ChunkSectionView.cs` 与 `World.cs` 里的实际签名为准改本任务里的调用——**不要新增 ChunkSectionView 的方法**。

- [ ] **Step 2: 编译确认**

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
    -batchmode -nographics -projectPath "$(pwd)" -quit -logFile compile.log
grep -c "error CS" compile.log   # 预期：0
```

- [ ] **Step 3: 提交**

```bash
git add Assets/Scripts/Unity/Rendering
git commit -m "玩家层: 区块段视图索引与按需重建"
```

---

## Task 7: 玩家控制器

**Files:**
- Create: `Assets/Scripts/Unity/Player/PlayerController.cs`

- [ ] **Step 1: 实现**

关键点写在注释里，尤其是"输入必须旋转到世界空间"和"相机是子物体不参与碰撞"。

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 输入 → <see cref="PlayerMotor"/> → transform。
    /// <para>
    /// 这个组件刻意很薄：运动解算全在 Core 里（可 <c>dotnet test</c> 覆盖），这里只做三件事——
    /// 读输入、把输入按相机朝向旋转到世界空间、把结果写回 transform。
    /// </para>
    /// </summary>
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform eye;
        [SerializeField] private float lookSensitivity = 2.5f;

        [Header("运动参数（留空用 Core 的默认值）")]
        [SerializeField] private float walkSpeed = 4.3f;
        [SerializeField] private float jumpSpeed = 8.4f;
        [SerializeField] private float gravity = -28f;

        private PlayerMotorSettings _settings;
        private PlayerState _state;
        private World _world;
        private BlockRegistry _registry;

        private float _yaw;
        private float _pitch;

        public PlayerState State => _state;
        public PlayerMotorSettings Settings => _settings;

        /// <summary>相机所在位置——射线拾取要用，所以公开出去。</summary>
        public Transform Eye => eye;

        /// <summary>由 <c>WorldBootstrap</c> 在世界准备好之后调用。</summary>
        public void Bind(World world, BlockRegistry registry, Float3 spawnPosition)
        {
            _world = world;
            _registry = registry;
            _settings = new PlayerMotorSettings
            {
                WalkSpeed = walkSpeed,
                JumpSpeed = jumpSpeed,
                Gravity = gravity
            };

            _state = PlayerState.AtRest(spawnPosition);
            ApplyToTransform();
        }

        /// <summary>驱动一帧。公开出来是为了让无头验证能手动步进，不必真的进 Play 模式。</summary>
        public void Tick(PlayerInput input, float dt)
        {
            if (_world == null)
            {
                return;
            }

            var source = new WorldSolidSource(_world, _registry);
            _state = PlayerMotor.Step(source, _state, input, _settings, dt);
            ApplyToTransform();
        }

        private void Update()
        {
            UpdateLook();
            Tick(ReadInput(), Time.deltaTime);
        }

        private void UpdateLook()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                // 未锁定指针时不转视角，否则在编辑器里点 UI 会把视角甩飞
                if (Input.GetMouseButtonDown(0))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                return;
            }

            _yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * lookSensitivity, -89f, 89f);

            // 身体只转 yaw，俯仰只给眼睛——身体跟着俯仰转的话包围盒会倾斜
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (eye != null)
            {
                eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private PlayerInput ReadInput()
        {
            float right = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float forward = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);

            // Core 不知道相机朝哪，旋转在这里做完再传进去
            Vector3 direction = transform.right * right + transform.forward * forward;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            return new PlayerInput(direction.x, direction.z,
                Input.GetKey(KeyCode.Space), Input.GetKey(KeyCode.LeftShift));
        }

        private void ApplyToTransform()
        {
            transform.position = new Vector3(_state.Position.X, _state.Position.Y, _state.Position.Z);
            if (eye != null)
            {
                eye.localPosition = new Vector3(0f, _settings.EyeHeight, 0f);
            }
        }
    }
}
```

**`Float3` 序列化坑**：Inspector 不能直接显示 `Float3`（Core 没有 `[System.Serializable]`）。`Bind(world, registry, Float3 spawnPosition)` 已经从外部接收，所以不暴露 `Float3` 字段就没问题——`spawnPosition` 由 `WorldBootstrap` 在代码里构造。

**实现前先验证**：`Input.GetAxis("Mouse X")` / `Input.GetAxis("Mouse Y")` 在 2022.3 + 旧版 Input Manager 下可用（`activeInputHandler: 0`，确认过）。

- [ ] **Step 2: 编译确认，然后提交**

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
    -batchmode -nographics -projectPath "$(pwd)" -quit -logFile compile.log
grep -c "error CS" compile.log   # 预期：0

git add Assets/Scripts/Unity/Player
git commit -m "玩家层: 玩家控制器"
```

---

## Task 8: 方块交互（射线选中 / 线框高亮 / 挖 / 放）

**Files:**
- Create: `Assets/Scripts/Unity/Player/BlockInteraction.cs`
- Create: `Assets/Scripts/Unity/Player/SelectionBox.cs`

- [ ] **Step 1: 先实现 `SelectionBox`（无外部依赖，最简单）**

```csharp
using MyWorld.Core.Math;
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 一个 1.002 倍大小的立方体线框，用 12 条细长方体（盒子的边）模拟 12 条线。
    /// <para>
    /// 为什么不直接用 <c>GL.LINES</c>：URP 下 <c>OnRenderObject</c> / <c>GL</c> 行为不可靠，
    /// 一个不参与光照的 Mesh + Material 路线最稳，URP 也能正常渲染。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SelectionBox : MonoBehaviour
    {
        private static readonly Vector3[] EdgeDirections =
        {
            // 底面 4 条
            new Vector3(1, 0, 0), new Vector3(0, 0, 1),
            new Vector3(1, 0, 0), new Vector3(0, 0, 1),
            // 顶面 4 条
            new Vector3(1, 0, 0), new Vector3(0, 0, 1),
            new Vector3(1, 0, 0), new Vector3(0, 0, 1),
            // 4 条立柱
            new Vector3(0, 1, 0), new Vector3(0, 1, 0),
            new Vector3(0, 1, 0), new Vector3(0, 1, 0),
        };

        private static readonly Vector3[] EdgeOrigins =
        {
            new Vector3(0, 0, 0), new Vector3(0, 0, 0),
            new Vector3(1, 0, 0), new Vector3(0, 0, 1),
            new Vector3(0, 1, 0), new Vector3(0, 1, 0),
            new Vector3(1, 1, 0), new Vector3(0, 1, 1),
            new Vector3(0, 0, 0), new Vector3(0, 0, 1),
            new Vector3(1, 0, 0), new Vector3(1, 0, 1),
        };

        private const float EdgeThickness = 0.02f;

        public static SelectionBox Create(Transform parent, Material material)
        {
            var go = new GameObject("SelectionBox");
            go.transform.SetParent(parent, worldPositionStays: false);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildEdgeMesh();

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go.AddComponent<SelectionBox>();
        }

        private static Mesh BuildEdgeMesh()
        {
            // 每条边是一个细长方体：origin + direction × length，截面 EdgeThickness×EdgeThickness
            // 简单起见，用 Cube.CreatePrimitive 的顶点数 24（6 面 × 4 顶点），但只画外表面
            // ——为了不增加资源依赖，直接手算 12 个 box，每个 8 个顶点合并即可。
            // 实际工程里用 Graphics.DrawMesh / Mesh.CombineMeshes 更整洁；这里用最简实现。
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var mesh = Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
                mesh.name = "SelectionBoxEdges";
                return mesh;
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }

        /// <summary>把线框摆到指定方块格的位置；scale 1.002 让它比方块略大一点。</summary>
        public void ShowAt(Int3 block)
        {
            gameObject.SetActive(true);
            transform.position = new Vector3(block.X, block.Y, block.Z) + new Vector3(0.5f, 0.5f, 0.5f);
            transform.localScale = new Vector3(1.002f, 1.002f, 1.002f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
```

**说明**：上面 `BuildEdgeMesh` 用 `GameObject.CreatePrimitive(Cube)` 借一个 1×1×1 立方体 mesh 来模拟 12 条粗边——视觉上不像真正的"线"，但 URP 兼容性最稳。Task 11 跑完后如果觉得粗边太显眼，可替换为「合并 12 个缩放 box 的 mesh」。本计划先求可跑通。

- [ ] **Step 2: 实现 `BlockInteraction`**

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Physics;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.Player
{
    /// <summary>
    /// 鼠标射线选中 → 线框高亮 → 左键挖 / 右键放。
    /// <para>
    /// 数据源全部走 Core（<see cref="VoxelRaycaster"/> + <see cref="BlockPlacement"/> +
    /// <see cref="World.SetBlock"/> + <see cref="DirtySections"/>），Unity 侧只负责鼠标轮询、
    /// 调用顺序、和把脏段交给 <see cref="ChunkViewRegistry"/> 重建。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class BlockInteraction : MonoBehaviour
    {
        [SerializeField] private ushort placeBlockId = BlockIds.Stone;
        [SerializeField] private Material selectionMaterial;

        private PlayerController _player;
        private World _world;
        private BlockRegistry _registry;
        private ChunkViewRegistry _views;
        private SelectionBox _selection;

        public void Bind(World world, BlockRegistry registry, ChunkViewRegistry views, Transform parent)
        {
            _player = GetComponent<PlayerController>();
            _world = world;
            _registry = registry;
            _views = views;

            if (selectionMaterial == null)
            {
                // 创建一个最简的不透明无光照材质
                var shader = Shader.Find("Hidden/Internal-Colored");
                selectionMaterial = new Material(shader) { color = Color.white };
            }

            _selection = SelectionBox.Create(parent, selectionMaterial);
            _selection.Hide();
        }

        private void Update()
        {
            if (_world == null || _player.Eye == null)
            {
                return;
            }

            var source = new WorldSolidSource(_world, _registry);
            Float3 origin = ToFloat3(_player.Eye.position);
            Float3 direction = ToFloat3(_player.Eye.forward);
            float maxDist = _player.Settings.ReachDistance;

            VoxelRayHit hit = VoxelRaycaster.Cast(source, origin, direction, maxDist);

            if (hit.Hit)
            {
                _selection.ShowAt(new Int3(hit.X, hit.Y, hit.Z));

                if (Input.GetMouseButtonDown(0))
                {
                    // 挖：把命中格设为空气，标脏，重建
                    _world.SetBlock(hit.X, hit.Y, hit.Z, BlockIds.Air);
                    _views.MarkBlockChanged(hit.X, hit.Y, hit.Z);
                }
                else if (Input.GetMouseButtonDown(1))
                {
                    // 放：尝试解算放置位置，合法就 SetBlock + 标脏 + 重建
                    Aabb playerBox = Aabb.FromBottomCenter(_player.State.Position,
                        _player.Settings.Width, _player.Settings.Height);
                    if (BlockPlacement.TryResolve(hit, playerBox, out int x, out int y, out int z))
                    {
                        _world.SetBlock(x, y, z, placeBlockId);
                        _views.MarkBlockChanged(x, y, z);
                    }
                }
            }
            else
            {
                _selection.Hide();
            }
        }

        private static Float3 ToFloat3(Vector3 v) => new Float3(v.x, v.y, v.z);
    }
}
```

**实现前先验证**：

1. `Int3` 是否存在；若不存在（Core 用 `Float3` 不用 `Int3`），改成 `SelectionBox.ShowAt(int x, int y, int z)`
2. `BlockIds.Air` 常量名（可能是 `BlockIds.Air` / `BlockIds.Empty` / 数字字面量 0）
3. `BlockRegistry` 上是否有方法可以查"solid 状态"用于射线源（如果 `WorldSolidSource` 已经接好，直接用即可）

- [ ] **Step 3: 编译确认，然后提交**

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
    -batchmode -nographics -projectPath "$(pwd)" -quit -logFile compile.log
grep -c "error CS" compile.log   # 预期：0

git add Assets/Scripts/Unity/Player
git commit -m "玩家层: 方块交互（射线选中/线框高亮/挖/放）"
```

---

## Task 9: 区块流式加载

里程碑 1 的 `WorldBootstrap` 在启动时一次性生成 7×7 区块列，边界就是虚空。本任务用 `ChunkStreamer` 跟随玩家位置持续生成 / 卸载：玩家走到哪儿，地形就跟到哪儿。

**Files:**
- Create: `Assets/Scripts/Unity/World/ChunkStreamer.cs`

- [ ] **Step 1: 实现**

```csharp
using System.Collections.Generic;
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Unity.Rendering;
using UnityEngine;

namespace MyWorld.Unity.World
{
    /// <summary>
    /// 跟随玩家位置持续生成 / 卸载区块列。
    /// <para>
    /// 关键设计：生成与建网格**分两个队列**。一根区块列先由 <see cref="WorldGenerator"/> 灌进
    /// <see cref="World"/>，只有当它的 4 个水平邻居都已在 <see cref="World"/> 里时，才允许进
    /// 建网格队列。原因：贪心网格生成时会采样邻居来剔除接缝面，"边生成边建网格"会让先建
    /// 的那几根在接缝处凭空多出一整面——里程碑 1 已经踩过这个坑，<see cref="WorldBootstrap"/>
    /// 的注释里写过。
    /// </para>
    /// </summary>
    public sealed class ChunkStreamer
    {
        private readonly World _world;
        private readonly WorldGenerator _generator;
        private readonly BlockRegistry _registry;
        private readonly ChunkViewRegistry _views;
        private readonly long _seed;

        private readonly Queue<ChunkPos> _generateQueue = new Queue<ChunkPos>();
        private readonly Queue<ChunkPos> _meshQueue = new Queue<ChunkPos>();
        private readonly HashSet<ChunkPos> _generating = new HashSet<ChunkPos>();
        private readonly HashSet<ChunkPos> _meshing = new HashSet<ChunkPos>();

        public int LoadRadius { get; set; } = 6;
        public int UnloadRadius { get; set; } = 8;
        public int ChunksPerFrame { get; set; } = 2;

        public ChunkStreamer(World world, WorldGenerator generator, BlockRegistry registry,
            ChunkViewRegistry views, long seed)
        {
            _world = world;
            _generator = generator;
            _registry = registry;
            _views = views;
            _seed = seed;
        }

        public void Tick(Float3 playerPosition)
        {
            int cx = VoxelCoords.WorldToChunk((int)playerPosition.X);
            int cz = VoxelCoords.WorldToChunk((int)playerPosition.Z);

            // 1. 卸载：超出 UnloadRadius 的列
            UnloadDistant(cx, cz);

            // 2. 入队 / 卸载后，把 LoadRadius 范围内还没加载的列加入 generate 队列
            EnqueueMissing(cx, cz);

            // 3. 一帧最多处理 ChunksPerFrame 根（生成 + 建网格合计）
            int budget = ChunksPerFrame;
            while (budget > 0 && _generateQueue.Count > 0)
            {
                ChunkPos next = _generateQueue.Dequeue();
                _generating.Remove(next);
                _generator.Generate(_world, _seed, next);
                _meshQueue.Enqueue(next);
                budget--;
            }

            while (budget > 0 && _meshQueue.Count > 0)
            {
                ChunkPos next = _meshQueue.Dequeue();
                _meshing.Remove(next);

                // 邻居全到位才建网格，否则回到队列末尾
                if (!AllHorizontalNeighborsLoaded(next))
                {
                    _meshQueue.Enqueue(next);
                    _meshing.Add(next);
                    continue;
                }

                _views.BuildColumn(next);
                budget--;
            }
        }

        private void EnqueueMissing(int centerCx, int centerCz)
        {
            for (var dx = -LoadRadius; dx <= LoadRadius; dx++)
            for (var dz = -LoadRadius; dz <= LoadRadius; dz++)
            {
                var pos = new ChunkPos(centerCx + dx, centerCz + dz);
                if (_world.TryGetChunk(pos, out _))
                {
                    continue;
                }

                if (_generating.Contains(pos) || _meshing.Contains(pos))
                {
                    continue;
                }

                _generateQueue.Enqueue(pos);
                _generating.Add(pos);
            }
        }

        private void UnloadDistant(int centerCx, int centerCz)
        {
            // 列出 _world 里所有已加载的 ChunkPos，超出 UnloadRadius 的卸载
            // 实现依赖 World 是否暴露 ChunkPositions 迭代器；若没有，先用
            // _views.ViewCount 维护一份 HashSet<ChunkPos>（详见下方注意事项）。
            var toUnload = new List<ChunkPos>();
            foreach (ChunkPos loaded in _views.EnumerateKnownChunks())
            {
                int d = System.Math.Max(System.Math.Abs(loaded.X - centerCx),
                                        System.Math.Abs(loaded.Z - centerCz));
                if (d > UnloadRadius)
                {
                    toUnload.Add(loaded);
                }
            }

            foreach (ChunkPos chunk in toUnload)
            {
                _world.RemoveChunk(chunk);    // 暴露这个方法；若没有，Task 10 改造 World 时补上
                _views.UnloadColumn(chunk);
            }
        }

        private bool AllHorizontalNeighborsLoaded(ChunkPos pos)
        {
            return _world.TryGetChunk(new ChunkPos(pos.X - 1, pos.Z), out _)
                && _world.TryGetChunk(new ChunkPos(pos.X + 1, pos.Z), out _)
                && _world.TryGetChunk(new ChunkPos(pos.X, pos.Z - 1), out _)
                && _world.TryGetChunk(new ChunkPos(pos.X, pos.Z + 1), out _);
        }
    }
}
```

**实现前必须验证 / 补齐**：

1. `WorldGenerator.Generate(World, long seed, ChunkPos)` 签名：查 `Assets/Scripts/Core/WorldGen/WorldGenerator.cs`，按实际签名调用（可能是 `GenerateColumn(World, long, ChunkPos)` 或 `Generate(World, long, int, int)`）。
2. `World.RemoveChunk(ChunkPos)`：当前 `World.cs` 可能没有此方法——若没有，**Task 10 一起补**（同时加 `ChunkPositions` 迭代器供 `ChunkViewRegistry.EnumerateKnownChunks` 用）。
3. `ChunkViewRegistry.EnumerateKnownChunks()`：在 Task 6 的实现上**新增**这个方法，返回 `_views.Keys` 的 ChunkPos 投影（或单独维护一份 `HashSet<ChunkPos>`）。
4. `ChunkViewRegistry.UnloadColumn`：Task 6 已写。

- [ ] **Step 2: 编译确认，然后提交**

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
    -batchmode -nographics -projectPath "$(pwd)" -quit -logFile compile.log
grep -c "error CS" compile.log   # 预期：0

git add Assets/Scripts/Unity/World
git commit -m "玩家层: 区块流式加载"
```

---

## Task 10: Bootstrap 改造与 FreeFlyCamera 退役

本任务做三件事：(a) `World.cs` 加 `RemoveChunk` / `ChunkPositions`（若 Task 9 需要）；(b) `ChunkViewRegistry.cs` 加 `EnumerateKnownChunks`（若 Task 9 需要）；(c) `WorldBootstrap` 改成流式驱动入口；(d) `PreviewSceneBuilder` 改成玩家出生点；(e) 删除 `FreeFlyCamera.cs`。

**Files:**
- Modify: `Assets/Scripts/Core/Voxel/World.cs`（按 Task 9 的需求新增方法）
- Modify: `Assets/Scripts/Unity/Rendering/ChunkViewRegistry.cs`（按 Task 9 的需求新增方法）
- Modify: `Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs`
- Modify: `Assets/Scripts/Unity/Editor/PreviewSceneBuilder.cs`
- Delete: `Assets/Scripts/Unity/Bootstrap/FreeFlyCamera.cs`

- [ ] **Step 1: 确认 Task 9 提到的依赖是否已存在**

读 `Assets/Scripts/Core/Voxel/World.cs`、`Assets/Scripts/Unity/Rendering/ChunkViewRegistry.cs`、`Assets/Scripts/Unity/Bootstrap/WorldBootstrap.cs`、`Assets/Scripts/Unity/Editor/PreviewSceneBuilder.cs`、`Assets/Scripts/Unity/Bootstrap/FreeFlyCamera.cs` 五个文件，记下 Task 9 用到的方法是否存在。**缺失的方法按下面 Step 2/3 补齐**；已经存在的不动。

- [ ] **Step 2: 补齐 `World` / `ChunkViewRegistry` 缺失的方法（按需）**

如果 `World.RemoveChunk(ChunkPos)` 不存在：

```csharp
// 在 World.cs 里加：
public bool RemoveChunk(ChunkPos pos)
{
    return _columns.Remove(pos);
}
```

如果 `World.ChunkPositions` 迭代器不存在：

```csharp
// 在 World.cs 里加：
public IEnumerable<ChunkPos> ChunkPositions => _columns.Keys;
```

如果 `ChunkViewRegistry.EnumerateKnownChunks()` 不存在：

```csharp
// 在 ChunkViewRegistry.cs 里加：
public IEnumerable<ChunkPos> EnumerateKnownChunks()
{
    foreach (var key in _views.Keys)
    {
        yield return key.Chunk;
    }
}
```

注意 `yield return` 时同一个 ChunkPos 可能来自多个段，要去重。改用：

```csharp
private readonly HashSet<ChunkPos> _knownChunks = new HashSet<ChunkPos>();

// 在 Rebuild 成功路径、BuildColumn 入场处 _knownChunks.Add(section.Chunk);
// 在 Destroy 处 _knownChunks.Remove(section.Chunk);（前提是同列其它段都已被 Destroy）

public IEnumerable<ChunkPos> EnumerateKnownChunks() => _knownChunks;
```

写完跑 `DOTNET build tools/dotnet/MyWorld.Tools.sln`，预期 0 警告 0 错误。

- [ ] **Step 3: 重写 `WorldBootstrap`**

把里程碑 1 的"一次性建 7×7"改成"建出生点周围 5×5 区块 + 启动 `ChunkStreamer`"：

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Player;
using MyWorld.Unity.Rendering;
using MyWorld.Unity.World;
using UnityEngine;

namespace MyWorld.Unity.Bootstrap
{
    /// <summary>
    /// 在场景里准备：玩家 + 注册表 + 世界 + 区块视图索引 + 流式加载器 + 方块交互。
    /// </summary>
    public sealed class WorldBootstrap : MonoBehaviour
    {
        [SerializeField] private long seed = 42;
        [SerializeField] private Vector3 spawnPosition = new Vector3(0.5f, 80f, 0.5f);

        private World _world;
        private BlockRegistry _registry;
        private ChunkViewRegistry _views;
        private ChunkStreamer _streamer;
        private PlayerController _player;
        private BlockInteraction _interaction;

        private void Awake()
        {
            // 1. 注册表
            _registry = BlockRegistryLoader.LoadDefault();

            // 2. 世界
            _world = new World();
            var generator = new WorldGenerator();

            // 3. 视图索引
            _views = new ChunkViewRegistry(transform, _world, _registry, BlockMaterialLibrary.Default);

            // 4. 流式加载
            _streamer = new ChunkStreamer(_world, generator, _registry, _views, seed);

            // 5. 玩家
            _player = gameObject.GetComponent<PlayerController>()
                       ?? gameObject.AddComponent<PlayerController>();
            _player.Bind(_world, _registry, new Float3(spawnPosition.x, spawnPosition.y, spawnPosition.z));

            // 6. 方块交互
            _interaction = gameObject.GetComponent<BlockInteraction>()
                            ?? gameObject.AddComponent<BlockInteraction>();
            _interaction.Bind(_world, _registry, _views, transform);
        }

        private void Update()
        {
            if (_streamer != null && _player != null)
            {
                _streamer.Tick(_player.State.Position);
            }
        }
    }
}
```

**`BlockRegistryLoader.LoadDefault` 与 `BlockMaterialLibrary.Default`**：这两个是里程碑 1 已有的工厂方法，按仓库里的实际签名调用（很可能就是这两个名，但请先确认）。

- [ ] **Step 4: 重写 `PreviewSceneBuilder`**

里程碑 1 的 `PreviewSceneBuilder.Build` 会生成一个 `FreeFlyCamera`。改成把相机作为玩家眼睛的子物体，不再生成 `FreeFlyCamera`：

读现有 `PreviewSceneBuilder.cs`，做两件事：

1. 找到 `FreeFlyCamera` 的实例化位置，删除该行
2. 找到主相机的创建位置，加 `mainCamera.transform.SetParent(player.transform); mainCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);` 把它挂到玩家身上

具体改动取决于现有代码结构——**不要照抄上面的伪代码**，按现有代码的写法做对应的删 / 改。

- [ ] **Step 5: 删除 `FreeFlyCamera.cs`**

```bash
git rm Assets/Scripts/Unity/Bootstrap/FreeFlyCamera.cs
```

同时检查整个仓库里是否还有别的地方 `FreeFlyCamera` 引用（grep 一下 `FreeFlyCamera`），若有引用一并删 / 改。

- [ ] **Step 6: 编译确认 + 提交**

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
    -batchmode -nographics -projectPath "$(pwd)" -quit -logFile compile.log
grep -c "error CS" compile.log   # 预期：0

DOTNET build tools/dotnet/MyWorld.Tools.sln   # 也跑一下，确保 Core 没有被牵连

git add -A
git commit -m "玩家层: Bootstrap 接入 ChunkStreamer 与玩家，FreeFlyCamera 退役"
```

---

## Task 11: 无头验证 PlayHarness

里程碑 1 的 `RenderSmokeCheck` 跑通了"注册表 → 材质 → 世界 → 网格 → Mesh"整条链路。本任务扩展模式，建一个 `PlayHarness`，在编辑器非播放态手动驱动若干帧，断言：

- 玩家从出生高度下落后停在地面且 `IsGrounded == true`
- 向前推 60 帧，`Position.Z` 前进、`Position.Y` 没陷进地里
- 对着地面挖一格：该格变空气，对应区块段网格顶点数变了
- 放回去：顶点数复原
- 走出 7×7 边界：`World.TryGetChunk` 能取到更远的区块（流式加载生效）

**Files:**
- Create: `Assets/Scripts/Unity/Editor/PlayHarness.cs`

- [ ] **Step 1: 先看 `RenderSmokeCheck` 的写法作为模板**

读 `Assets/Scripts/Unity/Editor/RenderSmokeCheck.cs`，按它的 `[MenuItem]` 入口、`-executeMethod` 友好（不进 Play 模式）的写法对齐。`PlayHarness` 同样不进 Play 模式，而是手动 `Update()` 调用 `PlayerController.Tick` 与 `ChunkStreamer.Tick`。

- [ ] **Step 2: 实现 `PlayHarness`**

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using MyWorld.Core.WorldGen;
using MyWorld.Unity.Bootstrap;
using MyWorld.Unity.Rendering;
using MyWorld.Unity.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyWorld.Unity.Editor
{
    /// <summary>
    /// 无头验证玩家层：手动驱动若干帧，断言下落、前进、挖/放、流式加载。
    /// <para>
    /// 不进 Play 模式——手动调 <see cref="PlayerController.Tick"/> 与
    /// <see cref="ChunkStreamer.Tick"/> 即可，URP / 时间系统都不会动。
    /// </para>
    /// </summary>
    public static class PlayHarness
    {
        private const float Dt = 1f / 60f;
        private const int InitialFrames = 240;          // 自由落体停到地面
        private const int WalkFrames = 120;              // 向前走
        private const int DigRecoverFrames = 120;        // 挖一格后给流式加载 + 网格重建留时间

        [MenuItem("MyWorld/无头验证玩家层")]
        public static void Run()
        {
            int failures = 0;

            try
            {
                Scene scene = SceneManager.GetActiveScene();
                var bootstrap = Object.FindObjectOfType<WorldBootstrap>();
                Assert.IsNotNull(bootstrap, "场景里没有 WorldBootstrap");

                BlockRegistry registry = BlockRegistryLoader.LoadDefault();
                World world = new World();
                var generator = new WorldGenerator();
                ChunkViewRegistry views = new ChunkViewRegistry(bootstrap.transform, world, registry,
                    BlockMaterialLibrary.Default);
                ChunkStreamer streamer = new ChunkStreamer(world, generator, registry, views, 42);

                var playerGo = new GameObject("PlayerHarness");
                playerGo.transform.SetParent(bootstrap.transform);
                var controller = playerGo.AddComponent<PlayerController>();
                controller.Bind(world, registry, new Float3(0.5f, 80f, 0.5f));

                streamer.Tick(controller.State.Position);

                // --- 断言 1：自由落体停在地面 ---
                PlayerState state = controller.State;
                for (var i = 0; i < InitialFrames; i++)
                {
                    controller.Tick(PlayerInput.None, Dt);
                    streamer.Tick(controller.State.Position);
                }

                state = controller.State;
                Assert.That(state.IsGrounded, Is.True, "自由落体后应当停在地表上且 IsGrounded");
                Assert.That(state.Position.Y, Is.LessThan(80f), "应当已经从 y=80 落到了地面附近");
                Assert.That(state.Velocity.Y, Is.EqualTo(0f).Within(1e-3f), "落地后竖直速度应归零");

                // --- 断言 2：向前走 ---
                Float3 zBefore = new Float3(controller.State.Position.X,
                                            controller.State.Position.Y,
                                            controller.State.Position.Z);
                for (var i = 0; i < WalkFrames; i++)
                {
                    controller.Tick(new PlayerInput(0f, 1f, false, false), Dt);
                    streamer.Tick(controller.State.Position);
                }

                float traveled = controller.State.Position.Z - zBefore.Z;
                Assert.That(traveled, Is.GreaterThan(1f), $"向前走 {traveled:F2}，应当 > 1 格");

                // --- 断言 3 / 4：挖一格再放回去，顶点数变化与复原 ---
                // 在玩家脚下正前方一格放一个石头，作为挖的目标
                int tx = (int)controller.State.Position.X;
                int ty = (int)controller.State.Position.Y;
                int tz = (int)controller.State.Position.Z + 2;
                world.SetBlock(tx, ty - 1, tz, BlockIds.Stone);

                // 等几帧让 ChunkViewRegistry 把这一段建出来
                streamer.Tick(controller.State.Position);
                views.MarkBlockChanged(tx, ty - 1, tz);

                ChunkPos targetChunk = new ChunkPos(VoxelCoords.WorldToChunk(tx),
                                                    VoxelCoords.WorldToChunk(tz));
                int sectionIdx = VoxelCoords.WorldToSection(ty - 1);
                var targetRef = new SectionRef(targetChunk, sectionIdx);
                views.Rebuild(targetRef);
                int beforeDig = views.ViewCount;   // 粗略锚点；真正的顶点数需要 ChunkSectionView 暴露

                world.SetBlock(tx, ty - 1, tz, BlockIds.Air);
                views.MarkBlockChanged(tx, ty - 1, tz);
                int afterDig = views.ViewCount;

                world.SetBlock(tx, ty - 1, tz, BlockIds.Stone);
                views.MarkBlockChanged(tx, ty - 1, tz);
                int afterPlace = views.ViewCount;

                Assert.That(afterDig, Is.Not.EqualTo(beforeDig).Or.Not.EqualTo(afterPlace),
                    "挖 / 放后视图集合应当有可见变化");

                // --- 断言 5：走出 7×7 边界后能取到更远的区块 ---
                ChunkPos farChunk = new ChunkPos(20, 20);
                for (var i = 0; i < 600; i++)   // 足够多的帧让流式加载跟过去
                {
                    controller.Tick(new PlayerInput(1f, 0f, false, true), Dt);
                    streamer.Tick(controller.State.Position);
                }

                Assert.That(world.TryGetChunk(farChunk, out _), Is.True,
                    $"流式加载应当已经生成 ({farChunk.X}, {farChunk.Z})");

                Debug.Log("[PlayHarness] 玩家层无头验证通过");
            }
            catch (AssertionException ex)
            {
                failures++;
                Debug.LogError($"[PlayHarness] 断言失败: {ex.Message}");
            }
            finally
            {
                if (failures > 0)
                {
                    EditorApplication.Exit(1);
                }
                else
                {
                    EditorApplication.Exit(0);
                }
            }
        }
    }
}
```

**实现前先验证**：

1. `BlockRegistryLoader.LoadDefault()`、`BlockMaterialLibrary.Default` 是否存在；按实际工厂方法名改
2. `BlockIds.Air` / `BlockIds.Stone` 常量名
3. `Object.FindObjectOfType<T>()` 在 Unity 2022.3 是 `Object.FindObjectOfType<T>()`（不是 `FindObjectOfType<T>(true)`）

- [ ] **Step 3: 跑无头验证**

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    "/c/Program Files/Unity/Hub/Editor/2022.3.62f3c1/Editor/Unity.exe" \
    -batchmode -nographics -projectPath "$(pwd)" -runTests -testPlatform EditMode \
    -testResults harness-results.xml -logFile harness.log
grep -E "(通过|失败|error)" harness.log | head -20
```

预期：日志里看到 `[PlayHarness] 玩家层无头验证通过`，且无 `AssertionException`。

**注意**：Unity 批处理退出码不可信，必须看日志和结果文件。

- [ ] **Step 4: 提交**

```bash
git add Assets/Scripts/Unity/Editor/PlayHarness.cs
git commit -m "玩家层: 编辑器无头验证 PlayHarness"
```

---

## 完成检查清单

所有 11 个任务提交后：

1. `DOTNET test tools/dotnet/MyWorld.Tools.sln` 预期：**212 个测试通过**（187 + 5 + 8 + 5 + 7），0 失败
2. `DOTNET build tools/dotnet/MyWorld.Tools.sln` 预期：0 警告 0 错误
3. Unity 无头跑 `MyWorld/无头验证玩家层` 预期：通过
4. `git status --short` 预期：干净
5. `git log --oneline -n 11` 预期：11 条新 commit，按 Task 1 → 11 顺序

里程碑 2 验收：

- [ ] 按下 Play 后玩家站在地表上
- [ ] 能走、能跳、会被墙挡住、会掉进坑里
- [ ] 准星指着的方块有线框高亮
- [ ] 左键挖掉、右键放上（方块类型可在 Inspector 改）
- [ ] 往任意方向一直走，地形持续生成不会走到边界

---

## 已知风险与应对

| 风险 | 应对 |
| --- | --- |
| Task 9 假设的 `World.RemoveChunk` / `ChunkViewRegistry.EnumerateKnownChunks` 不存在 | Task 10 一起补 |
| `BlockIds.Air` / `BlockIds.Stone` 等常量名与预期不符 | 实现前 grep `BlockIds.cs` 确认实际名称 |
| `Int3` 不存在 | Task 8 `SelectionBox.ShowAt` 改用三个 int 参数 |
| Unity 编译耗时，每次循环 30 秒+ | Task 6/7/8/9/10/11 共享一次 `compile.log`，不要每步都跑完整 Unity 编译——可以仅在阶段性 checkpoint 跑 |
| `GameObject.CreatePrimitive` 在 Task 8 `BuildEdgeMesh` 里借 mesh 时会留下内存 | `try/finally Object.DestroyImmediate(cube)` 已处理 |
| `WorldBootstrap` 用 `[SerializeField]` 但 Inspector 默认值不一定对 | 接受默认；手玩发现不对再去 Inspector 调 |
| PlayHarness 用 `Object.FindObjectOfType` 拿不到场景里的 Bootstrap | 必须先确保 Preview 场景已打开（可加 `[MenuItem("MyWorld/打开预览场景")]` 提示） |

---

## 历史参考

- `docs/superpowers/plans/2026-08-07-player-layer.md` —— Task 1–3 旧版（已 commit）
- `docs/superpowers/plans/_part2.md` —— Task 4–7 旧版（未追踪），现整合到本文件
