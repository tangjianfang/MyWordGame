# 箭

## 用途

热键栏/背包中的箭矢物品图标。斜置石镞木杆箭。

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
| 箭杆 | `#7A6042` |
| 石镞暗 | `#8A8A8A` |
| 石镞亮 | `#A3A3A3` |
| 羽毛白 | `#F2F2F2` |
| 羽毛灰 | `#C8C8C8` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 对角线构图：左下羽尾 → 右上石镞，与既有剑/镐 45° 摆放习惯一致
- 石镞三角形两阶灰；箭杆细木色；尾羽两片白 + 一片灰做层次
- 箭身贯穿对角线，羽与镞不贴出画框
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single arrow icon for an inventory slot, 1024x1024 pixel art designed to be
downscaled to 32x32. A straight arrow drawn diagonally from bottom-left to
top-right: a small gray stone arrowhead at the top-right end, a long thin
brown wooden shaft, and two small white feather fletching fins with one gray
fin at the bottom-left end. The arrow spans the full diagonal of the frame.
Black 1-pixel outline around every part. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #7A6042, #8A8A8A, #A3A3A3, #F2F2F2, #C8C8C8,
#1A1A1A outline only.

No text, no watermark, no bow, no quiver, no blood, no gradient, no cast
shadow, no glow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, watermark, bow, quiver, multiple arrows, blood, gradient, drop shadow,
glow, blur, 3D render, anti-aliasing, extra objects, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 7
- 对角线构图，镞/杆/羽三段分明
