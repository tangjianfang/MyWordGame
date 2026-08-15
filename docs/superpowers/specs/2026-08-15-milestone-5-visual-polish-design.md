# milestone-5 设计：视觉与体验打磨

> 日期：2026-08-15
> 状态：已通过设计评审（父子共同开发项目）
> 前置：milestone-4（完整存档系统）已完成，dotnet 436 / EditMode 539 全绿
> 诊断报告：`.superpowers/m5-diagnosis.md`（7 个问题全部有 file:line 证据）

## 背景与问题

实机试玩（build-and-run 出 exe）反馈 7 个体验问题，诊断已全部定位根源：

| # | 反馈 | 根源（诊断报告详见） |
| --- | --- | --- |
| 1 | 人物/生物全是粉红立体 | `CreatePrimitive` 裸 Default-Material（Standard shader），URP 下渲染洋红（`PlayerVisual.cs:62/74/131`、`MobView.cs:118/127`、`MobManager.cs:232`） |
| 2 | 瞄准选中方块是刺眼白色实心块 | `SelectionBox.BuildEdgeMesh` 实际返回整 Cube mesh + 纯白不透明 Unlit，套在方块上盖住贴图 |
| 3 | 进角色莫名少 4 血 | `MobManager.cs:183` 昼夜阈值反向（正午判夜晚→刷僵尸 2×2=4 血）；附带发现双血条 bug（摔落/饥饿走玩家不可见的私有血条） |
| 4 | 画面整体偏暗，黑夜尤暗 | Gamma 色彩空间 + URP 不兼容（系统性偏暗）；`DayNightCycle.ambientLight` 在 Skybox ambient 模式下无效；夜晚参数过低 |
| 5 | 启动不全屏两侧黑边 | `fullscreenMode:1` 固定 1920×1080 宽高比，非 16:9 显示器 pillarbox |
| 6 | 物品图标边界锯齿 | 16×16 图标非整数 3.5 倍放大 + `LoadImage` 未设 Point 过滤（`HotbarUI.cs:73/102`、`HandController.cs:90`） |
| 7 | 移动时规律卡顿 | 每帧 2 根整列同步生成+24 section 建网格尖峰；每帧 GC 分配（List/闭包 Sort/GUIStyle）；30s 同步写盘存档 |

## 目标

进游戏第一眼就是「正常的游戏」：彩色人物、融合选中框、满血开局、鲜亮画面、真全屏、干净图标、丝滑移动。

## 非目标（YAGNI）

- 贴图皮肤 / UV 贴图 / 角色动画（后续里程碑）
- Job System / Burst 多线程化（本次主线程内优化；若不足再立项）
- 世界内方块贴图 mipmap（糊掉像素感；远距闪烁接受，属像素美学）
- ExclusiveFullScreen（用 FullScreenWindow 适配即可）

## 1. URP 材质工厂（#1 + #2）

新建 `Assets/Scripts/Unity/Rendering/UrpMaterialFactory.cs`：

- 复用 `BlockMaterialLibrary` 已验证的 **Resources 哑材质模式**（`Resources.Load` 引用 URP shader 防 build 剔除；不要 `Shader.Find`）
- `static Material CreateLit(Color baseColor)` —— URP/Lit 染色
- `static Material CreateOverlay(Color rgba)` —— URP/Unlit + Transparent 表面类型（alpha < 1）

**玩家身体**（`PlayerVisual` 10 cube，沿用 m3 双色段设计）：

| 部位 | 色值 |
| --- | --- |
| 头/手（皮肤段） | `#E8B88A` |
| 躯干/上臂/大腿（上衣段） | `#3A6EA5` |
| 前臂/小腿（裤段） | `#2C3E66` |
| 鞋 | `#5C4033` |

材质工厂返回的材质按部位缓存（每种色一个实例，10 cube 共享 4 个材质实例，不泄漏）。

**生物**（`MobView` Body+Head、`MobManager` host cube）按 `MobKind` 染色：

| 生物 | Body | Head |
| --- | --- | --- |
| 猪 | `#E8A0A8` | `#E8B8C0` |
| 牛 | `#6B4A35` | `#F0E8E0`（头白花） |
| 鸡 | `#F0EDE5` | `#D94F3D`（红冠喙） |
| 僵尸 | `#5A8A4A` | `#6FA05C` |
| 村民 | `#7A5C3E` | `#E8B88A` |

**选中框**（`SelectionBox` + `BlockInteraction`）：材质换 `CreateOverlay(黑, alpha 0.35)`——Minecraft 式融合选中色：被选中方块整体变暗一层，贴图仍可见，不再有刺眼白壳。

## 2. 昼夜判定修复 + 单血条 + 出生点（#3）

三件事一起做，结束「进游戏莫名掉血」：

1. **昼夜判定**：`MobManager.cs:183` 的 `isNight = dayNightPhase < 0.5f` 改为 `_time.IsNight`（`TimeOfDay` 既有的正确判定，tick 13000–23000）。补测试：`DayPhase01=0.25`（正午）不取 NightCandidates、`0.7`（夜）取。
2. **单血条**：删除 `PlayerController` 私有 `int Health` 及其掉血路径，摔落/饥饿伤害统一写 `PlayerContext.Health`（血条 UI、死亡、存档的唯一真源）。
3. **出生点落地**：出生高度从固定 y=120 改为 `SurfaceHeightAt(spawnX, spawnZ) + 2`——不再有 22 格摔落，进游戏满血。

