# M11-U21 攻击光标（cursor-attack）

## 用途

milestone-11 交互光标系列：准星对准**可攻击生物**时的替换光标。
剑尖指向左上热点，「打它」一目了然。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素 |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（剑外全部键控透明） |
| 指针热点 | 左上剑尖（约第 3、3 像素） |
| 输出数量 | 1 张：`cursor-attack.png` |

## Alpha 的产出方式

剑之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 描边黑 | `#1A1A1A` |
| 剑刃主色 | `#A3A3A3` |
| 剑刃高光 | `#D8D8D8` |
| 剑刃暗线 | `#6E6E6E` |
| 护手金 | `#DCAE3A` |
| 剑柄木 | `#8A6741` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

一把斜 45° 的像素短剑，剑尖在左上、剑柄在右下：

- 剑刃是细长的银灰梯形，中线一道亮色高光、下缘一道暗线
- 剑尖收成一点，指向左上热点
- 中下部一枚金色横置护手，再往下 3~4 像素木色剑柄收尾
- 全部轮廓包 1 像素黑描边，剑占画布约 75%

## AI 提示词

```
A pixel art sword mouse cursor for a combat game, 1024x1024, designed to be
downscaled to 32x32 pixel art.

Style: chunky pixel art, flat shading, no perspective, no anti-aliasing.

Content: one single short sword drawn diagonally, its blade tip at the top left
and its hilt at the bottom right. The blade is a long narrow silver-gray shape
with one bright highlight line along its middle and one darker line along its
lower edge, tapering to a point aimed at the top-left corner. Below the blade
sits one small gold crossguard, then a short wooden grip. The whole sword is
wrapped in a one-pixel black outline and fills about 75 percent of the canvas.

Color palette strictly limited to: #1A1A1A, #A3A3A3, #D8D8D8, #6E6E6E, #DCAE3A,
#8A6741.

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing.

No arrow pointer, no hand, no shield, no scabbard, no blood, no text, no glow,
no shadow, no 3D render, no watermark.
```

## 负面提示词

```
arrow pointer, hand, shield, scabbard, blood, text, glow, shadow, 3D,
anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 6 个色值
4. 核对描边完整闭合
5. 存为 PNG-32，热点登记为左上剑尖（3, 3）

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 剑尖落在左上热点附近（≤ 3, 3）
- [ ] 剑刃/护手/剑柄三段比例清晰（刃最长）
- [ ] 叠在纯白、纯黑、天蓝背景上都清晰可辨
- [ ] 与 `cursor-dig.png` 同屏时一眼分清「挖」与「打」
