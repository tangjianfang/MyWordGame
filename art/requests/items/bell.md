# 铃铛

## 用途

热键栏/背包中的铃铛物品图标（乐器，可敲响发声）。

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
| 铜暗 | `#A8842E` |
| 铜主 | `#E8C04A` |
| 铜亮 | `#F7E08A` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视铜钟：钟形主体下缘外撇成喇叭口、口沿一圈暗金阶，顶部小圆环挂钮
- 钟口下露一粒深色钟舌点，左上一道淡金高光
- 金色与 m10 金系同源（`#A8842E`/`#E8C04A`/`#F7E08A`）
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single golden bell icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A brass bell seen straight from the
front: a bright golden bell body flaring outward into a skirt with a
slightly darker rim band along the bottom edge, a small round gold loop
mount on top, one tiny dark clapper dot visible under the skirt, and one
light highlight streak on the left side of the bell. The bell fills the
frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #A8842E, #E8C04A, #F7E08A, #1A1A1A outline only.

No text, no watermark, no hand, no hammer, no notes, no music symbols, no
sound waves, no gradient, no cast shadow, no glow, no anti-aliasing. Hard
pixel edges only.
```

## 负面提示词

```
text, hand, hammer, drumstick, notes, music symbols, sound waves, ribbon,
Christmas, gradient, drop shadow, glow, blur, 3D render, anti-aliasing, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 5
- 钟形轮廓外撇、顶环与钟舌点可见
