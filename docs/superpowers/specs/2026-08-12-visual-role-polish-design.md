# 视觉修整 + 玩家角色 + AI 视觉测试设计

> **For agentic workers:** 实施计划见 `docs/superpowers/plans/2026-08-12-visual-role-polish.md`。
> 本 spec 回答"做成什么样"——补缺失贴图、加玩家身体几何 + 走路动画、修 hotbar/积木显示、补 AI 视觉回归测试。

**目标：** 把"能跑通但视觉一片粉/空"的里程碑-3 收尾，让游戏看上去是个像样的体素沙盒。

**范围：** A 贴图补完（方块 + 玩家 + 物品 + mob）· B 玩家第三人称身体与走路动画 ·
C 已有 UI 视觉修整（hotbar / 选区线框 / 选中的物品手部） · D 积木颜色错位诊断修 ·
E AI 视觉回归测试基础设施（PlayHarness 截图 + 像素采样）。

**不属于本里程碑：** 真正骨骼动画（Animation Rigging）、粒子系统、shader 升级、
声效、UI 整体改版。技能图标 / 工具升级特效留待后续。

---

## 1. 背景

里程碑-3 跑通后，玩家实测反馈视觉有五处问题：

1. **粉色长方体**——`BlockMaterialLibrary.CreateMissingMaterial` 给缺贴图方块挂洋红占位，
   `Assets/StreamingAssets/blocks/textures/` 只有 7 个内置方块贴图，但注册表里有 15 个方块，
   8 个里程碑-3 方块（planks/log/leaves/sapling/crafting_table/iron_door/lever/redstone_dust）
   全显示为洋红立方体。玩家描述「移动物品粉色」=挖到/看到木头、树叶、门等都是粉色。
2. **角色无视觉**——玩家是隐形第一人称，按 F5 切第三人称只看到空气。
3. **Hotbar 矩阵空**——`HotbarUI.cs` 已经实现读 `StreamingAssets/items/textures/<id>.png`，
   但 `Assets/Art/Items/` + `StreamingAssets/items/textures/` 都没有贴图，
   所有物品显示为 `missingTex`（代码里 fallback 的棕色，不算好看但不会粉色）。
4. **积木颜色错位 / 黑红**——草块侧面 / 树叶 / 木板的实际渲染配色与设计稿有偏差，
   部分堆叠面看上去一面黑一面红。需要 PlayHarness 截图后才能精确诊断。
5. **缺乏视觉回归手段**——目前所有验证都是功能断言（NUnit），没有「进游戏看一眼对不对」的渠道。

---

## 2. 硬约束

| 项 | 值 |
| --- | --- |
| 贴图生成 | 沿用 `tools/generate_art.py` + `tools/postprocess_art.py`，复用现有 `art/requests/` 需求文件 |
| 贴图尺寸 | 方块 32×32、玩家皮肤 64×64、UI 元素按各 request 文件 |
| 贴图配色 | 方块严格使用 `art/README.md` 的全局调色板 |
| 玩家模型 | 程序生成几何（Primitive 拼 head/torso/arms/legs），不依赖外部 .blend / .fbx |
| 走路动画 | 基于速度驱动的简单腿/臂摆动 + 头上下身动（head bob），不用 Animation Rigging |
| 视觉测试 | 截图 + 像素采样（无 vision judge），基线 PNG 落 `tools/dotnet/MyWorld.Tools/VisualBaselines/` |
| 文档语言 | 中文（与 `CLAUDE.md` 一致）|

---

## 3. 贴图补完（任务 A）

### 3.1 范围

| 类别 | 缺图 | 来源 |
| --- | --- | --- |
| 方块 | planks, log-side, log-top, leaves, sapling, crafting_table-top, crafting_table-side, iron_door, lever, redstone_dust（10 个 PNG） | art/requests/blocks/{planks, log, leaves, sapling, crafting_table, iron_door, lever, redstone_dust}.md |
| 玩家皮肤 | skin.png (64×64) | art/requests/player/skin.md |
| 物品图标 | emerald, book, enchanted_book, lapis + 全部里程碑-3 物品（约 30 个 PNG） | 新增 art/requests/items/<id>.md |
| mob | pig, sheep, zombie, skeleton, creeper（5 个 PNG，用于 mob 视图） | 新增 art/requests/entities/<id>.md |
| villager | farmer, librarian, blacksmith（3 个 PNG） | 新增 art/requests/entities/villager-*.md |

