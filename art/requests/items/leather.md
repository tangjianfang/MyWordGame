# 皮革

## 用途

热键栏/背包中的皮革物品图标（兽皮鞣制原料）。

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
| 皮暗 | `#7A4E28` |
| 皮主 | `#A06A38` |
| 皮亮 | `#C89058` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视一张鞣制兽皮：近方圆的皮片，四角软尖微翘，边缘微波浪
- 中央一块亮色受光斑，边缘一圈暗阶压体积；橙棕皮色接近 MC 皮革的暖橙
- 不画毛、不画动物形，纯材料片
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single leather hide icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A single tanned leather hide seen
straight from the front: a roughly square animal skin sheet with slightly
wavy edges and the four corners pulled into soft points, warm orange-brown
in color, with one lighter highlight patch in the center and darker shading
around the edges. The hide fills the frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #7A4E28, #A06A38, #C89058, #1A1A1A outline only.

No text, no watermark, no fur, no hair, no animal, no stitches, no holes,
no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges
only.
```

## 负面提示词

```
text, watermark, fur, hair, animal, cow, stitches, sewing, holes, belt,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 5
- 皮片轮廓为近方形波浪边，无毛无针脚
