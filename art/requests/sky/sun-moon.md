# S-01 太阳与月相

一次需求包含**两张贴图**：`sun`（太阳）与 `moon-phases`（月相图集）。

## 用途

昼夜循环（P10）里天空上的两个天体。它们贴在跟随相机的天空穹顶上，
是玩家判断「现在几点了、天要黑了没有」的**唯一视觉线索**，
所以形状要极其明确，不能画成一团光晕。

## 为什么不要画光晕

天空是纯色渐变，太阳如果带发散的光晕，降采样后会变成一圈脏兮兮的过渡色，
而且不同天空色下光晕的边缘会露出方形贴图的边界。
**太阳就是一个纯色圆角方块**，Minecraft 也是这么做的，效果反而干净。

## 规格

| 项 | `sun` | `moon-phases` |
| --- | --- | --- |
| 最终尺寸 | 64 × 64 | 256 × 128（4 列 × 2 行，每格 64 × 64） |
| 生成尺寸 | 1024 × 1024 | 每格单独生成 1024 × 1024 后拼合 |
| 平铺 | 否 | 否 |
| Alpha | 有，仅 0 或 255 | 有，仅 0 或 255 |
| 颜色数 | 3 – 4 色 | 3 – 4 色 |

## 月相图集的排布（不可改）

8 格，从左上开始按行优先排列，对应一个完整的朔望周期：

| 序号 | 位置 | 相位 |
| --- | --- | --- |
| 0 | 第 1 行第 1 列 | 满月 |
| 1 | 第 1 行第 2 列 | 亏凸月 |
| 2 | 第 1 行第 3 列 | 下弦月（右半亮） |
| 3 | 第 1 行第 4 列 | 残月 |
| 4 | 第 2 行第 1 列 | 新月（几乎全暗，保留极细的一道边） |
| 5 | 第 2 行第 2 列 | 娥眉月 |
| 6 | 第 2 行第 3 列 | 上弦月（左半亮） |
| 7 | 第 2 行第 4 列 | 盈凸月 |

引擎按游戏内天数取模 8 选择格子，UV 偏移量为 `(index % 4) / 4, 1 - (index / 4 + 1) / 2`。

## 调色板（严格使用）

太阳：

| 用途 | HEX |
| --- | --- |
| 核心 | `#FFF3C4` |
| 主色 | `#FFD84A` |
| 边缘 | `#F2A81E` |

月亮：

| 用途 | HEX |
| --- | --- |
| 亮面 | `#F2F2EC` |
| 环形山 | `#C9C9C0` |
| 暗面 | `#3C3F4A`（新月与亏缺部分，不是透明） |

**月亮的暗面要画成暗色而不是透明**，这样月牙旁边仍有一个隐约的圆盘，
比一弯孤零零的月牙更像真的月亮。

## 视觉描述

### `sun`

- 一个**圆角正方形**，占满 64×64 中的中央 56×56，四周留 4 像素透明
- 四角各切掉约 6 像素做圆角，看起来介于方与圆之间
- 从中心到边缘三层同心色：核心 `#FFF3C4`（约占 40%）→ 主色 `#FFD84A` → 边缘 `#F2A81E`（2 像素）
- 每层之间是硬边，**不做渐变**
- 不要画光芒、不要画射线、不要画笑脸

### `moon-phases`

- 每格是一个圆盘，占满 64×64 中的中央 52×52，四周留透明
- 圆盘上散布 **4–6 个环形山**：`#C9C9C0` 的不规则小块，直径 4–8 像素
- 环形山**在 8 格中位置完全相同**（同一个月亮，只是被照亮的部分不同）
- 相位靠「亮面 / 暗面」的分界线体现，分界线是**竖直的圆弧**，不是直线也不是斜线
- 暗面区域的环形山也要保留，但整体压到 `#3C3F4A`

## AI 提示词

### `sun`

```
A pixel art sun sprite for a retro voxel game sky, 1024x1024, designed to be
downscaled to 64x64 pixel art.

Style: flat chunky pixel art, front view, no perspective, no lighting, no shading
gradient.

Content: a single rounded square sun, almost a square with clipped corners, filled
with three concentric flat color bands with hard edges: a large pale cream core, a
golden yellow middle band, and a thin darker amber outer rim. The bands have crisp
stair-stepped pixel edges and absolutely no blending between them.

Color palette strictly limited to: #FFF3C4, #FFD84A, #F2A81E.

Everything outside the sun is flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing. The sun is centered with an even magenta margin on all four sides.

No rays, no sunbeams, no corona, no halo, no glow, no bloom, no lens flare,
no sparkle, no face, no eyes, no clouds, no sky, no gradient, no text, no watermark,
no 3D render.
```

### `moon-phases` — 满月格（其余 7 格由它派生）

```
A pixel art full moon sprite for a retro voxel game sky, 1024x1024, designed to be
downscaled to 64x64 pixel art.

Style: flat chunky pixel art, front view, no perspective, no lighting gradient.

Content: a single round moon disc filled with a flat off-white color, with 4 to 6
irregular gray craters of varying size scattered across it. Craters are flat gray
blobs with hard edges and no inner shading. The disc edge is a clean stair-stepped
pixel circle with no anti-aliasing.

Color palette strictly limited to: #F2F2EC (surface), #C9C9C0 (craters).

Everything outside the disc is flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing. The moon is centered with an even magenta margin.

No glow, no halo, no bloom, no stars, no sky, no clouds, no face, no rabbit,
no gradient, no shading, no crescent shape, no text, no watermark, no 3D render.
```

## 后处理

### `sun`

1. 最近邻降采样到 64 × 64
2. 键控洋红为透明，去洋红边
3. 量化到 3 个色值
4. 手工整形：四角圆角对称，三层色带边界干净

### `moon-phases`

1. 满月格降采样到 64 × 64、键控、量化到 2 个色值 → 得到 `moon-full`
2. **派生其余 7 格**（脚本或手工）：复制 `moon-full`，
   把「暗面」区域内的所有不透明像素替换为 `#3C3F4A`
   - 分界线用圆弧计算：对第 k 相位，亮面边界是一条椭圆弧，
     k=0 全亮、k=4 全暗、k=2 右半亮、k=6 左半亮
3. 按 4 × 2 拼成 256 × 128 图集，**格与格之间不留间隔**
4. 检查 8 格的 Alpha 通道逐像素一致

## 验收标准

- [ ] `sun` 恰为 64 × 64，`moon-phases` 恰为 256 × 128
- [ ] 两者均为 32 位 PNG，Alpha 只有 0 和 255
- [ ] `sun` 的三层色带无任何过渡色，颜色数 ≤ 4
- [ ] `moon-phases` 的 8 格圆盘轮廓**逐像素完全一致**
- [ ] 8 格的环形山位置完全一致
- [ ] 按 0→7 顺序播放 8 格，相位变化连续，看不出跳变
- [ ] 把两者叠在天蓝 `#79A6FF` 和夜蓝 `#0B1026` 两种背景上，边缘都干净、无紫边、无白边
- [ ] 缩到屏幕上的实际大小（约 40 像素）后，太阳和月亮仍能一眼分辨
