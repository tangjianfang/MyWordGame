# 铁锄

## 用途

热键栏/背包中的铁锄物品图标。木柄 + 银铁锄板，农业工具第三档。

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
| 柄暗 | `#634C33` |
| 柄主 | `#7A6042` |
| 铁主 | `#8A8A8A` |
| 铁亮 | `#C8C8C8` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 造型与 `hoe-wooden` 完全同剪影（45° 木柄 + 直角锄板），仅头部换铁色
- 铁色与铁剑/铁镐同 `#8A8A8A`/`#C8C8C8`，头部上缘一道亮边
- 头部边缘平直（金属锻造，无石锄的豁口）

## AI 提示词

```
A single iron hoe icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A farming hoe drawn diagonally from bottom-left to
top-right: a long brown wooden handle, and at the top end a short flat silver
iron blade head extending to the left at a right angle to the handle (an
L-shaped top), the head a little wider than the handle with a clean bright
top edge. The tool spans the full diagonal of the frame. Black 1-pixel
outline around every part. The empty corners are one flat solid dark
backdrop color #2A2620.

Color palette strictly: #634C33, #7A6042, #8A8A8A, #C8C8C8, #2A2620, #1A1A1A
only.

No text, no watermark, no dirt, no plants, no seeds, no gradient, no cast
shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, dirt, soil, plants, seeds, farmer, gradient, drop shadow,
glow, blur, 3D render, anti-aliasing, extra objects
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 6
- 与 `hoe-wooden` 并排剪影一致，仅头部为银铁色
