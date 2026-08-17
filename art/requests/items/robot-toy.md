# 玩具机器人

## 用途

热键栏/背包中的玩具机器人图标（玩具，孩子最爱的摆件）。

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
| 壳暗 | `#5F5F5F` |
| 壳主 | `#8A8A8A` |
| 壳亮 | `#C8C8C8` |
| 指示红 | `#D63838` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视方头方身小机器人：方头两粒深色方眼 + 小横线嘴，头顶一根天线顶红球
- 宽方胸身中央一枚红色小按钮灯，两侧短臂自然下垂
- 银灰三阶与铁系同源（玩具版更圆润），红色点缀天线与按钮

## AI 提示词

```
A single toy robot icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. A friendly boxy toy robot seen straight from the
front: a square silver head with two small dark square eyes and one tiny
dark mouth slot, one small antenna with a red tip ball on top of the head,
a wider silver chest body with one small round red button light in the
center, and two short rounded arms hanging at the sides. The robot fills
the frame. Black 1-pixel outline. The empty corners are one flat solid
dark backdrop color #2A2620.

Color palette strictly: #5F5F5F, #8A8A8A, #C8C8C8, #D63838, #2A2620,
#1A1A1A only.

No text, no watermark, no legs, no walking pose, no laser, no weapon, no
glow, no gradient, no cast shadow, no anti-aliasing. Hard pixel edges only.
```

## 负面提示词

```
text, legs, feet, walking pose, laser beams, weapon, menacing robot, mech,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 6
- 天线红球与胸前红按钮可见，造型友好不威慑
