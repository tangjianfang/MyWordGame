# 木锄

## 用途

热键栏/背包中的木锄物品图标。木柄 + 木质锄板，农业工具最低档。

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
| 锄板亮 | `#8E7350` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 45° 对角线构图：左下木柄 → 右上锄板，与既有剑/镐摆法一致
- 顶端锄板与柄成直角向左伸出，全木色三阶
- 握柄色与既有全系列工具一致（`#634C33`/`#7A6042`），锄四档只换头部材质色

## AI 提示词

```
A single wooden hoe icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A farming hoe drawn diagonally from bottom-left to
top-right: a long brown wooden handle, and at the top end a short flat tan
wooden blade head extending to the left at a right angle to the handle (an
L-shaped top), the head a little wider than the handle. The tool spans the
full diagonal of the frame. Black 1-pixel outline around every part. The
empty corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #634C33, #7A6042, #8E7350, #2A2620, #1A1A1A only.

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
- 颜色数 ≤ 5
- 柄与锄板成 L 形，锄板为木色
