# 金币堆

## 用途

热键栏/背包中的金币堆图标（宝物，货币类奖励）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 无（完全不透明） |
| 背景 | 统一深色底 `#2A2620` |

## 调色板

| 用途 | HEX |
| --- | --- |
| 币暗 | `#A8842E` |
| 币主 | `#E8C04A` |
| 币亮 | `#F7E08A` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 视角小金字塔金币堆：底排四枚立币，其上两枚平叠，顶上一枚斜靠
- 每枚币外圈暗金边、币面主金、迎光处一粒亮金高光点
- 金色与 m10 金系同源（`#A8842E`/`#E8C04A`/`#F7E08A`），币面无图案

## AI 提示词

```
A single coin pile icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A small heap of round gold coins seen from a
slight three-quarter angle, stacked as a tiny pyramid: a bottom row of four
standing coins, two flat coins lying on top, and one leaning coin at the
peak. Each coin has a slightly darker gold rim and a lighter gold face,
with one tiny bright sparkle pixel on the frontmost coin. The pile fills
the lower two thirds of the frame. Black 1-pixel outline around the pile.
The empty area above is one flat solid dark backdrop color #2A2620.

Color palette strictly: #A8842E, #E8C04A, #F7E08A, #2A2620, #1A1A1A only.

No text, no watermark, no numbers on coins, no face portrait, no bag, no
chest, no sparkles everywhere, no gradient, no cast shadow, no glow, no
anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, numbers, face portrait on coin, dollar sign, money bag, treasure
chest, sparkles everywhere, gradient, drop shadow, glow, blur, 3D render,
anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 金字塔堆叠层次可辨，币面无图案无数字