### 3.2 流程

1. **写/补需求文件**：`art/requests/{blocks,player,items,entities}/<id>.md`，
   每个文件含「## AI 提示词」+「## 调色板」+「## 验收标准」三段。
2. **跑生成**：`python tools/generate_art.py --all --jobs 4`，
   产物落 `art/incoming/<id>.png`。
3. **跑后处理**：`python tools/postprocess_art.py --all`，
   产物落 `art/incoming/processed/<id>.png`（尺寸、Alpha、调色板、平铺自检、量化）。
4. **入库**：`python tools/postprocess_art.py --install`，
   按 `art/README.md` 末尾的入库路径表复制到 `Assets/StreamingAssets/blocks/textures/` /
   `Assets/Art/Player/` / `Assets/Art/Items/` / `Assets/Art/Entities/`。

### 3.3 缺资源时的兜底

- 如果 AI 生成失败（脚本抛错或产物 < 1KB）：在 `art/incoming/processed/` 留一份
  纯色 PNG（用 `gen-gameplay-assets.py` 同款 zlib 压缩，4×4 棋盘格 + 调色板主色）。
  验收标记为「占位」，不是「已生成」。
- 方块缺图时方块会回到洋红 fallback——所以**任务 A 没全部完成前不允许进 PlayHarness 截图**。

---

## 4. 玩家身体与走路动画（任务 B）

### 4.1 视觉结构

```
玩家 GameObject (PlayerController)
└── BodyRoot (挂在脚下)
    ├── Torso (Primitive.Cube, scale (0.6, 0.7, 0.3)，y=0.85)
    ├── Head   (Primitive.Cube, scale (0.5, 0.5, 0.5)，y=1.65，绕 z 摆动做 head-bob)
    ├── ArmL  (Primitive.Cube, scale (0.2, 0.7, 0.2)，x=-0.4，y=0.9)
    ├── ArmR  (Primitive.Cube, scale (0.2, 0.7, 0.2)，x=+0.4，y=0.9)
    ├── LegL  (Primitive.Cube, scale (0.25, 0.85, 0.25)，x=-0.15，y=0.4)
    └── LegR  (Primitive.Cube, scale (0.25, 0.85, 0.25)，x=+0.15，y=0.4)
```

每个部件用 `MaterialPropertyBlock` 染色（与 MobView/VillagerView 同一套路）：

- 头：肤色（用 skin 调色板的 #C98F68）
- 躯干：上衣蓝 #3E7A9C
- 手臂：上衣蓝（顶部 75%）+ 肤色（底部 25%，露出手）
- 腿：裤子灰 #4A4A5E + 鞋 #5A4632（最下 20%）
- 全身加 PropertyBlock 染色，**不依赖 skin.png**——皮肤图留给后续做贴图优化阶段。

### 4.2 走路动画

新建 `Assets/Scripts/Unity/Player/PlayerVisual.cs`，挂在 `PlayerController` 上：

| 输入 | 行为 |
| --- | --- |
| `PlayerState.Velocity.XZ` 长度 > 0.1 | 进入走动相位 `_walkPhase += velocity * dt * 8f` |
| 腿 / 臂摆动 | `leg.rotation.x = sin(_walkPhase) * 30°`，左右腿反相；臂摆动与腿反相 |
| Head bob | `head.position.y = baseY + abs(sin(_walkPhase * 2)) * 0.08` |
| 静止 | 用 lerp 把腿/臂角度归零（每帧 5°/s 衰减）|
| 跳跃中 | 腿并拢（rotation.x → 0），身体整体 y += 0.1 |
| 蹲/潜行 | 暂不实现 |

### 4.3 第三人称相机

`CameraThirdPerson` 已实现 F5 切换、distance=3、height=1.5。本任务**不修改**。
但视觉上需要玩家站在相机前才看得到身体，所以默认出生时第三人称可见性测试要 PlayHarness 启动后立刻按 F5。

