# 金币堆

## 用途

热键栏/背包中的金币堆图标（宝物，货币类奖励）。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | 有（仅 0 / 255，洋红键控） |
| 背景 | 整片纯洋红 `#FF00FF`，后处理键控为透明 |

## 调色板

| 用途 | HEX |
| --- | --- |
| 币暗 | `#A8842E` |
| 币主 | `#E8C04A` |
| 币亮 | `#F7E08A` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 视角小金字塔金币堆：底排四枚立币，其上两枚平叠，顶上一枚斜靠
- 每枚币外圈暗金边、币面主金、迎光处一粒亮金高光点
- 金色与 m10 金系同源（`#A8842E`/`#E8C04A`/`#F7E08A`），币面无图案
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single coin pile icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A small heap of round gold coins seen from a
slight three-quarter angle, stacked as a tiny pyramid: a bottom row of four
standing coins, two flat coins lying on top, and one leaning coin at the
peak. Each coin has a slightly darker gold rim and a lighter gold face,
with one tiny bright sparkle pixel on the frontmost coin. The pile fills
the lower two thirds of the frame. Black 1-pixel outline around the pile.
The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #A8842E, #E8C04A, #F7E08A, #1A1A1A outline only.

No text, no watermark, no numbers on coins, no face portrait, no bag, no
chest, no sparkles everywhere, no gradient, no cast shadow, no glow, no
anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, numbers, face portrait on coin, dollar sign, money bag, treasure
chest, sparkles everywhere, gradient, drop shadow, glow, blur, 3D render,
anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 5
- 金字塔堆叠层次可辨，币面无图案无数字
