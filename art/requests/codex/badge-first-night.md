# M11-C04 徽章 · 活过第一夜（badge-first-night）

## 用途

milestone-11 成就系统「活过第一夜」徽章：第一次在夜晚的怪物围攻中撑到天亮。
前景主题物 = **月亮 + 篝火**（夜里靠火光活下来的画面）。

**16 枚成就徽章共用同一模板**：圆形底盘占画面 60%（金边 `#8B7355`）、
前景主题物占 40%，只换前景——并排时是一整套，不是 16 张无关图。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-first-night.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 月亮（冷白） | `#DCE8F0` |
| 篝火火焰亮 | `#F79B22` |
| 篝火火焰暗 | `#D64B0A` |
| 篝火木柴 | `#634C33` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

圆形底盘居中，直径占 60%：外圈 1 像素黑描边 → 2 像素金边 → 深暖灰盘面。

前景（占 40%，叠在盘面中央）：
- 上弦月挂在盘面上部，冷白色，带黑描边
- 月亮下方一小堆篝火：橙色火焰（上亮下暗）+ 底部两根交叉棕色木柴

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no
anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: a crescent moon in cold pale blue-white #DCE8F0 sitting in the upper
part of the disc, and below it a small campfire with a two-tone orange flame
#F79B22 over #D64B0A and two crossed brown logs #634C33. The foreground fills
about 40 percent of the image and every shape has a near-black outline.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #DCE8F0, #F79B22,
#D64B0A, #634C33.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No text, no letters, no numbers, no stars, no ribbon, no laurel wreath, no
gradient, no glow, no bevel, no 3D render, no watermark.
```

## 负面提示词

```
text, letters, numbers, stars, ribbon, laurel wreath, gradient, glow, bevel, 3D,
anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 7 个色值
4. 与其余 15 枚徽章并排核对：底盘直径 / 金边宽度逐像素一致
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 底盘圆形居中、直径约 19 像素（60%）
- [ ] 金边 `#8B7355` 连续、厚 2 像素
- [ ] 月亮与篝火两个主体可分辨
- [ ] 徽章外全部透明（Alpha 0）
