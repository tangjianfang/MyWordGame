# 办公桌

## 用途

热键栏/背包中的办公桌物品图标（可摆放家具，配电脑三件套的桌子）。

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
| 家具木暗 | `#6E5232` |
| 家具木主 | `#8A6741` |
| 家具木亮 | `#B98D57` |
| 把手铁 | `#8A8A8A` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 前侧视角：宽桌面 + 右侧一列两层抽屉柜（抽屉面板各一枚小圆铁把手）+ 左侧空档两腿
- 家具系列统一暖木色系；抽屉面板用木暗阶、桌面用木亮阶
- 与 `table` 并排能区分（多了抽屉柜体块）
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single office desk icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A wide warm brown wooden desk seen from
a slight three-quarter front angle: a large flat lighter-wood desktop, a
drawer cabinet column on the right side with two stacked drawers, each
drawer front in darker wood with one small round silver knob, and two simple
legs under the open left side. The desk fills the frame. Black 1-pixel
outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #6E5232, #8A6741, #B98D57, #8A8A8A, #1A1A1A outline
only.

No text, no watermark, no computer, no monitor, no chair, no papers, no
gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, computer, monitor, keyboard, chair, papers, books, lamp,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 两层抽屉与小把手可见，桌上无电脑（电脑三件套是独立图标）
