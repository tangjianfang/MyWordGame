# 鼠标

## 用途

热键栏/背包中的鼠标物品图标（可摆放家具/黑客玩法配件）。

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
| 壳暗 | `#443D34` |
| 壳主 | `#6B6155` |
| 壳亮 | `#8B7F6F` |
| 滚轮 | `#443D34` |
| 底色 | `#2A2620` |
| 描边 | `#1A1A1A` |

## 视觉描述

- 3/4 俯侧视角：圆润暖灰鼠标壳，前部中缝分左右两键，缝间一小条深色滚轮
- 电脑三件套统一复古暖灰壳系，壳顶一道亮色高光
- 尾部不画线缆

## AI 提示词

```
A single computer mouse icon for an inventory slot, 1024x1024 pixel art
designed to be downscaled to 32x32. A small rounded computer mouse seen from
a three-quarter top-side angle: a smooth warm beige-gray mouse body, a
seam line splitting the front into a left and right button, and one small
darker rounded scroll wheel bar between the two buttons; one lighter
highlight sits on top of the shell. The mouse fills the frame. Black
1-pixel outline. The empty corners are one flat solid dark backdrop color
#2A2620.

Color palette strictly: #443D34, #6B6155, #8B7F6F, #2A2620, #1A1A1A only.

No text, no watermark, no cable, no wire, no tail, no cursor arrow, no
mousepad, no gradient, no cast shadow, no glow, no anti-aliasing. Hard
pixel edges only.
```

## 负面提示词

```
text, watermark, cable, wire, tail, cursor arrow, mousepad, hand, gradient,
drop shadow, glow, blur, 3D render, anti-aliasing
```

## 后处理

最近邻降采样到 32×32 + 调色板量化（不透明图，无需洋红键控）。

## 验收

- 32×32 PNG，32 位 RGBA，Alpha 全部 255
- 颜色数 ≤ 5
- 左右键中缝与滚轮可见，无线缆
