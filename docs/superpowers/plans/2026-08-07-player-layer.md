# 玩家层实施计划（里程碑 2）

> **For agentic workers:** 逐任务实现，每个任务先写失败的测试、跑一次确认它红、再写实现、
> 再跑一次确认它绿、然后提交。步骤用 `- [ ]` 勾选跟踪。

**Goal:** 让世界从"能看"变成"能玩"——玩家站在地表上走跳、被墙挡、挖方块、放方块，
往任意方向走地形持续生成。

**Architecture:** 运动解算、放置合法性、脏段收集三块纯数值逻辑放 Core（可 `dotnet test`
覆盖）；Unity 侧只做输入读取、坐标系转换、GameObject 生命周期。Core 依旧零 Unity 依赖。

**Tech Stack:** C# 9 / netstandard2.1（Core）、Unity 2022.3.62f3c1 + URP 14、NUnit、旧版 Input

---

## 设计依据

`docs/superpowers/specs/2026-08-07-player-layer-design.md`

## 文件结构

**Core 层新建**（`Assets/Scripts/Core/`）：

| 文件 | 职责 |
| --- | --- |
| `Player/PlayerState.cs` | 位置（脚底中心）、速度、是否着地 |
| `Player/PlayerInput.cs` | 世界空间的水平移动意图 + 跳跃 + 疾跑 |
| `Player/PlayerMotorSettings.cs` | 尺寸与运动参数，含 `Default` |
| `Player/PlayerMotor.cs` | `(状态, 输入, 参数, dt) → 新状态`，内部走 `VoxelCollision` |
| `Player/BlockPlacement.cs` | 由射线命中推出放置格，并判定是否合法 |
| `Voxel/DirtySections.cs` | 改一个方块后，哪些区块段需要重建网格 |

**Unity 层新建**（`Assets/Scripts/Unity/`）：

| 文件 | 职责 |
| --- | --- |
| `Rendering/ChunkViewRegistry.cs` | (区块, 段) → `ChunkSectionView`，按需建/重建/销毁 |
| `Player/PlayerController.cs` | 输入 → `PlayerMotor` → transform；鼠标视角与眼高 |
| `Player/BlockInteraction.cs` | 射线选中、线框高亮、左键挖、右键放 |
| `Player/SelectionBox.cs` | 线框立方体的 Mesh 与材质 |
| `World/ChunkStreamer.cs` | 跟随玩家生成/卸载区块列，分生成与建网格两个队列 |
| `Editor/PlayHarness.cs` | 无头驱动若干帧，断言下落停住、前进、挖了会刷新 |

**修改**：

| 文件 | 改动 |
| --- | --- |
| `Bootstrap/WorldBootstrap.cs` | 从"一次性建 7×7"改成"建出生点周边 + 交给 `ChunkStreamer`" |
| `Editor/PreviewSceneBuilder.cs` | 相机挂到玩家身上，`FreeFlyCamera` 退役 |
| `Bootstrap/FreeFlyCamera.cs` | 删除 |

## 每个任务开始前

在**仓库根目录**执行，确认起点干净：

```bash
git status --short
```

Git Bash 下 `dotnet` 必须补环境变量（见 CLAUDE.md 与记忆），本计划里所有
`dotnet test` 都指这条完整命令：

```bash
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    dotnet test tools/dotnet/MyWorld.Tools.sln
```

---

## Task 1: 玩家状态、输入与参数

三个纯数据类型，没有行为，所以不单独写测试——它们的正确性由 Task 2/3 的运动测试覆盖。
先建出来是为了让后面的签名能一次写对。

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

字段用可读写属性（Unity 侧要从 Inspector 灌值），但保持 `sealed class` 不是 struct——
参数集会被反复传递，struct 拷贝没意义。

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
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    dotnet build tools/dotnet/MyWorld.Tools.sln
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

测试用的世界：`World` 里在 `y = 63` 铺一层石头（`SetBlock` 会自动建区块列），
玩家从 `y = 70` 开始下落。所有测试共用一个 `WorldSolidSource`。

```csharp
using MyWorld.Core.Blocks;
using MyWorld.Core.Math;
using MyWorld.Core.Player;
using MyWorld.Core.Voxel;
using NUnit.Framework;

namespace MyWorld.Tests.Player
{
    /// <summary>竖直方向：重力累积、终端速度、落地、撞头。</summary>
    public sealed class PlayerMotorGravityTests
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

            _source = new WorldSolidSource(_world, BlockRegistry.CreateBuiltIn());
        }

        [Test]
        public void 空中一帧后速度等于重力乘时间()
        {
            var state = PlayerState.AtRest(new Float3(0f, 70f, 0f));

            PlayerState next = PlayerMotor.Step(_source, state, PlayerInput.None, _settings, Dt);

            Assert.That(next.Velocity.Y, Is.EqualTo(_settings.Gravity * Dt).Within(1e-4f),
                "自由落体第一帧的竖直速度应当正好是 重力 × dt");
            Assert.That(next.IsGrounded, Is.False, "还在空中不应当判定为着地");
        }

        [Test]
        public void 长时间下落速度被终端速度夹住()
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
        public void 下落最终停在地面顶上()
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
        public void 站在地面上竖直速度不会持续累积()
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
        public void 撞到天花板竖直速度归零()
        {
            // 头顶加一层石头：玩家高 1.8，站在 y=64 时头在 66.8 之下，把方块放到 y = 66
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
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~PlayerMotorGravityTests"
```