## 3. Linear 色彩空间 + 光照校准（#4）

1. `ProjectSettings` 切 **Linear** 色彩空间（URP 官方支持路径；Gamma+URP 是系统性偏暗的根源）。通过 `PreviewSceneBuilder` / 编辑器设置改，不手改 YAML 之外的部分（ProjectSettings 色彩空间字段本身按 Unity 流程改）。
2. 场景 `ambientMode` 改 **Flat**（`PreviewSceneBuilder` 构建场景时设置），使 `DayNightCycle.ambientLight` 真正生效。
3. 参数校准（初值，实机再调）：
   - 白天：sun intensity 1.0→1.3，ambient 0.7 灰
   - 夜晚：sun 0.2→0.35，ambient (0.15,0.18,0.3)→(0.22,0.25,0.40)
4. **视觉基线重拍**：Linear 后 visual-smoke 全部截图（7 张常规 + 5 张群系）重拍对比；贴图/UI 色跑偏在校准范围内修正（调材质参数，不重画贴图）。

## 4. 自适应全屏（#5）

`WorldBootstrap.Awake` 启动时（或专用小组件）：

```csharp
var display = Display.main;
Screen.SetResolution(display.systemWidth, display.systemHeight, FullScreenMode.FullScreenWindow);
```

按物理屏分辨率适配，非 16:9 显示器不再 pillarbox。`FullscreenToggle`（F11）行为保留。

## 5. 图标整数倍缩放 + Point 过滤（#6）

- `HotbarUI`：图标绘制目标 56×56 → **48×48**（16 的整数 3 倍），槽位尺寸相应微调保持布局；`LoadImage` 后 `tex.filterMode = FilterMode.Point`
- `HandController`（手持物品图）同修
- 其它用 `LoadImage` 画物品的 UI（合成/熔炉/交易）排查同修
- 方块贴图（32×32、Point、无 mip）保持现状

## 6. 卡顿三连修（#7）

### 6a. GC 消除

- `ChunkStreamer.EnqueueMissing` / `UnloadDistant`：每帧 new 的 List + 闭包 Sort 改为**成员级复用容器**（`List.Clear()` + 复用），Sort 比较器不闭包
- IMGUI UI（`HotbarUI` 等每帧 `new GUIStyle` 的）改为构造时缓存字段
- 验收：Profiler 一段移动路径无新 GC Alloc（EditMode 侧用容器行为等价测试守）

### 6b. 毫秒预算分帧

- `ChunksPerFrame=2`（固定根数）→ **时间预算制**：每帧区块工作（生成+建网格）总耗时 ≤ **8ms**，用 `System.Diagnostics.Stopwatch` 计时，预算耗尽即让剩余工作等下一帧
- `ChunkViewRegistry.BuildColumn` 的 24 section 同步重建改为**按 section 分帧入队**（每帧做一部分，Mesh 完成前该 section 视图不挂 GameObject）
- LoadRadius 范围不变（视觉边界不缩水）

### 6c. 异步存档

- `SaveLoadService.SaveNow` 拆两段：主线程**只收集快照**（LevelData 构造，快照是纯数据无 Unity 对象引用），`Task.Run` 后台做 JSON 序列化 + Deflate + 文件 IO
- `OnApplicationQuit` 的保存保持**同步**（进程退出前必须落盘）
- 后台写盘期间跳过新一轮保存（防重叠）；写失败 log + 脏区块保留（沿用 m4 语义）

## 7. 测试与验收

**自动化**：
- dotnet/EditMode：昼夜判定正反例、出生点高度、单血条掉血路径、UrpMaterialFactory 产出材质非 null/染色正确、SelectionBox 材质 alpha、图标绘制尺寸（EditMode 可测的 GUI 参数）、ChunkStreamer 容器复用行为等价、SaveNow 快照收集与后台写盘分离（可注入假 IO）
- dotnet 全量 ≥ 436、EditMode 全量 ≥ 539 不回归

**实机验收剧本**（build-and-run 后人工过一遍）：
1. 启动即全屏无黑边
2. 人物/生物彩色（无粉红），选中框半透明不刺眼
3. 进游戏满血 20，无莫名掉血
4. 画面鲜亮，黑夜能看清但氛围在
5. 图标无锯齿（整数倍像素干净）
6. 移动连续丝滑，无规律性顿挫；等 35s 存档点无卡顿
7. 退出重进状态无损（m4 回归）

**视觉基线**：visual-smoke 全绿 + 截图重拍归档（Linear 色彩变化的记录）。

## 与既有约束的关系

- Core 层零 UnityEngine 不变（本里程碑几乎全在 Unity 层 + ProjectSettings；昼夜判定在 Unity 侧 MobManager）
- 所有注释/断言中文
- ProjectSettings 按 Unity 流程改（能走代码/编辑器脚本的都走，如 PreviewSceneBuilder 设 ambientMode；色彩空间切换单独一步并全量回归）
