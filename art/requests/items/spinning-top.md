# 陀螺

## 用途

热键栏/背包中的陀螺物品图标（玩具，旋转玩法）。

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
| 木主 | `#7A6042` |
| 木亮 | `#9C7549` |
| 环红 | `#D63838` |
| 环蓝 | `#4E88CE` |
| 顶柄 | `#D9C89A` |
| 键控色 | `#FF00FF`（后处理删除） |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 侧视角静止陀螺：上小圆柄 → 浅色细颈 → 宽木身（红/蓝两道色环）→ 底部暗色尖锥
- 木色与家具/工具木系同源，色环用玩具系红蓝
- 静止摆放，不画旋转动感线
- 主体占画幅约 70%、居中；背景整片纯洋红 `#FF00FF` 键控为透明，无深色底

## AI 提示词

```
A single wooden spinning top icon for an inventory slot, 1024x1024 pixel
art designed to be downscaled to 32x32. A classic spinning top seen from a
slight three-quarter side angle, standing still: a small round pale handle
knob on top, a narrow lighter neck, a wide warm brown wooden body with one
red band and one blue band stripe around it, and a dark pointed tip at the
bottom. The top fills the frame. Black 1-pixel outline. The subject is centered, taking about 70 percent of the frame. The entire
background is solid flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing, with clear magenta margins on all sides.

Color palette strictly: #7A6042, #9C7549, #D63838, #4E88CE, #D9C89A, #1A1A1A outline only.

No text, no watermark, no motion blur, no spinning lines, no hand, no
string, no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel
edges only.
```

## 负面提示词

```
text, motion blur, spinning motion lines, hand, string, whip, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing, extra objects, dark background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控洋红为透明（洋红占比 > 50% → Alpha = 0），去洋红边
3. 量化到上表色值

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 仅 0/255，背景键控为透明
- 颜色数 ≤ 7
- 柄/颈/宽身/尖底四段结构清晰，色环两道
