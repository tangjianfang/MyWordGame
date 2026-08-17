# 美术资源需求与生成流程

本目录是项目所有图片资源的**需求提出处**。流程为：
在 `requests/` 下写需求 → 在 Claude Code 中调用多模态生成 → 产物放入 `incoming/`（不入版本管理）
→ 后处理 → 对照验收标准检查 → 按文末「入库路径」表移入 `Assets/`。

## 硬性约束（来自代码，不可协商）

### 1. 方块贴图必须四边无缝平铺

`GreedyMesher` 会把同材质的相邻面合并成一个大四边形，输出的 UV 是 `(0,0)..(宽,高)`，
纹理按格数重复铺开。因此**贴图左右边缘、上下边缘必须能无缝衔接**。

不满足会导致：一整片草地上出现规则的网格状接缝线，一眼就能看出破绽。

**自检方法**：把图片向右偏移一半宽度、向下偏移一半高度（PS 的「位移」滤镜 / GIMP 的 Offset），
接缝会跑到画面中央。如果中央能看出明显的线或色块断裂，就是不合格。

**注意「无缝」不等于「不许有图案」**：UV 是**每格重复一次**，所以玻璃的边框、砖块的砖缝、
木板的板条会在每个方块上各显示一遍——这是预期效果，不是要消除的接缝。
需要消除的只是「两块贴图拼接处对不上」。

### 2. 尺寸统一 32×32

方块贴图统一 **32×32 像素**。选 32 而非 16 的原因：AI 多模态直接产出精确的 16×16 像素画
成功率很低，32×32 在保持像素风的同时容错高得多，细节也更适合"高质量"的目标。

UI 元素尺寸见各自需求文件。

### 3. 无抗锯齿、无渐变羽化

最终 PNG 必须是硬边像素，颜色数受控。AI 生成结果通常带抗锯齿和噪点，**必须经过后处理**。

### 4. 需要透明的资源，一律用洋红键控，不要指望 AI 输出 Alpha

**这是实测结论**：第 0 批 15 张图全部产出为 `Format24bppRgb`——**没有 Alpha 通道**。
提示词里写 "transparent background" 得到的是白底或棋盘格底，不是透明。

所以凡是需要透明的资源（树叶、玻璃、准星、裂纹、心形、按钮、太阳月亮、云），
提示词一律要求：

> 把透明区域画成**纯洋红 `#FF00FF`**，完全饱和、硬边、不与主体做抗锯齿。

后处理再把洋红键控为 Alpha = 0。选洋红是因为它不在本项目任何一张贴图的调色板里，
不会误伤。

键控后**必须做一次「去洋红边」**：降采样会在洋红与主体之间产生偏紫的过渡色，
这些像素 Alpha 判定为不透明但颜色是脏紫，不清掉会在渲染时看到紫色描边。
判据：不透明像素中若同时满足 `R > G` 且 `B > G`，就是残留，替换为相邻主体色。

本项目所有 Alpha 都是 **0 或 255 两态**（Alpha Test / cutout），
**不允许半透明像素**（水的半透明由着色器的颜色 Alpha 控制，不靠贴图）。

## 生成流程

### 第一步：生成

```powershell
$env:MINIMAX_API_KEY = "你的key"          # 或写进仓库根目录的 .env（已 gitignore）

python tools/generate_art.py --list        # 看每个资源的提示词解析结果
python tools/generate_art.py --dry-run --only leaves
python tools/generate_art.py --only leaves glass cobblestone
python tools/generate_art.py --all --jobs 4
python tools/generate_art.py --only leaves --n 4   # 出 4 张备选挑一张
```

提示词的唯一来源就是各需求文件里「## AI 提示词」下的代码块，**改需求文件即刻生效**，
不要在脚本里另抄一份。产物落 `incoming/<资源名>.png`。

注意：MiniMax 文生图接口**没有独立的负面提示词参数**，所以各需求文件的
「## 负面提示词」段落不会被发送——真正起作用的是正面提示词末尾那串 `No ...`，
要禁止什么就写进正面提示词里。提示词上限 1500 字符，超了脚本会直接报错。

