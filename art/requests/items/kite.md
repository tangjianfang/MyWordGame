# 风筝

## 用途

热键栏/背包中的风筝物品图标（玩具，放飞玩法）。

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
| 红面 | `#D63838` |
| 蓝面 | `#4E88CE` |
| 骨架 | `#D9C89A` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视菱形风筝，一角朝正上：四片面料红蓝相间，十字竹骨一条竖一条横贯穿
- 下角垂两节小蝴蝶结飘尾（红蓝各一）
- 不画天空、不画白云，纯单体
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single kite icon for an inventory slot, 1024x1024 pixel art designed to
be downscaled to 32x32. A diamond-shaped kite seen straight from the front,
one point facing straight up: the four fabric panels in alternating red and
blue, with one vertical and one horizontal thin pale bamboo spar line
crossing over the panels, and a short wavy tail of two small bow ribbons,
one red and one blue, hanging from the bottom point. The kite fills the
frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #D63838, #4E88CE, #D9C89A, #1A1A1A outline only.

No text, no watermark, no sky, no clouds, no sun, no string going out of
frame, no birds, no gradient, no cast shadow, no glow, no anti-aliasing.
Hard pixel edges only.
```

## 负面提示词

```
text, sky, clouds, sun, birds, long string, child, hand, gradient, drop
shadow, glow, blur, 3D render, anti-aliasing, extra objects, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 5
- 菱形四色块分明、十字骨与飘尾可见
