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
dotnet test
```

`tools/dotnet/` 下的两个工程只是**链接** `Assets/` 中的同一份源码，不复制文件。
测试框架使用 NUnit，与 Unity Test Framework 一致，因此同一批测试将来可原样在 EditMode 下运行。

`global.json` 将 SDK 固定为 9.0.x，保证构建可复现。

## 首次在 Unity 中打开

**目标引擎：Unity 6（6000.0 LTS）。** 本项目不使用团结引擎（Tuanjie，基于 Unity 2022.3）——
若改用它，`Packages/manifest.json` 至少需要把 URP 降到 14.x、Collections 降到 2.1.x。

1. 安装 Unity Hub 与 Unity 6（6000.0 LTS）。
2. 在 Hub 中选择「Add project from disk」，指向本仓库根目录。
3. 首次打开时 Unity 会生成 `Library/`、`ProjectSettings/` 与各类 `.meta` 文件。

### 如果首次打开报包版本错误

`Packages/manifest.json` 是在没有 Unity 环境的情况下手写的，其中
URP、Input System、Test Framework 的具体补丁号**未经实机验证**。若 Package Manager 报某个
版本不存在，按以下顺序处理：

1. 在 Package Manager 里把报错的包切换到该 Unity 版本推荐的版本
2. 仍不行就**直接删除 `Packages/manifest.json`**，让 Unity 重新生成一份默认清单，
   再在 Package Manager 中手动添加：Burst、Collections、Mathematics、Input System、
   Universal RP、Newtonsoft Json

第 2 种做法一定可行，因为 Unity 只会装它自己认可的版本。

**Newtonsoft Json 是必需的**——`MyWorld.Core` 的方块注册表用它解析 JSON，缺了会编译失败。

## 视觉设计的表达方式

本项目不产出视觉稿，所有画面相关设计一律写成文本，存放于 `docs/specs/`：

- **画面描述**：第一人称视野、相机参数、天空与雾效、方块高亮表现
- **ASCII 示意**：区块切片、网格合并前后对比、光照传播范围、UI 布局
- **参数表**：颜色 HEX、尺寸、时间曲线等可直接落到代码的数值