---

## 5. UI 修整（任务 C）

### 5.1 Hotbar

`HotbarUI.cs` 已存在并工作。**本任务不重写**，只：

1. 槽位背景改用 `Assets/Art/UI/hotbar-slot.png`（已有，但要走流程入 Assets 后引用）
2. 选中槽边框改用 `Assets/Art/UI/hotbar-slot-selected.png`
3. 数量文本字号放大到 16pt + 黑底白字（小方块读不清）
4. 默认热键栏第 0 格预填 64 个 dirt（让首次进入游戏就能看到图标，不是空）

### 5.2 SelectionBox

`SelectionBox.cs` 用 `Hidden/Internal-Colored` 单色材质。改为：

- 主线宽 2 像素、颜色 #FFFFFF（白）
- 6 条主边（前后左右上下轮廓）比 12 边更显眼
- 但本任务**不重写 SelectionBox 几何**，只换材质颜色为更亮 + 加 `DisableLighting`

### 5.3 HandController

`HandController.cs` 已经能渲染右下角手持物 + 挥动动画。**本任务**：

1. 把挥动动画从「单轴 sin」改为「先向下、再向前」的复合 swing（更像挥剑）
2. 挥动时长 0.25s（当前 0.3s，更紧凑）

---

## 6. 积木颜色诊断与修复（任务 D）

### 6.1 诊断步骤

1. PlayHarness 跑通后，新增 `CaptureScreenshot(sceneName, cameraName, outputPath)` 静态方法：
   - 把主相机 `targetTexture` 设到一张 `RenderTexture(1280, 720)`
   - `Camera.Render()` 强制渲染一次
   - `RenderTexture.active = rt; tex.ReadPixels(...)` 拷到 `Texture2D`
   - `File.WriteAllBytes(outputPath, tex.EncodeToPNG())`
2. 跑 PlayHarness → 落 `Builds/screenshots/overworld.png` + `hotbar.png` + `selected-block.png`
3. **逐图人工分析**：
   - grass-side 是否「上 6 像素绿色 + 下 26 像素 dirt」？
   - log-top 与 log-side 色温是否对得上？
   - 有没有「一面黑一面红」？ → 检查 BlockDefinition 文件里的 `lightEmission` 字段是否被错填为非 0
   - 草块在水下是否还能看到（`opaque=false` 应该剔除面）？

### 6.2 修复策略

按诊断结果分类：

| 现象 | 修法 |
| --- | --- |
| grass-side 配色错 | 重写提示词 + 重生成 |
| 某方块两面颜色不一致 | 检查 `lightEmission` JSON 字段是否为 0 |
| 法线方向错 | 改 `BlockDefinition` 派生（当前 Core 是直接走 `Opaque`，无 face-level 数据）|
| UV 缩放不对 | 改 `GreedyMesher` 的 UV 计算（CLAUDE.md 明确写 UV 按格数铺开）|

### 6.3 兜底

如果诊断结果指向「贴图生成本身」而非「渲染代码」，走任务 A 重新生成贴图。
如果指向代码（lightEmission / 法线 / UV），改代码 + 加 EditMode 测试覆盖。

---

## 7. AI 视觉回归测试（任务 E）

### 7.1 截图能力

新增 `Assets/Scripts/Unity/Editor/ScreenshotCapture.cs`：

```csharp
public static class ScreenshotCapture
{
    // 在编辑器非播放态下，把指定相机强制渲染到 RenderTexture 并落盘 PNG。
    public static void Capture(string cameraName, int width, int height, string outputPath);
}
```

调用入口：

1. `MyWorld/截图：当前场景`（菜单项，编辑器里点一下就截）
2. `MyWorld.Unity.EditorTools.ScreenshotCapture.Capture("相机", 1280, 720, "Builds/screenshots/<name>.png")`
   可被 `-executeMethod` 批处理

### 7.2 视觉测试套件

新增 `Assets/Tests/EditMode/Visual/VisualRegressionTests.cs`，**关键：所有断言走像素采样，不调 AI**：

