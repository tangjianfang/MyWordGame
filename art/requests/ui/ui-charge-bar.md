# M11-U10 充能条（ui-charge-bar）

## 用途

milestone-11 战斗赛道：弓拉弓蓄力 / 蓄力挖掘的充能进度条，准星下方横向显示。
**本图按「满充能」状态绘制**，运行时按蓄力比例从左向右裁剪显示。
配色取岩浆橙系——「能量攒满」的暖色直觉，与铁灰护甲条一眼区分。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 128 × 16 像素（宽幅横条，与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 2048 × 512（再降采样） |
| 平铺 | 否（横向整体图，运行时裁剪） |
| Alpha | **有**，仅 0 或 255（条外全部键控透明） |
| 输出数量 | 1 张：`ui-charge-bar.png` |

## Alpha 的产出方式

条体之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 | `#17140F` |
| 内框暗灰 | `#3A3A3A` |
| 充能高光（顶部 2px） | `#F79B22` |
| 充能主色（岩浆橙） | `#D64B0A` |
| 充能阴影（底部 2px） | `#8A2400` |
| 背景键控色 | `#FF00FF` |

取自全局调色板岩浆橙系。

## 视觉描述

与 `ui-armor-bar` 完全同构，仅填充带换成橙系：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 内框 | 1 像素 | `#3A3A3A` |
| 3. 填充顶光 | 2 像素 | `#F79B22` |
| 4. 填充主体 | 其余 | `#D64B0A` |
| 5. 填充底影 | 2 像素 | `#8A2400` |

左右平头，不做圆头。

## AI 提示词

```
A pixel art full charge power bar for a retro game HUD, 2048x512, designed to be
downscaled to 128x16 pixel art.

Shape: one wide horizontal bar, much wider than tall, spanning nearly the full
canvas width, vertically centered with a small even margin above and below.
Flat front view, square flat ends.

Style: chunky pixel art, flat shading, no perspective, no gradient, no glow.

Content: the bar is filled completely from left to right with a warm orange energy
fill. From outside in: a one-pixel near-black outline #17140F, a one-pixel dark
gray inner frame #3A3A3A, then the fill band made of a two-pixel bright orange
stripe #F79B22 along the top, a wide flat deep orange middle #D64B0A, and a
two-pixel darkest red-orange stripe #8A2400 along the bottom.

Color palette strictly limited to: #17140F, #3A3A3A, #F79B22, #D64B0A, #8A2400.

Everything outside the bar is flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing.

No text, no numbers, no icons, no lightning bolts, no notches, no segment
divisions, no gradient, no glow, no shadow, no 3D render, no watermark.
```

## 负面提示词

```
text, numbers, lightning, icons, notches, segments, gradient, glow, shadow, 3D,
rounded ends, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 128 × 16
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 5 个色值
4. 逐层校正厚度表，拉直每一条色带
5. 存为 PNG-32（运行时按蓄力比例横向裁剪）

## 验收标准

- [ ] 尺寸恰为 128 × 16
- [ ] 上下色带厚度：顶光 2 / 底影 2，左右完全贯通
- [ ] 左右端平头
- [ ] 条外全部透明（Alpha 0），无残留洋红
- [ ] 与 `ui-armor-bar.png` 并排时仅填充色不同，框结构逐像素一致
