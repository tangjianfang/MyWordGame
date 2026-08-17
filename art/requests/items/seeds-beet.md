# 甜菜种子

## 用途

热键栏/背包中的甜菜种子图标（种植甜菜的原料）。

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
| 种子暗 | `#6B3226` |
| 种子主 | `#8B4433` |
| 种子亮 | `#A05242` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 与 `seeds-wheat` 同一小堆散粒剪影，色系换砖红三阶（甜菜 = 红皮作物）
- 砖红取全局调色板砖红系（`#6B3226`/`#8B4433`/`#A05242`），与砖块/甜菜块同源
- 每粒一端 1 像素暗色种脐点
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single beetroot seeds icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A small pile of loose seeds seen
straight from the front: about nine small oval deep red-brown beetroot seeds
heaped together in a loose mound, a few brighter brick-red ones catching
light on top of the pile, each seed with one tiny darker tip. The pile fills
the lower two thirds of the frame. Black 1-pixel outline around the whole
pile silhouette. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #6B3226, #8B4433, #A05242, #1A1A1A outline only.

No text, no watermark, no pouch, no sack, no beetroot, no sprout, no soil,
no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges
only.
```

## 负面提示词

```
text, watermark, pouch, sack, bag, beetroot, vegetable, sprout, seedling,
soil, dirt, gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 5
- 与 `seeds-wheat` 并排剪影一致，颜色为砖红系
