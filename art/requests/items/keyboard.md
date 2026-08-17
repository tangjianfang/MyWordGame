# 键盘

## 用途

热键栏/背包中的键盘物品图标（可摆放家具/黑客玩法配件）。

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
| 壳暗 | `#443D34` |
| 壳主 | `#6B6155` |
| 壳亮 | `#8B7F6F` |
| 键帽 | `#443D34` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 陡俯视角（接近正俯）：横向长方键身 + 浅色边框，三行小方键帽 + 底部一根长空格键
- 电脑三件套统一复古暖灰壳系，键帽用最暗阶、边框用亮阶
- 键帽排布整齐，不画字母符号

## AI 提示词

```
A single computer keyboard icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A flat keyboard seen from a steep
top-down angle: a wide horizontal warm beige-gray board with a lighter
plastic border frame around a neat key grid of small rounded dark square
keys in three rows, and one wider dark space bar along the bottom row. The
keyboard fills the frame, wider than tall. Black 1-pixel outline. The empty
corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #443D34, #6B6155, #8B7F6F, #2A2620, #1A1A1A only.

No text, no watermark, no letters, no symbols, no numbers, no cables, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, letters, symbols, numbers, qwerty, cables, wires, hands, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 键帽行距整齐、空格键更长，无任何字符
