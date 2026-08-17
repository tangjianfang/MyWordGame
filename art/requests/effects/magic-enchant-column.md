# FX-12 魔法阵·附魔光柱

## 用途

魔法阵特效：附魔台作业时从台面升起的垂直光柱，金色符文光屑绕柱上浮。
紫金双色（附魔视觉记忆色）。**背景必须整片纯洋红键控为透明**。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有，仅 0 或 255 |
| 生成背景 | **整片纯洋红 `#FF00FF`**，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 柱紫 | `#9C6AE8` |
| 柱心亮 | `#F4EEFC` |
| 光屑金 | `#DCAE3A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

一根竖直光柱：窄长圆头紫色光带纵贯画布中央，中心一条近白亮缝，
两侧各 2 颗小金色菱形光屑自下而上错位分布，底部一层更宽的紫晕底盘。

## AI 提示词

```
A single magic effect sprite for a retro voxel game, 1024x1024 pixel
art designed to be downscaled to 32x32. A vertical enchanting light
column: a tall narrow rounded beam of medium purple glowing light in
the center, a bright near-white thin core line running through it, four
small golden diamond sparkles floating up along the sides, and a wider
purple base glow at the bottom. Centered vertically, the column spans
the full height. The entire background is solid flat pure magenta
#FF00FF, fully saturated, hard edges, no anti-aliasing between column
and magenta.

Column palette strictly: #9C6AE8, #F4EEFC, #DCAE3A only.

No text, no runes, no book, no anvil, no shadow, no outline, no blur,
no semi-transparent pixels. Hard pixel edges.
```

## 负面提示词

```
text, letters, runes, watermark, shadow, outline, blur, gradient, 3D
render, book, anvil, table, hands, gray background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控（洋红 > 50% → Alpha = 0）
3. 去洋红边
4. 量化到上表 3 个色值

## 验收

- [ ] 32×32 PNG，32 位，Alpha 只有 0/255
- [ ] 不透明像素颜色数 ≤ 3
- [ ] 无偏紫残留
- [ ] 光柱左右对称，垂直拉伸 2 倍后无断裂感（贴图按柱高采样）
