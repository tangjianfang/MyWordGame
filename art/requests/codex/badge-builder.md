# M11-C17 徽章 · 建筑师（badge-builder）

## 用途

milestone-11 成就系统「建筑师」徽章：累计放置 1000 个方块。
前景主题物 = **一面砖墙 + 一柄灰泥抹刀**（一砌一抹）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-builder.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 砖红主色 | `#8B4433` |
| 砖红亮色 | `#A05242` |
| 抹刀钢灰 | `#8A8A8A` |
| 背景键控色 | `#FF00FF` |

砖红与全局调色板砖块同源。

## 视觉描述

圆形底盘同模板。前景：下部三层错缝小砖墙（`#8B4433`，
个别砖提亮 `#A05242`，砖缝 1 像素黑），一把钢灰抹刀斜立在砖墙右上。
全部包黑描边。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no
anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: filling about 40 percent of the image, a small brick wall of three
staggered rows of bricks in red-brown #8B4433 with a few bricks lighter #A05242
and one-pixel dark mortar lines, standing in the lower part of the disc; one
steel-gray trowel #8A8A8A stuck upright at its upper right. Every shape is
wrapped in a near-black outline.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #8B4433, #A05242,
#8A8A8A.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No house, no tower, no crane, no hammer, no blueprint, no text, no letters, no
numbers, no ribbon, no laurel wreath, no gradient, no glow, no bevel, no 3D
render, no watermark.
```

## 负面提示词

```
house, tower, crane, hammer, blueprint, text, letters, numbers, ribbon, laurel
wreath, gradient, glow, bevel, 3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 6 个色值
4. 与其余 15 枚徽章并排核对：底盘逐像素一致
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 底盘与 `badge-first-night` 逐像素一致（仅前景不同）
- [ ] 砖墙错缝可辨、抹刀与砖区分清楚
- [ ] 徽章外全部透明（Alpha 0）
