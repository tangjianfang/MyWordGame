# 笔记本

## 用途

热键栏/背包中的纸质笔记本物品图标（可摆放家具，附魔/知识玩法入口）。

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
| 封面暗 | `#6E5232` |
| 封面主 | `#8A6741` |
| 纸页 | `#F2EAD2` |
| 纸侧 | `#D9C89A` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 俯视角合拢态：横放笔记本，暖棕硬封面 + 左缘奶白书脊条 + 右侧纸页侧边
- 家具系列统一暖木色系（封面即家具木色阶），纸色与 `book.md` 纸页同源
- 与 `book.md` 区分：本作更扁长、有书脊条；封面无字

## AI 提示词

```
A single paper notebook icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A closed paper notebook seen from a
slight three-quarter top angle, lying slightly diagonal: a warm brown card
cover with a pale cream spine strip along the left edge, and a pale cream
page edge visible along the right side; a thin lighter wear line crosses the
cover. The notebook fills the frame, wider than tall. Black 1-pixel outline.
The empty corners are one flat solid dark backdrop color #2A2620.

Color palette strictly: #6E5232, #8A6741, #F2EAD2, #D9C89A, #2A2620, #1A1A1A
only.

No text, no watermark, no letters, no pen, no pencil, no ribbon, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, letters, writing, pen, pencil, ribbon, bookmark, spiral, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 6
- 封面/书脊条/纸页侧边三段分明，无笔无字
