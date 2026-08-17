# 王冠

## 用途

热键栏/背包中的王冠图标（宝物，成就/通关奖励）。

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
| 金暗 | `#A8842E` |
| 金主 | `#E8C04A` |
| 金亮 | `#F7E08A` |
| 宝石红 | `#D63838` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视三尖王冠：金冠带上三枚三角尖，每尖顶一粒小金珠
- 冠带正中镶一枚圆形红宝石，下缘一圈暗金压边
- 金色与 m10 金系同源（`#A8842E`/`#E8C04A`/`#F7E08A`），不画珠宝满镶
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single golden crown icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A royal crown seen straight from the
front: a bright gold band with three triangular points, one tiny gold bead
tip on top of each point, one small round red gem set in the center of the
band, and a slightly darker gold shade along the bottom rim. The crown
fills the frame, wider than tall. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #A8842E, #E8C04A, #F7E08A, #D63838,
#1A1A1A outline only.

No text, no watermark, no jewels everywhere, no feathers, no velvet cap, no
skull, no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel
edges only.
```

## 负面提示词

```
text, jewels everywhere, multiple gems, feathers, velvet cap, king face,
skull, gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 6
- 三尖 + 尖顶金珠 + 正中红宝石齐全