**关键**：不要让模型直接输出 32×32。脚本按目标图的宽高比自动放大到 1024 左右
（非正方形资源保持精确比例，例如 64×24 会生成 1408×528），再由后处理降采样。
直接要小尺寸会得到模糊的放大图。

### 第二步：后处理（必做）

```powershell
python tools/postprocess_art.py --only leaves glass
python tools/postprocess_art.py            # 处理所有素材齐备的资源
```

产物落 `incoming/processed/`。脚本依次做：

1. **降采样**到需求文件指定的尺寸：必须用**最近邻（Nearest Neighbor）**，
   不能用双线性/Lanczos，否则会糊
2. **洋红键控**（仅限需要透明的资源）：洋红占比 > 50% 的像素 Alpha 置 0，其余置 255
3. **去洋红边**：清掉键控残留的偏紫过渡像素
4. **限制颜色数**：按需求文件给出的调色板量化
5. **Alpha 归为 0 / 255 两态**
6. **平铺自检**：接缝比值超阀值直接判不合格，退出码非 0
7. **存为 32 位 PNG**

矿石不是整张生成：底层复用已验收的 `stone`，AI 只画洋红底上的矿脉层。
`heart-half/empty`、`button-hover/pressed`、`moon-phases` 8 相、`break-0..3`
均由主图派生，保证轮廓逐像素一致；`crosshair`、`hotbar-slot`、`panel`
几何确定，由脚本直接生成，不走 AI。

### 第三步：验收

对照需求文件的「验收标准」逐条检查。不通过就调整提示词重新生成，不要"凑合用"——
贴图是玩家每一帧都在看的东西。

### 第四步：入库

```powershell
python tools/postprocess_art.py --install   # 按文末「入库路径」表复制进 Assets/
```

入库后把需求索引里的状态改为「已入库」。

Unity 导入设置由 P9 的编辑器脚本自动套用（`Filter Mode = Point`、`Compression = None`），
不需要手动改。

## 全局调色板

所有方块共用一套色系，保证画面协调。各需求文件会从中取用。

| 名称 | 阴影 | 主色 | 高光 | 用在 |
| --- | --- | --- | --- | --- |
| 草绿 | `#4A7E2F` | `#5D9C3C` | `#74B84E` | grass-top / grass-side / 装饰方块 |
| 泥土棕 | `#5F4630` | `#7A5A3C` | `#91704B` | dirt / grass-side 下部 |
| 石灰 | `#6E6E6E` | `#8A8A8A` | `#A3A3A3` | stone / 矿石底 |
| 圆石灰 | `#4A4A4A` | `#7E7E7E` | `#A3A3A3` | cobblestone |
| 沙黄 | `#C0AC7C` | `#D9C89A` | `#EADDB4` | sand |
| 沙砾暖灰 | `#6A6258` | `#857C70` | `#9E958A` | gravel |
| 水蓝 | `#2C5893` | `#3A6FB5` | `#4E88CE` | water |
| 基岩深灰 | `#333333` | `#4A4A4A` | `#5E5E5E` | bedrock |
| 树皮棕 | `#4E3B27` | `#634C33` | `#7A6042` | log-side |
| 木心黄棕 | `#8A6A45` | `#A5814F` | `#BE9A63` | log-top |
| 木板黄 | `#8A6741` | `#9C7549` | `#B98D57` | planks |
| 叶绿（深冷） | `#2F5D24` | `#3F7A2E` | `#52963B` | leaves |
| 玻璃青白 | `#A9CBD6` | `#C8E2EA` | `#E7F4F8` | glass |
| 砖红 | `#6B3226` | `#8B4433` | `#A05242` | bricks |
| 岩浆橙 | `#8A2400` | `#D64B0A` | `#F79B22` | lava |
| 煤黑 | `#0F0F0F` | `#1F1F1F` | `#3A3A3A` | coal-ore |
| 铁褐 | `#8A6A4C` | `#B9906B` | `#D8B896` | iron-ore |
| 金黄 | `#A87322` | `#DCAE3A` | `#F7DA7A` | gold-ore |
| 钻石青 | `#1E7C7C` | `#4CC6C4` | `#A8F2EF` | diamond-ore |
| UI 暖灰 | `#443D34` | `#6B6155` | `#8B7F6F` | button / panel |

