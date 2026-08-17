# M11-C13 徽章 · 附魔师（badge-enchanter）

## 用途

milestone-11 成就系统「附魔师」徽章：附魔台累计附魔 10 次。
前景主题物 = **一本泛紫光符文的附魔书**（附魔界面/书类物品的通用记号）。

**16 枚成就徽章共用同一模板**：圆形底盘 60%（金边 `#8B7355`）+ 前景 40%。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（徽章外全部键控透明） |
| 输出数量 | 1 张：`badge-enchanter.png` |

## Alpha 的产出方式

圆形徽章之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 徽章描边（黑） | `#17140F` |
| 底盘金边（16 枚共用） | `#8B7355` |
| 底盘盘面（深暖灰） | `#443D34` |
| 书封棕 | `#8A6741` |
| 符文紫主色 | `#8C4FD4` |
| 符文紫高光 | `#C9A6F5` |
| 背景键控色 | `#FF00FF` |

紫色与 m10 机元矿同源——全游戏「附魔/神秘」统一紫。

## 视觉描述

圆形底盘同模板。前景：一本合上的厚书斜立盘面中央——
棕色书封 + 书口页缘一条浅线，封面上两三个小几何符文点
（`#8C4FD4`，其中一枚提亮 `#C9A6F5`）。包黑描边。

## AI 提示词

```
A pixel art round achievement badge medal for a retro voxel game, 1024x1024,
designed to be downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, front view, no perspective, no
anti-aliasing.

Base: a flat circular medallion centered in the canvas, its diameter about 60
percent of the image. From outside in: a one-pixel near-black outline #17140F, a
two-pixel antique gold rim #8B7355, and a flat dark warm-gray disc face #443D34.

Foreground: one closed thick book standing upright in the center of the disc,
filling about 40 percent of the image. The cover is brown #8A6741 with a thin
pale edge for the pages, wrapped in a near-black outline. On the cover sit two
or three small geometric rune marks in violet #8C4FD4, one of them brighter
#C9A6F5.

Color palette strictly limited to: #17140F, #8B7355, #443D34, #8A6741, #8C4FD4,
#C9A6F5.

The entire background outside the medallion is flat pure magenta #FF00FF, fully
saturated, hard edges, no anti-aliasing.

No open pages, no wand, no orb, no sparkles, no skull, no text, no letters, no
numbers, no ribbon, no laurel wreath, no gradient, no glow, no bevel, no 3D
render, no watermark.
```

## 负面提示词

```
open pages, wand, orb, sparkles, skull, text, letters, numbers, ribbon, laurel
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
- [ ] 书封上的紫色符文点可辨
- [ ] 徽章外全部透明（Alpha 0）
