# M11-C15 徽章 · 英雄（badge-hero）

## 用途

milestone-11 成就系统「英雄」徽章：完成第一章任务链（击退怪物围攻救下村庄）。
前景主题物 = **一颗金色五角星**（最高荣誉的通用符号）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-hero.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 五角星金主色 | `#F7DA7A` |
| 五角星金阴影 | `#DCAE3A` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

圆形底盘同模板。前景：一颗五角星立于盘面中央，
上半两角与下面三角用 `#F7DA7A`、右下半压 `#DCAE3A` 阴影，
一颗星占约 40%，包黑描边。不加光芒线（保持扁平）。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, symmetrical, no perspective,
no anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: one five-pointed star centered on the disc, filling about 40 percent
of the image, pointing straight up. The star is golden #F7DA7A with its lower
right half shaded #DCAE3A, wrapped in a near-black outline.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #F7DA7A, #DCAE3A.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No light rays, no sunburst, no shield, no sword, no crown, no text, no letters,
no numbers, no ribbon, no laurel wreath, no gradient, no glow, no bevel, no 3D
render, no watermark.
```

## 负面提示词

```
light rays, sunburst, shield, sword, crown, text, letters, numbers, ribbon,
laurel wreath, gradient, glow, bevel, 3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 5 个色值
4. 与其余 15 枚徽章并排核对：底盘逐像素一致
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 底盘与 `badge-first-night` 逐像素一致（仅前景不同）
- [ ] 五角星左右对称、角尖向上
- [ ] 徽章外全部透明（Alpha 0）
