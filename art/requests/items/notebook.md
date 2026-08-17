# 笔记本

## 用途

热键栏/背包中的纸质笔记本物品图标（可摆放家具，附魔/知识玩法入口）。

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
| 封面暗 | `#6E5232` |
| 封面主 | `#8A6741` |
| 纸页 | `#F2EAD2` |
| 纸侧 | `#D9C89A` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 俯视角合拢态：横放笔记本，暖棕硬封面 + 左缘奶白书脊条 + 右侧纸页侧边
- 家具系列统一暖木色系（封面即家具木色阶），纸色与 `book.md` 纸页同源
- 与 `book.md` 区分：本作更扁长、有书脊条；封面无字
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single paper notebook icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A closed paper notebook seen from a
slight three-quarter top angle, lying slightly diagonal: a warm brown card
cover with a pale cream spine strip along the left edge, and a pale cream
page edge visible along the right side; a thin lighter wear line crosses the
cover. The notebook fills the frame, wider than tall. Black 1-pixel outline.
The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #6E5232, #8A6741, #F2EAD2, #D9C89A, #1A1A1A outline
only.

No text, no watermark, no letters, no pen, no pencil, no ribbon, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, letters, writing, pen, pencil, ribbon, bookmark, spiral, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 封面/书脊条/纸页侧边三段分明，无笔无字
