# MyWordGame

自研的体素沙盒游戏，父子共同开发。

## 当前进度

- **m13 W5 收口**（2026-08-20）+ **m12 第 0 波**（2026-08-21），双链全绿——dotnet **1012/1012**、EditMode **1786/1786**（实测）
- 三层架构稳定：`MyWorld.Core`（纯 C# 零 UnityEngine）/ `MyWorld.Unity`（URP 2022.3 适配层）/ `MyWorld.Gameplay`（玩法接线）
- 内容范围：
  - **m5** 视觉打磨（Linear 色域 / `UrpMaterialFactory` / 自适应全屏 / 毫秒预算流式）
  - **m6** UI 修复 + 帮助菜单 + 引导任务双章
  - **m7** 生存可用性（安全重生 / 僵尸平衡 / 右键食物优先 / 掉落物吸附）
  - **m8** 五生物拼装造型 + Esc 暂停菜单
  - **m9** 战斗手感四件套（攻击/逃跑/闪红/击退）
  - **m10** 四矿石 + 工具门槛 + 镐耐久碎裂 + 四属性装备
  - **m11** 完整游戏（盔甲穿戴 / 附魔系统 / 村庄 / Boss / 音频四系统 / 视频两系统 / TitleScreenUi 多视频源）
- **m12 全波次已落地**（2026-08-21）：挖掘计时 + 六向放置/垫脚 + 世界管理（P0/P1）+ 成就 16 枚 / 图鉴 26 条目 / 药水乐器（W1-W3）+ **水生 5 种 + 飞行 4 种生物**（W4，MobKind 28-36）——双链 dotnet **1039/1039**、EditMode **1813/1813** 全绿
- **唯一遗留**：P2 美术批 71 项被 Token Plan 配额挡住（2026-08-21 实证），配额恢复后一条命令补齐
- **已完成 m13**：飞行（双击空格+F）/ 怪物血条 + 宝宝难度 / 远程参数化 + 火枪 / 武器面板 + 模态点外关闭

## 架构

依赖方向单向向下，Core 由编译器强制与 Unity 解耦：

| 层 | 位置 | 说明 |
| --- | --- | --- |
| `MyWorld.Core` | `Assets/Scripts/Core` | 纯 C#，**零 Unity 依赖**。世界数据、生成、光照、网格算法、存档格式、生物 AI 状态机 |
| `MyWorld.Unity` | `Assets/Scripts/Unity` | Unity 适配层：Burst Job 包装、Mesh 上传、生命周期管理、渲染、UI、音频、视频 |
| `MyWorld.Gameplay` | `Assets/Scripts/Gameplay` | 玩家控制、交互、物品栏、昼夜（接线为主） |

`MyWorld.Core.asmdef` 设置 `"noEngineReferences": true`——Core 里出现任何 `UnityEngine.*` 都编译不过。这既让世界逻辑可脱离引擎快速测试，也是将来做专用服务器的前提。

完整约束、命令、陷阱详见 `CLAUDE.md`（接手必读）。

## 开发与测试（不需要安装 Unity）

Core 是标准 .NET 类库，可直接在命令行开发和验证：

```bash
# 解决方案在 tools/dotnet/ 而非仓库根（Unity 每次导入会覆盖根目录 .sln）
dotnet test tools/dotnet/MyWorld.Tools.sln                # 全部测试
dotnet build tools/dotnet/MyWorld.Tools.sln              # 编译三个工程
dotnet run --project tools/dotnet/MyWorld.Preview        # ASCII 世界预览（默认种子）
dotnet run --project tools/dotnet/MyWorld.Preview 12345  # 指定种子
```

`tools/dotnet/*.csproj` 用 `<Compile Include>` **链接** `Assets/` 中的同一份源码，不复制。新增 `.cs` 落在被链接目录下自动纳入，无需改 csproj。

测试框架使用 NUnit，与 Unity Test Framework 一致——**同一批测试在 dotnet 链与 Unity EditMode 链都能跑**。

`global.json` 把 SDK 固定为 9.0.x（`rollForward: latestFeature`），保证构建可复现。

