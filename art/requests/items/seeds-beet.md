# 甜菜种子

## 用途

热键栏/背包中的甜菜种子图标（种植甜菜的原料）。

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
| 种子暗 | `#6B3226` |
| 种子主 | `#8B4433` |
| 种子亮 | `#A05242` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `seeds-wheat` 同一小堆散粒剪影，色系换砖红三阶（甜菜 = 红皮作物）
- 砖红取全局调色板砖红系（`#6B3226`/`#8B4433`/`#A05242`），与砖块/甜菜块同源
- 每粒一端 1 像素暗色种脐点

## AI 提示词

```
A single beetroot seeds icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A small pile of loose seeds seen
straight from the front: about nine small oval deep red-brown beetroot seeds
heaped together in a loose mound, a few brighter brick-red ones catching
light on top of the pile, each seed with one tiny darker tip. The pile fills
the lower two thirds of the frame. Black 1-pixel outline around the whole
pile silhouette. The empty area above is one flat solid dark backdrop color
#2A2620.

Color palette strictly: #6B3226, #8B4433, #A05242, #2A2620, #1A1A1A only.

No text, no watermark, no pouch, no sack, no beetroot, no sprout, no soil,
no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges
only.
```

## 负面提示词

```
text, watermark, pouch, sack, bag, beetroot, vegetable, sprout, seedling,
soil, dirt, gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 与 `seeds-wheat` 并排剪影一致，颜色为砖红系
