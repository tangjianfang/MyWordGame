# M11-C14 徽章 · 神射手（badge-archer）

## 用途

milestone-11 成就系统「神射手」徽章：弓箭累计击杀 50 只生物。
前景主题物 = **一面箭靶**（同心圆，红白金）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-archer.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 靶圈白 | `#F2F2F2` |
| 靶圈红 | `#D42B2B` |
| 靶心金 | `#DCAE3A` |
| 背景键控色 | `#FF00FF` |

红与 `heart` 同源；靶心金与底盘金边呼应。

## 视觉描述

圆形底盘同模板。前景：一面同心圆箭靶居盘面中央——
从外到内：白圈 → 红圈 → 白圈 → 金色靶心，全部包黑描边。
不画箭（与 `badge-skeleton-sniper` 的弓箭区分：那是「打中骷髅」，这是「打得准」）。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, perfectly concentric, no
perspective, no anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: one archery target of concentric rings centered on the disc, filling
about 40 percent of the image. From outside in: a white ring #F2F2F2, a red ring
#D42B2B, another white ring #F2F2F2, and a golden bullseye center #DCAE3A. The
whole target is wrapped in a near-black outline.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #F2F2F2, #D42B2B,
#DCAE3A.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No arrows, no bow, no feathers, no text, no letters, no numbers, no ribbon, no
laurel wreath, no gradient, no glow, no bevel, no 3D render, no watermark.
```

## 负面提示词

```
arrows, bow, feathers, text, letters, numbers, ribbon, laurel wreath, gradient,
glow, bevel, 3D, anti-aliasing, opaque background
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
- [ ] 四环同心、无箭
- [ ] 徽章外全部透明（Alpha 0）
