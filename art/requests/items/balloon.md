# 气球

## 用途

热键栏/背包中的气球物品图标（玩具，节日装饰）。

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
| 球主 | `#D63838` |
| 球亮 | `#F28C8C` |
| 系绳 | `#F2F2F2` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 正视单个红气球：正圆球体，左上一小片浅红高光斑
- 底部小三角打结点，垂下一小段波浪形白色系绳（不出画框）
- 红色与玩具/治疗系共用主红 `#D63838`，单球不画一束

## AI 提示词

```
A single balloon icon for an inventory slot, 1024x1024 pixel art designed
to be downscaled to 32x32. One round red balloon seen straight from the
front: a bright red sphere with one small lighter red shine patch on the
upper left, a small darker triangle knot at the bottom, and one short wavy
pale string hanging down from the knot, ending inside the frame. The
balloon fills the frame. Black 1-pixel outline. The empty corners are one
flat solid dark backdrop color #2A2620.

Color palette strictly: #D63838, #F28C8C, #F2F2F2, #2A2620, #1A1A1A only.

No text, no watermark, no bunch of balloons, no hand, no sky, no clouds,
no gradient, no cast shadow, no glow, no anti-aliasing. Hard pixel edges
only.
```

## 负面提示词

```
text, bunch of balloons, multiple balloons, hand, child, sky, clouds, party,
gradient, drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 单球 + 高光斑 + 打结点 + 短系绳齐全
