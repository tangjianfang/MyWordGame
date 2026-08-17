# 金头盔

## 用途

热键栏/背包/盔甲栏中的金头盔图标。金系盔甲第一件（护头）。

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
| 金暗 | `#A8842E` |
| 金主 | `#E8C04A` |
| 金亮 | `#F7E08A` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `helmet-iron` 同一正视圆顶盔剪影，只把三阶铁色换成 m10 已确立的金系
- 金色与金剑/金镐/金锭同源（`#A8842E`/`#E8C04A`/`#F7E08A`），图标即属性提示
- 左上一道金亮高光，护鼻条与帽檐带仍是暗金阶

## AI 提示词

```
A single golden helmet icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A rounded knight helmet seen straight
from the front: a warm bright yellow gold metal dome cap with a narrow
vertical nose guard strip down the center of the face opening, a slightly
darker gold rim band along the bottom edge, and one pale gold highlight
streak on the upper left of the dome. The helmet fills the frame. Black
1-pixel outline. The empty corners are one flat solid dark backdrop color
#2A2620.

Color palette strictly: #A8842E, #E8C04A, #F7E08A, #2A2620, #1A1A1A only.

No text, no watermark, no face, no eyes, no crest, no plume, no jewels, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, face, eyes, mouth, crest, plume, feathers, jewels, gems,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 与 `helmet-iron` 并排剪影一致，颜色为金系