**保留色**：`#FF00FF` 洋红只用于键控，**任何成品贴图里都不允许出现**。

## 跨资源一致性要求

以下关系必须成立，否则方块拼接处会露馅：

- `grass-side` 的**顶部 6 像素**必须使用草绿色系，且与 `grass-top` 边缘色调一致
- `grass-side` 的**下部 26 像素**必须与 `dirt` 的纹理风格和色值完全一致
- 四张矿石的**非斑块区域必须与 `stone.png` 逐像素完全一致**（做法见 B-14）
- `leaves` 必须比 `grass-top` 更深更冷，否则树冠压在草地上分不出层次
- `log-top` 的木心色必须比 `planks` 的主色暗，「原木做成木板」才说得通
- `log-top` 的外圈树皮色必须与 `log-side` 同系
- `title-decor-block`（U-07）的三个面必须用 `grass-top` / `grass-side` 的同一套色
- `button`（U-05）必须比 `panel`（U-06）亮，按钮才能从面板上浮出来
- 所有方块的噪点颗粒大小保持一致（1–2 像素级），不要有的细腻有的粗糙

## 需求索引

「状态」取值：`待生成` → `已生成`（产物在 `incoming/`）→ `已入库`（后处理验收通过并移入 `Assets/`）。

### 第 1 批 · 最小可玩闭环

跑通「走动 → 挖方块 → 放方块 → 退出重进世界还在」所需的全部图片。

| 编号 | 资源 | 文件数 | 尺寸 | 平铺 | Alpha | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| B-01 | [石头](requests/blocks/stone.md) | 1 | 32×32 | 四边无缝 | 无 | 已生成 |
| B-02 | [泥土](requests/blocks/dirt.md) | 1 | 32×32 | 四边无缝 | 无 | 已生成 |
| B-03 | [草方块顶面](requests/blocks/grass-top.md) | 1 | 32×32 | 四边无缝 | 无 | 已生成 |
| B-04 | [草方块侧面](requests/blocks/grass-side.md) | 1 | 32×32 | 左右无缝 | 无 | 已生成 |
| B-05 | [沙子](requests/blocks/sand.md) | 1 | 32×32 | 四边无缝 | 无 | 已生成 |
| B-06 | [水](requests/blocks/water.md) | 1 | 32×32 | 四边无缝 | 无 | 已生成 |
| B-07 | [基岩](requests/blocks/bedrock.md) | 1 | 32×32 | 四边无缝 | 无 | 已生成 |
| B-08 | [雪](requests/blocks/snow.md) | 1 | 32×32 | 四边无缝 | 无 | 已入库（程序占位，待正式美术替换） |
| U-01 | [准星](requests/ui/crosshair.md) | 1 | 32×32 | 否 | **有** | 已生成 |
| U-02 | [热键栏格子](requests/ui/hotbar-slot.md) | 2 | 48×48 | 否 | **有** | 已生成 |
| U-03 | [挖掘裂纹 5 阶](requests/ui/break-stages.md) | 5 | 32×32 | 否 | **有** | 已生成 |

> ⚠️ 第 1 批已生成的 15 张图**全部是 24 位无 Alpha**，
> 其中 U-01 / U-02 / U-03 共 8 张需要透明，必须按「硬性约束 4」用洋红键控**重新生成**。

### 第 2 批 · 树木、矿石、建材、昼夜

渲染层与玩法层展开后立刻会用到，提示词已全部写好，可以现在就生成。

