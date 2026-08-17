# M11-U15 经验飘字样式帧（fx-xp-float）

## 用途

milestone-11 第 2 波「经验 +N 飘字」（m9 未做项）：击杀生物获得经验时，
头顶飘出「+N」的**样式基准帧**。数字类动效帧统一风格：
**白色粗像素数字 + 黑色描边 + 洋红背景键控**。
本图固定画「+12」，运行时其余数字用同风格像素字体渲染。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素（与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（文字外全部键控透明） |
| 输出数量 | 1 张：`fx-xp-float.png` |

## Alpha 的产出方式

数字与星星之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 数字白 | `#FFFFFF` |
| 数字描边黑 | `#1A1A1A` |
| 星星金 | `#DCAE3A` |
| 背景键控色 | `#FF00FF` |

## 视觉描述

画面中央一行「+12」：

- 白色粗体像素数字与加号，每个笔画外面包一圈 **1 像素黑描边**——
  飘字会叠在任意场景上，无描边在雪地/天空前会看不清
- 数字高约占画布高度的 70%，整体居中
- 数字右上方一颗 3 × 3 金色四角星（「+经验」的记号）
- 背景整片纯洋红

## AI 提示词

```
A pixel art floating experience gain label for a retro game, 1024x1024, designed
to be downscaled to 32x32 pixel art.

Style: chunky bold pixel digits, flat shading, symmetric, no perspective, no
gradient, no glow, no motion blur.

Content: centered in the image, the number "+12" written as large bold white
pixel numerals with a plus sign. Every stroke of the digits and the plus sign is
wrapped in a thick one-pixel black outline. The digits fill about 70 percent of
the canvas height. To the upper right of the digits sits one small four-pointed
star in gold, about one digit tall.

Color palette strictly limited to: #FFFFFF, #1A1A1A, #DCAE3A.

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing.

No letters, no words, no sentences, no bars, no borders, no extra symbols, no
gradient, no glow, no shadow, no 3D render, no watermark.
```

## 负面提示词

```
letters, words, sentences, bars, borders, extra symbols, gradient, glow, shadow,
3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 3 个色值
4. 核对描边完整：白色像素不与背景透明区直接相邻
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 「+12」可读，笔画均带完整黑描边
- [ ] 颜色数 ≤ 3（不含透明）
- [ ] 叠在纯白、纯黑、天蓝三种背景上都清晰可辨
- [ ] 与 `fx-damage-number.png` 并排时字体风格一致
