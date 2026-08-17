# 铃铛

## 用途

热键栏/背包中的铃铛物品图标（乐器，可敲响发声）。

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
| 铜暗 | `#A8842E` |
| 铜主 | `#E8C04A` |
| 铜亮 | `#F7E08A` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视铜钟：钟形主体下缘外撇成喇叭口、口沿一圈暗金阶，顶部小圆环挂钮
- 钟口下露一粒深色钟舌点，左上一道淡金高光
- 金色与 m10 金系同源（`#A8842E`/`#E8C04A`/`#F7E08A`）

## AI 提示词

```
A single golden bell icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A brass bell seen straight from the
front: a bright golden bell body flaring outward into a skirt with a
slightly darker rim band along the bottom edge, a small round gold loop
mount on top, one tiny dark clapper dot visible under the skirt, and one
light highlight streak on the left side of the bell. The bell fills the
frame. Black 1-pixel outline. The empty corners are one flat solid dark
backdrop color #2A2620.

Color palette strictly: #A8842E, #E8C04A, #F7E08A, #2A2620, #1A1A1A only.

No text, no watermark, no hand, no hammer, no notes, no music symbols, no
sound waves, no gradient, no cast shadow, no glow, no anti-aliasing. Hard
pixel edges only.
```

## 负面提示词

```
text, hand, hammer, drumstick, notes, music symbols, sound waves, ribbon,
Christmas, gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 钟形轮廓外撇、顶环与钟舌点可见