| 编号 | 资源 | 文件数 | 尺寸 | 平铺 | Alpha | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| B-08 | [圆石](requests/blocks/cobblestone.md) | 1 | 32×32 | 四边无缝 | 无 | 待生成 |
| B-09 | [沙砾](requests/blocks/gravel.md) | 1 | 32×32 | 四边无缝 | 无 | 待生成 |
| B-10 | [原木顶面+侧面](requests/blocks/log.md) | 2 | 32×32 | 见文件 | 无 | 待生成 |
| B-11 | [木板](requests/blocks/planks.md) | 1 | 32×32 | 四边无缝 | 无 | 待生成 |
| B-12 | [树叶](requests/blocks/leaves.md) | 1 | 32×32 | 四边无缝 | **有** | 待生成 |
| B-13 | [玻璃](requests/blocks/glass.md) | 1 | 32×32 | 否 | **有** | 待生成 |
| B-14 | [矿石四件套](requests/blocks/ores.md) | 4 | 32×32 | 继承石头 | 无 | 待生成 |
| B-15 | [砖块](requests/blocks/bricks.md) | 1 | 32×32 | 四边无缝 | 无 | 待生成 |
| B-16 | [岩浆](requests/blocks/lava.md) | 1 | 32×32 | 四边无缝 | 无 | 待生成 |
| U-04 | [生命值心形](requests/ui/heart.md) | 3 | 24×24 | 否 | **有** | 待生成 |
| U-05 | [按钮三态](requests/ui/button.md) | 3 | 64×24 | 九宫格 | **有** | 待生成 |
| U-06 | [界面面板底](requests/ui/panel.md) | 1 | 64×64 | 九宫格 | **有** | 待生成 |
| U-07 | [标题装饰方块](requests/ui/title-logo.md) | 1 | 96×96 | 否 | **有** | 待生成 |
| S-01 | [太阳与月相](requests/sky/sun-moon.md) | 2 | 64×64 / 256×128 | 否 | **有** | 待生成 |
| S-02 | [云层遮罩](requests/sky/clouds.md) | 1 | 128×128 | 四边无缝 | **有** | 待生成 |
| P-01 | [玩家皮肤](requests/player/skin.md) | 1 | 64×64 | 否 | **有** | 待生成 |

第 2 批合计 **24 个 PNG 文件**。

### m6 批 · 14 张缺失物品贴图（程序占位）

任务 A4 未覆盖、milestone-6 才发现的 14 个物品 JSON 贴图引用缺失（实机品红块）。
**当前全部是程序占位**（`art/scripts/gen_m6_placeholders.py` 确定性生成），
dirt/log 复用方块贴图降采样，其余 12 张按物品语义程序绘制。

| 编号 | 资源 | 文件数 | 尺寸 | 平铺 | Alpha | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| M6-I | [14 张物品贴图批量](requests/items/m6-placeholder-batch.md) | 14 | 16×16 | 否 | 12 张**有** | 已入库（程序占位，待正式美术替换） |

### m10 批 · 矿物贴图（程序占位）

milestone-10 矿物进阶的 6 张新贴图全部先程序占位：

- 2 张矿石方块：夏季合金/机元（金/粗铁直接复用 B-14 已入库的
  gold-ore / iron-ore）。`art/scripts/gen_m10_ore_placeholders.py`：
  stone.png 逐像素打底 + 确定性哈希布斑块，面积/外圈/间隔约束对齐 B-14 验收标准
- 4 张材料物品图标：粗金/粗铁/夏季合金/机元（挖矿掉落物，进背包要立刻有图）。
  `art/scripts/gen_m10_item_placeholders.py`：ASCII 形状 + 确定性哈希铺色（m6 批同模式）

| 编号 | 资源 | 文件数 | 尺寸 | 平铺 | Alpha | 状态 |
| --- | --- | --- | --- | --- | --- | --- |
| M10-A | [夏季合金矿石](requests/blocks/summer-alloy-ore.md) | 1 | 32×32 | 继承石头 | 无 | 已入库（程序占位，待正式美术替换） |
| M10-B | [机元矿石](requests/blocks/machine-essence-ore.md) | 1 | 32×32 | 继承石头 | 无 | 已入库（程序占位，待正式美术替换） |
| M10-C | [4 张材料物品图标](requests/items/m10-material-placeholders.md) | 4 | 16×16 | 否 | **有** | 已入库（程序占位，待正式美术替换） |
| M10-D | [6 张装备物品图标（金/合金/机元剑+镐）](requests/items/m10-gear-placeholders.md) | 6 | 16×16 | 否 | **有** | 已入库（程序占位，待正式美术替换） |
| M10-E | [9 张升级链物品图标（金锭 + 四系剑/镐+1）](requests/items/m10-c2-upgrade-placeholders.md) | 9 | 16×16 | 否 | **有** | 已入库（程序占位，待正式美术替换） |

### av 批 · 音频与视频（MiniMax 生成）

