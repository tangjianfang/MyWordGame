# 陀螺

## 用途

热键栏/背包中的陀螺物品图标（玩具，旋转玩法）。

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
| 木主 | `#7A6042` |
| 木亮 | `#9C7549` |
| 环红 | `#D63838` |
| 环蓝 | `#4E88CE` |
| 顶柄 | `#D9C89A` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 侧视角静止陀螺：上小圆柄 → 浅色细颈 → 宽木身（红/蓝两道色环）→ 底部暗色尖锥
- 木色与家具/工具木系同源，色环用玩具系红蓝
- 静止摆放，不画旋转动感线

## AI 提示词

```
A single wooden spinning top icon for an inventory slot, 1024x1024 pixel
art designed to be downscaled to 32x32. A classic spinning top seen from a
slight three-quarter side angle, standing still: a small round pale handle
knob on top, a narrow lighter neck, a wide warm brown wooden body with one
red band and one blue band stripe around it, and a dark pointed tip at the
bottom. The top fills the frame. Black 1-pixel outline. The empty corners
are one flat solid dark backdrop color #2A2620.

Color palette strictly: #7A6042, #9C7549, #D63838, #4E88CE, #D9C89A,
#2A2620, #1A1A1A only.

No text, no watermark, no motion blur, no spinning lines, no hand, no
string, no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel
edges only.
```

## 负面提示词

```
text, motion blur, spinning motion lines, hand, string, whip, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing, extra objects
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 7
- 柄/颈/宽身/尖底四段结构清晰，色环两道
