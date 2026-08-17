# M11-U11 Boss 血条（ui-boss-bar）

## 用途

milestone-11 第 3 波：机元守卫 Boss 战时屏幕顶部的宽幅血条。
**本图按「满血」状态绘制**，运行时按 Boss 剩余血量从右向左裁剪显示。
填充色用心形血条（U-04）同一套红，血的含义全游戏一致。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 128 × 16 像素（8:1 宽幅，与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 2048 × 512（再降采样） |
| 平铺 | 否（横向整体图，运行时裁剪） |
| Alpha | **有**，仅 0 或 255（条外全部键控透明） |
| 输出数量 | 1 张：`ui-boss-bar.png` |

## Alpha 的产出方式

条体之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 | `#17140F` |
| 内框暗红 | `#3A2020` |
| 填充高光（顶部 2px） | `#F45C5C` |
| 填充主色 | `#D42B2B` |
| 填充阴影（底部 2px） | `#8C1B1B` |
| 背景键控色 | `#FF00FF` |

前四色与 `heart` 完全同源。

## 视觉描述

宽幅横条，占满画布宽度、上下各留少量洋红边：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 内框 | 1 像素 | `#3A2020` |
| 3. 填充顶光 | 2 像素 | `#F45C5C` |
| 4. 填充主体 | 其余 | `#D42B2B` |
| 5. 填充底影 | 2 像素 | `#8C1B1B` |

左右平头；**不加**骷髅头、尖刺、装饰端帽——Boss 名字与头图由运行时另叠。

## AI 提示词

```
A pixel art full boss health bar for a retro game HUD, 2048x512, designed to be
downscaled to 128x16 pixel art.

Shape: one very wide low horizontal bar, eight times wider than tall, spanning
nearly the full canvas width, vertically centered with a small even margin above
and below. Flat front view, square flat ends.

Style: chunky pixel art, flat shading, no perspective, no gradient, no glow.

Content: the bar is filled completely from left to right with a strong red fill.
From outside in: a one-pixel near-black outline #17140F, a one-pixel dark red
inner frame #3A2020, then the fill band made of a two-pixel light red stripe
#F45C5C along the top, a wide flat red middle #D42B2B, and a two-pixel dark red
stripe #8C1B1B along the bottom.

Color palette strictly limited to: #17140F, #3A2020, #F45C5C, #D42B2B, #8C1B1B.

Everything outside the bar is flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing.

No skull, no spikes, no ornaments, no end caps, no text, no numbers, no icons,
no notches, no segment divisions, no gradient, no glow, no shadow, no 3D render,
no watermark.
```

## 负面提示词

```
skull, spikes, ornaments, end caps, text, numbers, icons, notches, segments,
gradient, glow, shadow, 3D, rounded ends, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 128 × 16
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 5 个色值
4. 逐层校正厚度表，拉直每一条色带
5. 存为 PNG-32（运行时按血量比例横向裁剪，空出段由代码叠暗红底）

## 验收标准

- [ ] 尺寸恰为 128 × 16
- [ ] 上下色带厚度：顶光 2 / 底影 2，左右完全贯通
- [ ] 左右端平头，无任何端部装饰
- [ ] 条外全部透明（Alpha 0），无残留洋红
- [ ] 与 `heart-full` 同屏时红色观感一致（同一套红）
