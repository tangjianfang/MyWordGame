# M11-C07 徽章 · 怪物杀手（badge-mob-slayer）

## 用途

milestone-11 成就系统「怪物杀手」徽章：累计击杀 100 只敌对生物。
前景主题物 = **交叉双剑**（近战讨伐的记号）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-mob-slayer.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 剑刃亮 | `#A3A3A3` |
| 剑刃暗 | `#8A8A8A` |
| 剑柄木 | `#634C33` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

圆形底盘同模板。前景：两把像素短剑在盘面中央交叉成 X，
剑刃银灰（上亮下暗）、剑柄棕色朝外斜下，全部包黑描边。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no
anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: two identical short swords crossed over the center of the disc,
forming an X. Each blade is silver-gray with a lighter top edge #A3A3A3 and a
darker underside #8A8A8A, and each has a small brown wooden grip #634C33. Every
shape is wrapped in a near-black outline. The crossed swords fill about 40
percent of the image.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #A3A3A3, #8A8A8A,
#634C33.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No text, no letters, no numbers, no skull, no shield, no blood, no ribbon, no
laurel wreath, no gradient, no glow, no bevel, no 3D render, no watermark.
```

## 负面提示词

```
text, letters, numbers, skull, shield, blood, ribbon, laurel wreath, gradient,
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
- [ ] 双剑交叉呈 X 形、左右对称
- [ ] 徽章外全部透明（Alpha 0）
