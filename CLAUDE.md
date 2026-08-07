# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

MyWordGame 是一个自研体素沙盒游戏（Unity 6 + 纯 C# Core 层），父子共同开发。
仓库内所有文档、注释、测试断言消息一律用中文，新增内容请保持一致。

## 常用命令

全部在**仓库根目录**执行，**不需要安装 Unity**：

```bash
dotnet test tools/dotnet/MyWorld.Tools.sln                # 全部测试（当前 187 个）
dotnet test tools/dotnet/MyWorld.Tools.sln --filter "FullyQualifiedName~GreedyMesherTests"   # 单个测试类
dotnet build tools/dotnet/MyWorld.Tools.sln              # 编译三个工程

dotnet run --project tools/dotnet/MyWorld.Preview        # ASCII 世界预览（默认种子）
dotnet run --project tools/dotnet/MyWorld.Preview 12345  # 指定种子
```

**解决方案必须显式指定路径**：Unity 每次导入都会在仓库根目录重新生成自己的 `.sln`/`.csproj`，
因此我们的解决方案放在 `tools/dotnet/MyWorld.Tools.sln`，根目录的工程文件已入 `.gitignore`。

Unity 侧跑同一批测试（EditMode）：

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . -runTests -testPlatform EditMode `
  -testResults unity-test-results.xml -logFile unity-tests.log
```

跑之前必须确保没有其它 Unity 实例占用项目（否则报 `another Unity instance is running`）。
**Unity 批处理的退出码不可信——崩溃时仍返回 0**，必须看日志和结果文件。

`MyWorld.Preview` 输出纵向剖面、俯视高度图、贪心网格压缩比，是本项目**验证世界生成效果的
主要手段**——改了 `WorldGenerator` 参数后跑一次，对比前后两张图。字形约定见
`docs/specs/visual-text-conventions.md`。

## 源码位置与工程结构（关键陷阱）

源码的**物理位置在 `Assets/` 下**，`tools/dotnet/*.csproj` 只是用 `<Compile Include="..\..\..\Assets\...">`
**链接**同一份文件，不复制。因此：

- **永远编辑 `Assets/Scripts/Core/**` 和 `Assets/Tests/EditMode/**`**，`tools/dotnet/` 下只有 csproj 和 Preview 的 Program.cs
- 两个工程都设了 `EnableDefaultCompileItems=false`，新增 `.cs` 文件只要落在被链接的目录下即自动纳入，无需改 csproj
- `Assets/Tests/EditMode` 用 NUnit，与 Unity Test Framework 一致，同一批测试将来可原样在 EditMode 下跑

## 分层与硬约束

| 层 | 位置 | 状态 |
| --- | --- | --- |
| `MyWorld.Core` | `Assets/Scripts/Core` | 已实现 |
| `MyWorld.Unity` | `Assets/Scripts/Unity` | 已实现渲染层（材质库、Mesh 上传、区块段视图、启动器） |
| `MyWorld.Gameplay` | `Assets/Scripts/Gameplay` | **尚未创建**（玩家控制、交互、物品栏） |

Core 层的约束由编译器强制，不是约定：

- `MyWorld.Core.asmdef` 设了 `"noEngineReferences": true` —— **Core 里出现任何 `UnityEngine.*` 都编译不过**。
  需要向量就用 `Core/Math/Float3`、`Float2`，不要用 `Vector3`
- `MyWorld.Core.csproj`：`netstandard2.1` + `LangVersion 9.0`（对齐 Unity 6 的 API 兼容级别）+
  `TreatWarningsAsErrors=true`。**Core 层一个警告都不能留**
- Core 唯一的外部依赖是 Newtonsoft.Json（方块注册表解析 JSON）

## Core 层设计要点

读完这些再动代码，多数坑都在这里：

**坐标换算只走 `VoxelCoords`。** 世界高度 `[-64, 320)`，区块 16×16、纵向切成 24 个 section。
换算一律用算术右移 `>>4` 和位掩码 `&15`，因为 `/` 和 `%` 在负坐标上会向零取整而出错。
不要在别处手写坐标换算。

**读容忍、写严格。** `ChunkColumn.GetBlock` / `World.GetBlock` 越界或未加载时**返回空气而非抛异常**——
网格生成需要采样区块外的"邻居"来剔除接缝面，宽容读取免掉大量边界判断；且读取不会创建区块，
避免采样时撑大内存。而 `SetBlock` 越界一律抛异常。

**"实心"有两套互不相同的含义，别混用：**
- `ChunkMeshSource.IsSolid` → 判 `Opaque`（是否挡视线）。水不挡视线，所以水下地形要渲染
- `WorldSolidSource.IsSolidAt` → 判 `Solid`（是否挡移动）。水不挡移动，玩家可以游进去

**热路径用泛型约束而非接口引用。** `IBlockSource` / `ISolidBlockSource` 的实现都是 `readonly struct`，
`GreedyMesher.Build<TSource>` 和 `VoxelRaycaster.Cast<TSource>` 用 `where TSource : I...` 约束，
让 JIT 特化掉虚调用。新增数据源请沿用 struct + 泛型的写法。

