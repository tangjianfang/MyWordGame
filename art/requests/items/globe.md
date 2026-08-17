# 地球仪

## 用途

热键栏/背包中的地球仪物品图标（可摆放家具，装饰/探险玩法）。

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
| 海洋 | `#3A6FB5` |
| 陆地 | `#5D9C3C` |
| 极冰 | `#E7F4F8` |
| 支架木暗 | `#6E5232` |
| 支架木主 | `#8A6741` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视：斜轴地球仪——蓝海球体 + 几块绿色陆块 + 顶白极冠，细木半环弧 + 圆木底座
- 海/陆/冰取全局水蓝/草绿/玻璃青白系，支架用家具统一暖木色
- 陆块抽象不对应真实大陆
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single desk globe icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A world globe seen straight from the front: a
round sphere with pale blue oceans, a few small abstract green landmass
blobs, and a white polar cap at the top; a thin warm brown wooden arc arm
holds the sphere on a tilted axis, mounted on a round brown wooden base
stand at the bottom. The globe fills the frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #3A6FB5, #5D9C3C, #E7F4F8, #6E5232, #8A6741, #1A1A1A outline only.

No text, no watermark, no real continents, no country borders, no clouds,
no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges
only.
```

## 负面提示词

```
text, real continents, country borders, map labels, clouds, stars, space,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 7
- 球体 + 木弧 + 底座三件结构完整