| 测试 | 校验 | 阈值 |
| --- | --- |
| `MainViewport_NotAllBlack` | 中心 32×32 区域平均亮度 > 30/255 | 0 失败 |
| `MainViewport_NotAllMagenta` | 全屏 magenta (#FF00FF) 像素占比 < 5% | magenta 占比超 = 有未入库方块 |
| `MainViewport_HasGrassGreen` | 屏幕下方 1/3 区域草绿像素 (#4A7E2F~#74B84E) 占比 > 10% | 玩家站在草地上必须能看到 |
| `MainViewport_HasHotbarSlots` | 屏幕底部 hotbar 区域 9 个槽位宽度内亮度差 < 阈值 | 槽位均匀分布 |
| `MainViewport_HotbarNotEmpty` | hotbar 区域中心槽位中心 16×16 像素不全等于背景色 | 有物品图标 |
| `BuildTextures_NoPinkPixels` | 加载所有方块材质，主色像素（非背景）不含 magenta | 任何材质含 magenta = 测试失败 |

每个像素采样测试都有：

- `Assert.That(pixels, Is.Not.Null)` 防护
- 失败时 `TestContext.Out.WriteLine` 输出「哪些像素不达标」的统计
- 不引用 Newtonsoft / UnityEditor（让 dotnet 也能跑同套断言逻辑）

### 7.3 基线对比（可选，post-MVP）

第一版只做像素采样断言；后续可加「与基线 PNG 做像素 diff，超 N% 像素变化即失败」。
本里程碑**不做**基线对比，避免引入「截图对比」的 flaky 测试。

### 7.4 CI 集成

加 `tools/scripts/visual-smoke.sh`：

1. `dotnet test --filter "VisualRegression"`（像素采样断言）
2. `Unity.exe -batchmode -executeMethod ScreenshotCapture.CaptureAll`（落盘 PNG）
3. PNG 文件作为 artifact，不做断言（人工 review）

加进 `tools/scripts/build-and-run.sh` 作为可选 STEP `--with-visual`。

---

## 8. 任务依赖与执行顺序

```
A 贴图补完 ─┬─> C.4 玩家身体染色 ─> B 玩家身体 + 走路动画
           ├─> C.1 Hotbar 改用真贴图
           └─> E 视觉测试（基于贴图是否落库）

D 截图诊断 ─> 根据诊断结果触发 A 重生成 / 或代码修复

E 视觉测试 ─> 必须 A 完成后才有意义（贴图没入库会全 magenta 失败）
```

整体顺序：**A → D 诊断 → D 修复 → B → C → E**。

---

## 9. 不属于本里程碑

- 骨骼动画 / Animation Rigging / Blend Tree
- 自定义 shader（URP/Lit 已经够）
- 粒子系统（火花 / 烟雾 / 血）
- 声效 / BGM
- UI 整体改版（按钮、菜单、设置面板）
- 皮肤系统（玩家皮肤图先空着，留位置）
- 多人模式头像同步
- 模组 / 自定义资源加载

---

## 10. 验收标准

| 检查 | 命令 | 期望 |
| --- | --- | --- |
| 全部 dotnet 测试通过 | `dotnet test tools/dotnet/MyWorld.Tools.sln` | ≥ 315/315 通过（含新增视觉测试）|
| Unity EditMode 通过 | `tools/scripts/build-and-run.sh --skip-build --no-launch` | dotnet + Unity EditMode 都过 |
| Build OK | `tools/scripts/build-and-run.sh --skip-tests --clean` | MyWordGame.exe 落地 |
| 启动 .exe Player.log 无 exception | `tasklist | grep MyWordGame` + `cat Player.log` | 进程在跑 + 日志干净 |
| 截图落盘 | `MyWorld.Unity.EditorTools.ScreenshotCapture.CaptureAll` | Builds/screenshots/*.png ≥ 5 张 |
| 视觉测试无 magenta 失败 | `dotnet test --filter "Visual"` | 全部过 |
| 玩家模型可见 | `Builds/screenshots/third-person.png` | 中心区域有头/躯干像素 |
| Hotbar 不空 | `Builds/screenshots/hotbar.png` | 9 槽都有图标 |

