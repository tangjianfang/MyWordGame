# 附魔棒

## 用途

热键栏/背包中的附魔棒图标（对工具施加附魔的道具）。

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
| 柄暗 | `#634C33` |
| 柄主 | `#7A6042` |
| 宝石暗 | `#5E4478` |
| 宝石主 | `#9C82BC` |
| 宝石辉光 | `#B9A2D4` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 45° 对角线构图：左下木柄 → 右上顶端，顶端两爪夹持一枚菱形紫色宝石
- 宝石紫与机元/附魔系同族（`#5E4478`/`#9C82BC`/`#B9A2D4`），旁侧两粒 1 像素亮紫火花
- 握柄木色与全系列工具一致（`#634C33`/`#7A6042`）
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single enchanting rod icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A magic rod drawn diagonally from
bottom-left to top-right: a brown wooden rod handle, and at the top end a
small pronged claw tip holding one faceted purple gem crystal; two tiny
pale violet spark pixels float beside the gem. The rod spans the full
diagonal of the frame. Black 1-pixel outline around every part. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #634C33, #7A6042, #5E4478, #9C82BC, #B9A2D4, #1A1A1A outline only.

No text, no watermark, no hand, no wand stars trail, no large glow halo, no
gradient, no cast shadow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, hand, wizard, wand with star trail, sparkles everywhere, glow halo,
staff, gradient, drop shadow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 7
- 顶端爪托宝石结构可见，火花不超过两粒