音频 33 条走 `tools/generate_media.py --audio`（music-2.6，SFX 带 ffmpeg 合成兜底），
视频 2 条走 `--videos`（Hailuo-2.3 768P，**每日 3 次配额**，队列
`art/incoming/video/quota.json`，gitignored）。需求在 `art/requests/audio|video/`，
入库 `Assets/Resources/Audio/` 与 `Assets/StreamingAssets/video/`。

| 编号 | 资源 | 条数 | 说明 | 状态 |
| --- | --- | --- | --- | --- |
| AV-BGM | [BGM 三首](requests/audio/bgm/bgm.md) | 3 | 氛围钢琴 无缝循环 | 待生成 |
| AV-AMB | [环境循环](requests/audio/ambient/ambient.md) | 3 | 鸟/虫/洞穴 | 待生成 |
| AV-SFX | [事件音](requests/audio/events/events.md) | 11 | mono 短音（含合成兜底模板） | 待生成 |
| AV-MOB | [生物叫声](requests/audio/mobs/mobs.md) | 16 | 6 专属 + 2 通用 × idle/hurt | 待生成 |
| AV-VID | [视频两条](requests/video/menu-bg.md) | 2 | 主菜单背景 + 笔记本屏 | 待生成 |
| AV-LS | [笔记本屏幕贴图](requests/items/laptop-screen.md) | 1 | 32×32 程序占位，运行时换 RenderTexture | 程序占位（待正式美术替换） |

第 4 批合计 **36 个文件**（音频 33 ogg + 视频 2 mp4 + 屏幕贴图 1 png）。
注：plan 文字提到 mob 14，但实际需要 6 专属 × 2 + 2 通用 × 2 = 16 条（Task 10
测试要求 `generic-small/large-{idle,hurt}` 4 个文件名全在），以实现为准修计划。

### 第 3 批 · 待玩法定案后再写提示词

这些资源的样子取决于还没设计的玩法，**现在写提示词是浪费**——
比如工具图标的形状由合成系统决定，怪物的比例由战斗手感决定。
先记在这里，免得漏掉：

| 类别 | 内容 | 依赖的玩法决策 |
| --- | --- | --- |
| 工具与物品图标 | 镐、斧、锹、剑、木棍、原材料 | 合成系统与工具分级 |
| 合成界面 | 合成格背景、箭头、结果格 | 合成台的格子数 |
| 生物群系方块 | 冰、仙人掌、枯木、红沙（雪已提需求，见 B-08） | 生物群系划分 |
| 功能方块 | 工作台、熔炉（含燃烧态）、箱子 | 是否做方块实体 |
| 生物 | 猪、羊、僵尸、苦力怕 | 是否做 AI 与战斗 |
| 岩浆流动动画 | 32×32 × N 帧竖排图集 | 是否做动画贴图系统 |
| 粒子 | 泡泡、烟、火星 | 是否做粒子系统 |

## 不需要图片的资源（明确排除，别浪费时间生成）

| 想要的效果 | 实现方式 |
| --- | --- |
| 热键栏里的方块图标 | **运行时用小相机等距渲染真实方块**，不画 2D 图标。这样加一种方块就自动有图标，零美术成本 |
| 天空颜色、昼夜渐变 | 着色器按时间插值的颜色参数，写进 `docs/specs/` 的参数表 |
| 距离雾 | 着色器参数（颜色 + 起止距离） |
| 星星 | 天空穹顶上按固定种子随机撒的点，程序生成 |
| 挖掘碎屑粒子 | 从被挖方块的贴图上随机采样 4×4 区域，复用已有贴图 |
| 方块高亮线框 | 引擎画 12 条线段 |
| 水面波动 | 顶点着色器的正弦位移，不靠帧动画 |
| 界面文字 | 像素字体 + TextMeshPro，**绝不让 AI 画字** |

## 入库路径

| 类别 | 目标目录 |
| --- | --- |
| 方块贴图 | `Assets/StreamingAssets/blocks/textures/` |
| UI | `Assets/Art/UI/` |
| 天空 | `Assets/Art/Sky/` |
| 玩家 | `Assets/Art/Player/` |
| 音频 | `Assets/Resources/Audio/` |
| 视频 | `Assets/StreamingAssets/video/` |
