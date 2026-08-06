# MyWordGame

一个自研的体素沙盒游戏，父子共同开发。

## 架构

三层划分，依赖方向单向向下：

| 层 | 位置 | 说明 |
| --- | --- | --- |
| `MyWorld.Core` | `Assets/Scripts/Core` | 纯 C#，**零 Unity 依赖**。世界数据、生成、光照、网格算法、存档格式 |
| `MyWorld.Unity` | `Assets/Scripts/Unity` | Unity 适配层：Burst Job 包装、Mesh 上传、生命周期管理 |
| `MyWorld.Gameplay` | `Assets/Scripts/Gameplay` | 玩家控制、交互、物品栏、昼夜 |

`MyWorld.Core.asmdef` 设置了 `"noEngineReferences": true`，由编译器强制保证 Core 不会引用
Unity API。这既让世界逻辑可以脱离引擎快速测试，也是将来做专用服务器的前提。

## 开发与测试（不需要安装 Unity）

Core 层是标准 .NET 类库，可直接在命令行开发和验证：

```powershell
dotnet test tools/dotnet/MyWorld.Tools.sln
```

注意解决方案在 `tools/dotnet/` 下而不在仓库根目录——**Unity 每次导入都会在根目录重新生成
自己的 `.sln` 和 `.csproj`**，两者放一起会互相覆盖。根目录的工程文件已加入 `.gitignore`。

`tools/dotnet/` 下的三个工程只是**链接** `Assets/` 中的同一份源码，不复制文件。
测试框架使用 NUnit，与 Unity Test Framework 一致，因此**同一批测试在两边都能跑**：

```powershell
# Unity 侧（EditMode）
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . -runTests -testPlatform EditMode `
  -testResults unity-test-results.xml -logFile unity-tests.log
```

运行前确保没有其它 Unity 实例占用本项目，否则会报
`another Unity instance is running with this project open`。
**Unity 批处理的退出码不可信**（崩溃时仍返回 0），必须以日志和结果文件为准。

`global.json` 将 SDK 固定为 9.0.x，保证构建可复现。

## 引擎环境

**实际使用：Unity 2022.3.62f3c1（中国版）**，已实机验证全部 155 个测试在 EditMode 下通过。

最初规划的是 Unity 6（6000.0 LTS），但实际安装的是 2022.3。这对本项目**没有实质影响**：
Core 层是 `netstandard2.1` + C# 9，2022.3 完全支持；我们需要的
`Mesh.AllocateWritableMeshData`、Burst、Job System、URP Forward+ 在 2022.3 上也都具备。

若日后升级到 Unity 6，需要调整的只有 `Packages/manifest.json` 里的包版本（如 URP 14.x → 17.x）。

### 首次打开

1. 在 Unity Hub 中选择「Add project from disk」，指向本仓库根目录。
2. Unity 会生成 `Library/`（已忽略）与根目录的 `.sln`/`.csproj`（已忽略）。
3. `ProjectSettings/` 与 `.meta` 文件**已纳入版本管理**，不要删除——`.meta` 决定资源 GUID，
   丢失会导致场景引用全部断链。

**Newtonsoft Json 是必需的**——`MyWorld.Core` 的方块注册表用它解析 JSON，缺了会编译失败。

## 视觉设计的表达方式

本项目不产出视觉稿，所有画面相关设计一律写成文本，存放于 `docs/specs/`：

- **画面描述**：第一人称视野、相机参数、天空与雾效、方块高亮表现
- **ASCII 示意**：区块切片、网格合并前后对比、光照传播范围、UI 布局
- **参数表**：颜色 HEX、尺寸、时间曲线等可直接落到代码的数值
