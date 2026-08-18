# 子弹

## 用途

热键栏/背包/工作台合成产物图标的子弹物品贴图。竖立单颗老式圆头子弹：黄铜弹头（圆头）+ 铁黑色弹壳（圆柱）。

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
| 黄铜弹头亮 | `#E8C46A` |
| 黄铜弹头主 | `#C49B3A` |
| 黄铜弹头暗 | `#8A6B26` |
| 铁弹壳主 | `#4A4A4A` |
| 铁弹壳暗 | `#2A2A2A` |
| 描边 | `#1A1A1A` |
| 键控色 | `#FF00FF`（后处理删除） |

## 视觉描述

- 竖立单颗子弹：弹头（圆头，黄铜）在上（y∈[5..13] 圆弧）、弹壳（圆柱，铁黑）在下（y∈[13..26] 矩形）
- 弹头高光在顶端（y=5..6 一行），主色居中（y=7..10），底部（y=11..12）压暗成黄铜暗
- 弹壳主体铁色，y=15 一行亮一档（金属反光），y=24 一行暗（壳底）
- 弹壳底边 y=26 收窄成圆（壳底弧）
- 主体宽 8 px、居中；高约 22 px（占画幅 70%）
- 背景整片纯洋红 `#FF00FF` 键控为透明

## AI 提示词

```
A single bullet icon for an inventory slot, 1024x1024 pixel art designed to be
downscaled to 32x32. A vertical musket ball / Minié-style bullet: a round brass
bullet head on top (warm yellow-gold with a slight highlight on the very top),
a dark gray iron cylindrical shell below. The bullet head occupies about 30%
of the height, the shell occupies about 50%, with a small rim/transition
between them. Black 1-pixel outline around the whole bullet. The bullet is
centered horizontally, taking about 8 pixels wide at 32x32 scale. Hard pixel
edges only, no anti-aliasing.

Color palette strictly: #E8C46A, #C49B3A, #8A6B26 (brass); #4A4A4A, #2A2A2A
(iron shell); #1A1A1A outline only.

No text, no watermark, no cartridge, no hands, no muzzle flash, no gradient, no
cast shadow, no glow, no anti-aliasing. Hard pixel edges only. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges, no
anti-aliasing, with clear magenta margins on all sides.
```

## 负面提示词

```
text, watermark, cartridge, modern ammunition, hands, shooter, muzzle flash,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing, extra objects,
dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 7（弹头亮 / 主 / 暗 + 弹壳主 / 暗 + 描边 + 键控）
- 圆头弹头 + 圆柱弹壳两段清晰可辨
- 子弹整体居中竖立，不贴边不出框

## 状态

程序占位（art/scripts/gen_m13_w3_musket_placeholders.py），待正式美术替换