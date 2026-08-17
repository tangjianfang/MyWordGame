# M11-U18 连击计数样式帧（fx-combo）

## 用途

milestone-11 战斗手感：连续命中时显示的连击计数**样式基准帧**。
数字类动效帧统一风格：**白色粗像素数字 + 黑色描边 + 洋红背景键控**。
本图固定画「x5」，运行时其余数字用同风格像素字体渲染。

## 规格

| 项 | 值 |
| --- | --- |
| 最终尺寸 | 32 × 32 像素（与 `postprocess_art.ASSETS` 注册值一致） |
| 生成尺寸 | 1024 × 1024（再降采样） |
| 平铺 | 否 |
| Alpha | **有**，仅 0 或 255（主体外全部键控透明） |
| 输出数量 | 1 张：`fx-combo.png` |

## Alpha 的产出方式

文字与星星之外的所有区域画成纯洋红 `#FF00FF`，后处理键控为透明。

## 调色板（严格使用）

| 用途 | HEX |
| --- | --- |
| 文字白 | `#FFFFFF` |
| 文字描边黑 | `#1A1A1A` |
| 星星金 | `#DCAE3A` |
| 背景键控色 | `#FF00FF` |

星星金与经验飘字（`fx-xp-float`）同色——「积累有回报」的视觉语言一致。

## 视觉描述

画面中央一行「x5」：

- 白色粗体像素小写「x」+ 数字「5」，笔画外一圈 **1 像素黑描边**
- 文字整体居中，高约占画布高度的 60%（连击计数比伤害数字次要，小一号）
- 文字左侧一颗 4 × 4 金色四角星（连击记号）
- 背景整片纯洋红

## AI 提示词

```
A pixel art combo hit counter label for a retro game, 1024x1024, designed to be
downscaled to 32x32 pixel art.

Style: chunky bold pixel digits, flat shading, no perspective, no gradient, no
glow, no motion blur.

Content: centered in the image, the counter "x5" — a small letter x followed by
the digit 5 — written in bold white pixel style. Every stroke is wrapped in a
thick one-pixel black outline. The text fills about 60 percent of the canvas
height. To the left of the text sits one four-pointed star in gold, slightly
taller than one digit.

Color palette strictly limited to: #FFFFFF, #1A1A1A, #DCAE3A.

The entire background is flat pure magenta #FF00FF, fully saturated, hard edges,
no anti-aliasing.

No words other than the single x, no letters other than x, no bars, no borders,
no extra symbols, no gradient, no glow, no shadow, no 3D render, no watermark.
```

## 负面提示词

```
words, letters other than x, bars, borders, extra symbols, gradient, glow,
shadow, 3D, anti-aliasing, opaque background
```

## 后处理

1. 最近邻降采样到 32 × 32
2. 键控：洋红 Alpha = 0，其余 Alpha = 255；去洋红边
3. 量化到 3 个色值
4. 核对描边完整、星星位于文字左侧
5. 存为 PNG-32

## 验收标准

- [ ] 尺寸恰为 32 × 32
- [ ] 「x5」可读、带完整黑描边，且只含一个字母 x
- [ ] 金星一颗、位于文字左侧
- [ ] 颜色数 ≤ 3（不含透明）
- [ ] 与 `fx-damage-number.png` 同屏时明显小一号（层级正确）
