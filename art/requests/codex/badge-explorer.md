# M11-C16 徽章 · 探索者（badge-explorer）

## 用途

milestone-11 成就系统「探索者」徽章：踏足全部生物群系。
前景主题物 = **一枚指南针**（远行的记号）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-explorer.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 表盘羊皮色 | `#D9C89A` |
| 指针红（北） | `#D42B2B` |
| 指针白（南） | `#F2F2F2` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

圆形底盘同模板。前景：一枚圆形指南针居盘面中央——
羊皮色表盘、外圈细金线（与底盘金边同色）、中心一根双色指针
（上半红指北、下半白指南），指针略微偏转（有「在赶路」的动感）。
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

Foreground: one round compass centered on the disc, filling about 40 percent of
the image. It has a parchment face #D9C89A, a thin gold ring matching the medal
rim, and one two-tone needle pivoting slightly off straight up: its upper half
red #D42B2B and its lower half white #F2F2F2. Every shape is wrapped in a
near-black outline.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #D9C89A, #D42B2B,
#F2F2F2.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No map, no roads, no mountains, no footprints, no letters for directions, no
text, no numbers, no ribbon, no laurel wreath, no gradient, no glow, no bevel,
no 3D render, no watermark.
```

## 负面提示词

```
map, roads, mountains, footprints, direction letters, text, numbers, ribbon,
laurel wreath, gradient, glow, bevel, 3D, anti-aliasing, opaque background
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
- [ ] 指针红白两半分明、略微偏转
- [ ] 徽章外全部透明（Alpha 0）
