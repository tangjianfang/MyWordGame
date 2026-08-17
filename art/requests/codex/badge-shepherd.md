# M11-C10 徽章 · 牧羊人（badge-shepherd）

## 用途

milestone-11 成就系统「牧羊人」徽章：剪羊毛/繁殖累计 10 只羊。
前景主题物 = **一颗羊头**（白色绒毛 + 浅肤色脸）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-shepherd.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 绒毛白 | `#F2F2F2` |
| 绒毛阴影 | `#C8C8C8` |
| 脸肤色 | `#D2A48A` |
| 眼黑 | `#1A1A1A` |
| 背景键控色 | `#FF00FF` |

前三色与 `entities/sheep.md` 图标完全同源——徽章与生物图鉴里的羊是同一只羊。

## 视觉描述

圆形底盘同模板。前景：一颗正面羊头占盘面中央——
外圈云朵状白色绒毛（下缘压 `#C8C8C8`），中央浅肤色长脸，
两只黑点眼、两只侧垂小耳。全部包黑描边。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no
anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: one sheep head seen from the front, centered on the disc and filling
about 40 percent of the image. A cloud-like ring of white wool #F2F2F2 with
slightly darker wool #C8C8C8 along its lower edge surrounds a long tan face
#D2A48A, with two small black dot eyes and two small floppy ears on the sides.
Every shape is wrapped in a near-black outline.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #F2F2F2, #C8C8C8,
#D2A48A, #1A1A1A.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No full sheep body, no legs, no grass, no shears, no text, no letters, no
numbers, no ribbon, no laurel wreath, no gradient, no glow, no bevel, no 3D
render, no watermark.
```

## 负面提示词

```
full body, legs, grass, shears, text, letters, numbers, ribbon, laurel wreath,
gradient, glow, bevel, 3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 7 个色值
4. 与其余 15 枚徽章并排核对：底盘逐像素一致
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 底盘与 `badge-first-night` 逐像素一致（仅前景不同）
- [ ] 与 `entities/sheep` 图标并排时观感同源（同套毛白/肤色）
- [ ] 徽章外全部透明（Alpha 0）
