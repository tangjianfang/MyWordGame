# M11-C12 徽章 · 面包师（badge-baker）

## 用途

milestone-11 成就系统「面包师」徽章：熔炉累计烤出 50 个面包。
前景主题物 = **一条面包**（与物品图标 `items/bread.md` 同观感）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-baker.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 面包皮主色 | `#B98D57` |
| 面包皮暗部 | `#8A6741` |
| 割痕亮色 | `#EADDB4` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

圆形底盘同模板。前景：一条斜放的长面包占盘面中央——
弧形顶的棕金色面包体（下缘压暗），顶面三道短斜割痕用亮色 `#EADDB4`。
包黑描边。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no
anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: one loaf of bread lying diagonally across the center of the disc,
filling about 40 percent of the image. The loaf is golden brown #B98D57 with a
darker underside #8A6741 and an arched top marked by three short diagonal
slashes in pale cream #EADDB4. The loaf is wrapped in a near-black outline.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #B98D57, #8A6741,
#EADDB4.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No wheat, no oven, no furnace, no rolling pin, no chef hat, no text, no letters,
no numbers, no ribbon, no laurel wreath, no gradient, no glow, no bevel, no 3D
render, no watermark.
```

## 负面提示词

```
wheat, oven, furnace, rolling pin, chef hat, text, letters, numbers, ribbon,
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
- [ ] 三道割痕可辨、面包轮廓可读
- [ ] 徽章外全部透明（Alpha 0）