预期：5 个全过。

**如果"下落最终停在地面顶上"差了 1e-4**：那是 `VoxelCollision` 的 `Skin` 间隙，
断言容差 0.01 已经包住了；如果差得更多，说明速度没在落地时归零，回头看 Step 2 的 `HitY` 分支。

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

namespace MyWorld.Tests.Player
{
    /// <summary>水平移动、疾跑、撞墙与跳跃。</summary>
    public sealed class PlayerMotorMoveTests
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

            _source = new WorldSolidSource(_world, BlockRegistry.CreateBuiltIn());
        }

        private PlayerState Run(PlayerState state, PlayerInput input, int frames)
        {
            for (var i = 0; i < frames; i++)
            {
                state = PlayerMotor.Step(_source, state, input, _settings, Dt);
            }

            return state;
        }

        private PlayerState OnGround(float x = 0f, float z = 0f)
            => new PlayerState(new Float3(x, 64f, z), default, true);

        [Test]
        public void 地面上持续前推会以行走速度移动()
        {
            PlayerState state = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 60);

            // 有一帧的加速过程，所以断言"接近但不超过"一秒的行程
            Assert.That(state.Position.Z, Is.GreaterThan(_settings.WalkSpeed * 0.9f),
                "推满一秒应当走出接近 WalkSpeed 格");
            Assert.That(state.Position.Z, Is.LessThanOrEqualTo(_settings.WalkSpeed + 0.1f),
                "不应当超过行走速度上限");
        }

        [Test]
        public void 疾跑比行走走得远()
        {
            PlayerState walk = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 60);
            PlayerState sprint = Run(OnGround(), new PlayerInput(0f, 1f, false, true), 60);

            Assert.That(sprint.Position.Z, Is.GreaterThan(walk.Position.Z),
                "按住疾跑应当比普通行走走得更远");
        }

        [Test]
        public void 松开方向键后水平速度衰减到零()
        {
            PlayerState state = Run(OnGround(), new PlayerInput(0f, 1f, false, false), 30);
            state = Run(state, PlayerInput.None, 60);

            Assert.That(System.Math.Abs(state.Velocity.Z), Is.LessThan(0.05f),
                "松开方向键一秒后水平速度应当已经衰减到接近零");
        }

        [Test]
        public void 撞墙后停下且速度归零()
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
        public void 贴墙斜推能沿墙滑动()
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
        public void 地面上跳跃会离地并升高()
        {
            PlayerState state = PlayerMotor.Step(_source, OnGround(), new PlayerInput(0f, 0f, true, false),
                _settings, Dt);

            Assert.That(state.Velocity.Y, Is.GreaterThan(0f), "起跳后竖直速度应当向上");
            Assert.That(state.IsGrounded, Is.False, "起跳后应当离地");
        }

        [Test]
        public void 空中按跳无效()
        {
            var air = new PlayerState(new Float3(0f, 80f, 0f), default, false);

            PlayerState state = PlayerMotor.Step(_source, air, new PlayerInput(0f, 0f, true, false),
                _settings, Dt);

            Assert.That(state.Velocity.Y, Is.LessThan(0f),
                "空中按跳不应当生效，竖直速度仍应当在重力作用下向下");
        }

        [Test]
        public void 跳跃高度约为一格多()
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
env "ProgramFiles(x86)=C:\Program Files (x86)" "APPDATA=C:\Users\tjf\AppData\Roaming" \
    "LOCALAPPDATA=C:\Users\tjf\AppData\Local" \
    dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~PlayerMotor"
```

预期：Task 2 的 5 个 + 本任务的 8 个，共 13 个全过。

**如果"跳跃高度约为一格多"落在区间外**：`JumpSpeed² / (2 × |Gravity|)` = 8.4² / 56 ≈ 1.26 格，
理论值在区间内。真跑出界说明重力在起跳当帧就被扣了一次（顺序错了），
检查 `StepVertical` 里跳跃分支是否**直接返回**而没有再减重力。

- [ ] **Step 4: 提交**

```bash
git add Assets/Scripts/Core/Player Assets/Tests/EditMode/Player
git commit -m "玩家层: 水平移动、疾跑、沿墙滑动与跳跃"
```

---
