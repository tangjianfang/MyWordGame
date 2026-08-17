# 木椅

## 用途

热键栏/背包中的木椅物品图标（可摆放家具）。

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
| 家具木暗 | `#6E5232` |
| 家具木主 | `#8A6741` |
| 家具木亮 | `#B98D57` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 前侧视角：两条横板靠背 + 方形座面 + 四条微外撇短腿
- 家具系列统一暖木色系（`#6E5232`/`#8A6741`/`#B98D57`，与 planks 同源）
- 靠背顶缘一道亮色受光边，座面略深

## AI 提示词

```
A single wooden chair icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A simple wooden chair seen from a slight
three-quarter front angle: a warm brown wooden backrest with two horizontal
slats, a square flat seat, and four short legs splayed slightly outward. One
lighter highlight edge runs along the top of the backrest. The chair fills
the frame. Black 1-pixel outline. The empty corners are one flat solid dark
backdrop color #2A2620.

Color palette strictly: #6E5232, #8A6741, #B98D57, #2A2620, #1A1A1A only.

No text, no watermark, no cushion, no person, no table, no gradient, no cast
shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, cushion, pillow, person, sitting, table, room, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 靠背/座面/四腿三段结构清晰
