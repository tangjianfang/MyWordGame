# M11-C09 徽章 · 苦力怕幸存者（badge-creeper-survivor）

## 用途

milestone-11 成就系统「苦力怕幸存者」徽章：被苦力怕爆炸波及后仍存活。
前景主题物 = **苦力怕脸**（绿色像素脸，一眼认出）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-creeper-survivor.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 苦力怕绿主色 | `#5D9C3C` |
| 苦力怕绿暗部 | `#3F7A2E` |
| 五官黑 | `#1A1A1A` |
| 背景键控色 | `#FF00FF` |

绿色取全局调色板草绿系（苦力怕与草地同源，但脸谱化后不会混淆）。

## 视觉描述

圆形底盘同模板。前景：一张方形苦力怕脸占盘面中央——
绿色方块脸（边缘压暗 `#3F7A2E`），两只方形黑眼、鼻梁一方黑块、
下垂的锯齿嘴。脸部包黑描边。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no
anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: one square creeper monster face centered on the disc, filling about
40 percent of the image. The face is green #5D9C3C with darker green #3F7A2E
around its edges, wrapped in a near-black outline. Its features are pure black
squares: two square eyes on top, one small square nose in the middle, and a
jagged drooping mouth below the nose.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #5D9C3C, #3F7A2E,
#1A1A1A.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No full body, no arms, no legs, no explosion, no smoke, no text, no letters, no
numbers, no ribbon, no laurel wreath, no gradient, no glow, no bevel, no 3D
render, no watermark.
```

## 负面提示词

```
full body, arms, legs, explosion, smoke, text, letters, numbers, ribbon, laurel
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
- [ ] 苦力怕脸五官（双眼/鼻/垂嘴）在 32×32 下可读
- [ ] 徽章外全部透明（Alpha 0）