**`ChunkSection` 是局部调色板 + 位打包。** 位宽只取 0/1/2/4/8/16（能整除 64，条目不跨 `ulong` 边界），
随调色板大小自动升位。位宽 0 表示整段同一种方块，此时**不分配数组**。

**贪心网格的 UV 按格数铺开**（`(0,0)..(width,height)`），所以**方块贴图必须四边无缝平铺**，
否则大平面会出现规则网格状接缝。这条约束反向决定了整个美术流程，见 `art/README.md`。
遍历从 `axis = -1` 开始，使最外层能与邻区块比对；区域外的面留给邻区块生成，避免双方重复出面。

**确定性生成。** `ValueNoise2D` 用整数哈希，不持有随机数对象；`WorldGenerator` 只依赖 seed 与
**世界坐标**（不是区块内坐标），因此与生成顺序无关、跨区块接缝自然对齐、可并行调用。
改动这里务必保持这个性质。

**存档只存改动过的区块。** `RegionFile` 把 32×32 区块聚成一个文件（魔数 `MWRG`，小端序），
未改动的区块加载时由 seed 重新生成。`ChunkSerializer` 只写非空 section（用 32 位掩码标记），
数据段 Deflate 压缩。改二进制布局必须同步 bump `FormatVersion` 并处理旧版本。

## 方块定义是数据驱动的

`Assets/StreamingAssets/blocks/*.json`，一个文件一种方块，格式与字段说明见同目录 `_format.md`。
**加新方块通常不需要改 C# 代码**：复制一个 JSON，改 id 和贴图名即可。

两处必须手动保持一致，否则世界生成会放错方块：

- 内置 7 个方块的 `numericId` **写死为 0–6**，必须与 `Core/Voxel/BlockIds.cs` 的常量一一对应
- 新方块不写 `numericId` 时，系统按 `id` 字母序从 1000 起自动分配（排序保证跨机器结果一致）

`BlockDefinitionFilesTests` 会校验真实的 JSON 文件：解析、ID 冲突、numericId 与 `BlockIds` 一致、
**以及引用的贴图是否都已在 `art/requests/blocks/` 下提过需求**。改 JSON 后跑 `dotnet test` 立刻能发现问题。

## 美术资源流程

`art/` 是图片资源的需求提出处，完整流程与硬性约束见 `art/README.md`。要点：

- 需求写在 `art/requests/`（含调色板、AI 提示词、后处理步骤、验收清单），产物先落 `art/incoming/`（不入版本管理），验收后才移进 `Assets/`
- 方块贴图统一 **32×32**、无抗锯齿、**四边无缝**；生成时要 512/1024，再用**最近邻**降采样
- 入库后要把 `art/README.md` 需求索引表的状态改成「已入库」

## 视觉设计一律写成文本

本项目不产出视觉稿、不做浏览器原型。画面相关设计写进 `docs/specs/`，用三种形式：
ASCII 示意（由 `MyWorld.Preview` 从真实数据渲染，不手绘）、可落地为参数的画面描述、
具体数值的参数表。避免"更好看""更有氛围"这类无法验证的描述。规范见
`docs/specs/visual-text-conventions.md`。

## Unity 侧

**实际使用 Unity 2022.3.62f3c1（中国版）**，不是最初规划的 Unity 6。已实机验证
187 个测试在 EditMode 下全部通过。这对架构无实质影响：Core 是 `netstandard2.1` + C# 9，
2022.3 完全支持；`Mesh.AllocateWritableMeshData`、Burst、Job System、URP Forward+ 也都具备。

`Packages/manifest.json` 的版本已经实机解析验证，**不要改成 Unity 6 的版本号**（URP 17.x、
Burst 1.8.30 在 2022.3 上不存在，会卡死包解析）。**URP 用 14.0.11**（编辑器内置，解析不走网络），
管线资产在 `Assets/Settings/`，由 `MyWorld/接入 URP 管线` 菜单（`UrpSetup.Apply`）生成并挂到
Graphics 与全部质量档位上——**不要手改那两个 ProjectSettings 的 YAML**。

`ProjectSettings/` 与 `.meta` 文件**已纳入版本管理**，不要删——`.meta` 决定资源 GUID。
Newtonsoft Json 包是必需的，缺了 Core 编译失败。

编辑器菜单 `MyWorld/` 下有三个批处理入口，都能用 `-executeMethod` 无头跑：

| 菜单项 | 方法 | 作用 |
| --- | --- | --- |
| 重建预览场景 | `PreviewSceneBuilder.Build` | 重新生成 `Assets/Scenes/Preview.unity` |
| 渲染层冒烟检查 | `RenderSmokeCheck.Run` | 无头跑通「注册表 → 材质 → 世界 → 网格 → Mesh」整条链路 |
| 接入 URP 管线 | `UrpSetup.Apply` | 建管线资产并挂到 Graphics 与质量档位 |

