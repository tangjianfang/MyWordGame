# M11-U09 护甲值条（ui-armor-bar）

## 用途

milestone-11 第 2 波：HUD 护甲值条，显示 `gearBonus` defense 汇总的护甲量。
放在血量条上方，与心形血条同宽一档。**本图按「满护甲」状态绘制**，
运行时按 defense 比例从右向左裁剪显示。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 128 × 16 像素（宽幅横条，与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 2048 × 512（再降采样） |
| 平铺 | 否（横向整体图，运行时裁剪） |
| Alpha | **有**，仅 0 或 255（条外全部键控透明） |
| 输出数量 | 1 张：`ui-armor-bar.png` |

## Alpha 的产出方式

条体之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 外描边 | `#17140F` |
| 内框暗灰 | `#3A3A3A` |
| 填充高光（顶部 2px） | `#A3A3A3` |
| 填充主色（铁灰） | `#8A8A8A` |
| 填充阴影（底部 2px） | `#6E6E6E` |
| 背景键控色 | `#FF00FF` |

护甲=铁甲，取全局调色板的石灰系，与红色血条、绿色经验条拉开。

## 视觉描述

横向长条，占满画布宽度、上下各留少量洋红边：

| 层 | 厚度 | 颜色 |
| --- | --- | --- |
| 1. 外描边 | 1 像素 | `#17140F` |
| 2. 内框 | 1 像素 | `#3A3A3A` |
| 3. 填充顶光 | 2 像素 | `#A3A3A3` |
| 4. 填充主体 | 其余 | `#8A8A8A` |
| 5. 填充底影 | 2 像素 | `#6E6E6E` |

左右端是平头（不做圆头/尖角），运行时裁剪后端点不会变形。

## AI 提示词

```
A pixel art full armor points bar for a retro game HUD, 2048x512, designed to be
downscaled to 128x16 pixel art.

Shape: one wide horizontal bar, much wider than tall, spanning nearly the full
canvas width, vertically centered with a small even margin above and below.
Flat front view, square flat ends.

Style: chunky pixel art, flat shading, no perspective, no gradient, no glow.

Content: the bar is filled completely from left to right with a metallic
silver-gray fill. From outside in: a one-pixel near-black outline #17140F, a
one-pixel dark gray inner frame #3A3A3A, then the fill band made of a two-pixel
light silver stripe #A3A3A3 along the top, a wide flat silver-gray middle #8A8A8A,
and a two-pixel darker gray stripe #6E6E6E along the bottom.

Color palette strictly limited to: #17140F, #3A3A3A, #A3A3A3, #8A8A8A, #6E6E6E.

Everything outside the bar is flat pure magenta #FF00FF, fully saturated, hard
edges, no anti-aliasing.

No text, no numbers, no icons, no notches, no segment divisions, no gradient,
no glow, no shadow, no 3D render, no watermark.
```

## 负面提示词

```
text, numbers, icons, notches, segments, gradient, glow, shadow, 3D, rounded ends,
pointed ends, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 128 × 16
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 5 个色值
4. 逐层校正厚度表，拉直每一条色带
5. 存为 PNG-32（运行时按护甲比例横向裁剪，空出段由代码叠深色底）

## 验收标准

- [ ] 尺寸恰为 128 × 16
- [ ] 上下色带厚度：顶光 2 / 底影 2，左右完全贯通
- [ ] 左右端平头，无圆角、无斜切
- [ ] 条外全部透明（Alpha 0），无残留洋红
- [ ] 与 `heart-full` 并排放在血条上方，宽度视觉匹配、铁灰不与红色混淆