`MyWorld.Preview` 是验证世界生成效果的主要手段——改了 `WorldGenerator` 参数后跑一次，对比前后两张图。字形约定见 `docs/specs/visual-text-conventions.md`。

## Unity 侧 EditMode

引擎实际版本：**Unity 2022.3.62f3c1（中国版）**，不是最初规划的 Unity 6。对本项目没有实质影响——Core 是 `netstandard2.1` + C# 9，2022.3 完全支持；URP 用 14.0.11（编辑器内置，解析不走网络）。

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" `
  -batchmode -nographics -projectPath . -runTests -testPlatform EditMode `
  -testResults unity-test-results.xml -logFile unity-tests.log
```

跑前必须确保没有其它 Unity 实例占用项目（否则报 `another Unity instance is running`）。
**Unity 批处理的退出码不可信**——崩溃时仍返回 0，必须看日志和结果 XML。

一键流水线（测试 + Build + 启动）：

```bash
./tools/scripts/build-and-run.sh                       # 完整流水线
./tools/scripts/build-and-run.sh --skip-tests         # 只 Build + 启动
./tools/scripts/build-and-run.sh --no-launch --clean  # Build 但不启动，先清旧产物
```

脚本按顺序跑 dotnet test → Unity EditMode → URP 接入 → Scenes In Build 注册 → Windows Standalone Build → 后台启动 `Builds/Windows/MyWordGame.exe`。每步都 grep 结果文件/log 确认通过，失败立刻 `exit 1`。

## 首次打开

1. Unity Hub → 「Add project from disk」→ 指向本仓库根目录
2. Unity 会生成 `Library/`（已忽略）与根目录的 `.sln`/`.csproj`（已忽略）
3. `ProjectSettings/` 与 `.meta` 文件**已纳入版本管理**，不要删——`.meta` 决定资源 GUID，丢失会导致场景引用全部断链
4. Newtonsoft Json 包是必需的——`MyWorld.Core` 的方块注册表用它解析 JSON，缺了会编译失败

## 文档导航

| 目录 | 内容 | 何时看 |
| --- | --- | --- |
| `docs/specs/` | 视觉/玩法规范文本 | 需要用文字定画面或玩法时 |
| `docs/superpowers/specs/` | 阶段性技术设计 | 开启一个新里程碑前 |
| `docs/superpowers/plans/` | 实施计划（按日期） | 设计文档通过后落地 |
| `docs/小孩子玩后需求/` | 孩子反馈原文 | 评估新一轮需求时 |
| `CLAUDE.md` | 项目完整约束（命令/陷阱/契约/里程碑约定） | 接手前必读 |
| `AGENTS.md` | 子代理速查表（硬约束 + 入口） | ZCode 子代理接入 |

## 视觉设计的表达方式

本项目不产出视觉稿、不做浏览器原型。画面相关设计一律写成文本，存放于 `docs/specs/`，三种形式：

- **画面描述**：第一人称视野、相机参数、天空与雾效、方块高亮表现
- **ASCII 示意**：区块切片、网格合并前后对比、光照传播范围、UI 布局
- **参数表**：颜色 HEX、尺寸、时间曲线等可直接落到代码或 JSON 的数值

规范与字形约定见 `docs/specs/visual-text-conventions.md`——硬性边界：禁止"更好看""更有氛围"这类不可验证的描述。

## 收口铁律（m13 起固定）

- **双链绿 ≠ 实机可见**——dotnet + EditMode 全绿后必须 `./tools/scripts/build-and-run.sh` 重新出包 `Builds/Windows/MyWordGame.exe` 验证
- **每个里程碑收口与每次内容合入后必须出包**——av 赛道 08-18 凌晨提交的 BGM/环境音/标题画面视频在 08-14 旧 exe 里全部"不存在"，孩子在旧包上验收以为是漏集成
- 验收剧本头部必须标注 `Builds/Windows/MyWordGame.exe` mtime（包构建时间）
- 子代理不 commit，由主会话评审后按域顺序提交
