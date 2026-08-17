# 长笛

## 用途

热键栏/背包中的长笛物品图标（乐器，可吹奏发声）。

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
| 木暗 | `#634C33` |
| 木主 | `#7A6042` |
| 木亮 | `#8E7350` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 45° 对角线构图：细长木管从左下伸到右上，顶端一小段深色吹口块
- 管身中部一列四个 1 像素暗色指孔，上缘一道亮色高光线
- 木色与全系列工具握柄同源，纯木管不画金属键

## AI 提示词

```
A single wooden flute icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A slim wooden flute drawn diagonally
from bottom-left to top-right: a warm brown wooden tube with a small darker
mouthpiece block near the top end, a neat row of four tiny dark finger
holes along the middle, and one lighter highlight line along the top edge
of the tube. The flute spans the full diagonal of the frame. Black 1-pixel
outline. The empty corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #634C33, #7A6042, #8E7350, #2A2620, #1A1A1A only.

No text, no watermark, no hands, no notes, no music symbols, no metal keys,
no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges
only.
```

## 负面提示词

```
text, hands, musician, notes, music symbols, metal keys, gradient, drop
shadow, glow, blur, 3D render, anti-aliasing, extra objects
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 吹口块与四指孔可见，无金属件
