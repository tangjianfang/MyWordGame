# 皮革

## 用途

热键栏/背包中的皮革物品图标（兽皮鞣制原料）。

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
| 皮暗 | `#7A4E28` |
| 皮主 | `#A06A38` |
| 皮亮 | `#C89058` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视一张鞣制兽皮：近方圆的皮片，四角软尖微翘，边缘微波浪
- 中央一块亮色受光斑，边缘一圈暗阶压体积；橙棕皮色接近 MC 皮革的暖橙
- 不画毛、不画动物形，纯材料片

## AI 提示词

```
A single leather hide icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A single tanned leather hide seen
straight from the front: a roughly square animal skin sheet with slightly
wavy edges and the four corners pulled into soft points, warm orange-brown
in color, with one lighter highlight patch in the center and darker shading
around the edges. The hide fills the frame. Black 1-pixel outline. The empty
corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #7A4E28, #A06A38, #C89058, #2A2620, #1A1A1A only.

No text, no watermark, no fur, no hair, no animal, no stitches, no holes,
no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges
only.
```

## 负面提示词

```
text, watermark, fur, hair, animal, cow, stitches, sewing, holes, belt,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 皮片轮廓为近方形波浪边，无毛无针脚
